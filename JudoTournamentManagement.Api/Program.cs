using System.Globalization;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using System.Reflection;
using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Hubs;
using JudoTournamentManagement.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var authTokenHmacSecret = builder.Configuration["Security:AuthTokenHmacSecret"];
if (string.IsNullOrWhiteSpace(authTokenHmacSecret))
{
    if (builder.Environment.IsDevelopment()
        || builder.Environment.IsEnvironment("Testing")
        || builder.Environment.IsEnvironment("RateLimitValidation"))
    {
        builder.Configuration["Security:AuthTokenHmacSecret"] =
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    }
    else
    {
        throw new InvalidOperationException(
            "Missing required configuration key 'Security:AuthTokenHmacSecret'.");
    }
}

var databaseDirectory = Path.Combine(builder.Environment.ContentRootPath, "App_Data");
Directory.CreateDirectory(databaseDirectory);
var databasePath = Path.Combine(databaseDirectory, "judo-tournament.db");

builder.WebHost.ConfigureKestrel(options =>
{
    // Default request body limit to reduce accidental or abusive oversized payloads.
    options.Limits.MaxRequestBodySize = 10 * 1024 * 1024; // 10 MB
});

builder.Services.AddLocalization();
builder.Services.AddControllers(options => options.SuppressAsyncSuffixInActionNames = false)
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHealthChecks();
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
builder.Services.AddAuthentication("BearerToken")
    .AddScheme<AuthenticationSchemeOptions, BearerTokenAuthenticationHandler>("BearerToken", _ => { });
var authPermitLimit = builder.Environment.IsEnvironment("Testing") ? 10000 : 100;
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(new
        {
            title = "Zu viele Anfragen.",
            detail = "Bitte spaeter erneut versuchen.",
            status = StatusCodes.Status429TooManyRequests
        }, cancellationToken: token);
    };

    options.AddPolicy("AuthPolicy", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: ip,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = authPermitLimit,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});
builder.Services.AddAuthorization();
builder.Services.AddScoped<ITournamentStore, SqliteTournamentStore>();
builder.Services.AddScoped<ITatamisStore, SqliteTatamisStore>();
builder.Services.AddScoped<ICategoriesStore, SqliteCategoriesStore>();
builder.Services.AddScoped<IClubsStore, SqliteClubsStore>();
builder.Services.AddScoped<IAthletesStore, SqliteAthletesStore>();
builder.Services.AddScoped<IRegistrationsStore, SqliteRegistrationsStore>();
builder.Services.AddScoped<IFightsStore, SqliteFightsStore>();
builder.Services.AddScoped<IBracketService, BracketService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IMatchService, MatchService>();
builder.Services.AddScoped<ITatamiQueueService, TatamiQueueService>();
builder.Services.AddScoped<IRankingService, RankingService>();
builder.Services.AddScoped<IPasswordHasherService, Pbkdf2PasswordHasherService>();
builder.Services.AddScoped<IAuthService, SqliteAuthService>();
builder.Services.AddScoped<IBackupService, BackupService>();
builder.Services.AddSignalR();

var app = builder.Build();
await InitializeDatabaseAsync(app);

var supportedCultures = new[]
{
    new CultureInfo("de-DE"),
    new CultureInfo("en-US")
};

app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("de-DE"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.TryAdd(
        "Content-Security-Policy",
        "default-src 'self'; frame-ancestors 'none'; object-src 'none'; base-uri 'self'; script-src 'self' 'unsafe-hashes' 'sha256-MhtPZXr7+LpJUY5qtMutB+qWfQtMaPccfe7QXtCcEYc='; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self' ws: wss:; font-src 'self' data:");

    await next();
});
app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapHealthChecks("/health");
app.MapControllers();
app.MapHub<TournamentHub>("/hubs/tournament");

// Serve the Angular single-page application for any non-API route so that
// client-side routing (deep links, refresh) resolves to index.html.
app.MapFallbackToFile("index.html");

app.Run();

static async Task InitializeDatabaseAsync(WebApplication application)
{
    await using var scope = application.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger("DatabaseInitialization");
    await AdoptLegacySchemaForMigrationsAsync(dbContext, logger);
    await dbContext.Database.MigrateAsync();

    await EnsureLegacyFightTatamiColumnAsync(dbContext, logger);
    await EnsureLegacyFightWhiteSideColumnsAsync(dbContext, logger);
    await EnsureLegacyTournamentAccentSideColorColumnAsync(dbContext, logger);
}

