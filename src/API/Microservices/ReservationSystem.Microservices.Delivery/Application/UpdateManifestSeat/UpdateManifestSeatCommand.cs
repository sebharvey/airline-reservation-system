namespace ReservationSystem.Microservices.Delivery.Application.UpdateManifestSeat;

public sealed record UpdateManifestSeatCommand(string ETicketNumber, Guid InventoryId, string? NewSeatNumber);
