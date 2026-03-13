using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Offer.Models.Database.JsonFields;

/// <summary>
/// Deserialisation target for the <c>Attributes</c> JSON column in
/// <c>[offer].[Offers]</c>.
///
/// This class exists purely to model the JSON stored in the database.
/// It carries <see cref="JsonPropertyNameAttribute"/> annotations to control
/// the on-disk JSON shape independently of C# naming conventions.
///
/// Example JSON:
/// <code>
/// {
///   "baggageAllowance": "23kg",
///   "isRefundable": true,
///   "isChangeable": false,
///   "seatsRemaining": 4
/// }
/// </code>
///
/// Mapping chain:
///   SQL JSON string
///   → <see cref="OfferAttributes"/>                                   (Infrastructure — deserialise)
///   → <see cref="Domain.ValueObjects.OfferMetadata"/>                 (Domain — business logic)
///   → <see cref="OfferAttributes"/>                                   (Infrastructure — serialise, on write)
/// </summary>
public sealed class OfferAttributes
{
    [JsonPropertyName("baggageAllowance")]
    public string BaggageAllowance { get; set; } = string.Empty;

    [JsonPropertyName("isRefundable")]
    public bool IsRefundable { get; set; }

    [JsonPropertyName("isChangeable")]
    public bool IsChangeable { get; set; }

    /// <summary>
    /// Number of seats still available at this fare. Updated by inventory systems.
    /// </summary>
    [JsonPropertyName("seatsRemaining")]
    public int SeatsRemaining { get; set; }
}
