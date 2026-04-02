using System.Security.Cryptography;
using System.Text;

namespace ReservationSystem.Shared.Business.Security;

/// <summary>
/// Shared password and token hashing utilities used by the Identity and User
/// microservices when creating accounts, verifying credentials, and managing
/// refresh tokens.
///
/// All hashing uses SHA-256. Passwords and tokens are stored as Base64-encoded
/// hashes — the plaintext is never persisted.
/// </summary>
public static class PasswordHasher
{
    /// <summary>
    /// Hashes a plaintext password with SHA-256 and returns the Base64-encoded digest.
    /// </summary>
    public static string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Verifies a plaintext password against a stored SHA-256 hash.
    /// </summary>
    public static bool VerifyPassword(string plaintext, string storedHash)
    {
        var computedHash = HashPassword(plaintext);
        return string.Equals(computedHash, storedHash, StringComparison.Ordinal);
    }

    /// <summary>
    /// Hashes an opaque token string (e.g. a refresh token) with SHA-256 and
    /// returns the Base64-encoded digest, suitable for safe storage.
    /// </summary>
    public static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Generates a cryptographically random 32-byte token and returns it
    /// Base64-encoded. Used for refresh tokens and one-time reset tokens.
    /// </summary>
    public static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes);
    }
}
