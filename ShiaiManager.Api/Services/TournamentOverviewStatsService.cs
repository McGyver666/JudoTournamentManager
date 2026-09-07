using ShiaiManager.Api.Data;
using ShiaiManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ShiaiManager.Api.Services;

/// <summary>
/// SQLite-backed aggregate statistics for the live tournament overview.
/// </summary>
public sealed class TournamentOverviewStatsService : IOverviewStatsService
{
    private readonly AppDbContext _dbContext;
    private static readonly string CompletedStatus = FightStatus.Completed.ToString();

    /// <summary>Initializes a new service instance.</summary>
    public TournamentOverviewStatsService(AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<TournamentOverviewStats> GetAsync(
        Guid tournamentId,
        CancellationToken cancellationToken)
    {
        var registeredAthletes = await _dbContext.Registrations
            .AsNoTracking()
            .CountAsync(r => r.TournamentId == tournamentId, cancellationToken);
        var clubCount = await _dbContext.Clubs
            .AsNoTracking()
            .CountAsync(c => c.TournamentId == tournamentId, cancellationToken);
        var categoryCount = await _dbContext.Categories
            .AsNoTracking()
            .CountAsync(c => c.TournamentId == tournamentId, cancellationToken);
        var fights = await _dbContext.Fights
            .AsNoTracking()
            .Where(f => f.TournamentId == tournamentId && !f.IsBye)
            .Select(f => new
            {
                f.Status,
                f.WhiteIpponCount,
                f.BlueIpponCount,
                f.StartedAtUtc,
                f.CompletedAtUtc,
            })
            .ToListAsync(cancellationToken);

        var completedFights = fights.Where(f => f.Status == CompletedStatus).ToArray();
        var durations = completedFights
            .Where(f => f.StartedAtUtc.HasValue && f.CompletedAtUtc.HasValue)
            .Select(f => (f.CompletedAtUtc!.Value - f.StartedAtUtc!.Value).TotalSeconds)
            .ToArray();

        return new TournamentOverviewStats(
            tournamentId,
            registeredAthletes,
            clubCount,
            categoryCount,
            completedFights.Length,
            fights.Count,
            durations.Length == 0 ? null : durations.Average(),
            completedFights.Sum(f => f.WhiteIpponCount + f.BlueIpponCount));
    }
}