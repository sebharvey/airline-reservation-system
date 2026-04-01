namespace ReservationSystem.Microservices.Ancillary.Models.Seat;

/// <summary>
/// Represents the seat count for a single cabin on an aircraft type.
/// </summary>
public sealed record CabinCount(string Cabin, int Count);
