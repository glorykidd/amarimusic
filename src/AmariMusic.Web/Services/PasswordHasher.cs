using System.Security.Cryptography;

namespace AmariMusic.Services;

/// <summary>
/// PBKDF2-based password hashing for admin credentials. Hash format:
/// "{iterations}.{saltBase64}.{hashBase64}".
/// </summary>
public static class PasswordHasher
{
    private const int Iterations = 210_000;
    private const int MaxIterations = 2_000_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string? encodedHash)
    {
        if (string.IsNullOrWhiteSpace(encodedHash))
            return false;

        var parts = encodedHash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations) || iterations < 1 || iterations > MaxIterations)
            return false;

        byte[] salt, expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        if (salt.Length != SaltSize || expectedHash.Length != HashSize)
            return false;

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
