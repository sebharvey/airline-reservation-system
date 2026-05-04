using System.Text.Json;
using System.Text.Json.Serialization;

namespace ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices.Dto;

/// <summary>
/// Deserialises passengerId from the Delivery service response, which now returns an int (e.g. 1),
/// back into the PAX-N string form ("PAX-1") expected by orchestration code.
/// Also handles legacy responses that already return the string form.
/// </summary>
public sealed class PassengerIdAsStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
            return reader.GetString() ?? string.Empty;

        if (reader.TokenType == JsonTokenType.Number)
            return $"PAX-{reader.GetInt32()}";

        throw new JsonException($"Cannot convert token type '{reader.TokenType}' to string for passengerId.");
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
