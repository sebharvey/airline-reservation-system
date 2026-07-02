using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using RichardSzalay.MockHttp;
using Xunit;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;

namespace ReservationSystem.Tests.Orchestration.Retail.Infrastructure;

public sealed class DeliveryServiceClientManifestTests
{
    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private static DeliveryServiceClient BuildClient(MockHttpMessageHandler mockHttp)
    {
        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://delivery-ms.example.com");
        var factory = new StaticHttpClientFactory(httpClient);
        return new DeliveryServiceClient(factory, NullLogger<DeliveryServiceClient>.Instance);
    }

    // The Delivery MS matches manifest entries to issued tickets by the integer paxId.
    // The orchestration must therefore serialise a paxId on every entry — omitting it
    // deserialises to 0 on the Delivery side, matches no ticket, and silently skips the
    // passenger, leaving the flight manifest empty.
    [Fact]
    public async Task WriteManifestAsync_SerialisesPaxIdOnEachEntry()
    {
        var mockHttp = new MockHttpMessageHandler();
        string? capturedBody = null;
        mockHttp
            .When(HttpMethod.Post, "https://delivery-ms.example.com/api/v1/manifest")
            .Respond(async req =>
            {
                capturedBody = await req.Content!.ReadAsStringAsync();
                return new HttpResponseMessage(HttpStatusCode.Created);
            });

        var client = BuildClient(mockHttp);

        var entries = new List<ManifestPassengerEntry>
        {
            new() { PaxId = 1, PassengerId = "PAX-1", GivenName = "Anjali", Surname = "Smith", ETicketNumber = "932-1000020823", CabinCode = "Y" },
            new() { PaxId = 2, PassengerId = "PAX-2", GivenName = "Ravi",   Surname = "Smith", ETicketNumber = "932-1000020824", CabinCode = "Y" },
        };

        await client.WriteManifestAsync(
            bookingReference: "NX7CJR",
            orderId: Guid.NewGuid(),
            inventoryId: Guid.NewGuid(),
            segmentId: 1,
            flightNumber: "AX004",
            origin: "JFK",
            destination: "LHR",
            departureDate: "2026-07-03",
            aircraftType: "A351",
            departureTime: "17:00",
            arrivalTime: "05:00",
            bookingType: "Confirmed",
            entries: entries,
            ct: CancellationToken.None);

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody!);
        var entriesEl = doc.RootElement.GetProperty("entries");
        Assert.Equal(2, entriesEl.GetArrayLength());

        var paxIds = entriesEl.EnumerateArray()
            .Select(e => e.GetProperty("paxId").GetInt32())
            .ToList();
        Assert.Equal(new[] { 1, 2 }, paxIds);
    }
}
