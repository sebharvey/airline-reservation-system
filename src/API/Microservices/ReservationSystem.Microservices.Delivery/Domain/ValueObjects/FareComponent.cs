namespace ReservationSystem.Microservices.Delivery.Domain.ValueObjects;

/// <summary>
/// One component in an IATA linear fare construction — origin, carrier, destination, and NUC amount.
/// </summary>
public sealed record FareComponent(
    string Origin,
    string Carrier,
    string Destination,
    decimal NucAmount,
    string? FareBasis = null
);
