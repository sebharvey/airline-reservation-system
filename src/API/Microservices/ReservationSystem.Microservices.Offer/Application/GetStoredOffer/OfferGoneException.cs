namespace ReservationSystem.Microservices.Offer.Application.GetStoredOffer;

/// <summary>
/// Thrown when a stored offer exists but is expired or already consumed.
/// Maps to HTTP 410 Gone.
/// </summary>
public sealed class OfferGoneException : Exception
{
    public OfferGoneException(string message) : base(message) { }
}
