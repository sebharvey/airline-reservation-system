using ReservationSystem.Microservices.Ancillary.Application.Product.CreateProduct;
using ReservationSystem.Microservices.Ancillary.Application.Product.CreateProductGroup;
using ReservationSystem.Microservices.Ancillary.Application.Product.CreateProductPrice;
using ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProduct;
using ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProductGroup;
using ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProductPrice;
using ReservationSystem.Microservices.Ancillary.Domain.Entities.Product;
using ReservationSystem.Microservices.Ancillary.Models.Product.Requests;
using ProductEntity = ReservationSystem.Microservices.Ancillary.Domain.Entities.Product.Product;
using ReservationSystem.Microservices.Ancillary.Models.Product.Responses;

namespace ReservationSystem.Microservices.Ancillary.Models.Product.Mappers;

public static class ProductMapper
{
    // ── ProductGroup: Request → Command ─────────────────────────────────────

    public static CreateProductGroupCommand ToCommand(CreateProductGroupRequest request) =>
        new(Name: request.Name, SortOrder: request.SortOrder);

    public static UpdateProductGroupCommand ToCommand(Guid groupId, UpdateProductGroupRequest request) =>
        new(ProductGroupId: groupId, Name: request.Name, SortOrder: request.SortOrder, IsActive: request.IsActive);

    // ── ProductGroup: Domain → Response ─────────────────────────────────────

    public static ProductGroupResponse ToResponse(ProductGroup group) =>
        new()
        {
            ProductGroupId = group.ProductGroupId,
            Name = group.Name,
            SortOrder = group.SortOrder,
            IsActive = group.IsActive,
            CreatedAt = group.CreatedAt,
            UpdatedAt = group.UpdatedAt
        };

    public static IReadOnlyList<ProductGroupResponse> ToResponse(IEnumerable<ProductGroup> groups) =>
        groups.Select(ToResponse).ToList().AsReadOnly();

    // ── Product: Request → Command ───────────────────────────────────────────

    public static CreateProductCommand ToCommand(CreateProductRequest request) =>
        new(
            ProductGroupId: request.ProductGroupId,
            Name: request.Name,
            Description: request.Description,
            IsSegmentSpecific: request.IsSegmentSpecific,
            SsrCode: request.SsrCode,
            ImageBase64: request.ImageBase64);

    public static UpdateProductCommand ToCommand(Guid productId, UpdateProductRequest request) =>
        new(
            ProductId: productId,
            ProductGroupId: request.ProductGroupId,
            Name: request.Name,
            Description: request.Description,
            IsSegmentSpecific: request.IsSegmentSpecific,
            SsrCode: request.SsrCode,
            ImageBase64: request.ImageBase64,
            IsActive: request.IsActive);

    // ── Product: Domain → Response ───────────────────────────────────────────

    public static ProductResponse ToResponse(ProductEntity product) =>
        new()
        {
            ProductId = product.ProductId,
            ProductGroupId = product.ProductGroupId,
            Name = product.Name,
            Description = product.Description,
            IsSegmentSpecific = product.IsSegmentSpecific,
            SsrCode = product.SsrCode,
            ImageBase64 = product.ImageBase64,
            IsActive = product.IsActive,
            Prices = product.Prices.Select(ToResponse).ToList().AsReadOnly(),
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };

    public static IReadOnlyList<ProductResponse> ToResponse(IEnumerable<ProductEntity> products) =>
        products.Select(ToResponse).ToList().AsReadOnly();

    // ── ProductPrice: Request → Command ─────────────────────────────────────

    public static CreateProductPriceCommand ToCommand(Guid productId, CreateProductPriceRequest request) =>
        new(ProductId: productId, CurrencyCode: request.CurrencyCode, Price: request.Price, Tax: request.Tax);

    public static UpdateProductPriceCommand ToCommand(Guid priceId, UpdateProductPriceRequest request) =>
        new(PriceId: priceId, Price: request.Price, Tax: request.Tax, IsActive: request.IsActive);

    // ── ProductPrice: Domain → Response ─────────────────────────────────────

    public static ProductPriceResponse ToResponse(ProductPrice price) =>
        new()
        {
            PriceId = price.PriceId,
            ProductId = price.ProductId,
            OfferId = price.OfferId,
            CurrencyCode = price.CurrencyCode,
            Price = price.Price,
            Tax = price.Tax,
            IsActive = price.IsActive,
            CreatedAt = price.CreatedAt,
            UpdatedAt = price.UpdatedAt
        };
}
