using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;

namespace ReservationSystem.Orchestration.Loyalty.Application.SearchCustomers;

public sealed class SearchCustomersHandler
{
    private readonly CustomerServiceClient _customerServiceClient;
    private readonly IdentityServiceClient _identityServiceClient;

    public SearchCustomersHandler(
        CustomerServiceClient customerServiceClient,
        IdentityServiceClient identityServiceClient)
    {
        _customerServiceClient = customerServiceClient;
        _identityServiceClient = identityServiceClient;
    }

    public async Task<IReadOnlyList<CustomerSummaryResponse>> HandleAsync(
        SearchCustomersQuery query,
        CancellationToken cancellationToken)
    {
        var searchTerm = query.Query?.Trim();

        // Run name/loyalty search and email lookup in parallel
        var customerSearchTask = _customerServiceClient.SearchCustomersAsync(searchTerm, cancellationToken);
        var emailLookupTask = LookUpByEmailAsync(searchTerm, cancellationToken);

        await Task.WhenAll(customerSearchTask, emailLookupTask);

        var customers = customerSearchTask.Result;
        var emailCustomer = emailLookupTask.Result;

        // Merge email result if it's not already in the name/loyalty results
        List<CustomerDto> merged;
        if (emailCustomer is not null &&
            !customers.Any(c => c.LoyaltyNumber == emailCustomer.LoyaltyNumber))
        {
            merged = new List<CustomerDto>(customers.Count + 1) { emailCustomer };
            merged.AddRange(customers);
        }
        else
        {
            merged = customers.ToList();
        }

        return merged.Select(c => new CustomerSummaryResponse
        {
            LoyaltyNumber = c.LoyaltyNumber,
            GivenName = c.GivenName,
            Surname = c.Surname,
            TierCode = c.TierCode,
            PointsBalance = c.PointsBalance,
            IsActive = c.IsActive,
            CreatedAt = c.CreatedAt,
        }).ToList();
    }

    private async Task<CustomerDto?> LookUpByEmailAsync(string? searchTerm, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(searchTerm) || !searchTerm.Contains('@'))
            return null;

        var identityAccount = await _identityServiceClient.GetAccountByEmailAsync(searchTerm, cancellationToken);
        if (identityAccount is null)
            return null;

        return await _customerServiceClient.GetCustomerByIdentityIdAsync(identityAccount.UserAccountId, cancellationToken);
    }
}
