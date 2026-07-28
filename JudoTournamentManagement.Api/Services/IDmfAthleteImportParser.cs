using JudoTournamentManagement.Api.Models;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Parses Meisterschaftsmanager DMF files into athlete import data.
/// </summary>
public interface IDmfAthleteImportParser
{
    /// <summary>
    /// Parses DMF file bytes and returns normalized import data.
    /// </summary>
    /// <param name="fileContent">Raw file bytes.</param>
    /// <param name="fileName">Original file name used to infer metadata like gender marker.</param>
    /// <exception cref="DmfImportParseException">Thrown when the input file is malformed.</exception>
    Dm4AthleteImportData Parse(ReadOnlyMemory<byte> fileContent, string fileName);
}

/// <summary>
/// Represents a malformed DMF import file.
/// </summary>
public sealed class DmfImportParseException : Exception
{
    /// <summary>
    /// Initializes a new instance of the exception.
    /// </summary>
    public DmfImportParseException(string message)
        : base(message)
    {
    }
}
