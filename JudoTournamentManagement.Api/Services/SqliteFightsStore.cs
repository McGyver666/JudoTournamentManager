using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// SQLite-backed read access to fight records.
/// </summary>
public sealed class SqliteFightsStore : IFightsStore
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<SqliteFightsStore> _logger;

    /// <summary>Initializes a new store instance.</summary>
    public SqliteFightsStore(AppDbContext dbContext, ILogger<SqliteFightsStore> logger)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(logger);
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Fight>> GetAllAsync(
        Guid tournamentId,
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Fights
            .AsNoTracking()
            .Where(f => f.TournamentId == tournamentId && f.CategoryId == categoryId)
            .OrderBy(f => f.BracketType)
            .ThenBy(f => f.Round)
            .ThenBy(f => f.FightNumber)
            .Select(f => MapToModel(f))
            .ToArrayAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Fight?> GetByIdAsync(Guid fightId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.Fights
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == fightId, cancellationToken);

        return record is null ? null : MapToModel(record);
    }

    private static Fight MapToModel(FightRecord r) =>
        new(r.Id,
            r.TournamentId,
            r.CategoryId,
            Enum.Parse<FightBracketType>(r.BracketType),
            r.Round,
            r.FightNumber,
            r.PoolNumber,
            r.WhiteSourceFightId,
            r.WhiteSourceOutcome is null ? null : Enum.Parse<FightSlotSourceOutcome>(r.WhiteSourceOutcome),
            r.BlueSourceFightId,
            r.BlueSourceOutcome is null ? null : Enum.Parse<FightSlotSourceOutcome>(r.BlueSourceOutcome),
            r.WhiteAthleteId,
            r.BlueAthleteId,
            r.WinnerId,
            r.IsBye,
            Enum.Parse<FightStatus>(r.Status),
            r.TatamiId,
            r.QueueOrder,
            r.WhiteScore,
            r.BlueScore,
            r.WhitePenalties,
            r.BluePenalties,
            r.WhiteIpponCount,
            r.WhiteWazaAriCount,
            r.WhiteYukoCount,
            r.BlueIpponCount,
            r.BlueWazaAriCount,
            r.BlueYukoCount,
            r.PausedAtUtc,
            r.OsaeKomiSide,
            r.OsaeKomiStartedAtUtc,
            r.StartedAtUtc,
            r.CompletedAtUtc,
            r.CreatedAtUtc,
            r.UpdatedAtUtc,
            IsGoldenScore: false);
}
