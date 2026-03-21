namespace ReservationSystem.Microservices.Delivery.Application.VoidDocument;

public sealed class VoidDocumentCommand
{
    public string DocumentNumber { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
    public string Actor { get; init; } = string.Empty;
}
