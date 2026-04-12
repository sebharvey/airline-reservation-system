namespace ReservationSystem.Microservices.Offer.Application.RepriceStoredOffer;

public sealed record RepriceStoredOfferCommand(Guid OfferId, Guid? SessionId);
