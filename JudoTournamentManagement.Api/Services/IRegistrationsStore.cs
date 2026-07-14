using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Abstraction for registration persistence.
/// </summary>
public interface IRegistrationsStore
{
    /// <summary>
    /// Returns all registrations for a tournament with full athlete and category details.
    /// Ordered by category age group, then athlete last name.
    /// </summary>
    Task<IReadOnlyList<RegistrationDetail>> GetDetailedAsync(Guid tournamentId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns one registration by identifier, or <c>null</c> if not found.
    /// </summary>
    Task<Registration?> GetByIdAsync(Guid registrationId, CancellationToken cancellationToken);

    /// <summary>
    /// Registers an athlete (without assigning a category).
    /// Captures weight and license confirmation at registration time.
    /// Returns <c>null</c> when the athlete already has a registration in this tournament
    /// (one registration per athlete per tournament).
    /// </summary>
    Task<Registration?> CreateAsync(
        Guid tournamentId,
        Guid athleteId,
        decimal weightKg,
        bool licenseConfirmed,
        CancellationToken cancellationToken);

    /// <summary>
    /// Registers an athlete with optional DokuMe license verification.
    /// If dokumeQrUrl is provided, parses and validates the QR code against athlete data.
    /// If validation fails and no override reason is provided, returns null with validation error logged.
    /// Returns <c>null</c> when the athlete already has a registration in this tournament.
    /// </summary>
    Task<Registration?> CreateWithLicenseCheckAsync(
        Guid tournamentId,
        Guid athleteId,
        decimal weightKg,
        bool licenseConfirmed,
        string? dokumeQrUrl,
        string? licenseCheckOverrideReason,
        IDokumePassParser dokumePassParser,
        DateOnly tournamentDate,
        string operatorName,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a registration. Returns <c>false</c> if the registration was not found.
    /// </summary>
    Task<bool> DeleteAsync(Guid registrationId, CancellationToken cancellationToken);

    /// <summary>
    /// Assigns a category to a registered athlete. Returns <c>null</c> if registration not found.
    /// </summary>
    Task<Registration?> AssignCategoryAsync(Guid registrationId, Guid categoryId, CancellationToken cancellationToken);

    /// <summary>
    /// Automatically assigns all unassigned registrations in a tournament to the best-fitting
    /// unlocked category based on gender, birth year bounds and weight class.
    /// Already-assigned registrations are left unchanged.
    /// </summary>
    Task<AutoAssignResult> AutoAssignAsync(Guid tournamentId, CancellationToken cancellationToken);
}
