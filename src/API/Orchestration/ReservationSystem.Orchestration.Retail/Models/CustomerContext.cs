namespace ReservationSystem.Orchestration.Retail.Models;

/// <summary>
/// Customer attributes resolved from the loyalty profile at authentication time.
/// Passed through the search pipeline to downstream microservices so they can
/// apply customer-specific logic (e.g. tier-based pricing).
/// Add new properties here as additional customer attributes are required.
/// </summary>
public sealed record CustomerContext(string LoyaltyNumber, string TierCode);
