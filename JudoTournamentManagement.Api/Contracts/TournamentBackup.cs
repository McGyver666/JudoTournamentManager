using JudoTournamentManagement.Api.Data;

namespace JudoTournamentManagement.Api.Contracts;

/// <summary>
/// Serializable backup snapshot of a single tournament and all its related data.
/// Version is checked on restore to detect incompatible formats.
/// </summary>
public sealed class TournamentBackup
{
    /// <summary>
    /// Schema version. Current supported value: "1.0".
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// UTC timestamp when the backup was created.
    /// </summary>
    public DateTimeOffset ExportedAtUtc { get; set; }

    /// <summary>
    /// The tournament record.
    /// </summary>
    public TournamentRecord Tournament { get; set; } = null!;

    /// <summary>
    /// All tatami records belonging to this tournament.
    /// </summary>
    public IReadOnlyList<TatamiRecord> Tatamis { get; set; } = [];

    /// <summary>
    /// All category records belonging to this tournament.
    /// </summary>
    public IReadOnlyList<CategoryRecord> Categories { get; set; } = [];

    /// <summary>
    /// All club records belonging to this tournament.
    /// </summary>
    public IReadOnlyList<ClubRecord> Clubs { get; set; } = [];

    /// <summary>
    /// All athlete records belonging to this tournament.
    /// </summary>
    public IReadOnlyList<AthleteRecord> Athletes { get; set; } = [];

    /// <summary>
    /// All registration records belonging to this tournament.
    /// </summary>
    public IReadOnlyList<RegistrationRecord> Registrations { get; set; } = [];

    /// <summary>
    /// All fight records belonging to this tournament.
    /// </summary>
    public IReadOnlyList<FightRecord> Fights { get; set; } = [];

    /// <summary>
    /// Audit log entries belonging to this tournament.
    /// </summary>
    public IReadOnlyList<AuditLogRecord> AuditLogs { get; set; } = [];
}
