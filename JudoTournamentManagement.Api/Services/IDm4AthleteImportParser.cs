using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Parses NWJV E-Melder DM4 files into athlete import data.
/// </summary>
public interface IDm4AthleteImportParser
{
    /// <summary>
    /// Parses DM4 file bytes and returns normalized import data.
    /// </summary>
    /// <exception cref="Dm4ImportParseException">Thrown when the input file is malformed.</exception>
    Dm4AthleteImportData Parse(ReadOnlyMemory<byte> fileContent);
}

/// <summary>
/// Represents a malformed DM4 import file.
/// </summary>
public sealed class Dm4ImportParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the exception.
    /// </summary>
    public Dm4ImportParseException(string message)
        : base(message)
    {
    }
}
