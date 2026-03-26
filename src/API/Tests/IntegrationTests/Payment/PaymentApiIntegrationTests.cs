using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ReservationSystem.Tests.IntegrationTests.Payment;

/// <summary>
/// Integration tests for the Payment Microservice API.
///
/// Tests run sequentially against the live API, exercising the full payment lifecycle
/// for two independent payment transactions against a single booking:
///
///   Payment 1 — Fare:         initialise → authorise → settle → GET payment → GET events
///   Payment 2 — SeatAncillary: initialise → authorise → settle → GET payment → GET events
///
/// Each payment produces two PaymentEvent rows (Authorised + Settled), reflecting how
/// a real booking generates multiple independent payment transactions, each with their
/// own event history.
/// </summary>
[TestCaseOrderer("ReservationSystem.Tests.IntegrationTests.Payment.PaymentPriorityOrderer", "ReservationSystem.Tests")]
public class PaymentApiIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PAYMENT_API_BASE_URL"))
            ? "https://reservation-system-db-microservice-payment.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("PAYMENT_API_BASE_URL")!;

    private static readonly string? HostKey = Environment.GetEnvironmentVariable("PAYMENT_API_HOST_KEY");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _client;

    // Shared booking reference used across both payments
    private static readonly string BookingReference = "AB1234";

    // Payment 1 — Fare
    private static Guid? _farePaymentId;
    private static decimal _fareAmount = 250.00m;

    // Payment 2 — SeatAncillary
    private static Guid? _seatPaymentId;
    private static decimal _seatAmount = 30.00m;

    public PaymentApiIntegrationTests()
    {
        _client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _client.Timeout = TimeSpan.FromSeconds(30);

        if (!string.IsNullOrEmpty(HostKey))
            _client.DefaultRequestHeaders.Add("x-functions-key", HostKey);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    // =========================================================================
    // Payment 1 — Fare
    // =========================================================================

    [Fact, PaymentTestPriority(1)]
    public async Task T01_InitialiseFarePayment_ReturnsCreatedWithPaymentId()
    {
        var request = new
        {
            bookingReference = BookingReference,
            paymentType = "Fare",
            method = "CreditCard",
            currencyCode = "GBP",
            amount = _fareAmount,
            description = "Fare payment for booking AB1234"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/payment/initialise", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<InitialisePaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().NotBeEmpty();
        body.Amount.Should().Be(_fareAmount);
        body.Status.Should().Be("Initialised");

        _farePaymentId = body.PaymentId;
    }

    [SkippableFact, PaymentTestPriority(2)]
    public async Task T02_AuthoriseFarePayment_ReturnsAuthorised()
    {
        Skip.If(_farePaymentId is null, "No fare paymentId from T01");

        var request = new
        {
            cardDetails = new
            {
                cardNumber = "4111111111111111",
                expiryDate = "12/26",
                cvv = "123",
                cardholderName = "John Smith"
            }
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment/{_farePaymentId}/authorise", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthorisePaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(_farePaymentId!.Value);
        body.AuthorisedAmount.Should().Be(_fareAmount);
        body.Status.Should().Be("Authorised");
    }

    [SkippableFact, PaymentTestPriority(3)]
    public async Task T03_SettleFarePayment_ReturnsSettled()
    {
        Skip.If(_farePaymentId is null, "No fare paymentId from T01");

        var request = new { settledAmount = _fareAmount };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment/{_farePaymentId}/settle", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SettlePaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(_farePaymentId!.Value);
        body.SettledAmount.Should().Be(_fareAmount);
        body.SettledAt.Should().NotBeNull();
    }

    [SkippableFact, PaymentTestPriority(4)]
    public async Task T04_GetFarePayment_ReturnsSettledRecord()
    {
        Skip.If(_farePaymentId is null, "No fare paymentId from T01");

        var response = await _client.GetAsync($"/api/v1/payment/{_farePaymentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(_farePaymentId!.Value);
        body.BookingReference.Should().Be(BookingReference);
        body.PaymentType.Should().Be("Fare");
        body.Method.Should().Be("CreditCard");
        body.CardType.Should().Be("Visa");
        body.CardLast4.Should().Be("1111");
        body.CurrencyCode.Should().Be("GBP");
        body.Amount.Should().Be(_fareAmount);
        body.AuthorisedAmount.Should().Be(_fareAmount);
        body.SettledAmount.Should().Be(_fareAmount);
        body.Status.Should().Be("Settled");
        body.AuthorisedAt.Should().NotBeNull();
        body.SettledAt.Should().NotBeNull();
    }

    [SkippableFact, PaymentTestPriority(5)]
    public async Task T05_GetFarePaymentEvents_ReturnsTwoEvents()
    {
        Skip.If(_farePaymentId is null, "No fare paymentId from T01");

        var response = await _client.GetAsync($"/api/v1/payment/{_farePaymentId}/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await response.Content.ReadFromJsonAsync<List<PaymentEventResponse>>(JsonOptions);

        events.Should().NotBeNull();
        // Authorised event created by authorise step, Settled event created by settle step
        events!.Should().HaveCount(2);

        var authorisedEvent = events.Should().ContainSingle(e => e.EventType == "Authorised").Subject;
        authorisedEvent.PaymentId.Should().Be(_farePaymentId!.Value);
        authorisedEvent.Amount.Should().Be(_fareAmount);
        authorisedEvent.CurrencyCode.Should().Be("GBP");
        authorisedEvent.PaymentEventId.Should().NotBeEmpty();

        var settledEvent = events.Should().ContainSingle(e => e.EventType == "Settled").Subject;
        settledEvent.PaymentId.Should().Be(_farePaymentId!.Value);
        settledEvent.Amount.Should().Be(_fareAmount);
        settledEvent.CurrencyCode.Should().Be("GBP");

        // Events must be in chronological order
        events[0].CreatedAt.Should().BeOnOrBefore(events[1].CreatedAt);
    }

    // =========================================================================
    // Payment 2 — SeatAncillary
    // =========================================================================

    [Fact, PaymentTestPriority(6)]
    public async Task T06_InitialiseSeatPayment_ReturnsCreatedWithPaymentId()
    {
        var request = new
        {
            bookingReference = BookingReference,
            paymentType = "SeatAncillary",
            method = "CreditCard",
            currencyCode = "GBP",
            amount = _seatAmount,
            description = "Seat ancillary payment for booking AB1234"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/payment/initialise", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<InitialisePaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().NotBeEmpty();
        body.Amount.Should().Be(_seatAmount);
        body.Status.Should().Be("Initialised");

        // Must be a different PaymentId from the fare payment
        body.PaymentId.Should().NotBe(_farePaymentId ?? Guid.Empty);

        _seatPaymentId = body.PaymentId;
    }

    [SkippableFact, PaymentTestPriority(7)]
    public async Task T07_AuthoriseSeatPayment_ReturnsAuthorised()
    {
        Skip.If(_seatPaymentId is null, "No seat paymentId from T06");

        var request = new
        {
            cardDetails = new
            {
                cardNumber = "5500005555555559",
                expiryDate = "06/27",
                cvv = "456",
                cardholderName = "John Smith"
            }
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment/{_seatPaymentId}/authorise", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthorisePaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(_seatPaymentId!.Value);
        body.AuthorisedAmount.Should().Be(_seatAmount);
        body.Status.Should().Be("Authorised");
    }

    [SkippableFact, PaymentTestPriority(8)]
    public async Task T08_SettleSeatPayment_ReturnsSettled()
    {
        Skip.If(_seatPaymentId is null, "No seat paymentId from T06");

        var request = new { settledAmount = _seatAmount };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment/{_seatPaymentId}/settle", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SettlePaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(_seatPaymentId!.Value);
        body.SettledAmount.Should().Be(_seatAmount);
        body.SettledAt.Should().NotBeNull();
    }

    [SkippableFact, PaymentTestPriority(9)]
    public async Task T09_GetSeatPayment_ReturnsSettledRecord()
    {
        Skip.If(_seatPaymentId is null, "No seat paymentId from T06");

        var response = await _client.GetAsync($"/api/v1/payment/{_seatPaymentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(_seatPaymentId!.Value);
        body.BookingReference.Should().Be(BookingReference);
        body.PaymentType.Should().Be("SeatAncillary");
        body.CardType.Should().Be("Mastercard");
        body.CardLast4.Should().Be("5559");
        body.Amount.Should().Be(_seatAmount);
        body.AuthorisedAmount.Should().Be(_seatAmount);
        body.SettledAmount.Should().Be(_seatAmount);
        body.Status.Should().Be("Settled");
    }

    [SkippableFact, PaymentTestPriority(10)]
    public async Task T10_GetSeatPaymentEvents_ReturnsTwoEvents()
    {
        Skip.If(_seatPaymentId is null, "No seat paymentId from T06");

        var response = await _client.GetAsync($"/api/v1/payment/{_seatPaymentId}/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await response.Content.ReadFromJsonAsync<List<PaymentEventResponse>>(JsonOptions);

        events.Should().NotBeNull();
        events!.Should().HaveCount(2);

        events.Should().ContainSingle(e => e.EventType == "Authorised")
            .Which.Amount.Should().Be(_seatAmount);

        events.Should().ContainSingle(e => e.EventType == "Settled")
            .Which.Amount.Should().Be(_seatAmount);

        // Events must be in chronological order
        events[0].CreatedAt.Should().BeOnOrBefore(events[1].CreatedAt);

        // Seat events must be isolated from fare events
        events.Should().OnlyContain(e => e.PaymentId == _seatPaymentId!.Value);
    }

    // =========================================================================
    // Error / edge-case tests
    // =========================================================================

    [Fact, PaymentTestPriority(11)]
    public async Task T11_GetPayment_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/payment/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, PaymentTestPriority(12)]
    public async Task T12_GetPaymentEvents_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/payment/{Guid.NewGuid()}/events");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, PaymentTestPriority(13)]
    public async Task T13_GetPayment_InvalidIdFormat_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/v1/payment/not-a-guid");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact, PaymentTestPriority(14)]
    public async Task T14_GetPaymentEvents_InvalidIdFormat_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/v1/payment/not-a-guid/events");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact, PaymentTestPriority(15)]
    public async Task T15_InitialisePayment_MissingRequiredFields_ReturnsBadRequest()
    {
        var request = new { amount = 100.00m };

        var response = await _client.PostAsJsonAsync("/api/v1/payment/initialise", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact, PaymentTestPriority(16)]
    public async Task T16_AuthorisePayment_NonExistentId_ReturnsNotFound()
    {
        var request = new
        {
            cardDetails = new
            {
                cardNumber = "4111111111111111",
                expiryDate = "12/26",
                cvv = "123",
                cardholderName = "John Smith"
            }
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment/{Guid.NewGuid()}/authorise", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}

#region Response DTOs

public sealed class InitialisePaymentResponse
{
    public Guid PaymentId { get; init; }
    public decimal Amount { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class AuthorisePaymentResponse
{
    public Guid PaymentId { get; init; }
    public decimal AuthorisedAmount { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class SettlePaymentResponse
{
    public Guid PaymentId { get; init; }
    public decimal SettledAmount { get; init; }
    public DateTime? SettledAt { get; init; }
}

public sealed class PaymentResponse
{
    public Guid PaymentId { get; init; }
    public string? BookingReference { get; init; }
    public string PaymentType { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string? CardType { get; init; }
    public string? CardLast4 { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal? AuthorisedAmount { get; init; }
    public decimal? SettledAmount { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime? AuthorisedAt { get; init; }
    public DateTime? SettledAt { get; init; }
    public string? Description { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class PaymentEventResponse
{
    public Guid PaymentEventId { get; init; }
    public Guid PaymentId { get; init; }
    public string EventType { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime CreatedAt { get; init; }
}

#endregion

#region Test Ordering Infrastructure

[AttributeUsage(AttributeTargets.Method)]
public sealed class PaymentTestPriorityAttribute : Attribute
{
    public int Priority { get; }
    public PaymentTestPriorityAttribute(int priority) => Priority = priority;
}

public sealed class PaymentPriorityOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        var sortedCases = new SortedDictionary<int, List<TTestCase>>();

        foreach (var testCase in testCases)
        {
            var priority = testCase.TestMethod.Method
                .GetCustomAttributes(typeof(PaymentTestPriorityAttribute).AssemblyQualifiedName)
                .FirstOrDefault()
                ?.GetNamedArgument<int>(nameof(PaymentTestPriorityAttribute.Priority)) ?? 0;

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
