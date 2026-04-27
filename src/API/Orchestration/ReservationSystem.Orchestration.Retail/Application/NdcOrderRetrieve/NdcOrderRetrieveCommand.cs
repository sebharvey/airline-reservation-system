namespace ReservationSystem.Orchestration.Retail.Application.NdcOrderRetrieve;

public sealed record NdcOrderRetrieveCommand(
    string BookingReference,
    string Surname);
