using System.Security.Cryptography;
using JudoTournamentManagement.Api.Data;
using JudoTournamentManagement.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// SQLite-backed implementation of the anonymous guest share for match lists.
/// </summary>
public sealed class SqliteGuestShareService : IGuestShareService
{
    private readonly AppDbContext _dbContext;

    /// <summary>Initializes a new service instance.</summary>
    public SqliteGuestShareService(AppDbContext dbContext)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<GuestShareState> GetStateAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var record = await _dbContext.GuestShares
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.TournamentId == tournamentId, cancellationToken);

        return ToState(tournamentId, record, DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public async Task<GuestShareState> EnableAsync(
        Guid tournamentId,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var record = await _dbContext.GuestShares
            .SingleOrDefaultAsync(x => x.TournamentId == tournamentId, cancellationToken);

        if (record is null)
        {
            record = new GuestShareRecord
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                Token = GenerateToken(),
                CreatedUtc = now
            };
            _dbContext.GuestShares.Add(record);
        }

        record.IsEnabled = true;
        record.ExpiresAtUtc = expiresAtUtc;
        record.UpdatedUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToState(tournamentId, record, now);
    }

    /// <inheritdoc />
    public async Task<GuestShareState> DisableAsync(Guid tournamentId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var record = await _dbContext.GuestShares
            .SingleOrDefaultAsync(x => x.TournamentId == tournamentId, cancellationToken);

        if (record is null)
        {
            return ToState(tournamentId, null, now);
        }

        record.IsEnabled = false;
        record.UpdatedUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToState(tournamentId, record, now);
    }

    /// <inheritdoc />
    public async Task<GuestShareState> RotateAsync(
        Guid tournamentId,
        DateTimeOffset? expiresAtUtc,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var record = await _dbContext.GuestShares
            .SingleOrDefaultAsync(x => x.TournamentId == tournamentId, cancellationToken);

        if (record is null)
        {
            record = new GuestShareRecord
            {
                Id = Guid.NewGuid(),
                TournamentId = tournamentId,
                CreatedUtc = now
            };
            _dbContext.GuestShares.Add(record);
        }

        record.Token = GenerateToken();
        record.IsEnabled = true;
        record.ExpiresAtUtc = expiresAtUtc;
        record.RotatedAtUtc = now;
        record.UpdatedUtc = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ToState(tournamentId, record, now);
    }

    /// <inheritdoc />
    public async Task<Guid?> ValidateTokenAsync(string token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var record = await _dbContext.GuestShares
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Token == token, cancellationToken);

        if (record is null || !record.IsEnabled)
        {
            return null;
        }

        if (record.ExpiresAtUtc.HasValue && record.ExpiresAtUtc.Value <= DateTimeOffset.UtcNow)
        {
            return null;
        }

        return record.TournamentId;
    }

    private static GuestShareState ToState(Guid tournamentId, GuestShareRecord? record, DateTimeOffset now)
    {
        if (record is null)
        {
            return new GuestShareState(tournamentId, false, false, false, null, null);
        }

        var notExpired = !record.ExpiresAtUtc.HasValue || record.ExpiresAtUtc.Value > now;
        var isActive = record.IsEnabled && notExpired;
        return new GuestShareState(
            tournamentId,
            true,
            record.IsEnabled,
            isActive,
            record.Token,
            record.ExpiresAtUtc);
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }
}