static async Task AdoptLegacySchemaForMigrationsAsync(AppDbContext dbContext, ILogger logger)
{
    var appliedMigrations = (await dbContext.Database.GetAppliedMigrationsAsync()).ToList();
    if (appliedMigrations.Count > 0)
    {
        return;
    }

    if (!await TableExistsAsync(dbContext, "Tournaments"))
    {
        return;
    }

    var availableMigrations = dbContext.Database.GetMigrations().ToList();
    if (availableMigrations.Count == 0)
    {
        return;
    }

    // Legacy databases created before migration-first startup have no history table.
    // Mark a baseline migration so EF can apply only the missing migration steps.
    await dbContext.Database.ExecuteSqlRawAsync(
        """
        CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (
            MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY,
            ProductVersion TEXT NOT NULL
        );
        """);

    var baselineMigrations = await ResolveBaselineMigrationsAsync(dbContext, availableMigrations);
    var productVersion = ResolveEfProductVersion();

    foreach (var migrationId in baselineMigrations)
    {
        await dbContext.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
            SELECT {0}, {1}
            WHERE NOT EXISTS (
                SELECT 1
                FROM __EFMigrationsHistory
                WHERE MigrationId = {0}
            );
            """,
            migrationId,
            productVersion);

        logger.LogWarning(
            "Legacy migration baseline applied for {MigrationId}.",
            migrationId);
    }
}

static async Task<IReadOnlyList<string>> ResolveBaselineMigrationsAsync(
    AppDbContext dbContext,
    IReadOnlyList<string> availableMigrations)
{
    var hasUserAccounts = await TableExistsAsync(dbContext, "UserAccounts");
    var hasGoldenScore = await ColumnExistsAsync(dbContext, "Categories", "GoldenScoreEnabled");
    var hasLicenseConfirmed = await ColumnExistsAsync(dbContext, "Registrations", "LicenseConfirmed");

    if (hasUserAccounts && hasGoldenScore && hasLicenseConfirmed)
    {
        return availableMigrations;
    }

    return new[] { availableMigrations[0] };
}

static string ResolveEfProductVersion()
{
    var informationalVersionAttribute = Attribute.GetCustomAttribute(
        typeof(DbContext).Assembly,
        typeof(AssemblyInformationalVersionAttribute)) as AssemblyInformationalVersionAttribute;
    var informationalVersion = informationalVersionAttribute?.InformationalVersion;

    if (string.IsNullOrWhiteSpace(informationalVersion))
    {
        return "8.0.0";
    }

    var plusIndex = informationalVersion.IndexOf('+');
    return plusIndex > 0
        ? informationalVersion[..plusIndex]
        : informationalVersion;
}

static async Task<bool> TableExistsAsync(AppDbContext dbContext, string tableName)
{
    await using var connection = dbContext.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText =
        "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";

    var parameter = command.CreateParameter();
    parameter.ParameterName = "$name";
    parameter.Value = tableName;
    command.Parameters.Add(parameter);

    var result = await command.ExecuteScalarAsync();
    return result is not null;
}

static async Task<bool> ColumnExistsAsync(AppDbContext dbContext, string tableName, string columnName)
{
    await using var connection = dbContext.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = $"PRAGMA table_info('{tableName}');";

    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        if (string.Equals(reader.GetString(1), columnName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
    }

    return false;
}

static async Task EnsureLegacyFightTatamiColumnAsync(AppDbContext dbContext, ILogger logger)
{
    await using var connection = dbContext.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = "PRAGMA table_info('Fights');";

    var hasTatamiId = false;
    await using (var reader = await command.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            var columnName = reader.GetString(1);
            if (string.Equals(columnName, "TatamiId", StringComparison.OrdinalIgnoreCase))
            {
                hasTatamiId = true;
                break;
            }
        }
    }

    if (hasTatamiId)
    {
        logger.LogInformation("Schema check: Fights.TatamiId already exists.");
        return;
    }

    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE Fights ADD COLUMN TatamiId TEXT NULL;");
    logger.LogWarning("Schema patch applied: added missing Fights.TatamiId column.");
}

static async Task EnsureLegacyFightWhiteSideColumnsAsync(AppDbContext dbContext, ILogger logger)
{
    await using var connection = dbContext.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    await using (var command = connection.CreateCommand())
    {
        command.CommandText = "PRAGMA table_info('Fights');";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }
    }

    var renamePairs = new (string OldName, string NewName)[]
    {
        ("RedAthleteId", "WhiteAthleteId"),
        ("RedScore", "WhiteScore"),
        ("RedPenalties", "WhitePenalties"),
        ("RedIpponCount", "WhiteIpponCount"),
        ("RedWazaAriCount", "WhiteWazaAriCount"),
        ("RedYukoCount", "WhiteYukoCount")
    };

    foreach (var (oldName, newName) in renamePairs)
    {
        if (!columns.Contains(oldName) || columns.Contains(newName))
        {
            continue;
        }

        await using (var renameCommand = connection.CreateCommand())
        {
            renameCommand.CommandText = $"ALTER TABLE Fights RENAME COLUMN {oldName} TO {newName};";
            await renameCommand.ExecuteNonQueryAsync();
        }
        logger.LogWarning(
            "Schema patch applied: renamed Fights.{OldName} to Fights.{NewName}.",
            oldName,
            newName);

        columns.Remove(oldName);
        columns.Add(newName);
    }

    await dbContext.Database.ExecuteSqlRawAsync(
        "UPDATE Fights SET OsaeKomiSide = 'White' WHERE OsaeKomiSide = 'Red';");
}

static async Task EnsureLegacyTournamentAccentSideColorColumnAsync(AppDbContext dbContext, ILogger logger)
{
    await using var connection = dbContext.Database.GetDbConnection();
    if (connection.State != System.Data.ConnectionState.Open)
    {
        await connection.OpenAsync();
    }

    await using var command = connection.CreateCommand();
    command.CommandText = "PRAGMA table_info('Tournaments');";

    var hasAccentSideColor = false;
    await using (var reader = await command.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            if (string.Equals(reader.GetString(1), "AccentSideColor", StringComparison.OrdinalIgnoreCase))
            {
                hasAccentSideColor = true;
                break;
            }
        }
    }

    if (hasAccentSideColor)
    {
        logger.LogInformation("Schema check: Tournaments.AccentSideColor already exists.");
        return;
    }

    await dbContext.Database.ExecuteSqlRawAsync("ALTER TABLE Tournaments ADD COLUMN AccentSideColor TEXT NOT NULL DEFAULT 'Blue';");
    logger.LogWarning("Schema patch applied: added missing Tournaments.AccentSideColor column.");
}

/// <summary>
/// Application entry type for integration/testing scenarios.
/// </summary>
public partial class Program;
