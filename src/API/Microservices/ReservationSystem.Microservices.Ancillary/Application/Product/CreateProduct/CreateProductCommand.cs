namespace ReservationSystem.Microservices.Ancillary.Application.Product.CreateProduct;

public sealed record CreateProductCommand(
    Guid ProductGroupId,
    string Name,
    string Description,
    bool IsSegmentSpecific,
    string? SsrCode,
    string? ImageBase64,
    string AvailableChannels = """["WEB","APP","NDC","GDS","KIOSK","CC","AIRPORT"]""",
    string? AvailabilityRules = null);
