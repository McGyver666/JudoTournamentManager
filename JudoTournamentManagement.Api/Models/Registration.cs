namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Lightweight registration record (IDs only). Use <see cref="RegistrationDetail"/> for display.
/// </summary>
/// <param name="Id">Unique registration identifier.</param>
/// <param name="TournamentId">Tournament this registration belongs to.</param>
/// <param name="AthleteId">Registered athlete.</param>
/// <param name="CategoryId">Target category (null until assigned).</param>
/// <param name="CreatedAtUtc">Creation timestamp in UTC.</param>
public sealed record Registration(
    Guid Id,
    Guid TournamentId,
    Guid AthleteId,
    Guid? CategoryId,
    DateTimeOffset CreatedAtUtc);
