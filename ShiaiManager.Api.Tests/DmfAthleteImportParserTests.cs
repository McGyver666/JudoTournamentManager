using ShiaiManager.Api.Models;
using ShiaiManager.Api.Services;

namespace ShiaiManager.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class DmfAthleteImportParserTests
{
    private readonly DmfAthleteImportParser _parser = new();

    [Theory]
    [InlineData("dmf-fixture-01 (m).dmf", 1, "Judo Testverein Nord", "Muster", "Test Person", 2005, 66)]
    [InlineData("dmf-fixture-02 (m).dmf", 3, "Judo Testverein Nord", "Muster", "Anna", 2012, 32)]
    [InlineData("dmf-fixture-03 (m).dmf", 1, "Judo Testverein Nord", "Testname", "Lena", 2011, 48)]
    public void Parse_WithKnownDmfSamples_ReturnsAthletes(
        string fileName,
        int expectedCount,
        string expectedClub,
        string firstLastName,
        string firstFirstName,
        int firstBirthYear,
        decimal firstWeight)
    {
        var filePath = GetFixturePath(fileName);
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
        var filePath = GetFixturePath("dmf-fixture-01 (m).dmf");
        var bytes = File.ReadAllBytes(filePath);

        var ex = Assert.Throws<DmfImportParseException>(() => _parser.Parse(bytes, "dmf-fixture-01.dmf"));

        Assert.Contains("Geschlecht", ex.Message);
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(FindRepositoryRoot(), "ShiaiManager.Api.Tests", "TestData", "Dmf", fileName);

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
            var marker = Path.Combine(current.FullName, "ShiaiManager.sln");
            if (File.Exists(marker))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
