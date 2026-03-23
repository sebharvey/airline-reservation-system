using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;

namespace ReservationSystem.Orchestration.Loyalty.Application.Login;

/// <summary>
/// Orchestrates a login request by delegating credential validation and
/// token issuance entirely to the Identity microservice.
/// </summary>
public sealed class LoginHandler
{
    private readonly IdentityServiceClient _identityServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;

    public LoginHandler(
        IdentityServiceClient identityServiceClient,
        CustomerServiceClient customerServiceClient)
    {
        _identityServiceClient = identityServiceClient;
        _customerServiceClient = customerServiceClient;
    }

    public async Task<LoginResponse> HandleAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var result = await _identityServiceClient.LoginAsync(command.Email, command.Password, cancellationToken);

        var customer = await _customerServiceClient.GetCustomerByIdentityIdAsync(result.UserAccountId, cancellationToken);

        return new LoginResponse
        {
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            ExpiresAt = result.ExpiresAt,
            TokenType = "Bearer",
            LoyaltyNumber = customer?.LoyaltyNumber ?? string.Empty
        };
    }
}
