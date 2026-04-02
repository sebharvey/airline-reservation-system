using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.GetCustomerOrders;

/// <summary>
/// Returns all order references linked to a loyalty account.
/// Returns null when the customer is not found.
/// </summary>
public sealed class GetCustomerOrdersHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerOrderRepository _customerOrderRepository;

    public GetCustomerOrdersHandler(
        ICustomerRepository customerRepository,
        ICustomerOrderRepository customerOrderRepository)
    {
        _customerRepository = customerRepository;
        _customerOrderRepository = customerOrderRepository;
    }

    public async Task<IReadOnlyList<CustomerOrder>?> HandleAsync(
        GetCustomerOrdersQuery query, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByLoyaltyNumberAsync(query.LoyaltyNumber, cancellationToken);
        if (customer is null)
            return null;

        return await _customerOrderRepository.GetByCustomerIdAsync(customer.CustomerId, cancellationToken);
    }
}
