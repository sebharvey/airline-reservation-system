using System.Text.Json;
using ReservationSystem.Orchestration.Retail.Application.AdminManifest;
using ReservationSystem.Orchestration.Retail.Infrastructure.ExternalServices;
using Xunit;

namespace ReservationSystem.Tests.Orchestration.Retail.Application;

public sealed class ManifestReconstructionTests
{
    private const string InventoryId = "324dcb40-4bbc-4d82-ab06-2dd1d414823a";

    private static OrderMsOrderResult Order(Guid orderId, string bookingReference, string json) =>
        new()
        {
            OrderId = orderId,
            BookingReference = bookingReference,
            OrderStatus = "Confirmed",
            OrderData = JsonDocument.Parse(json).RootElement.Clone()
        };

    private static string OrderJson(string flightNumber = "AX006", string departureDate = "2026-07-02") => $$"""
    {
      "currency": "GBP",
      "bookingType": "Confirmed",
      "dataLists": {
        "passengers": [
          { "passengerId": "PAX-1", "givenName": "Anjali", "surname": "Smith", "type": "ADT", "gender": "F", "dob": "1990-05-01" },
          { "passengerId": "PAX-2", "givenName": "Ravi", "surname": "Smith", "type": "CHD" }
        ]
      },
      "orderItems": [
        { "productType": "FLIGHT", "flightNumber": "{{flightNumber}}", "departureDate": "{{departureDate}}", "cabinCode": "W", "inventoryId": "{{InventoryId}}", "origin": "JFK", "destination": "LHR" },
        { "productType": "SEAT", "passengerId": "PAX-1", "seatNumber": "12H", "inventoryId": "{{InventoryId}}" }
      ],
      "eTickets": [
        { "passengerId": "PAX-1", "paxId": 1, "eTicketNumber": "932-1000020823", "segmentIds": ["s1"] },
        { "passengerId": "PAX-2", "paxId": 2, "eTicketNumber": "932-1000020824", "segmentIds": ["s1"] }
      ]
    }
    """;

    [Fact]
    public void FromOrders_ProjectsPassengersWithSeatsAndETickets()
    {
        var orderId = Guid.NewGuid();
        var orders = new[] { Order(orderId, "Y7WWF0", OrderJson()) };

        var entries = ManifestReconstruction.FromOrders(orders, "AX006", "2026-07-02");

        Assert.Equal(2, entries.Count);

        var pax1 = entries.Single(e => e.PassengerId == 1);
        Assert.Equal(orderId, pax1.OrderId);
        Assert.Equal("Y7WWF0", pax1.BookingReference);
        Assert.Equal("Anjali", pax1.GivenName);
        Assert.Equal("932-1000020823", pax1.ETicketNumber);
        Assert.Equal("12H", pax1.SeatNumber);
        Assert.Equal("W", pax1.CabinCode);
        Assert.Equal("ADT", pax1.PtcCode);
        Assert.Equal("F", pax1.Gender);
        Assert.Equal(new DateOnly(1990, 5, 1), pax1.DateOfBirth);
        Assert.Equal("Confirmed", pax1.BookingType);

        var pax2 = entries.Single(e => e.PassengerId == 2);
        Assert.Equal("932-1000020824", pax2.ETicketNumber);
        Assert.Null(pax2.SeatNumber);
        Assert.Equal("CHD", pax2.PtcCode);
    }

    [Fact]
    public void FromOrders_SkipsOrdersNotOnRequestedFlight()
    {
        var orders = new[] { Order(Guid.NewGuid(), "AAA111", OrderJson(flightNumber: "AX999")) };

        var entries = ManifestReconstruction.FromOrders(orders, "AX006", "2026-07-02");

        Assert.Empty(entries);
    }

    [Fact]
    public void FromOrders_SkipsOrdersOnADifferentDate()
    {
        var orders = new[] { Order(Guid.NewGuid(), "AAA111", OrderJson(departureDate: "2026-07-03")) };

        var entries = ManifestReconstruction.FromOrders(orders, "AX006", "2026-07-02");

        Assert.Empty(entries);
    }

    [Fact]
    public void FromOrders_ToleratesMissingOrderDataAndBadOrders()
    {
        var good = Order(Guid.NewGuid(), "Y7WWF0", OrderJson());
        var noData = new OrderMsOrderResult { OrderId = Guid.NewGuid(), BookingReference = "NODATA", OrderStatus = "Confirmed" };

        var entries = ManifestReconstruction.FromOrders(new[] { noData, good }, "AX006", "2026-07-02");

        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal("Y7WWF0", e.BookingReference));
    }
}
