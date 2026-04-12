namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

public sealed class ProductGroupDto
{
    public Guid ProductGroupId { get; init; }
    public string Name { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class ProductGroupListDto
{
    public IReadOnlyList<ProductGroupDto> Groups { get; init; } = [];
}

public sealed class ProductPriceDto
{
    public Guid PriceId { get; init; }
    public Guid ProductId { get; init; }
    public Guid OfferId { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public decimal Tax { get; init; }
    public bool IsActive { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class ProductDto
{
    public Guid ProductId { get; init; }
    public Guid ProductGroupId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsSegmentSpecific { get; init; }
    public string? SsrCode { get; init; }
    public string? ImageBase64 { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<ProductPriceDto> Prices { get; init; } = [];
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

public sealed class ProductListDto
{
    public IReadOnlyList<ProductDto> Products { get; init; } = [];
}
