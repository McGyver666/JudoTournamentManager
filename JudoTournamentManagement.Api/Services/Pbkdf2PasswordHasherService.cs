using System.Security.Cryptography;

namespace JudoTournamentManagement.Api.Services;

/// <summary>
/// PBKDF2-SHA256 password hasher.
/// </summary>
public sealed class Pbkdf2PasswordHasherService : IPasswordHasherService
{
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int IterationCount = 210_000;

    /// <inheritdoc />
    public (byte[] Hash, byte[] Salt, int Iterations) HashPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            IterationCount,
            HashAlgorithmName.SHA256,
            HashSize);

        return (hash, salt, IterationCount);
    }

    /// <inheritdoc />
    public bool Verify(string password, byte[] hash, byte[] salt, int iterations)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        ArgumentNullException.ThrowIfNull(hash);
        ArgumentNullException.ThrowIfNull(salt);

        if (iterations <= 0)
        {
            return false;
        }

        var computed = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            hash.Length);

        return CryptographicOperations.FixedTimeEquals(computed, hash);
    }
}
