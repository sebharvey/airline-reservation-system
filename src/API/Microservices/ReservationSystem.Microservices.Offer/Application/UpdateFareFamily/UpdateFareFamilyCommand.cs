namespace ReservationSystem.Microservices.Offer.Application.UpdateFareFamily;

public sealed record UpdateFareFamilyCommand(
    Guid FareFamilyId,
    string Name,
    string? Description,
    int DisplayOrder);
