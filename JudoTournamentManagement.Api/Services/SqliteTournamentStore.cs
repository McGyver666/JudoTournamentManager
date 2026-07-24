using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// SQLite-backed tournament storage for offline operation.
/// </summary>
public sealed class SqliteTournamentStore : ITournamentStore
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SqliteTournamentStore> _logger;
    private readonly ICategoryPresetsStore _categoryPresetsStore;

    /// <summary>
    /// Initializes a new store instance.
    /// </summary>
    public SqliteTournamentStore(
        AppDbContext dbContext,
        ILogger<SqliteTournamentStore> logger,
        ICategoryPresetsStore categoryPresetsStore)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(categoryPresetsStore);
        _dbContext = dbContext;
        _logger = logger;
        _categoryPresetsStore = categoryPresetsStore;
    }

    /// <summary>
    /// Initializes a new store instance.
    /// Compatibility constructor for tests that do not provide a preset store.
    /// </summary>
    public SqliteTournamentStore(
        AppDbContext dbContext,
        ILogger<SqliteTournamentStore> logger)
        : this(dbContext, logger, new SqliteCategoryPresetsStore(dbContext))
    {
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Tournament>> GetAllAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.Tournaments
            .AsNoTracking()
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Name)
            .Select(x => MapToModel(x))
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Tournament?> GetByIdAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Tournaments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);

        return record is null ? null : MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<Tournament> CreateAsync(
        string name,
        DateOnly date,
        string venue,
        string organizer,
        CancellationToken cancellationToken)
    {
        return await CreateAsync(name, date, venue, organizer, "Blue", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Tournament> CreateAsync(
        string name,
        DateOnly date,
        string venue,
        string organizer,
        string accentSideColor,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(venue);
        ArgumentNullException.ThrowIfNull(organizer);

        var utcNow = DateTimeOffset.UtcNow;
        var record = new TournamentRecord
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Date = date,
            Venue = venue.Trim(),
            Organizer = organizer.Trim(),
            AccentSideColor = NormalizeAccentSideColor(accentSideColor),
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow
        };

        _dbContext.Tournaments.Add(record);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await _categoryPresetsStore.SeedDefaultsAsync(record.Id, date.Year, cancellationToken);

        _logger.LogInformation("Tournament {TournamentId} created.", record.Id);
        return MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(
        Guid tournamentId,
        string name,
        DateOnly date,
        string venue,
        string organizer,
        CancellationToken cancellationToken)
    {
        return await UpdateAsync(tournamentId, name, date, venue, organizer, "Blue", cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(
        Guid tournamentId,
        string name,
        DateOnly date,
        string venue,
        string organizer,
        string accentSideColor,
        CancellationToken cancellationToken)
    {
        return await UpdateAsync(tournamentId, name, date, venue, organizer, accentSideColor, 20, 10, 5, true, 180, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> UpdateAsync(
        Guid tournamentId,
        string name,
        DateOnly date,
        string venue,
        string organizer,
        string accentSideColor,
        int osaeKomiIpponSeconds,
        int osaeKomiWazaAriSeconds,
        int osaeKomiYukoSeconds,
        bool osaeKomiYukoEnabled,
        int minimumRestBetweenFightsSeconds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(venue);
        ArgumentNullException.ThrowIfNull(organizer);

        var record = await _dbContext.Tournaments
            .FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);

        if (record is null)
        {
            _logger.LogWarning("Tournament {TournamentId} not found for update.", tournamentId);
            return false;
        }

        record.Name = name.Trim();
        record.Date = date;
        record.Venue = venue.Trim();
        record.Organizer = organizer.Trim();
        record.AccentSideColor = NormalizeAccentSideColor(accentSideColor);
        record.OsaeKomiIpponSeconds = osaeKomiIpponSeconds;
        record.OsaeKomiWazaAriSeconds = osaeKomiWazaAriSeconds;
        record.OsaeKomiYukoSeconds = osaeKomiYukoSeconds;
        record.OsaeKomiYukoEnabled = osaeKomiYukoEnabled;
        record.MinimumRestBetweenFightsSeconds = minimumRestBetweenFightsSeconds;
        record.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Tournament {TournamentId} updated.", tournamentId);
        return true;
    }

    private static Tournament MapToModel(TournamentRecord record)
    {
        return new Tournament(
            record.Id,
            record.Name,
            record.Date,
            record.Venue,
            record.Organizer,
            record.CreatedAtUtc,
            record.UpdatedAtUtc)
        {
            AccentSideColor = NormalizeAccentSideColor(record.AccentSideColor),
            OsaeKomiIpponSeconds = record.OsaeKomiIpponSeconds,
            OsaeKomiWazaAriSeconds = record.OsaeKomiWazaAriSeconds,
            OsaeKomiYukoSeconds = record.OsaeKomiYukoSeconds,
            OsaeKomiYukoEnabled = record.OsaeKomiYukoEnabled,
            MinimumRestBetweenFightsSeconds = record.MinimumRestBetweenFightsSeconds,
        };
    }

    private static string NormalizeAccentSideColor(string accentSideColor)
    {
        return string.Equals(accentSideColor, "Red", StringComparison.OrdinalIgnoreCase) ? "Red" : "Blue";
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var tournament = await _dbContext.Tournaments
            .FirstOrDefaultAsync(x => x.Id == tournamentId, cancellationToken);

        if (tournament is null)
        {
            _logger.LogWarning("Tournament {TournamentId} not found for deletion.", tournamentId);
            return false;
        }

        // Cascade manually in dependency order (child → parent).
        var fights = await _dbContext.Fights
            .Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        _dbContext.Fights.RemoveRange(fights);

        var registrations = await _dbContext.Registrations
            .Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        _dbContext.Registrations.RemoveRange(registrations);

        var athletes = await _dbContext.Athletes
            .Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        _dbContext.Athletes.RemoveRange(athletes);

        var clubs = await _dbContext.Clubs
            .Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        _dbContext.Clubs.RemoveRange(clubs);

        var categories = await _dbContext.Categories
            .Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        _dbContext.Categories.RemoveRange(categories);

        var tatamis = await _dbContext.Tatamis
            .Where(x => x.TournamentId == tournamentId).ToListAsync(cancellationToken);
        _dbContext.Tatamis.RemoveRange(tatamis);

        _dbContext.Tournaments.Remove(tournament);
        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Tournament {TournamentId} and all dependent data deleted.", tournamentId);
        return true;
    }
}
