using JudoTournamentManagement.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class DokumePassParserTests
{
    private readonly IDokumePassParser _parser = new DokumePassParser(NullLogger<DokumePassParser>.Instance);

    [Fact]
    public void ParseQrUrl_WithValidUrl_ExtractsClaimsSuccessfully()
    {
        const string validQrUrl = "https://qr.dokume.net?d=l&i=48958&s=eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzM4NCJ9.eyJpc3MiOiJEb2t1TWUiLCJVSUQiOiI0ODk1OCIsIk5PIjoiSi0wMDgwNjU5IiwiQ0lEIjoiTS0wMDgwNzcwIiwiSUQiOiI0NjkwMjUiLCJGTiI6IkpvbmFzIiwiTE4iOiJXaW5rbGVyIiwiRE9CIjoiMTk4NS0wMy0xOCIsIk5BVCI6IjM3IiwiVE0iOiIiLCJMVCI6IjUyIiwiTFROIjoiSnVkb3Bhc3MiLCJleHAiOjE4MDM4NTU1OTAsIkxUMiI6NjksIktFWSI6ImNtUlpURzVLVDNsblkwODBibGRzV1hkcFVGcFJSVEpyY2xkNWJBPT0ifQ.UpMF99IJm--i_ry8efCvuwKfUuH_OWwucZkabuJDPNrebcvHumlgx5rQq1suEw9-r-hsXNaOoU7Y8MYlbFBdb4xp-bchNwlJzGxDeRoEcGDIG7-LfICKgQSAzdNY7ULivGR-_YvA6YVHgZGxLsDA_CTMFXNPUateNTGyPzj-BDs";

        var result = _parser.ParseQrUrl(validQrUrl);

        Assert.NotNull(result);
        Assert.Equal("L-TEST-9001", result.PassNumber);
        Assert.Equal("Alex", result.FirstName);
        Assert.Equal("Muster", result.LastName);
        Assert.Equal(new DateOnly(1985, 3, 18), result.DateOfBirth);
        Assert.Equal("LizenzTest", result.LicenseTypeName);
        Assert.Equal("DokuMe", result.Issuer);
        Assert.True(result.IsRs384Claimed);
        Assert.False(result.SignatureVerified); // Signature is deliberately not verified
    }

    [Fact]
    public void ParseQrUrl_WithNullUrl_ReturnsNull()
    {
        var result = _parser.ParseQrUrl(null);
        Assert.Null(result);
    }

    [Fact]
    public void ParseQrUrl_WithEmptyUrl_ReturnsNull()
    {
        var result = _parser.ParseQrUrl(string.Empty);
        Assert.Null(result);
    }

    [Fact]
    public void ParseQrUrl_WithInvalidHost_ReturnsNull()
    {
        const string invalidQrUrl = "https://evil.example.com?d=l&s=eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzM4NCJ9.test.test";
        var result = _parser.ParseQrUrl(invalidQrUrl);
        Assert.Null(result);
    }

    [Fact]
    public void ParseQrUrl_WithInvalidScheme_ReturnsNull()
    {
        const string invalidQrUrl = "http://qr.dokume.net?d=l&s=eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzM4NCJ9.test.test";
        var result = _parser.ParseQrUrl(invalidQrUrl);
        Assert.Null(result);
    }

    [Fact]
    public void ParseQrUrl_WithMissingDocumentType_ReturnsNull()
    {
        const string invalidQrUrl = "https://qr.dokume.net?i=48958&s=eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzM4NCJ9.test.test";
        var result = _parser.ParseQrUrl(invalidQrUrl);
        Assert.Null(result);
    }

    [Fact]
    public void ParseQrUrl_WithWrongDocumentType_ReturnsNull()
    {
        const string invalidQrUrl = "https://qr.dokume.net?d=x&s=eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzM4NCJ9.test.test";
        var result = _parser.ParseQrUrl(invalidQrUrl);
        Assert.Null(result);
    }

    [Fact]
    public void ParseQrUrl_WithMissingJwtToken_ReturnsNull()
    {
        const string invalidQrUrl = "https://qr.dokume.net?d=l&i=48958";
        var result = _parser.ParseQrUrl(invalidQrUrl);
        Assert.Null(result);
    }

    [Fact]
    public void ParseQrUrl_WithMalformedJwt_ReturnsNull()
    {
        const string invalidQrUrl = "https://qr.dokume.net?d=l&s=invalid.jwt.structure";
        var result = _parser.ParseQrUrl(invalidQrUrl);
        Assert.Null(result);
    }

    [Fact]
    public void ParseQrUrl_WithWrongAlgorithm_ReturnsNull()
    {
        const string invalidQrUrl = "https://qr.dokume.net?d=l&s=eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.test.test";
        var result = _parser.ParseQrUrl(invalidQrUrl);
        Assert.Null(result);
    }

    [Fact]
    public void ParseQrUrl_WithWrongIssuer_ReturnsNull()
    {
        const string invalidQrUrl = "https://qr.dokume.net?d=l&s=eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzM4NCJ9.eyJpc3MiOiJFdmlsSXNzdWVyIn0.test";
        var result = _parser.ParseQrUrl(invalidQrUrl);
        Assert.Null(result);
    }

    [Fact]
    public void ParseQrUrl_WithMissingClaims_ReturnsNull()
    {
        // JWT with missing required claims (NO, FN, LN, DOB, or exp)
        const string invalidQrUrl = "https://qr.dokume.net?d=l&s=eyJ0eXAiOiJKV1QiLCJhbGciOiJSUzM4NCJ9.eyJpc3MiOiJEb2t1TWUiLCJGTiI6IkpvaG4ifQ.test";
        var result = _parser.ParseQrUrl(invalidQrUrl);
        Assert.Null(result);
    }

    [Fact]
    public void ValidatePass_WithMatchingData_ReturnsValid()
    {
        var pass = new DokumePassCheckResult
        {
            PassNumber = "L-TEST-9001",
            FirstName = "Alex",
            LastName = "Muster",
            DateOfBirth = new DateOnly(1985, 3, 18),
            LicenseTypeName = "LizenzTest",
            ExpiryDate = new DateOnly(2027, 2, 28),
            Issuer = "DokuMe",
            IsRs384Claimed = true
        };

        var result = _parser.ValidatePass(
            pass,
            new DateOnly(2026, 7, 14),
            "Alex",
            "Muster",
            1985);

        Assert.True(result.IsValid);
        Assert.Equal(DokumePassValidationFlags.None, result.Flags);
        Assert.Contains("gültig", result.Message);
    }

    [Fact]
    public void ValidatePass_WithMismatchedFirstName_ReturnsInvalid()
    {
        var pass = new DokumePassCheckResult
        {
            PassNumber = "L-TEST-9001",
            FirstName = "Alex",
            LastName = "Muster",
            DateOfBirth = new DateOnly(1985, 3, 18),
            LicenseTypeName = "LizenzTest",
            ExpiryDate = new DateOnly(2027, 2, 28),
            Issuer = "DokuMe",
            IsRs384Claimed = true
        };

        var result = _parser.ValidatePass(
            pass,
            new DateOnly(2026, 7, 14),
            "Johann",
            "Muster",
            1985);

        Assert.False(result.IsValid);
        Assert.True((result.Flags & DokumePassValidationFlags.FirstNameMismatch) != 0);
    }

    [Fact]
    public void ValidatePass_WithMismatchedLastName_ReturnsInvalid()
    {
        var pass = new DokumePassCheckResult
        {
            PassNumber = "L-TEST-9001",
            FirstName = "Alex",
            LastName = "Muster",
            DateOfBirth = new DateOnly(1985, 3, 18),
            LicenseTypeName = "LizenzTest",
            ExpiryDate = new DateOnly(2027, 2, 28),
            Issuer = "DokuMe",
            IsRs384Claimed = true
        };

        var result = _parser.ValidatePass(
            pass,
            new DateOnly(2026, 7, 14),
            "Alex",
            "Mueller",
            1985);

        Assert.False(result.IsValid);
        Assert.True((result.Flags & DokumePassValidationFlags.LastNameMismatch) != 0);
    }

    [Fact]
    public void ValidatePass_WithMismatchedBirthYear_ReturnsInvalid()
    {
        var pass = new DokumePassCheckResult
        {
            PassNumber = "L-TEST-9001",
            FirstName = "Alex",
            LastName = "Muster",
            DateOfBirth = new DateOnly(1985, 3, 18),
            LicenseTypeName = "LizenzTest",
            ExpiryDate = new DateOnly(2027, 2, 28),
            Issuer = "DokuMe",
            IsRs384Claimed = true
        };

        var result = _parser.ValidatePass(
            pass,
            new DateOnly(2026, 7, 14),
            "Alex",
            "Muster",
            1990);

        Assert.False(result.IsValid);
        Assert.True((result.Flags & DokumePassValidationFlags.BirthYearMismatch) != 0);
    }

    [Fact]
    public void ValidatePass_WithExpiredPass_ReturnsInvalid()
    {
        var pass = new DokumePassCheckResult
        {
            PassNumber = "L-TEST-9001",
            FirstName = "Alex",
            LastName = "Muster",
            DateOfBirth = new DateOnly(1985, 3, 18),
            LicenseTypeName = "LizenzTest",
            ExpiryDate = new DateOnly(2026, 6, 30), // Tournament date is after expiry
            Issuer = "DokuMe",
            IsRs384Claimed = true
        };

        var result = _parser.ValidatePass(
            pass,
            new DateOnly(2026, 7, 14),
            "Alex",
            "Muster",
            1985);

        Assert.False(result.IsValid);
        Assert.True((result.Flags & DokumePassValidationFlags.PassExpired) != 0);
    }

    [Fact]
    public void ValidatePass_OnExpiryDate_IsValid()
    {
        var pass = new DokumePassCheckResult
        {
            PassNumber = "L-TEST-9001",
            FirstName = "Alex",
            LastName = "Muster",
            DateOfBirth = new DateOnly(1985, 3, 18),
            LicenseTypeName = "LizenzTest",
            ExpiryDate = new DateOnly(2026, 7, 14), // Tournament on exact expiry date
            Issuer = "DokuMe",
            IsRs384Claimed = true
        };

        var result = _parser.ValidatePass(
            pass,
            new DateOnly(2026, 7, 14),
            "Alex",
            "Muster",
            1985);

        Assert.True(result.IsValid);
        Assert.False((result.Flags & DokumePassValidationFlags.PassExpired) != 0);
    }

    [Fact]
    public void ValidatePass_WithWhitespaceVariations_NormalizesNames()
    {
        var pass = new DokumePassCheckResult
        {
            PassNumber = "L-TEST-9001",
            FirstName = "  Alex  ",
            LastName = "Muster",
            DateOfBirth = new DateOnly(1985, 3, 18),
            LicenseTypeName = "LizenzTest",
            ExpiryDate = new DateOnly(2027, 2, 28),
            Issuer = "DokuMe",
            IsRs384Claimed = true
        };

        var result = _parser.ValidatePass(
            pass,
            new DateOnly(2026, 7, 14),
            "Alex",
            "Muster",
            1985);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidatePass_WithCaseVariations_IgnoresCase()
    {
        var pass = new DokumePassCheckResult
        {
            PassNumber = "L-TEST-9001",
            FirstName = "ALEX",
            LastName = "muster",
            DateOfBirth = new DateOnly(1985, 3, 18),
            LicenseTypeName = "LizenzTest",
            ExpiryDate = new DateOnly(2027, 2, 28),
            Issuer = "DokuMe",
            IsRs384Claimed = true
        };

        var result = _parser.ValidatePass(
            pass,
            new DateOnly(2026, 7, 14),
            "Alex",
            "Muster",
            1985);

        Assert.True(result.IsValid);
    }
}

