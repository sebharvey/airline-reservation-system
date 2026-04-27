using ReservationSystem.Orchestration.Retail.Application.NdcAirShopping;

namespace ReservationSystem.Orchestration.Retail.Application.NdcSeatAvailability;

public sealed record NdcSeatAvailabilityCommand(
    Guid OfferRefId,
    IReadOnlyList<NdcPassengerType>? Passengers);
