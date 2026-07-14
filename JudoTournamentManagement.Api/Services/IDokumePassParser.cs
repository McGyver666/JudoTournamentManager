namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Parses and validates DokuMe Budo-pass QR codes.
/// Reads claims locally from JWT without network calls or signature verification.
/// The signature verification is marked as deliberately unverified and reserved for future DokuMe key distribution.
/// </summary>
public interface IDokumePassParser
{
    /// <summary>
    /// Parses a DokuMe QR code URL and extracts pass data.
    /// Returns null if the URL is invalid or the JWT is malformed.
    /// Never stores the URL, JWT, signature, or the KEY claim.
    /// </summary>
    DokumePassCheckResult? ParseQrUrl(string? qrUrl);

    /// <summary>
    /// Validates that a parsed pass is still valid on the given tournament date.
    /// Returns a validation result with match status and any discrepancies.
    /// </summary>
    DokumePassValidationResult ValidatePass(
        DokumePassCheckResult parsed,
        DateOnly tournamentDate,
        string athleteFirstName,
        string athleteLastName,
        int athleteBirthYear);
}

/// <summary>
/// Result of parsing a DokuMe QR code. Contains only the minimal, safe claims needed for registration.
/// The JWT signature is deliberately NOT verified at this point.
/// </summary>
public sealed class DokumePassCheckResult
{
    /// <summary>
    /// License/pass number from the 'NO' claim. Used to identify the athlete's license.
    /// </summary>
    public string PassNumber { get; set; } = string.Empty;

    /// <summary>
    /// First name from the 'FN' claim.
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Last name from the 'LN' claim.
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Date of birth from the 'DOB' claim (ISO 8601 format).
    /// </summary>
    public DateOnly DateOfBirth { get; set; }

    /// <summary>
    /// License type name from the 'LTN' claim, e.g., "Judopass".
    /// </summary>
    public string LicenseTypeName { get; set; } = string.Empty;

    /// <summary>
    /// Expiry date from the 'exp' claim (UTC Unix timestamp converted to UTC DateOnly).
    /// The pass is valid until and including this date.
    /// </summary>
    public DateOnly ExpiryDate { get; set; }

    /// <summary>
    /// Issuer from the 'iss' claim. Should be "DokuMe".
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// True if the JWT header specifies RS384 algorithm.
    /// Signature verification is not performed at this point.
    /// </summary>
    public bool IsRs384Claimed { get; set; }

    /// <summary>
    /// Always false in this version. Indicates that the RSA signature has NOT been verified.
    /// Reserved for future DokuMe key distribution.
    /// </summary>
    public bool SignatureVerified => false;
}

/// <summary>
/// Result of validating a parsed pass against an athlete and tournament date.
/// </summary>
public sealed class DokumePassValidationResult
{
    /// <summary>
    /// True if all checks pass: name match, birth year match, and expiry date is on or after tournament date.
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Parsed pass data for display and storage.
    /// </summary>
    public DokumePassCheckResult Pass { get; set; } = null!;

    /// <summary>
    /// Human-readable error or validation message in German.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Flags indicating which fields did not match (for UI display).
    /// </summary>
    public DokumePassValidationFlags Flags { get; set; }
}

/// <summary>
/// Flags for validation discrepancies.
/// </summary>
[Flags]
public enum DokumePassValidationFlags
{
    None = 0,
    FirstNameMismatch = 1,
    LastNameMismatch = 2,
    BirthYearMismatch = 4,
    PassExpired = 8,
    InvalidUrl = 16,
    InvalidJwt = 32,
    MissingClaims = 64,
    WrongIssuer = 128,
    UnsupportedAlgorithm = 256
}
