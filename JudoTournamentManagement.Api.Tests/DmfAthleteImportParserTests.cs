using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class DmfAthleteImportParserTests
{
    private readonly DmfAthleteImportParser _parser = new();

    [Theory]
    [InlineData("BEM-200202U18 (m).dmf", 1, "DJK Sportfreunde Dülmen", "Ciunta", "Raul Emanuel", 2003, 60)]
    [InlineData("DJK-Duelmen-220129U13 (m).dmf", 3, "DJK Sportfreunde Dülmen", "Klapper", "Henri", 2011, 35)]
    [InlineData("DJK-Duelmen-220129U15 (m).dmf", 1, "DJK Sportfreunde Dülmen", "Oechtering", "Jonas", 2009, 50)]
    public void Parse_WithKnownDmfSamples_ReturnsAthletes(
        string fileName,
        int expectedCount,
        string expectedClub,
        string firstLastName,
        string firstFirstName,
        int firstBirthYear,
        decimal firstWeight)
    {
        var filePath = Path.Combine(FindRepositoryRoot(), fileName);
        var bytes = File.ReadAllBytes(filePath);

        var result = _parser.Parse(bytes, fileName);

        Assert.Equal(expectedClub, result.ClubName);
        Assert.Equal(Gender.Male, result.Gender);
        Assert.Equal(expectedCount, result.Athletes.Count);

        var first = result.Athletes[0];
        Assert.Equal(firstLastName, first.LastName);
        Assert.Equal(firstFirstName, first.FirstName);
        Assert.Equal(firstBirthYear, first.BirthYear);
        Assert.Equal(firstWeight, first.WeightKg);
    }

    [Fact]
    public void Parse_WithoutGenderMarker_Throws()
    {
        var filePath = Path.Combine(FindRepositoryRoot(), "BEM-200202U18 (m).dmf");
        var bytes = File.ReadAllBytes(filePath);

        var ex = Assert.Throws<DmfImportParseException>(() => _parser.Parse(bytes, "BEM-200202U18.dmf"));

        Assert.Contains("Geschlecht", ex.Message);
    }

    [Fact]
    public void Parse_WithInvalidHeader_Throws()
    {
        var bytes = "not-a-dmf"u8.ToArray();

        var ex = Assert.Throws<DmfImportParseException>(() => _parser.Parse(bytes, "invalid (m).dmf"));

        Assert.Contains("DMF", ex.Message);
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var marker = Path.Combine(current.FullName, "JudoTournamentManagement.sln");
            if (File.Exists(marker))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
