using System.Text.Json;
using Xunit;
using ReservationSystem.Orchestration.Operations.Application.CheckIn;
using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;

namespace ReservationSystem.Tests.Orchestration.Operations.Application.CheckIn;

public sealed class CheckInHelperTests
{
    private static JsonElement ParseOrderData(string json) =>
        JsonDocument.Parse(json).RootElement;

    [Fact]
    public void ParseOrderLookups_ReadsPaxIdIntegerAsKey()
    {
        var orderData = ParseOrderData("""
            {
              "dataLists": {
                "passengers": [
                  { "paxId": 2, "passengerId": "PAX-2", "givenName": "Jane", "surname": "Doe" }
                ]
              },
              "eTickets": [
                { "eTicketNumber": "932-0000000001", "passengerId": "PAX-2" }
              ]
            }
            """);

        var (ticketToPaxId, paxIdToInfo) = CheckInHelper.ParseOrderLookups(orderData);

        Assert.Equal(2, ticketToPaxId["932-0000000001"]);
        Assert.True(paxIdToInfo.ContainsKey(2));
        Assert.Equal("Jane", paxIdToInfo[2].GivenName);
        Assert.Equal("Doe", paxIdToInfo[2].Surname);
    }

    [Fact]
    public void ParseOrderLookups_PassengerWithoutPaxId_IsSkipped()
    {
        var orderData = ParseOrderData("""
            {
              "dataLists": {
                "passengers": [
                  { "passengerId": "PAX-1", "givenName": "John", "surname": "Smith" }
                ]
              },
              "eTickets": [
                { "eTicketNumber": "932-0000000002", "passengerId": "PAX-1" }
              ]
            }
            """);

        var (ticketToPaxId, paxIdToInfo) = CheckInHelper.ParseOrderLookups(orderData);

        Assert.Empty(paxIdToInfo);
        Assert.Empty(ticketToPaxId);
    }

    [Fact]
    public void ParseOrderLookups_MultiplePassengers_BothMapped()
    {
        var orderData = ParseOrderData("""
            {
              "dataLists": {
                "passengers": [
                  { "paxId": 1, "passengerId": "PAX-1", "givenName": "Alice", "surname": "A" },
                  { "paxId": 2, "passengerId": "PAX-2", "givenName": "Bob",   "surname": "B" }
                ]
              },
              "eTickets": [
                { "eTicketNumber": "932-0000000010", "passengerId": "PAX-1" },
                { "eTicketNumber": "932-0000000011", "passengerId": "PAX-2" }
              ]
            }
            """);

        var (ticketToPaxId, paxIdToInfo) = CheckInHelper.ParseOrderLookups(orderData);

        Assert.Equal(1, ticketToPaxId["932-0000000010"]);
        Assert.Equal(2, ticketToPaxId["932-0000000011"]);
        Assert.Equal("Alice", paxIdToInfo[1].GivenName);
        Assert.Equal("Bob",   paxIdToInfo[2].GivenName);
    }

    [Fact]
    public void ParsePaxToTicketMap_PaxIdInteger_DerivesPaxNKey()
    {
        var orderData = ParseOrderData("""
            {
              "eTickets": [
                { "paxId": 1, "eTicketNumber": "932-0000000001" },
                { "paxId": 2, "eTicketNumber": "932-0000000002" }
              ]
            }
            """);

        var result = CheckInHelper.ParsePaxToTicketMap(orderData);

        Assert.Equal("932-0000000001", result["PAX-1"]);
        Assert.Equal("932-0000000002", result["PAX-2"]);
    }

    [Fact]
    public void ParsePaxToTicketMap_LegacyPassengerIdString_StillMapped()
    {
        var orderData = ParseOrderData("""
            {
              "eTickets": [
                { "passengerId": "PAX-3", "eTicketNumber": "932-0000000003" }
              ]
            }
            """);

        var result = CheckInHelper.ParsePaxToTicketMap(orderData);

        Assert.Equal("932-0000000003", result["PAX-3"]);
    }

    [Fact]
    public void BuildTimaticNotes_UsesPaxIdIntegerDirectly()
    {
        var ticketToPaxId = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["932-0000000020"] = 3
        };
        var notes = new[]
        {
            new OciTimaticNote
            {
                TicketNumber = "932-0000000020",
                CheckType    = "DOC",
                Status       = "PASS",
                Timestamp    = "2026-06-10T10:00:00Z"
            }
        };

        var result = CheckInHelper.BuildTimaticNotes(notes, ticketToPaxId: ticketToPaxId);

        Assert.Single(result);
        Assert.Equal(3, result[0].PaxId);
    }
}
