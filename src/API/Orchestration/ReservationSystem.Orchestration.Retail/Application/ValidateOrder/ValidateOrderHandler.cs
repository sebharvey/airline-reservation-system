using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Retail.Application.ValidateOrder;

public sealed class ValidateOrderHandler
{
    internal const string Issuer   = "apex-air-retail";
    internal const string Audience = "apex-air-manage-booking";
    internal const string BookingReferenceClaim = "booking_reference";
    private const int TokenExpiryMinutes = 60;

    private readonly OrderServiceClient _orderServiceClient;
    private readonly IConfiguration _configuration;

    public ValidateOrderHandler(OrderServiceClient orderServiceClient, IConfiguration configuration)
    {
        _orderServiceClient = orderServiceClient;
        _configuration = configuration;
    }

    public async Task<ValidateOrderResult?> HandleAsync(
        string bookingReference, string givenName, string surname, CancellationToken cancellationToken)
    {
        var order = await _orderServiceClient.RetrieveOrderAsync(bookingReference, surname, cancellationToken);
        if (order is null) return null;

        // Verify the given name matches at least one passenger (case-insensitive)
        if (order.OrderData.HasValue && !GivenNameMatchesPassenger(order.OrderData.Value, givenName))
            return null;

        var secret = _configuration["Jwt:Secret"];
        if (string.IsNullOrEmpty(secret))
            throw new InvalidOperationException("Jwt:Secret is not configured.");

        var keyBytes = Convert.FromBase64String(secret);
        var key = new SymmetricSecurityKey(keyBytes);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiresAt = DateTime.UtcNow.AddMinutes(TokenExpiryMinutes);

        var claims = new[]
        {
            new Claim(BookingReferenceClaim, bookingReference),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new ValidateOrderResult(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private static bool GivenNameMatchesPassenger(JsonElement orderData, string givenName)
    {
        try
        {
            if (!orderData.TryGetProperty("dataLists", out var dl) ||
                !dl.TryGetProperty("passengers", out var paxArray))
                return true; // cannot verify, allow through

            foreach (var pax in paxArray.EnumerateArray())
            {
                var stored = pax.TryGetProperty("givenName", out var gn) ? gn.GetString() ?? "" : "";
                if (stored.Equals(givenName, StringComparison.OrdinalIgnoreCase) ||
                    stored.StartsWith(givenName + " ", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch
        {
            return true; // if parsing fails, allow through
        }
    }
}

public sealed record ValidateOrderResult(string Token, DateTime ExpiresAt);
