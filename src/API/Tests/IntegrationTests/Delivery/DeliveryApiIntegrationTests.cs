using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bogus;
using FluentAssertions;
using Xunit;

namespace ReservationSystem.Tests.IntegrationTests.Delivery;

/// <summary>
/// Integration tests for the Delivery Microservice API.
/// Tests run sequentially against the live API, exercising the full ticket lifecycle:
/// issue, void, and reissue — covering both the happy path and validation errors.
/// </summary>
[TestCaseOrderer("ReservationSystem.Tests.IntegrationTests.Delivery.PriorityOrderer", "ReservationSystem.Tests")]
public class DeliveryApiIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DELIVERY_API_BASE_URL"))
            ? "https://reservation-system-db-microservice-delivery-cxhzaxfaagd4g9gy.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("DELIVERY_API_BASE_URL")!;

    private static readonly string? HostKey = Environment.GetEnvironmentVariable("DELIVERY_API_HOST_KEY");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _client;
    private readonly Faker _faker;

    // Shared state across ordered tests
    private static string? _bookingReference;
    private static string? _eTicketNumber;
    private static List<string>? _issuedETicketNumbers;

    public DeliveryApiIntegrationTests()
    {
        _client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _client.Timeout = TimeSpan.FromSeconds(30);

        if (!string.IsNullOrEmpty(HostKey))
            _client.DefaultRequestHeaders.Add("x-functions-key", HostKey);

        _faker = new Faker();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // ── Lifecycle tests ─────────────────────────────────────────────────────────

    [Fact, TestPriority(1)]
    public async Task T01_IssueTickets_ReturnsCreatedWithETicketNumbers()
    {
        // Arrange
        _bookingReference = _faker.Random.AlphaNumeric(6).ToUpper();
        var inventoryId = Guid.NewGuid();

        var request = new
        {
            bookingReference = _bookingReference,
            passengers = new[]
            {
                new
                {
                    passengerId = "PAX-1",
                    givenName = _faker.Name.FirstName(),
                    surname = _faker.Name.LastName()
                }
            },
            segments = new[]
            {
                new
                {
                    segmentId = "SEG-1",
                    inventoryId,
                    flightNumber = "AX001",
                    departureDate = DateTime.UtcNow.AddDays(30).ToString("yyyy-MM-dd"),
                    origin = "LHR",
                    destination = "JFK",
                    cabinCode = "Y",
                    fareBasisCode = "YFLEXGB"
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tickets", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IssueTicketsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Tickets.Should().HaveCount(1);
        body.Tickets[0].ETicketNumber.Should().StartWith("932-").And.HaveLength(14);
        body.Tickets[0].PassengerId.Should().Be("PAX-1");
        body.Tickets[0].SegmentId.Should().Be("SEG-1");
        body.Tickets[0].TicketId.Should().NotBeEmpty();

        _eTicketNumber = body.Tickets[0].ETicketNumber;
        _issuedETicketNumbers = body.Tickets.Select(t => t.ETicketNumber).ToList();
    }

    [SkippableFact, TestPriority(2)]
    public async Task T02_VoidTicket_ReturnsOkWithVoidedStatus()
    {
        SkipIfNoETicketNumber();

        // Arrange
        var request = new { reason = "Voluntary cancellation", actor = "RetailAPI", version = 1 };

        // Act
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tickets/{_eTicketNumber}/void")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        var response = await _client.SendAsync(patchRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<VoidTicketResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.ETicketNumber.Should().Be(_eTicketNumber);
        body.IsVoided.Should().BeTrue();
        body.VoidedAt.Should().NotBeNull();
    }

    [SkippableFact, TestPriority(3)]
    public async Task T03_VoidAlreadyVoidedTicket_ReturnsUnprocessableEntity()
    {
        SkipIfNoETicketNumber();

        // Arrange
        var request = new { reason = "Second void attempt", actor = "RetailAPI", version = 1 };

        // Act
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tickets/{_eTicketNumber}/void")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        var response = await _client.SendAsync(patchRequest);

        // Assert — voiding an already-voided ticket is a business rule violation
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [SkippableFact, TestPriority(4)]
    public async Task T04_ReissueTickets_ReturnsOkWithNewETicketNumbers()
    {
        Skip.If(_issuedETicketNumbers is null || _bookingReference is null,
            "T01 did not produce issued tickets");

        // Arrange — reissue onto a new flight segment
        var inventoryId = Guid.NewGuid();

        var request = new
        {
            bookingReference = _bookingReference,
            voidedETicketNumbers = _issuedETicketNumbers,
            passengers = new[]
            {
                new
                {
                    passengerId = "PAX-1",
                    givenName = _faker.Name.FirstName(),
                    surname = _faker.Name.LastName()
                }
            },
            segments = new[]
            {
                new
                {
                    segmentId = "SEG-1",
                    inventoryId,
                    flightNumber = "AX002",
                    departureDate = DateTime.UtcNow.AddDays(31).ToString("yyyy-MM-dd"),
                    origin = "LHR",
                    destination = "JFK",
                    cabinCode = "Y",
                    fareBasisCode = "YFLEXGB"
                }
            },
            reason = "Flight change",
            actor = "RetailAPI"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tickets/reissue", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ReissueTicketsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Tickets.Should().HaveCount(1);
        body.Tickets[0].ETicketNumber.Should().StartWith("932-").And.HaveLength(14);
        body.Tickets[0].ETicketNumber.Should().NotBeOneOf(_issuedETicketNumbers,
            "reissued ticket must have a new e-ticket number");
        body.VoidedETicketNumbers.Should().BeEquivalentTo(_issuedETicketNumbers);
    }

    // ── Standalone tests ────────────────────────────────────────────────────────

    [Fact, TestPriority(5)]
    public async Task T05_IssueTickets_MissingBookingReference_ReturnsBadRequest()
    {
        // Arrange — omit bookingReference
        var request = new
        {
            passengers = new[] { new { passengerId = "PAX-1", givenName = "John", surname = "Doe" } },
            segments = new[]
            {
                new
                {
                    segmentId = "SEG-1",
                    inventoryId = Guid.NewGuid(),
                    flightNumber = "AX001",
                    departureDate = "2026-12-01",
                    origin = "LHR",
                    destination = "JFK",
                    cabinCode = "Y",
                    fareBasisCode = "YFLEXGB"
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tickets", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact, TestPriority(6)]
    public async Task T06_IssueTickets_NoPassengers_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            bookingReference = _faker.Random.AlphaNumeric(6).ToUpper(),
            passengers = Array.Empty<object>(),
            segments = new[]
            {
                new
                {
                    segmentId = "SEG-1",
                    inventoryId = Guid.NewGuid(),
                    flightNumber = "AX001",
                    departureDate = "2026-12-01",
                    origin = "LHR",
                    destination = "JFK",
                    cabinCode = "Y",
                    fareBasisCode = "YFLEXGB"
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tickets", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact, TestPriority(7)]
    public async Task T07_IssueTickets_NoSegments_ReturnsBadRequest()
    {
        // Arrange
        var request = new
        {
            bookingReference = _faker.Random.AlphaNumeric(6).ToUpper(),
            passengers = new[] { new { passengerId = "PAX-1", givenName = "John", surname = "Doe" } },
            segments = Array.Empty<object>()
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tickets", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact, TestPriority(8)]
    public async Task T08_VoidTicket_NonExistentETicketNumber_ReturnsNotFound()
    {
        // Arrange
        var request = new { reason = "Test", actor = "Test", version = 1 };

        // Act
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, "/api/v1/tickets/932-0000000000/void")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        var response = await _client.SendAsync(patchRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, TestPriority(9)]
    public async Task T09_IssueTickets_MultiplePassengersAndSegments_ReturnsOneTicketPerPaxPerSegment()
    {
        // Arrange — 2 passengers × 2 segments = 4 tickets
        var bookingRef = _faker.Random.AlphaNumeric(6).ToUpper();
        var givenName1 = _faker.Name.FirstName();
        var surname1 = _faker.Name.LastName();
        var givenName2 = _faker.Name.FirstName();
        var surname2 = _faker.Name.LastName();

        var request = new
        {
            bookingReference = bookingRef,
            passengers = new[]
            {
                new { passengerId = "PAX-1", givenName = givenName1, surname = surname1 },
                new { passengerId = "PAX-2", givenName = givenName2, surname = surname2 }
            },
            segments = new[]
            {
                new
                {
                    segmentId = "SEG-1",
                    inventoryId = Guid.NewGuid(),
                    flightNumber = "AX003",
                    departureDate = DateTime.UtcNow.AddDays(60).ToString("yyyy-MM-dd"),
                    origin = "LHR",
                    destination = "JFK",
                    cabinCode = "J",
                    fareBasisCode = "JFLEXGB"
                },
                new
                {
                    segmentId = "SEG-2",
                    inventoryId = Guid.NewGuid(),
                    flightNumber = "AX004",
                    departureDate = DateTime.UtcNow.AddDays(67).ToString("yyyy-MM-dd"),
                    origin = "JFK",
                    destination = "LHR",
                    cabinCode = "J",
                    fareBasisCode = "JFLEXGB"
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/tickets", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<IssueTicketsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Tickets.Should().HaveCount(4, "one ticket per passenger per segment");
        body.Tickets.Select(t => t.ETicketNumber).Distinct().Should().HaveCount(4,
            "each issued ticket must have a unique e-ticket number");
        body.Tickets.All(t => t.ETicketNumber.StartsWith("932-")).Should().BeTrue();

        // Cleanup — void all issued tickets so test data does not accumulate as active records
        foreach (var ticket in body.Tickets)
        {
            var voidReq = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/tickets/{ticket.ETicketNumber}/void")
            {
                Content = JsonContent.Create(new { reason = "Test cleanup", actor = "Test", version = 1 }, options: JsonOptions)
            };
            await _client.SendAsync(voidReq);
        }
    }

    private static void SkipIfNoETicketNumber()
    {
        Skip.If(string.IsNullOrEmpty(_eTicketNumber), "T01 did not produce an e-ticket number");
    }
}

#region Response DTOs

public sealed class IssueTicketsResponse
{
    public IReadOnlyList<TicketSummaryDto> Tickets { get; init; } = [];
}

public sealed class TicketSummaryDto
{
    public Guid TicketId { get; init; }
    public string ETicketNumber { get; init; } = string.Empty;
    public string PassengerId { get; init; } = string.Empty;
    public string SegmentId { get; init; } = string.Empty;
    public string FlightNumber { get; init; } = string.Empty;
    public string DepartureDate { get; init; } = string.Empty;
}

public sealed class VoidTicketResponse
{
    public string ETicketNumber { get; init; } = string.Empty;
    public bool IsVoided { get; init; }
    public DateTimeOffset? VoidedAt { get; init; }
}

public sealed class ReissueTicketsResponse
{
    public IReadOnlyList<string> VoidedETicketNumbers { get; init; } = [];
    public IReadOnlyList<TicketSummaryDto> Tickets { get; init; } = [];
}

#endregion

#region Test Ordering Infrastructure

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestPriorityAttribute : Attribute
{
    public int Priority { get; }
    public TestPriorityAttribute(int priority) => Priority = priority;
}

public sealed class PriorityOrderer : Xunit.Abstractions.ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : Xunit.Abstractions.ITestCase
    {
        var sortedCases = new SortedDictionary<int, List<TTestCase>>();

        foreach (var testCase in testCases)
        {
            var priority = testCase.TestMethod.Method
                .GetCustomAttributes(typeof(TestPriorityAttribute).AssemblyQualifiedName)
                .FirstOrDefault()
                ?.GetNamedArgument<int>(nameof(TestPriorityAttribute.Priority)) ?? 0;

            if (!sortedCases.TryGetValue(priority, out var list))
            {
                list = new List<TTestCase>();
                sortedCases[priority] = list;
            }

            list.Add(testCase);
        }

        foreach (var list in sortedCases.Values)
            foreach (var testCase in list)
                yield return testCase;
    }
}

#endregion
