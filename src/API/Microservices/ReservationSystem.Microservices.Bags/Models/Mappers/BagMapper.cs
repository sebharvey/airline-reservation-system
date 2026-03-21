using ReservationSystem.Microservices.Bags.Application.CreateBagPolicy;
using ReservationSystem.Microservices.Bags.Application.CreateBagPricing;
using ReservationSystem.Microservices.Bags.Application.UpdateBagPolicy;
using ReservationSystem.Microservices.Bags.Application.UpdateBagPricing;
using ReservationSystem.Microservices.Bags.Models.Requests;
using ReservationSystem.Microservices.Bags.Models.Responses;

namespace ReservationSystem.Microservices.Bags.Models.Mappers;

/// <summary>
/// Static mapping methods between all model representations for Bags.
///
/// Mapping directions:
///   HTTP request  →  Application command
///   Domain entity →  HTTP response
///
/// Static methods are used deliberately — no state, no DI overhead, trivially testable.
/// </summary>
public static class BagMapper
{
    // -------------------------------------------------------------------------
    // BagPolicy: HTTP request → Application command
    // -------------------------------------------------------------------------

    public static CreateBagPolicyCommand ToCommand(CreateBagPolicyRequest request) =>
        new(
            CabinCode: request.CabinCode,
            FreeBagsIncluded: request.FreeBagsIncluded,
            MaxWeightKgPerBag: request.MaxWeightKgPerBag);

    public static UpdateBagPolicyCommand ToCommand(Guid policyId, UpdateBagPolicyRequest request) =>
        new(
            PolicyId: policyId,
            FreeBagsIncluded: request.FreeBagsIncluded,
            MaxWeightKgPerBag: request.MaxWeightKgPerBag,
            IsActive: request.IsActive);

    // -------------------------------------------------------------------------
    // BagPolicy: Domain entity → HTTP response
    // -------------------------------------------------------------------------

    public static BagPolicyResponse ToResponse(Domain.Entities.BagPolicy policy) =>
        new()
        {
            PolicyId = policy.PolicyId,
            CabinCode = policy.CabinCode,
            FreeBagsIncluded = policy.FreeBagsIncluded,
            MaxWeightKgPerBag = policy.MaxWeightKgPerBag,
            IsActive = policy.IsActive,
            CreatedAt = policy.CreatedAt,
            UpdatedAt = policy.UpdatedAt
        };

    public static IReadOnlyList<BagPolicyResponse> ToResponse(IEnumerable<Domain.Entities.BagPolicy> policies) =>
        policies.Select(ToResponse).ToList().AsReadOnly();

    // -------------------------------------------------------------------------
    // BagPricing: HTTP request → Application command
    // -------------------------------------------------------------------------

    public static CreateBagPricingCommand ToCommand(CreateBagPricingRequest request) =>
        new(
            BagSequence: request.BagSequence,
            Price: request.Price,
            CurrencyCode: request.CurrencyCode,
            ValidFrom: request.ValidFrom,
            ValidTo: request.ValidTo);

    public static UpdateBagPricingCommand ToCommand(Guid pricingId, UpdateBagPricingRequest request) =>
        new(
            PricingId: pricingId,
            Price: request.Price,
            IsActive: request.IsActive,
            ValidFrom: request.ValidFrom,
            ValidTo: request.ValidTo);

    // -------------------------------------------------------------------------
    // BagPricing: Domain entity → HTTP response
    // -------------------------------------------------------------------------

    public static BagPricingResponse ToResponse(Domain.Entities.BagPricing pricing) =>
        new()
        {
            PricingId = pricing.PricingId,
            BagSequence = pricing.BagSequence,
            CurrencyCode = pricing.CurrencyCode,
            Price = pricing.Price,
            IsActive = pricing.IsActive,
            ValidFrom = pricing.ValidFrom,
            ValidTo = pricing.ValidTo,
            CreatedAt = pricing.CreatedAt,
            UpdatedAt = pricing.UpdatedAt
        };

    public static IReadOnlyList<BagPricingResponse> ToResponse(IEnumerable<Domain.Entities.BagPricing> pricings) =>
        pricings.Select(ToResponse).ToList().AsReadOnly();
}
