namespace ReservationSystem.Microservices.Offer.Application.CreateFareFamily;

public sealed record CreateFareFamilyCommand(
    string Name,
    string? Description,
    int DisplayOrder);
