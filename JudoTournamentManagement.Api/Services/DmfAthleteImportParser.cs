using System.Globalization;
using System.Text;
using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Parser for Meisterschaftsmanager DMF exports.
/// </summary>
public sealed class DmfAthleteImportParser : IDmfAthleteImportParser
{
    private const int MinimumBirthYear = 1940;
    private const int MaximumBirthYear = 2035;
    private const int DefaultGrade = 1;
    private const string HeaderMagic = "DiskMelderDataFile";

    /// <inheritdoc />
    public Dm4AthleteImportData Parse(ReadOnlyMemory<byte> fileContent, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (fileContent.Length == 0)
        {
            throw new DmfImportParseException("Die DMF-Datei ist leer.");
        }

        var span = fileContent.Span;
        ValidateHeader(span);

        var gender = ParseGenderFromFileName(fileName);
        var strings = ReadLengthPrefixedStrings(span);

        if (strings.Count < 13)
        {
            throw new DmfImportParseException("Die DMF-Datei enthält zu wenige Daten für den Athletenimport.");
        }

        // Header metadata in known sample files appears before club data.
        var clubName = strings[4];
        if (string.IsNullOrWhiteSpace(clubName))
        {
            throw new DmfImportParseException("Der Vereinsname in der DMF-Datei fehlt.");
        }

        var contactName = strings[8];
        var phone = strings[11];

        var athletes = ParseAthletes(strings.Skip(12).ToArray());
        if (athletes.Count == 0)
        {
            throw new DmfImportParseException("Die DMF-Datei enthält keine auswertbaren Athletenzeilen.");
        }

        return new Dm4AthleteImportData(
            clubName.Trim(),
            string.IsNullOrWhiteSpace(contactName) ? null : contactName.Trim(),
            null,
            string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            gender,
            athletes);
    }

    private static void ValidateHeader(ReadOnlySpan<byte> content)
    {
        var headerBytes = Encoding.ASCII.GetBytes(HeaderMagic);
        if (content.Length < headerBytes.Length + 1)
        {
            throw new DmfImportParseException("Die DMF-Datei ist zu kurz.");
        }

        for (var i = 0; i < headerBytes.Length; i++)
        {
            if (content[i] != headerBytes[i])
            {
                throw new DmfImportParseException("Unbekannter DMF-Dateikopf.");
            }
        }

        if (content[headerBytes.Length] != 0x1A)
        {
            throw new DmfImportParseException("Ungültiger DMF-Dateikopf.");
        }
    }

    private static Gender ParseGenderFromFileName(string fileName)
    {
        var lowerName = fileName.ToLowerInvariant();
        if (lowerName.Contains("(m)", StringComparison.Ordinal))
        {
            return Gender.Male;
        }

        if (lowerName.Contains("(w)", StringComparison.Ordinal))
        {
            return Gender.Female;
        }

        throw new DmfImportParseException("Das Geschlecht konnte aus dem Dateinamen nicht ermittelt werden. Erwartet wird '(m)' oder '(w)'.");
    }

    private static IReadOnlyList<string> ReadLengthPrefixedStrings(ReadOnlySpan<byte> content)
    {
        // Known DMF samples contain 4 bytes between header marker and first length-prefixed string.
        var offset = HeaderMagic.Length + 1 + 4;
        var values = new List<string>();

        while (TryReadNextString(content, ref offset, out var value))
        {
            values.Add(value);
            if (values.Count > 512)
            {
                break;
            }
        }

        return values;
    }

    private static bool TryReadNextString(ReadOnlySpan<byte> content, ref int offset, out string value)
    {
        for (var position = offset; position <= content.Length - 3; position++)
        {
            var candidateLength = ReadUInt16LittleEndian(content, position);
            if (candidateLength == 0 || candidateLength > 200)
            {
                continue;
            }

            var payloadStart = position + 2;
            var payloadEnd = payloadStart + candidateLength;
            if (payloadEnd > content.Length)
            {
                continue;
            }

            var payload = content[payloadStart..payloadEnd];
            if (!LooksLikeText(payload))
            {
                continue;
            }

            value = Encoding.Latin1.GetString(payload).Trim();
            offset = payloadEnd;
            return true;
        }

        value = string.Empty;
        return false;
    }

    private static int ReadUInt16LittleEndian(ReadOnlySpan<byte> content, int position)
    {
        return content[position] | (content[position + 1] << 8);
    }

    private static bool LooksLikeText(ReadOnlySpan<byte> payload)
    {
        var printableCount = 0;
        for (var i = 0; i < payload.Length; i++)
        {
            var b = payload[i];
            if (b == 0)
            {
                return false;
            }

            // Accept common western text ranges and whitespace.
            if ((b >= 0x20 && b <= 0x7E) || b >= 0x80)
            {
                printableCount++;
            }
        }

        return printableCount == payload.Length;
    }

    private static IReadOnlyList<Dm4AthleteImportRow> ParseAthletes(IReadOnlyList<string> tokens)
    {
        var athletes = new List<Dm4AthleteImportRow>();

        for (var i = 0; i + 3 < tokens.Count; i++)
        {
            var lastName = tokens[i].Trim();
            var firstName = tokens[i + 1].Trim();
            var birthYearValue = tokens[i + 2].Trim();
            var weightValue = tokens[i + 3].Trim();

            if (!LooksLikeName(lastName) || !LooksLikeName(firstName))
            {
                continue;
            }

            if (!int.TryParse(birthYearValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var birthYear)
                || birthYear < MinimumBirthYear
                || birthYear > MaximumBirthYear)
            {
                continue;
            }

            if (!TryParseWeight(weightValue, out var weightKg))
            {
                continue;
            }

            athletes.Add(new Dm4AthleteImportRow(
                lastName,
                firstName,
                DefaultGrade,
                weightKg,
                birthYear));

            i += 3;
        }

        return athletes;
    }

    private static bool LooksLikeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        // Reject obvious non-name control labels.
        return value.Any(char.IsLetter) && !value.Any(char.IsDigit);
    }

    private static bool TryParseWeight(string value, out decimal? weightKg)
    {
        weightKg = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var parsed)
            && !decimal.TryParse(value, NumberStyles.Number | NumberStyles.AllowLeadingSign, CultureInfo.GetCultureInfo("de-DE"), out parsed))
        {
            return false;
        }

        weightKg = Math.Abs(parsed);
        return true;
    }
}
