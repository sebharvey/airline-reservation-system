using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Microservices.Delivery.Models.Converters;

/// <summary>
/// Accepts passengerId as either a plain integer (1) or a PAX-N string ("PAX-1")
/// from callers that have not yet migrated to the numeric form. Always serialises as int.
/// </summary>
public sealed class PassengerIdJsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
            return reader.GetInt32();

        if (reader.TokenType == JsonTokenType.String)
        {
            var s = reader.GetString() ?? string.Empty;
            var raw = s.StartsWith("PAX-", StringComparison.OrdinalIgnoreCase) ? s[4..] : s;
            return int.TryParse(raw, out var n) ? n : 0;
        }

        throw new JsonException($"Cannot convert token type '{reader.TokenType}' to int for passengerId.");
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value);
}
