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
/// Integration tests for bag policy admin CRUD via the Operations API.
/// Tests run sequentially against the live API, exercising the full lifecycle:
/// create, read, update, read again, delete, confirm 404.
///
/// Prerequisites: no bag policy for cabin 'F' must exist before running.
/// If cabin F is already seeded, delete it first or the Create step will return 409.
/// </summary>
[TestCaseOrderer("ReservationSystem.Tests.IntegrationTests.Ancillary.BagPolicyPriorityOrderer", "ReservationSystem.Tests")]
public class BagPolicyAdminIntegrationTests : IAsyncLifetime
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
    private static Guid? _policyId;
    private static int _originalFreeBags;
    private static int _updatedFreeBags;

    public BagPolicyAdminIntegrationTests()
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
    public async Task T01_CreateBagPolicy_ReturnsCreated()
    {
        _originalFreeBags = 2;

        var request = new
        {
            cabinCode = "F",
            freeBagsIncluded = _originalFreeBags,
            maxWeightKgPerBag = 32
        };

        var response = await _client.PostAsJsonAsync("/api/v1/admin/bag-policies", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<BagPolicyResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.PolicyId.Should().NotBeEmpty();
        body.CabinCode.Should().Be("F");
        body.FreeBagsIncluded.Should().Be(_originalFreeBags);
        body.MaxWeightKgPerBag.Should().Be(32);
        body.IsActive.Should().BeTrue();

        _policyId = body.PolicyId;

        response.Headers.Location.Should().NotBeNull();
    }

    [SkippableFact, TestPriority(2)]
    public async Task T02_GetBagPolicy_ReturnsCreatedPolicy()
    {
        SkipIfNoPolicyId();

        var response = await _client.GetAsync($"/api/v1/admin/bag-policies/{_policyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BagPolicyResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.PolicyId.Should().Be(_policyId!.Value);
        body.CabinCode.Should().Be("F");
        body.FreeBagsIncluded.Should().Be(_originalFreeBags, "value must persist from T01");
        body.MaxWeightKgPerBag.Should().Be(32);
        body.IsActive.Should().BeTrue();
    }

    [SkippableFact, TestPriority(3)]
    public async Task T03_UpdateBagPolicy_ReturnsUpdatedPolicy()
    {
        SkipIfNoPolicyId();

        _updatedFreeBags = 3;

        var request = new
        {
            freeBagsIncluded = _updatedFreeBags,
            maxWeightKgPerBag = 30,
            isActive = true
        };

        var response = await _client.PutAsJsonAsync($"/api/v1/admin/bag-policies/{_policyId}", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BagPolicyResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.PolicyId.Should().Be(_policyId!.Value);
        body.FreeBagsIncluded.Should().Be(_updatedFreeBags);
        body.MaxWeightKgPerBag.Should().Be(30);
        body.IsActive.Should().BeTrue();
    }

    [SkippableFact, TestPriority(4)]
    public async Task T04_GetBagPolicy_ReflectsUpdatedFields()
    {
        SkipIfNoPolicyId();
        Skip.If(_updatedFreeBags == 0, "T03 did not run successfully");

        var response = await _client.GetAsync($"/api/v1/admin/bag-policies/{_policyId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<BagPolicyResponse>(JsonOptions);
        body.Should().NotBeNull();
        body!.FreeBagsIncluded.Should().Be(_updatedFreeBags, "update from T03 must be durable");
        body.MaxWeightKgPerBag.Should().Be(30, "update from T03 must be durable");
    }

    [SkippableFact, TestPriority(5)]
    public async Task T05_DeleteBagPolicy_ReturnsNoContent()
    {
        SkipIfNoPolicyId();

        var response = await _client.DeleteAsync($"/api/v1/admin/bag-policies/{_policyId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [SkippableFact, TestPriority(6)]
    public async Task T06_GetDeletedBagPolicy_ReturnsNotFound()
    {
        SkipIfNoPolicyId();

        var response = await _client.GetAsync($"/api/v1/admin/bag-policies/{_policyId}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── List ──────────────────────────────────────────────────────────────────

    [Fact, TestPriority(7)]
    public async Task T07_GetAllBagPolicies_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/v1/admin/bag-policies");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<IReadOnlyList<BagPolicyResponse>>(JsonOptions);
        body.Should().NotBeNull();
    }

    // ── Validation ────────────────────────────────────────────────────────────

    [Fact, TestPriority(8)]
    public async Task T08_CreateBagPolicy_InvalidCabinCode_ReturnsBadRequest()
    {
        var request = new { cabinCode = "X", freeBagsIncluded = 1, maxWeightKgPerBag = 23 };

        var response = await _client.PostAsJsonAsync("/api/v1/admin/bag-policies", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact, TestPriority(9)]
    public async Task T09_CreateBagPolicy_ZeroWeight_ReturnsBadRequest()
    {
        var request = new { cabinCode = "Y", freeBagsIncluded = 1, maxWeightKgPerBag = 0 };

        var response = await _client.PostAsJsonAsync("/api/v1/admin/bag-policies", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact, TestPriority(10)]
    public async Task T10_GetBagPolicy_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/v1/admin/bag-policies/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, TestPriority(11)]
    public async Task T11_DeleteBagPolicy_NonExistentId_ReturnsNotFound()
    {
        var response = await _client.DeleteAsync($"/api/v1/admin/bag-policies/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SkipIfNoPolicyId() =>
        Skip.If(_policyId is null, "Skipped because T01 did not produce a policy ID.");
}

#region Response DTOs

internal sealed class BagPolicyResponse
{
    public Guid PolicyId { get; init; }
    public string CabinCode { get; init; } = string.Empty;
    public int FreeBagsIncluded { get; init; }
    public int MaxWeightKgPerBag { get; init; }
    public bool IsActive { get; init; }
    public string CreatedAt { get; init; } = string.Empty;
    public string UpdatedAt { get; init; } = string.Empty;
}

#endregion

#region Test Ordering Infrastructure

[AttributeUsage(AttributeTargets.Method)]
internal sealed class BagPolicyTestPriorityAttribute : Attribute
{
    public int Priority { get; }
    public BagPolicyTestPriorityAttribute(int priority) => Priority = priority;
}

internal sealed class BagPolicyPriorityOrderer : ITestCaseOrderer
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
