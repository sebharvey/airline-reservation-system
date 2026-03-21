using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;

namespace ReservationSystem.Microservices.Order.Application.CreateOrder;

/// <summary>
/// Handles the <see cref="CreateOrderCommand"/>.
/// Confirms a basket and creates a new order.
/// </summary>
public sealed class CreateOrderHandler
{
    private readonly IBasketRepository _basketRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(
        IBasketRepository basketRepository,
        IOrderRepository orderRepository,
        ILogger<CreateOrderHandler> logger)
    {
        _basketRepository = basketRepository;
        _orderRepository = orderRepository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
