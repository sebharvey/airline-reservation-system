namespace ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProduct;

public sealed record UpdateProductCommand(
    Guid ProductId,
    Guid ProductGroupId,
    string Name,
    string Description,
    bool IsSegmentSpecific,
    string? SsrCode,
    string? ImageBase64,
    string AvailableChannels,
    bool IsActive);
