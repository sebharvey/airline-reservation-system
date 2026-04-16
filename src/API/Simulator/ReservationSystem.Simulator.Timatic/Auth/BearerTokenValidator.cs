// Description: Validates incoming Bearer tokens against the configured Timatic:ApiToken value
// by comparing SHA-256 hashes of both strings. No plaintext comparison is performed.

using System.Security.Cryptography;
using System.Text;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Configuration;

namespace ReservationSystem.Simulator.Timatic.Auth;

internal static class BearerTokenValidator
{
    private const string ConfigKey = "Timatic:ApiToken";

    /// <summary>
    /// Returns true when the incoming Authorization: Bearer token's SHA-256 hash matches
    /// the SHA-256 hash of the value stored in <c>Timatic:ApiToken</c> app configuration.
    /// Returns false if the header is absent, malformed, or the stored token is not configured.
    /// </summary>
    internal static bool IsAuthorized(HttpRequestData request, IConfiguration configuration)
    {
        var storedToken = configuration[ConfigKey];
        if (string.IsNullOrEmpty(storedToken))
            return false;

        if (!request.Headers.TryGetValues("Authorization", out var values))
            return false;

        var authHeader = values.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return false;

        var incomingToken = authHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrEmpty(incomingToken))
            return false;

        return Hash(incomingToken) == Hash(storedToken);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
