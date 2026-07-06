using System.Text;
using JudoTournamentManagement.Api.Models;
using JudoTournamentManagement.Api.Services;

namespace JudoTournamentManagement.Api.Tests;

[Trait("Category", "UnitTest")]
public sealed class Dm4AthleteImportParserTests
{
    private readonly Dm4AthleteImportParser _parser = new();

    private static string BuildValidContent(string geschlecht) => string.Join("\n",
    [
        "[Identifikation]",
        "File=Diskmelder",
        "Type=V",
        "Version=4",
        "[Meldung]",
        $"Geschlecht={geschlecht}",
        "[Vereine]",
        "Anzahl=1",
        "1=\"\"1\",\"DJK Dülmen\",\"\",\"6003003\",\"Meis\",\"Jean-Andre\",\"Hasenpatt 8\",\"48249\",\"Dülmen\",\"01794853997\",\"\",\"\",\"jean-andre.meis@posteo.de\",\"\",\"Coesfeld\",\"Münster\",\"NRW\",\"\",\"\"\"",
        "[Teilnehmer]",
        "1=\"\"1\",\"Höfer\",\"Justus\",\"4\",\"34\",\"\",\"2015\",\"\",\"\",\"\",\"\",\"\"\"",
        "2=\"\"1\",\"Kriesch\",\"Karl\",\"5\",\"26\",\"\",\"2016\",\"\",\"\",\"\",\"\",\"\"\"",
        "Anzahl=2"
    ]);

    [Fact]
    public void Parse_WithValidDm4_ReturnsMappedAthletes()
    {
        var content = BuildValidContent("m");

        var result = _parser.Parse(Encoding.UTF8.GetBytes(content));

        Assert.Equal("DJK Dülmen", result.ClubName);
        Assert.Equal(Gender.Male, result.Gender);
        Assert.Equal(2, result.Athletes.Count);

        var first = result.Athletes[0];
        Assert.Equal("Höfer", first.LastName);
        Assert.Equal("Justus", first.FirstName);
        Assert.Equal(4, first.Grade);
        Assert.Equal(34m, first.WeightKg);
        Assert.Equal(2015, first.BirthYear);
    }

    [Fact]
    public void Parse_WithFemaleGenderMarker_ReturnsFemaleAthletes()
    {
        var content = BuildValidContent("w")
            .Replace("Höfer\",\"Justus\",\"4\",\"34\",\"\",\"2015", "Willerding\",\"Johanna\",\"7\",\"50\",\"\",\"2011", StringComparison.Ordinal)
            .Replace("2=\"\"1\",\"Kriesch\",\"Karl\",\"5\",\"26\",\"\",\"2016\",\"\",\"\",\"\",\"\",\"\"\"\nAnzahl=2", "Anzahl=1", StringComparison.Ordinal);

        var result = _parser.Parse(Encoding.UTF8.GetBytes(content));

        Assert.Equal(Gender.Female, result.Gender);
        Assert.Single(result.Athletes);
    }

    [Fact]
    public void Parse_WithMissingParticipants_Throws()
    {
        var content = string.Join("\n",
        [
            "[Meldung]",
            "Geschlecht=m",
            "[Vereine]",
            "Anzahl=1",
            "1=\"\"1\",\"DJK Dülmen\",\"\",\"6003003\",\"Meis\",\"Jean-Andre\",\"Hasenpatt 8\",\"48249\",\"Dülmen\",\"01794853997\",\"\",\"\",\"jean-andre.meis@posteo.de\",\"\",\"Coesfeld\",\"Münster\",\"NRW\",\"\",\"\"\""
        ]);

        var ex = Assert.Throws<Dm4ImportParseException>(() => _parser.Parse(Encoding.UTF8.GetBytes(content)));

        Assert.Contains("[Teilnehmer]", ex.Message);
    }

    [Fact]
    public void Parse_WithInvalidRow_Throws()
    {
        var content = string.Join("\n",
        [
            "[Meldung]",
            "Geschlecht=m",
            "[Vereine]",
            "Anzahl=1",
            "1=\"\"1\",\"DJK Dülmen\",\"\",\"6003003\",\"Meis\",\"Jean-Andre\",\"Hasenpatt 8\",\"48249\",\"Dülmen\",\"01794853997\",\"\",\"\",\"jean-andre.meis@posteo.de\",\"\",\"Coesfeld\",\"Münster\",\"NRW\",\"\",\"\"\"",
            "[Teilnehmer]",
            "1=\"\"1\",\"OnlyLastName\",\"\",\"\",\"\",\"\"",
            "Anzahl=1"
        ]);

        Assert.Throws<Dm4ImportParseException>(() => _parser.Parse(Encoding.UTF8.GetBytes(content)));
    }

    [Fact]
    public void Parse_WithWindows1252Bytes_DecodesUmlauts()
    {
        var content = BuildValidContent("m");

        // Encoding.Latin1 uses single-byte code points and reproduces common umlauts.
        var bytes = Encoding.Latin1.GetBytes(content);
        var result = _parser.Parse(bytes);

        Assert.Equal("DJK Dülmen", result.ClubName);
        Assert.Equal("Höfer", result.Athletes[0].LastName);
    }
}
