using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Domain.Repositories;

namespace ReservationSystem.Microservices.Customer.Application.AddCustomerOrder;

/// <summary>
/// Links a confirmed order to a loyalty account.
/// Returns false when the customer is not found; true on success.
/// </summary>
public sealed class AddCustomerOrderHandler
{
    private readonly ICustomerRepository _customerRepository;
    private readonly ICustomerOrderRepository _customerOrderRepository;
    private readonly ILogger<AddCustomerOrderHandler> _logger;

    public AddCustomerOrderHandler(
        ICustomerRepository customerRepository,
        ICustomerOrderRepository customerOrderRepository,
        ILogger<AddCustomerOrderHandler> logger)
    {
        _customerRepository = customerRepository;
        _customerOrderRepository = customerOrderRepository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(AddCustomerOrderCommand command, CancellationToken cancellationToken = default)
    {
        var customer = await _customerRepository.GetByLoyaltyNumberAsync(command.LoyaltyNumber, cancellationToken);
        if (customer is null)
        {
            _logger.LogWarning("AddCustomerOrder: customer not found for loyalty number {LoyaltyNumber}", command.LoyaltyNumber);
            return false;
        }

        // Idempotent — skip if already recorded
        if (await _customerOrderRepository.ExistsAsync(command.OrderId, cancellationToken))
        {
            _logger.LogDebug("AddCustomerOrder: order {OrderId} already linked to customer {LoyaltyNumber}", command.OrderId, command.LoyaltyNumber);
            return true;
        }

        var order = CustomerOrder.Create(customer.CustomerId, command.OrderId, command.BookingReference);
        await _customerOrderRepository.AddAsync(order, cancellationToken);

        _logger.LogInformation("Linked order {BookingReference} to customer {LoyaltyNumber}", command.BookingReference, command.LoyaltyNumber);
        return true;
    }
}
