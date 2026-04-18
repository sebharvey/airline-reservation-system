namespace ReservationSystem.Microservices.Delivery.Domain.Entities;

/// <summary>
/// Maps one tax line on a ticket to the coupon(s) it applies to.
/// Part of the [delivery].[TicketTaxCoupon] normalised attribution table.
/// </summary>
public sealed class TicketTaxCoupon
{
    public Guid TicketTaxCouponId { get; private set; }
    public Guid TicketTaxId { get; private set; }

    /// <summary>1-based coupon number (1–4).</summary>
    public int CouponNumber { get; private set; }

    private TicketTaxCoupon() { }

    internal static TicketTaxCoupon Create(Guid ticketTaxId, int couponNumber) =>
        new()
        {
            TicketTaxCouponId = Guid.NewGuid(),
            TicketTaxId = ticketTaxId,
            CouponNumber = couponNumber
        };
}

/// <summary>
/// A single tax line on a ticket, with the coupon(s) it is attributed to.
/// Stored in [delivery].[TicketTax] + [delivery].[TicketTaxCoupon].
/// </summary>
public sealed class TicketTax
{
    private readonly List<TicketTaxCoupon> _appliedToCoupons = [];

    public Guid TicketTaxId { get; private set; }
    public Guid TicketId { get; private set; }

    /// <summary>IATA tax code, e.g. "GB", "YQ", "US".</summary>
    public string TaxCode { get; private set; } = string.Empty;

    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Which coupon numbers (1–4) this tax is attributed to.</summary>
    public IReadOnlyCollection<TicketTaxCoupon> AppliedToCoupons => _appliedToCoupons;

    private TicketTax() { }

    public static TicketTax Create(
        Guid ticketId,
        string taxCode,
        decimal amount,
        string currency,
        IEnumerable<int> couponNumbers)
    {
        var tax = new TicketTax
        {
            TicketTaxId = Guid.NewGuid(),
            TicketId = ticketId,
            TaxCode = taxCode,
            Amount = amount,
            Currency = currency
        };
        foreach (var n in couponNumbers)
            tax._appliedToCoupons.Add(TicketTaxCoupon.Create(tax.TicketTaxId, n));
        return tax;
    }
}
