namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// Provides password hashing and verification using approved cryptographic primitives.
/// </summary>
public interface IPasswordHasherService
{
    /// <summary>
    /// Creates a password hash and salt.
    /// </summary>
    (byte[] Hash, byte[] Salt, int Iterations) HashPassword(string password);

    /// <summary>
    /// Verifies the provided password against a stored hash configuration.
    /// </summary>
    bool Verify(string password, byte[] hash, byte[] salt, int iterations);
}
