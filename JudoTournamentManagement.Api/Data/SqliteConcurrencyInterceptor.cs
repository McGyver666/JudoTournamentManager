using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace JudoTournamentManagement.Api.Data;

/// <summary>
/// Applies a SQLite <c>busy_timeout</c> to every connection so a competing writer waits for
/// a lock to clear instead of failing immediately with "database is locked" (SQLite error 5).
/// WAL journal mode is intentionally not enabled because the application performs file-copy
/// backups, which would miss writes still held in the <c>-wal</c> sidecar file.
/// </summary>
public sealed class SqliteConcurrencyInterceptor : DbConnectionInterceptor
{
    private const string PragmaCommand = "PRAGMA busy_timeout=5000;";

    /// <summary>Shared stateless instance.</summary>
    public static SqliteConcurrencyInterceptor Instance { get; } = new();

    /// <inheritdoc />
    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ApplyPragmas(connection);
        base.ConnectionOpened(connection, eventData);
    }

    /// <inheritdoc />
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = PragmaCommand;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken).ConfigureAwait(false);
    }

    private static void ApplyPragmas(DbConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = PragmaCommand;
        command.ExecuteNonQuery();
    }
}
