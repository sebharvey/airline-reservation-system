using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Order.Domain.Repositories;
using System.Text.Json;

namespace ReservationSystem.Microservices.Order.Application.RetrieveOrder;

/// <summary>
/// Handles the <see cref="RetrieveOrderQuery"/>.
/// Retrieves an order by booking reference and validates a passenger surname match.
/// Returns null if the order is not found or the surname does not match any passenger.
/// </summary>
public sealed class RetrieveOrderHandler
{
    private readonly IOrderRepository _repository;
    private readonly ILogger<RetrieveOrderHandler> _logger;

    public RetrieveOrderHandler(
        IOrderRepository repository,
        ILogger<RetrieveOrderHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<Domain.Entities.Order?> HandleAsync(
        RetrieveOrderQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Retrieving order by booking reference {BookingReference} with surname validation",
            query.BookingReference);

        var order = await _repository.GetByBookingReferenceAsync(query.BookingReference, cancellationToken);
        if (order is null)
        {
            _logger.LogWarning("Order with booking reference {BookingReference} not found", query.BookingReference);
            return null;
        }

        // Validate surname match against passengers in order data
        try
        {
            using var doc = JsonDocument.Parse(order.OrderData);
            var hasMatch = false;
            if (doc.RootElement.TryGetProperty("dataLists", out var dataLists) &&
                dataLists.TryGetProperty("passengers", out var passengers))
            {
                foreach (var pax in passengers.EnumerateArray())
                {
                    if (pax.TryGetProperty("surname", out var sn) &&
                        string.Equals(sn.GetString(), query.Surname, StringComparison.OrdinalIgnoreCase))
                    {
                        hasMatch = true;
                        break;
                    }
                }
            }

            if (!hasMatch)
            {
                _logger.LogWarning(
                    "Surname mismatch for order with booking reference {BookingReference}",
                    query.BookingReference);
                return null;
            }
        }
        catch
        {
            // If order data cannot be parsed, treat as surname mismatch
            return null;
        }

        return order;
    }
}
