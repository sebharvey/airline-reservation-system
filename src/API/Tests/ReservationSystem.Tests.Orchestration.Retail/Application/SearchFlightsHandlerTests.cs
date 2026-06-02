using Microsoft.Extensions.Logging.Abstractions;
using RichardSzalay.MockHttp;
using Xunit;
using ReservationSystem.Orchestration.Retail.Application.SearchFlights;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices.Dto;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace ReservationSystem.Tests.Orchestration.Retail.Application;

public sealed class SearchFlightsHandlerTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private static OfferServiceClient BuildClient(MockHttpMessageHandler mockHttp)
    {
        var httpClient = mockHttp.ToHttpClient();
        httpClient.BaseAddress = new Uri("https://offer-ms.example.com");
        var factory = new StaticHttpClientFactory(httpClient);
        return new OfferServiceClient(factory, NullLogger<OfferServiceClient>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenDirectFlightsFound_ReturnsDirectItineraries()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var searchResult = new OfferSearchResultDto
        {
            SessionId = Guid.NewGuid(),
            Origin = "LHR",
            Destination = "JFK",
            DepartureDate = "2099-12-25",
            Segments =
            [
                new SegmentItemDto
                {
                    Origin = "LHR",
                    Destination = "JFK",
                    Flights =
                    [
                        new FlightItemDto
                        {
                            InventoryId = Guid.NewGuid(),
                            FlightNumber = "AX001",
                            Origin = "LHR",
                            Destination = "JFK",
                            DepartureDate = "2099-12-25",
                            DepartureTime = "10:00",
                            ArrivalTime = "13:00",
                            ArrivalDayOffset = 0,
                            DurationMinutes = 420,
                            AircraftType = "A351",
                            ExpiresAt = "2099-12-24T23:00:00Z",
                            Offers =
                            [
                                new OfferItemDto
                                {
                                    OfferId = Guid.NewGuid(),
                                    CabinCode = "Y",
                                    FareBasisCode = "YECO",
                                    CurrencyCode = "GBP",
                                    BaseFareAmount = 450m,
                                    TaxAmount = 80m,
                                    TotalAmount = 530m,
                                    BookingType = "Revenue",
                                    SeatsAvailable = 50
                                }
                            ]
                        }
                    ]
                }
            ]
        };

        mockHttp
            .When(HttpMethod.Post, "*/api/v1/search")
            .Respond(HttpStatusCode.OK, JsonContent.Create(searchResult, options: JsonOptions));

        var client = BuildClient(mockHttp);
        var handler = new SearchFlightsHandler(client);
        var command = new SearchFlightsCommand("LHR", "JFK", "2099-12-25", 2, "Revenue");

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result.Itineraries);
        Assert.Single(result.Itineraries);
        Assert.Single(result.Itineraries[0].Segments);
    }

    [Fact]
    public async Task HandleAsync_WhenNoDirectFlightsAndOriginIsHub_ReturnsEmptyItineraries()
    {
        // Arrange
        var mockHttp = new MockHttpMessageHandler();
        var emptyResult = new OfferSearchResultDto
        {
            SessionId = Guid.NewGuid(),
            Origin = "LHR",
            Destination = "CDG",
            DepartureDate = "2099-12-25",
            Segments = [new SegmentItemDto { Origin = "LHR", Destination = "CDG", Flights = [] }]
        };

        var searchMock = mockHttp
            .When(HttpMethod.Post, "*/api/v1/search")
            .Respond(HttpStatusCode.OK, JsonContent.Create(emptyResult, options: JsonOptions));

        var client = BuildClient(mockHttp);
        var handler = new SearchFlightsHandler(client);
        // LHR is the hub — no connecting fallback should be attempted
        var command = new SearchFlightsCommand("LHR", "CDG", "2099-12-25", 1, "Revenue");

        // Act
        var result = await handler.HandleAsync(command, CancellationToken.None);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Itineraries);
        // Only one search call should have been made (no connecting legs attempted)
        Assert.Equal(1, mockHttp.GetMatchCount(searchMock));
    }

    /// <summary>
    /// Minimal IHttpClientFactory that always returns the same pre-configured HttpClient.
    /// </summary>
    private sealed class StaticHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }
}
