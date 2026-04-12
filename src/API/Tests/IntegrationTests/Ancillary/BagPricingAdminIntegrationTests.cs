using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ReservationSystem.Tests.IntegrationTests.Ancillary;

/// <summary>
/// Integration tests for bag pricing admin CRUD via the Operations API.
/// Tests run sequentially against the live API, exercising the full lifecycle:
/// create, read, update, read again, delete, confirm 404.
///
/// Uses currency "USD" and sequence 1 to avoid conflicting with the seeded GBP rules.
/// </summary>
[TestCaseOrderer("ReservationSystem.Tests.IntegrationTests.Ancillary.BagPricingPriorityOrderer", "ReservationSystem.Tests")]
public class BagPricingAdminIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPERATIONS_API_BASE_URL"))
            ? "https://reservation-system-db-api-operations-gzfhekfvawaubkbs.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("OPERATIONS_API_BASE_URL")!;

    private static readonly string? HostKey = Environment.GetEnvironmentVariable("OPERATIONS_API_HOST_KEY");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _client;

    // Shared state across ordered tests
    private static Guid? _pricingId;
    private static decimal _originalPrice;
    private static decimal _updatedPrice;

    public BagPricingAdminIntegrationTests()
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

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    [Fact, TestPriority(1)]
    public async Task T01_CreateBagPricing_ReturnsCreated()
    {
        _originalPrice = 55.00m;

        var request = new
        {
            bagSequence = 1,
            currencyCode = "USD",
            price = _originalPrice,
            validFrom = "2026-01-01T00:00:00Z",
            validTo = (string?)null
        };

        var response = await _client.PostAsJsonAsync("/api/v1/admin/bag-pricing", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<BagPricingResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.PricingId.Should().NotBeEmpty();
        body.BagSequence.Should().Be(1);
        body.CurrencyCode.Should().Be("USD");
        body.Price.Should().Be(_originalPrice);
        body.IsActive.Should().BeTrue();
        body.ValidTo.Should().BeNull();

        _pricingId = body.PricingId;

        response.Headers.Location.Should().NotBeNull();
    }

    [SkippableFact, TestPriority(2)]
    public async Task T02_GetBagPricing_ReturnsCreatedRule()
    {
        SkipIfNoPricingId();

        var response = await _client.GetAsync($"/api/v1/admin/bag-pricing/{_pricingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BagPricingResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.PricingId.Should().Be(_pricingId!.Value);
        body.BagSequence.Should().Be(1);
        body.CurrencyCode.Should().Be("USD");
        body.Price.Should().Be(_originalPrice, "value must persist from T01");
        body.IsActive.Should().BeTrue();
    }

    [SkippableFact, TestPriority(3)]
    public async Task T03_UpdateBagPricing_ReturnsUpdatedRule()
    {
        SkipIfNoPricingId();

        _updatedPrice = 65.00m;

        var request = new
        {
            price = _updatedPrice,
            isActive = true,
            validFrom = "2026-01-01T00:00:00Z",
            validTo = "2026-12-31T23:59:59Z"
        };

        var response = await _client.PutAsJsonAsync($"/api/v1/admin/bag-pricing/{_pricingId}", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BagPricingResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.PricingId.Should().Be(_pricingId!.Value);
        body.Price.Should().Be(_updatedPrice);
        body.IsActive.Should().BeTrue();
        body.ValidTo.Should().NotBeNull("validTo was set in the update");
    }

    [SkippableFact, TestPriority(4)]
    public async Task T04_GetBagPricing_ReflectsUpdatedFields()
    {
        SkipIfNoPricingId();
        Skip.If(_updatedPrice == 0, "T03 did not run successfully");

        var response = await _client.GetAsync($"/api/v1/admin/bag-pricing/{_pricingId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BagPricingResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.Price.Should().Be(_updatedPrice, "update from T03 must be durable");
        body.ValidTo.Should().NotBeNull("validTo set in T03 must persist");
    }

    [SkippableFact, TestPriority(5)]
    public async Task T05_DeactivateBagPricing_IsActiveBecomesfalse()
    {
        SkipIfNoPricingId();

        var request = new
        {
            price = _updatedPrice,
            isActive = false,
            validFrom = "2026-01-01T00:00:00Z",
            validTo = "2026-12-31T23:59:59Z"
        };

        var response = await _client.PutAsJsonAsync($"/api/v1/admin/bag-pricing/{_pricingId}", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BagPricingResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.IsActive.Should().BeFalse("rule was explicitly deactivated");
    }

    [SkippableFact, TestPriority(6)]
    public async Task T06_DeleteBagPricing_ReturnsNoContent()
    {
        SkipIfNoPricingId();

        var response = await _client.DeleteAsync($"/api/v1/admin/bag-pricing/{_pricingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [SkippableFact, TestPriority(7)]
    public async Task T07_GetDeletedBagPricing_ReturnsNotFound()
    {
        SkipIfNoPricingId();

        var response = await _client.GetAsync($"/api/v1/admin/bag-pricing/{_pricingId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact, TestPriority(8)]
    public async Task T08_GetAllBagPricing_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/admin/bag-pricing");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<BagPricingResponse>>(JsonOptions);
        body.Should().NotBeNull();
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact, TestPriority(9)]
    public async Task T09_CreateBagPricing_InvalidSequence_ReturnsBadRequest()
    {
        var request = new
        {
            bagSequence = 5,
            currencyCode = "GBP",
            price = 50.00m,
            validFrom = "2026-01-01T00:00:00Z"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/admin/bag-pricing", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact, TestPriority(10)]
    public async Task T10_CreateBagPricing_ZeroPrice_ReturnsBadRequest()
    {
        var request = new
        {
            bagSequence = 1,
            currencyCode = "EUR",
            price = 0m,
            validFrom = "2026-01-01T00:00:00Z"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/admin/bag-pricing", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact, TestPriority(11)]
    public async Task T11_GetBagPricing_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/admin/bag-pricing/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, TestPriority(12)]
    public async Task T12_DeleteBagPricing_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/api/v1/admin/bag-pricing/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, TestPriority(13)]
    public async Task T13_CreateBagPricing_DuplicateSequenceAndCurrency_ReturnsConflict()
    {
        // GBP sequence 1 is seeded — creating a duplicate should return 409
        var request = new
        {
            bagSequence = 1,
            currencyCode = "GBP",
            price = 60.00m,
            validFrom = "2026-01-01T00:00:00Z"
        };

        var response = await _client.PostAsJsonAsync("/api/v1/admin/bag-pricing", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SkipIfNoPricingId() =>
        Skip.If(_pricingId is null, "Skipped because T01 did not produce a pricing ID.");
}

#region Response DTOs

internal sealed class BagPricingResponse
{
    public Guid PricingId { get; init; }
    public int BagSequence { get; init; }
    public string CurrencyCode { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public bool IsActive { get; init; }
    public string ValidFrom { get; init; } = string.Empty;
    public string? ValidTo { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

#endregion

#region Test Ordering Infrastructure

internal sealed class BagPricingPriorityOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
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
