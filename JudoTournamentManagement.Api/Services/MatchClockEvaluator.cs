using JudoTournamentManagement.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Periodically evaluates running fights and enforces server-authoritative timing transitions.
/// </summary>
public sealed class MatchClockEvaluator : BackgroundService
{
    private const string InProgress = "InProgress";
    private const string SystemUser = "system";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MatchClockEvaluator> _logger;

    /// <summary>
    /// Initializes a new hosted evaluator.
    /// </summary>
    public MatchClockEvaluator(IServiceScopeFactory scopeFactory, ILogger<MatchClockEvaluator> logger)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        ArgumentNullException.ThrowIfNull(logger);
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>
    /// Executes one timing evaluation cycle.
    /// </summary>
    public async Task EvaluateOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var matchService = scope.ServiceProvider.GetRequiredService<IMatchService>();

        var now = DateTimeOffset.UtcNow;
        var fights = await dbContext.Fights
            .AsNoTracking()
            .Include(f => f.Category)
            .Include(f => f.Tournament)
            .Where(f => f.Status == InProgress && f.StartedAtUtc != null)
            .ToListAsync(cancellationToken);

        foreach (var fight in fights)
        {
            try
            {
                if (await TryStopOsaeKomiAsync(matchService, fight, now, cancellationToken))
                {
                    continue;
                }

                await TryPauseFightAsync(matchService, fight, now, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Match clock evaluation failed for fight {FightId}.", fight.Id);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(250));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await EvaluateOnceAsync(stoppingToken);
        }
    }

    private static async Task<bool> TryStopOsaeKomiAsync(
        IMatchService matchService,
        FightRecord fight,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (fight.OsaeKomiStartedAtUtc is null || fight.OsaeKomiSide is null)
        {
            return false;
        }

        var tournament = fight.Tournament;
        var ipponSeconds = tournament?.OsaeKomiIpponSeconds ?? 20;
        var wazaAriSeconds = tournament?.OsaeKomiWazaAriSeconds ?? 10;

        var holderIsWhite = string.Equals(fight.OsaeKomiSide, "White", StringComparison.OrdinalIgnoreCase);
        var holderHasWazaAri = holderIsWhite ? fight.WhiteWazaAriCount > 0 : fight.BlueWazaAriCount > 0;
        var effectiveCapSeconds = holderHasWazaAri ? wazaAriSeconds : ipponSeconds;

        var holdSeconds = (now - fight.OsaeKomiStartedAtUtc.Value).TotalSeconds;
        if (holdSeconds < effectiveCapSeconds)
        {
            return false;
        }

        var result = await matchService.StopOsaeKomiAsync(fight.Id, SystemUser, cancellationToken);
        return result == MatchActionResult.Success;
    }

    private static async Task TryPauseFightAsync(
        IMatchService matchService,
        FightRecord fight,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (fight.OsaeKomiSide is not null)
        {
            return;
        }

        var category = fight.Category;
        if (category is null || fight.StartedAtUtc is null)
        {
            return;
        }

        var elapsedSeconds = (now - fight.StartedAtUtc.Value).TotalSeconds;
        var regularDuration = category.MatchDurationSeconds;
        if (elapsedSeconds < regularDuration)
        {
            return;
        }

        var timeLimitSeconds = regularDuration;
        if (category.GoldenScoreEnabled && IsTiedForGoldenScore(fight))
        {
            timeLimitSeconds += category.GoldenScoreDurationSeconds;
        }

        if (elapsedSeconds < timeLimitSeconds)
        {
            return;
        }

        _ = await matchService.PauseAsync(fight.Id, SystemUser, cancellationToken);
    }

    private static bool IsTiedForGoldenScore(FightRecord fight) =>
        fight.WhiteWazaAriCount == fight.BlueWazaAriCount
        && fight.WhiteYukoCount == fight.BlueYukoCount;
}
