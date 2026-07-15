using System.Globalization;
using System.Text;
using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Parser for NWJV E-Melder DM4 exports.
/// </summary>
public sealed class Dm4AthleteImportParser : IDm4AthleteImportParser
{
    /// <inheritdoc />
    public Dm4AthleteImportData Parse(ReadOnlyMemory<byte> fileContent)
    {
        if (fileContent.Length == 0)
        {
            throw new Dm4ImportParseException("Die DM4-Datei ist leer.");
        }

        var text = Decode(fileContent);
        var lines = SplitLines(text);

        var section = string.Empty;
        string? genderMarker = null;
        string? clubLineValue = null;
        var participantLines = new List<(int LineNumber, string Value)>();

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var line = lines[i].Trim();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line;
                continue;
            }

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (section == "[Meldung]" && key.Equals("Geschlecht", StringComparison.OrdinalIgnoreCase))
            {
                genderMarker = value;
                continue;
            }

            if (section == "[Vereine]" && !key.Equals("Anzahl", StringComparison.OrdinalIgnoreCase))
            {
                if (clubLineValue is not null)
                {
                    throw new Dm4ImportParseException("Die DM4-Datei muss genau einen Verein in [Vereine] enthalten.");
                }

                clubLineValue = value;
                continue;
            }

            if (section == "[Teilnehmer]" && !key.Equals("Anzahl", StringComparison.OrdinalIgnoreCase))
            {
                participantLines.Add((lineNumber, value));
            }
        }

        if (clubLineValue is null)
        {
            throw new Dm4ImportParseException("Die Sektion [Vereine] mit genau einem Verein fehlt.");
        }

        if (participantLines.Count == 0)
        {
            throw new Dm4ImportParseException("Die Sektion [Teilnehmer] enthält keine Athleten.");
        }

        var gender = ParseGender(genderMarker);
        var clubFields = ParseQuotedCsv(clubLineValue, 0);
        if (clubFields.Count < 2 || string.IsNullOrWhiteSpace(clubFields[1]))
        {
            throw new Dm4ImportParseException("Der Vereinsname (2. Feld in [Vereine]) fehlt.");
        }

        var clubName = clubFields[1].Trim();

        string? contactName = null;
        string? contactEmail = null;
        string? contactPhone = null;

        if (clubFields.Count > 5)
        {
            var firstName = clubFields[5].Trim();
            var lastName = clubFields[4].Trim();
            if (!string.IsNullOrEmpty(firstName) || !string.IsNullOrEmpty(lastName))
            {
                contactName = $"{firstName} {lastName}".Trim();
            }
        }

        if (clubFields.Count > 9)
        {
            var phone = clubFields[9].Trim();
            if (!string.IsNullOrEmpty(phone))
            {
                contactPhone = phone;
            }
        }

        if (clubFields.Count > 12)
        {
            var email = clubFields[12].Trim();
            if (!string.IsNullOrEmpty(email))
            {
                contactEmail = email;
            }
        }

        var athletes = new List<Dm4AthleteImportRow>(participantLines.Count);

        foreach (var participantLine in participantLines)
        {
            var fields = ParseQuotedCsv(participantLine.Value, participantLine.LineNumber);
            if (fields.Count < 7)
            {
                throw new Dm4ImportParseException(
                    $"Teilnehmer-Zeile {participantLine.LineNumber} ist unvollständig (mindestens 7 Felder erforderlich).");
            }

            var lastName = fields[1].Trim();
            var firstName = fields[2].Trim();
            if (string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(firstName))
            {
                throw new Dm4ImportParseException(
                    $"Teilnehmer-Zeile {participantLine.LineNumber}: Vorname/Nachname darf nicht leer sein.");
            }

            if (!int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var grade)
                || grade is < 1 or > 14)
            {
                throw new Dm4ImportParseException(
                    $"Teilnehmer-Zeile {participantLine.LineNumber}: Ungueltiger Guertelgrad im 4. Feld.");
            }

            decimal? weight = null;
            var weightField = fields[4].Trim();
            if (!string.IsNullOrWhiteSpace(weightField))
            {
                if (!decimal.TryParse(weightField, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedWeight)
                    && !decimal.TryParse(weightField, NumberStyles.Number, CultureInfo.GetCultureInfo("de-DE"), out parsedWeight))
                {
                    throw new Dm4ImportParseException(
                        $"Teilnehmer-Zeile {participantLine.LineNumber}: Ungueltiges Gewicht im 5. Feld.");
                }

                weight = parsedWeight;
            }

            if (!int.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var birthYear)
                || birthYear is < 1940 or > 2030)
            {
                throw new Dm4ImportParseException(
                    $"Teilnehmer-Zeile {participantLine.LineNumber}: Ungueltiger Jahrgang im 7. Feld.");
            }

            athletes.Add(new Dm4AthleteImportRow(lastName, firstName, grade, weight, birthYear));
        }

        return new Dm4AthleteImportData(clubName, contactName, contactEmail, contactPhone, gender, athletes);
    }

    private static Gender ParseGender(string? marker)
    {
        if (string.Equals(marker?.Trim(), "m", StringComparison.OrdinalIgnoreCase))
        {
            return Gender.Male;
        }

        if (string.Equals(marker?.Trim(), "w", StringComparison.OrdinalIgnoreCase))
        {
            return Gender.Female;
        }

        throw new Dm4ImportParseException("Das Feld Geschlecht in [Meldung] muss 'm' oder 'w' sein.");
    }

    private static string Decode(ReadOnlyMemory<byte> fileContent)
    {
        var utf8Strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        try
        {
            return utf8Strict.GetString(fileContent.Span);
        }
        catch (DecoderFallbackException)
        {
            // Fallback for legacy NWJV exports. Latin1 is always available in .NET
            // and correctly decodes common Western European umlauts used in names.
            return Encoding.Latin1.GetString(fileContent.Span);
        }
    }

    private static string[] SplitLines(string content)
    {
        return content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');
    }

    private static IReadOnlyList<string> ParseQuotedCsv(string raw, int lineNumber)
    {
        var value = raw.Trim();

        // NWJV DM4 rows contain an extra leading and trailing quote around the CSV payload.
        if (value.StartsWith("\"\"", StringComparison.Ordinal))
        {
            value = value[1..];
        }

        if (value.EndsWith("\"\"", StringComparison.Ordinal))
        {
            value = value[..^1];
        }

        var fields = new List<string>();
        var index = 0;

        while (index < value.Length)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            if (index >= value.Length)
            {
                break;
            }

            if (value[index] != '"')
            {
                throw new Dm4ImportParseException(
                    lineNumber > 0
                        ? $"Teilnehmer-Zeile {lineNumber}: Unerwartetes CSV-Format."
                        : "Unerwartetes CSV-Format in [Vereine].");
            }

            index++;
            var field = new StringBuilder();

            while (index < value.Length)
            {
                var current = value[index];
                if (current == '"')
                {
                    if (index + 1 < value.Length && value[index + 1] == '"')
                    {
                        field.Append('"');
                        index += 2;
                        continue;
                    }

                    index++;
                    break;
                }

                field.Append(current);
                index++;
            }

            fields.Add(field.ToString());

            while (index < value.Length && char.IsWhiteSpace(value[index]))
            {
                index++;
            }

            if (index < value.Length)
            {
                if (value[index] != ',')
                {
                    throw new Dm4ImportParseException(
                        lineNumber > 0
                            ? $"Teilnehmer-Zeile {lineNumber}: Unerwartetes CSV-Format."
                            : "Unerwartetes CSV-Format in [Vereine].");
                }

                index++;
            }
        }

        return fields;
    }
}
