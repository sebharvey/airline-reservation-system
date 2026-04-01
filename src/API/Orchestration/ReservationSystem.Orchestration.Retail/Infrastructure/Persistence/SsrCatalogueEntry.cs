namespace ReservationSystem.Orchestration.Retail.Infrastructure.Persistence;

public sealed class SsrCatalogueEntry
{
    public Guid SsrCatalogueId { get; set; }
    public string SsrCode { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
