namespace ReservationSystem.Microservices.Customer.Domain.Entities;

/// <summary>
/// Links a confirmed order to a loyalty account.
/// No foreign key to order.Order — orders may be purged once flown.
/// </summary>
public sealed class CustomerOrder
{
    public Guid CustomerOrderId { get; private set; }
    public Guid CustomerId { get; private set; }
    public Guid OrderId { get; private set; }
    public string BookingReference { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }

    private CustomerOrder() { }

    public static CustomerOrder Create(Guid customerId, Guid orderId, string bookingReference)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bookingReference);

        return new CustomerOrder
        {
            CustomerOrderId = Guid.NewGuid(),
            CustomerId = customerId,
            OrderId = orderId,
            BookingReference = bookingReference,
            CreatedAt = DateTime.UtcNow
        };
    }
}
