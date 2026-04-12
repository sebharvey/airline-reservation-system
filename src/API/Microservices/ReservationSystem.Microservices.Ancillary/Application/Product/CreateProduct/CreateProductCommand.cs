namespace ReservationSystem.Microservices.Ancillary.Application.Product.CreateProduct;

public sealed record CreateProductCommand(
    Guid ProductGroupId,
    string Name,
    string Description,
    bool IsSegmentSpecific,
    string? SsrCode,
    string? ImageBase64);
