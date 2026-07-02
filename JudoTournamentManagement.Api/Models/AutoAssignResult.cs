namespace JudoTournamentManagement.Api.Models;

/// <summary>
/// Represents a single athlete who could not be matched to any category during auto-assignment.
/// </summary>
/// <param name="AthleteId">Athlete identifier.</param>
/// <param name="FirstName">Given name.</param>
/// <param name="LastName">Family name.</param>
/// <param name="Reason">Human-readable reason for the missing assignment.</param>
public sealed record UnassignedAthlete(
    Guid AthleteId,
    string FirstName,
    string LastName,
    string Reason);

/// <summary>
/// Summary returned after an auto-assignment run.
/// </summary>
/// <param name="AssignedCount">Number of registrations that received a category assignment.</param>
/// <param name="UnassignedCount">Number of registrations that could not be matched.</param>
/// <param name="Unassigned">Details for each unmatched athlete.</param>
public sealed record AutoAssignResult(
    int AssignedCount,
    int UnassignedCount,
    IReadOnlyList<UnassignedAthlete> Unassigned);
