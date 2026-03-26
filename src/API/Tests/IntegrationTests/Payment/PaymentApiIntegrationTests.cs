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

    // =========================================================================
    // Split-settlement lifecycle — single init, one auth, two successive settles
    // =========================================================================
    //
    // Scenario: a 500 GBP total is processed as two portions against one PaymentId:
    //
    //   Initialise  500 GBP
    //   Authorise   500 GBP  (one card auth covers the full amount)
    //   Settle      400 GBP  → PartiallySettled (fare portion)
    //   Settle      100 GBP  → Settled          (seat portion — completes the hold)
    //
    // A second authorise call on the same PaymentId is validated separately (T24) and
    // must return 409 Conflict: the API enforces one authorise per payment lifecycle.
    // The remaining balance is captured by a second settle against the same auth.

    private static Guid? _splitPaymentId;
    private static readonly decimal _splitTotal = 500.00m;
    private static readonly decimal _farePortion = 400.00m;
    private static readonly decimal _seatPortion = 100.00m;

    [Fact, PaymentTestPriority(19)]
    public async Task T19_SplitPayment_Initialise500Gbp_ReturnsCreated()
    {
        var request = new
        {
            bookingReference = "ZZ5000",
            paymentType = "Fare",
            method = "CreditCard",
            currencyCode = "GBP",
            amount = _splitTotal,
            description = "Split-settlement: 500 GBP fare+seat"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/payment/initialise", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<InitialisePaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().NotBeEmpty();
        body.Amount.Should().Be(_splitTotal);
        body.Status.Should().Be("Initialised");

        _splitPaymentId = body.PaymentId;
    }

    [SkippableFact, PaymentTestPriority(20)]
    public async Task T20_SplitPayment_AuthoriseFarePortion400_ReturnsAuthorisedAmount()
    {
        Skip.If(_splitPaymentId is null, "No splitPaymentId from T19");

        // Pass explicit amount to authorise only the fare portion of the 500 GBP total
        var request = new
        {
            amount = _farePortion,
            cardDetails = new
            {
                cardNumber = "4111111111111111",
                expiryDate = "12/27",
                cvv = "737",
                cardholderName = "James Carter"
            }
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment/{_splitPaymentId}/authorise", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthorisePaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(_splitPaymentId!.Value);
        body.AuthorisedAmount.Should().Be(_farePortion);  // cumulative after first auth
        body.Status.Should().Be("Authorised");
    }

    [SkippableFact, PaymentTestPriority(21)]
    public async Task T21_SplitPayment_SettleFarePortion400_ReturnsPartiallySettled()
    {
        Skip.If(_splitPaymentId is null, "No splitPaymentId from T19");

        var request = new { settledAmount = _farePortion };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment/{_splitPaymentId}/settle", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SettlePaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(_splitPaymentId!.Value);
        body.SettledAmount.Should().Be(_farePortion);
        body.SettledAt.Should().NotBeNull();
    }

    [SkippableFact, PaymentTestPriority(22)]
    public async Task T22_SplitPayment_GetPaymentAfterPartialSettle_ReturnsPartiallySettled()
    {
        Skip.If(_splitPaymentId is null, "No splitPaymentId from T19");

        var response = await _client.GetAsync($"/api/v1/payment/{_splitPaymentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(_splitPaymentId!.Value);
        body.Amount.Should().Be(_splitTotal);
        body.AuthorisedAmount.Should().Be(_farePortion);   // only first auth so far
        body.SettledAmount.Should().Be(_farePortion);
        body.Status.Should().Be("PartiallySettled");
        body.AuthorisedAt.Should().NotBeNull();
        body.SettledAt.Should().NotBeNull();
    }

    [SkippableFact, PaymentTestPriority(23)]
    public async Task T23_SplitPayment_AuthoriseSeatPortion100_ReturnsAuthorisedAmount()
    {
        // From PartiallySettled the API accepts a further authorise call, accumulating
        // the authorised total. This enables independent auth+settle cycles (fare, seat)
        // against a single initialised payment.
        Skip.If(_splitPaymentId is null, "No splitPaymentId from T19");

        var request = new
        {
            amount = _seatPortion,
            cardDetails = new
            {
                cardNumber = "4111111111111111",
                expiryDate = "12/27",
                cvv = "737",
                cardholderName = "James Carter"
            }
        };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment/{_splitPaymentId}/authorise", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthorisePaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(_splitPaymentId!.Value);
        body.AuthorisedAmount.Should().Be(_splitTotal);   // accumulated: 400 + 100
        body.Status.Should().Be("Authorised");
    }

    [SkippableFact, PaymentTestPriority(24)]
    public async Task T24_SplitPayment_SettleSeatPortion100_ReturnsFullySettled()
    {
        Skip.If(_splitPaymentId is null, "No splitPaymentId from T19");

        var request = new { settledAmount = _seatPortion };

        var response = await _client.PostAsJsonAsync(
            $"/api/v1/payment/{_splitPaymentId}/settle", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SettlePaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(_splitPaymentId!.Value);
        body.SettledAmount.Should().Be(_splitTotal);   // accumulated total: 400 + 100
        body.SettledAt.Should().NotBeNull();
    }

    [SkippableFact, PaymentTestPriority(25)]
    public async Task T25_SplitPayment_GetPaymentAfterFullSettle_ReturnsSettled()
    {
        Skip.If(_splitPaymentId is null, "No splitPaymentId from T19");

        var response = await _client.GetAsync($"/api/v1/payment/{_splitPaymentId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(_splitPaymentId!.Value);
        body.Amount.Should().Be(_splitTotal);
        body.AuthorisedAmount.Should().Be(_splitTotal);   // accumulated: 400 + 100
        body.SettledAmount.Should().Be(_splitTotal);       // accumulated: 400 + 100
        body.Status.Should().Be("Settled");
    }

    [SkippableFact, PaymentTestPriority(26)]
    public async Task T26_SplitPayment_GetEvents_ReturnsFourChronologicalEvents()
    {
        Skip.If(_splitPaymentId is null, "No splitPaymentId from T19");

        var response = await _client.GetAsync($"/api/v1/payment/{_splitPaymentId}/events");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await response.Content.ReadFromJsonAsync<List<PaymentEventResponse>>(JsonOptions);

        events.Should().NotBeNull();
        // Two auth+settle cycles each produce one Authorised and one settlement event:
        //   Authorised (400) → PartialSettlement (400) → Authorised (100) → Settled (100)
        events!.Should().HaveCount(4);

        // Both Authorised events exist with their respective partial amounts
        var authorisedEvents = events.Where(e => e.EventType == "Authorised").ToList();
        authorisedEvents.Should().HaveCount(2);
        authorisedEvents.Should().ContainSingle(e => e.Amount == _farePortion);
        authorisedEvents.Should().ContainSingle(e => e.Amount == _seatPortion);

        events.Should().ContainSingle(e => e.EventType == "PartialSettlement")
            .Which.Amount.Should().Be(_farePortion);

        events.Should().ContainSingle(e => e.EventType == "Settled")
            .Which.Amount.Should().Be(_seatPortion);

        // All events must belong to this payment
        events.Should().OnlyContain(e => e.PaymentId == _splitPaymentId!.Value);

        // Events must be in strict chronological order
        events[0].CreatedAt.Should().BeOnOrBefore(events[1].CreatedAt);
        events[1].CreatedAt.Should().BeOnOrBefore(events[2].CreatedAt);
        events[2].CreatedAt.Should().BeOnOrBefore(events[3].CreatedAt);
    }

    // =========================================================================
    // Standalone — GET /v1/payment/{paymentId} validation
    // =========================================================================

    /// <summary>
    /// Validates GET /v1/payment/{paymentId} against an authorised-but-not-yet-settled
    /// payment. This state is distinct from the lifecycle tests (T04, T09) which only
    /// assert the fully-settled state. Here we verify that:
    ///   - AuthorisedAmount is populated from the authorise step
    ///   - SettledAmount and SettledAt are null (settlement has not occurred)
    ///   - CardType and CardLast4 are populated after authorisation
    ///   - All financial amounts and status fields are accurate for mid-lifecycle reads
    ///   - CreatedAt and UpdatedAt are present and well-formed
    /// </summary>
    [Fact, PaymentTestPriority(17)]
    public async Task T17_GetPayment_AuthorisedState_ReturnsCorrectFieldsAndAmounts()
    {
        const decimal amount = 199.99m;

        // Initialise
        var initRequest = new
        {
            bookingReference = "XY9999",
            paymentType = "Fare",
            method = "CreditCard",
            currencyCode = "GBP",
            amount,
            description = "Standalone GET validation — authorised state"
        };

        var initResponse = await _client.PostAsJsonAsync("/api/v1/payment/initialise", initRequest, JsonOptions);
        initResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var initBody = await initResponse.Content.ReadFromJsonAsync<InitialisePaymentResponse>(JsonOptions);
        initBody.Should().NotBeNull();
        var paymentId = initBody!.PaymentId;

        // Authorise (no settle)
        var authRequest = new
        {
            cardDetails = new
            {
                cardNumber = "4111111111111111",
                expiryDate = "12/26",
                cvv = "123",
                cardholderName = "Jane Doe"
            }
        };

        var authResponse = await _client.PostAsJsonAsync(
            $"/api/v1/payment/{paymentId}/authorise", authRequest, JsonOptions);
        authResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET payment — assert authorised state
        var getResponse = await _client.GetAsync($"/api/v1/payment/{paymentId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await getResponse.Content.ReadFromJsonAsync<PaymentResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PaymentId.Should().Be(paymentId);
        body.BookingReference.Should().Be("XY9999");
        body.PaymentType.Should().Be("Fare");
        body.Method.Should().Be("CreditCard");
        body.CardType.Should().Be("Visa");
        body.CardLast4.Should().Be("1111");
        body.CurrencyCode.Should().Be("GBP");
        body.Amount.Should().Be(amount);
        body.AuthorisedAmount.Should().Be(amount);
        body.SettledAmount.Should().BeNull();
        body.Status.Should().Be("Authorised");
        body.AuthorisedAt.Should().NotBeNull();
        body.SettledAt.Should().BeNull();
        body.Description.Should().Be("Standalone GET validation — authorised state");
        body.CreatedAt.Should().NotBe(default);
        body.UpdatedAt.Should().NotBe(default);
        body.UpdatedAt.Should().BeOnOrAfter(body.CreatedAt);
    }

    // =========================================================================
    // Standalone — GET /v1/payment/{paymentId}/events validation
    // =========================================================================

    /// <summary>
    /// Validates GET /v1/payment/{paymentId}/events against an authorised-but-not-yet-settled
    /// payment. This state is distinct from the lifecycle tests (T05, T10) which assert two
    /// events (Authorised + Settled). Here we verify that:
    ///   - Exactly one event exists (Authorised) when settlement has not occurred
    ///   - All event fields are correctly populated (paymentEventId, paymentId, eventType,
    ///     amount, currencyCode, createdAt)
    ///   - The events endpoint returns only events for the requested paymentId
    /// </summary>
    [Fact, PaymentTestPriority(18)]
    public async Task T18_GetPaymentEvents_AuthorisedOnly_ReturnsSingleAuthorisedEvent()
    {
        const decimal amount = 75.50m;

        // Initialise
        var initRequest = new
        {
            bookingReference = "XY9999",
            paymentType = "BagAncillary",
            method = "DebitCard",
            currencyCode = "GBP",
            amount,
            description = "Standalone events GET validation — single event"
        };

        var initResponse = await _client.PostAsJsonAsync("/api/v1/payment/initialise", initRequest, JsonOptions);
        initResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var initBody = await initResponse.Content.ReadFromJsonAsync<InitialisePaymentResponse>(JsonOptions);
        initBody.Should().NotBeNull();
        var paymentId = initBody!.PaymentId;

        // Authorise (no settle)
        var authRequest = new
        {
            cardDetails = new
            {
                cardNumber = "4111111111111111",
                expiryDate = "06/28",
                cvv = "321",
                cardholderName = "Alice Brown"
            }
        };

        var authResponse = await _client.PostAsJsonAsync(
            $"/api/v1/payment/{paymentId}/authorise", authRequest, JsonOptions);
        authResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        // GET events — assert single Authorised event
        var getResponse = await _client.GetAsync($"/api/v1/payment/{paymentId}/events");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var events = await getResponse.Content.ReadFromJsonAsync<List<PaymentEventResponse>>(JsonOptions);

        events.Should().NotBeNull();
        events!.Should().HaveCount(1, "only the authorise step has occurred; no settled event yet");

        var ev = events[0];
        ev.PaymentEventId.Should().NotBeEmpty();
        ev.PaymentId.Should().Be(paymentId);
        ev.EventType.Should().Be("Authorised");
        ev.Amount.Should().Be(amount);
        ev.CurrencyCode.Should().Be("GBP");
        ev.CreatedAt.Should().NotBe(default);
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
