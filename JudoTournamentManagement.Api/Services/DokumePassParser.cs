using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Parses DokuMe Budo-pass QR codes locally without network calls or signature verification.
/// Validates URLs against strict allowlists and extracts only safe JWT claims.
/// </summary>
public sealed class DokumePassParser : IDokumePassParser
{
    private const string ValidHost = "qr.dokume.net";
    private const string ValidScheme = "https";
    private const string ValidDocumentType = "l"; // l = license/pass
    private const string ExpectedIssuer = "DokuMe";
    private const string ExpectedAlgorithm = "RS384";
    private const int MaxUrlLength = 2000;

    private readonly ILogger<DokumePassParser> _logger;

    public DokumePassParser(ILogger<DokumePassParser> logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    public DokumePassCheckResult? ParseQrUrl(string? qrUrl)
    {
        if (string.IsNullOrWhiteSpace(qrUrl))
        {
            _logger.LogWarning("QR URL is null or empty.");
            return null;
        }

        if (qrUrl.Length > MaxUrlLength)
        {
            _logger.LogWarning("QR URL exceeds maximum length of {MaxUrlLength}.", MaxUrlLength);
            return null;
        }

        // Parse and validate URL
        if (!Uri.TryCreate(qrUrl, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("QR URL is not a valid URI: {QrUrl}", "***redacted***");
            return null;
        }

        if (uri.Scheme != ValidScheme || uri.Host != ValidHost)
        {
            _logger.LogWarning("QR URL is from an invalid host. Expected {Scheme}://{Host}", ValidScheme, ValidHost);
            return null;
        }

        // Extract and validate query parameters
        var query = ParseQueryString(uri.Query);

        if (!query.TryGetValue("d", out var documentType) || documentType != ValidDocumentType)
        {
            _logger.LogWarning("QR document type is invalid or missing.");
            return null;
        }

        if (!query.TryGetValue("s", out var jwtToken) || string.IsNullOrEmpty(jwtToken))
        {
            _logger.LogWarning("QR JWT (s parameter) is missing or empty.");
            return null;
        }

        // Parse JWT locally (no signature verification)
        return ParseJwt(jwtToken);
    }

    public DokumePassValidationResult ValidatePass(
        DokumePassCheckResult parsed,
        DateOnly tournamentDate,
        string athleteFirstName,
        string athleteLastName,
        int athleteBirthYear)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(athleteFirstName);
        ArgumentNullException.ThrowIfNullOrWhiteSpace(athleteLastName);

        var flags = DokumePassValidationFlags.None;
        var isValid = true;

        // Normalize names for comparison: trim, collapse whitespace, ignore case
        var passFirstName = NormalizeName(parsed.FirstName);
        var passLastName = NormalizeName(parsed.LastName);
        var athleteFirstNameNorm = NormalizeName(athleteFirstName);
        var athleteLastNameNorm = NormalizeName(athleteLastName);

        // Check first name
        if (!string.Equals(passFirstName, athleteFirstNameNorm, StringComparison.OrdinalIgnoreCase))
        {
            flags |= DokumePassValidationFlags.FirstNameMismatch;
            isValid = false;
        }

        // Check last name
        if (!string.Equals(passLastName, athleteLastNameNorm, StringComparison.OrdinalIgnoreCase))
        {
            flags |= DokumePassValidationFlags.LastNameMismatch;
            isValid = false;
        }

        // Check birth year (extract year from DateOnly)
        var passYear = parsed.DateOfBirth.Year;
        if (passYear != athleteBirthYear)
        {
            flags |= DokumePassValidationFlags.BirthYearMismatch;
            isValid = false;
        }

        // Check expiry: tournament date must be <= expiry date (inclusive)
        if (tournamentDate > parsed.ExpiryDate)
        {
            flags |= DokumePassValidationFlags.PassExpired;
            isValid = false;
        }

        var message = isValid
            ? $"Lizenz gültig: {parsed.PassNumber}, gültig bis {parsed.ExpiryDate:dd.MM.yyyy}"
            : BuildValidationErrorMessage(flags);

        return new DokumePassValidationResult
        {
            IsValid = isValid,
            Pass = parsed,
            Message = message,
            Flags = flags
        };
    }

    private DokumePassCheckResult? ParseJwt(string jwtToken)
    {
        try
        {
            var parts = jwtToken.Split('.');
            if (parts.Length != 3)
            {
                _logger.LogWarning("JWT does not have 3 parts.");
                return null;
            }

            // Decode header
            var headerJson = DecodeBase64Url(parts[0]);
            if (headerJson == null)
            {
                _logger.LogWarning("JWT header is not valid Base64url.");
                return null;
            }

            var headerDoc = JsonDocument.Parse(headerJson);
            var headerRoot = headerDoc.RootElement;

            if (!headerRoot.TryGetProperty("alg", out var algElement) || algElement.GetString() != ExpectedAlgorithm)
            {
                _logger.LogWarning("JWT algorithm is not {ExpectedAlgorithm}.", ExpectedAlgorithm);
                return null;
            }

            // Decode payload
            var payloadJson = DecodeBase64Url(parts[1]);
            if (payloadJson == null)
            {
                _logger.LogWarning("JWT payload is not valid Base64url.");
                return null;
            }

            var payloadDoc = JsonDocument.Parse(payloadJson);
            var payloadRoot = payloadDoc.RootElement;

            // Validate issuer
            if (!payloadRoot.TryGetProperty("iss", out var issElement) || issElement.GetString() != ExpectedIssuer)
            {
                _logger.LogWarning("JWT issuer is not {ExpectedIssuer}.", ExpectedIssuer);
                return null;
            }

            // Extract required claims
            var result = new DokumePassCheckResult
            {
                IsRs384Claimed = true,
                Issuer = ExpectedIssuer
            };

            if (payloadRoot.TryGetProperty("NO", out var noElement))
            {
                result.PassNumber = noElement.GetString() ?? string.Empty;
            }

            if (payloadRoot.TryGetProperty("FN", out var fnElement))
            {
                result.FirstName = fnElement.GetString() ?? string.Empty;
            }

            if (payloadRoot.TryGetProperty("LN", out var lnElement))
            {
                result.LastName = lnElement.GetString() ?? string.Empty;
            }

            if (payloadRoot.TryGetProperty("DOB", out var dobElement) && dobElement.GetString() is { } dobStr)
            {
                if (DateOnly.TryParse(dobStr, out var dob))
                {
                    result.DateOfBirth = dob;
                }
            }

            if (payloadRoot.TryGetProperty("LTN", out var ltnElement))
            {
                result.LicenseTypeName = ltnElement.GetString() ?? string.Empty;
            }

            if (payloadRoot.TryGetProperty("exp", out var expElement) && expElement.GetInt64() is var expUnix && expUnix > 0)
            {
                var expDateTime = UnixTimeStampToDateTime(expUnix);
                result.ExpiryDate = DateOnly.FromDateTime(expDateTime);
            }

            // Validate required claims
            if (string.IsNullOrEmpty(result.PassNumber) ||
                string.IsNullOrEmpty(result.FirstName) ||
                string.IsNullOrEmpty(result.LastName) ||
                result.DateOfBirth == default ||
                result.ExpiryDate == default)
            {
                _logger.LogWarning("JWT is missing required claims (NO, FN, LN, DOB, or exp).");
                return null;
            }

            _logger.LogInformation("JWT parsed successfully. Pass: {PassNumber}, Name: {FirstName} {LastName}",
                result.PassNumber, result.FirstName, result.LastName);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error parsing JWT.");
            return null;
        }
    }

    private static string? DecodeBase64Url(string base64Url)
    {
        try
        {
            // Add padding if necessary
            var padded = base64Url.Length % 4 == 0 ? base64Url : base64Url + new string('=', 4 - base64Url.Length % 4);

            // Replace URL-safe characters with standard Base64
            var standard = padded.Replace('-', '+').Replace('_', '/');

            var bytes = Convert.FromBase64String(standard);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null;
        }
    }

    private static DateTime UnixTimeStampToDateTime(long unixTimeStamp)
    {
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
        dateTime = dateTime.AddSeconds(unixTimeStamp);
        return dateTime;
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Trim and collapse multiple spaces into one
        return System.Text.RegularExpressions.Regex.Replace(name.Trim(), @"\s+", " ");
    }

    private static Dictionary<string, string> ParseQueryString(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrEmpty(query))
            return result;

        var queryString = query.StartsWith('?') ? query.Substring(1) : query;
        var pairs = queryString.Split('&');

        foreach (var pair in pairs)
        {
            var keyValue = pair.Split('=');
            if (keyValue.Length == 2)
            {
                var key = Uri.UnescapeDataString(keyValue[0]);
                var value = Uri.UnescapeDataString(keyValue[1]);
                result[key] = value;
            }
        }

        return result;
    }

    private static string BuildValidationErrorMessage(DokumePassValidationFlags flags)
    {
        var parts = new List<string>();

        if ((flags & DokumePassValidationFlags.FirstNameMismatch) != 0)
            parts.Add("Vorname stimmt nicht überein");

        if ((flags & DokumePassValidationFlags.LastNameMismatch) != 0)
            parts.Add("Nachname stimmt nicht überein");

        if ((flags & DokumePassValidationFlags.BirthYearMismatch) != 0)
            parts.Add("Geburtsjahr stimmt nicht überein");

        if ((flags & DokumePassValidationFlags.PassExpired) != 0)
            parts.Add("Lizenz ist abgelaufen");

        return parts.Count > 0 ? string.Join("; ", parts) : "Lizenz ungültig";
    }
}
