namespace ReservationSystem.Microservices.OfferApi.Models.Database;

/// <summary>
/// Dapper row model for the <c>[offer].[Offers]</c> table.
///
/// Column names match the SQL schema exactly so Dapper can bind them without
/// custom type handlers. The <see cref="Attributes"/> column is a raw JSON
/// string; deserialisation into a typed object is handled by the repository
/// after Dapper returns the record (see <see cref="JsonFields.OfferAttributes"/>).
/// </summary>
public sealed class OfferRecord
{
    public Guid Id { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public DateTimeOffset DepartureAt { get; init; }
    public string FareClass { get; init; } = string.Empty;
    public decimal TotalPrice { get; init; }
    public string Currency { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    /// <summary>
    /// Raw JSON from the <c>Attributes</c> column.
    /// Deserialise via <see cref="JsonFields.OfferAttributes"/> before use.
    /// </summary>
    public string? Attributes { get; init; }

    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
