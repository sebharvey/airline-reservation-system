using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;

namespace ReservationSystem.Orchestration.Loyalty.Application.Register;

/// <summary>
/// Orchestrates new member registration across the Customer and Identity microservices.
///
/// Sequence:
///   1. Create customer record in Customer MS (no identity link yet).
///   2. Create identity account in Identity MS (email + password).
///   3. Patch customer with the resolved IdentityId to link the two records.
///   4. Award 1,500 sign-up bonus points (Earn transaction) to the new customer.
///   5. Return profile response.
/// </summary>
public sealed class RegisterHandler
{
    private readonly IdentityServiceClient _identityServiceClient;
    private readonly CustomerServiceClient _customerServiceClient;

    public RegisterHandler(
        IdentityServiceClient identityServiceClient,
        CustomerServiceClient customerServiceClient)
    {
        _identityServiceClient = identityServiceClient;
        _customerServiceClient = customerServiceClient;
    }

    public async Task<ProfileResponse> HandleAsync(RegisterCommand command, CancellationToken cancellationToken)
    {
        // Step 1: Create customer profile (no identity link yet).
        var customer = await _customerServiceClient.CreateCustomerAsync(
            command.GivenName,
            command.Surname,
            command.DateOfBirth,
            command.PreferredLanguage ?? "en-GB",
            command.PhoneNumber,
            command.Nationality,
            cancellationToken);

        // Step 2: Create identity account.
        var identity = await _identityServiceClient.CreateAccountAsync(
            command.Email,
            command.Password,
            cancellationToken);

        // Step 3: Link identity to customer.
        await _customerServiceClient.LinkIdentityAsync(
            customer.LoyaltyNumber,
            identity.UserAccountId,
            cancellationToken);

        // Step 4: Award sign-up bonus points.
        await _customerServiceClient.AddPointsAsync(
            customer.LoyaltyNumber,
            points: 1500,
            transactionType: "Earn",
            description: "Sign up bonus",
            cancellationToken);

        // Step 5: Fetch full customer record to populate the response.
        var fullCustomer = await _customerServiceClient.GetCustomerAsync(
            customer.LoyaltyNumber, cancellationToken);

        return new ProfileResponse
        {
            LoyaltyNumber = customer.LoyaltyNumber,
            GivenName = command.GivenName,
            Surname = command.Surname,
            Email = command.Email,
            PhoneNumber = command.PhoneNumber,
            DateOfBirth = command.DateOfBirth,
            Tier = fullCustomer?.TierCode ?? customer.TierCode,
            PointsBalance = fullCustomer?.PointsBalance ?? 0,
            MemberSince = fullCustomer?.CreatedAt ?? DateTime.UtcNow
        };
    }
}
