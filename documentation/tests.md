# Integration test guide

This guide explains how to write integration tests for microservice APIs in this repository. Follow the patterns established in the Customer microservice tests exactly — every new test suite must be structurally identical.

---

## Overview

Integration tests run against the live Azure Functions API. They are not unit tests — they exercise real HTTP endpoints, real persistence, and real business logic in sequence. Each test class covers the full lifecycle of the entity it owns.

Test files live alongside the microservice they target:

```
src/API/Tests/IntegrationTests/
├── Customer/
│   └── CustomerApiIntegrationTests.cs   ← reference implementation
├── Bags/
│   └── BagsApiIntegrationTests.cs       ← follow this pattern
├── Identity/
│   └── IdentityApiIntegrationTests.cs
└── ...
```

---

## Project setup

All test suites share a single test project: `src/API/Tests/ReservationSystem.Tests.csproj`.

Required NuGet packages (already present — do not add duplicates):

| Package | Version | Purpose |
|---------|---------|---------|
| `xunit` | 2.6.6 | Test framework |
| `xunit.runner.visualstudio` | 2.5.6 | IDE integration |
| `Xunit.SkippableFact` | 1.4.13 | Conditional test skipping |
| `Bogus` | 35.4.0 | Fake data generation |
| `FluentAssertions` | 6.12.0 | Assertion DSL |
| `Microsoft.NET.Test.Sdk` | 17.8.0 | Test runner host |

---

## File structure

Each test file contains three regions in order:

1. **Test class** — the xUnit test class with all test methods.
2. **Response DTOs** — sealed record/class types used to deserialise API responses.
3. **Test ordering infrastructure** — `TestPriorityAttribute` and `PriorityOrderer` (copy verbatim from the Customer tests).

```
namespace ReservationSystem.Tests.IntegrationTests.<Domain>;

public class <Domain>ApiIntegrationTests : IAsyncLifetime { ... }

#region Response DTOs
// ...
#endregion

#region Test Ordering Infrastructure
// ...
#endregion
```

---

## Test class skeleton

```csharp
[TestCaseOrderer(
    "ReservationSystem.Tests.IntegrationTests.<Domain>.PriorityOrderer",
    "ReservationSystem.Tests.IntegrationTests.<Domain>")]
public class <Domain>ApiIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("<DOMAIN>_API_BASE_URL"))
            ? "https://<default-azure-url>"
            : Environment.GetEnvironmentVariable("<DOMAIN>_API_BASE_URL")!;

    private static readonly string? HostKey =
        Environment.GetEnvironmentVariable("<DOMAIN>_API_HOST_KEY");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _client;
    private readonly Faker _faker;

    // Static state shared across ordered tests
    private static Guid? _entityId;
    // ... add fields for every piece of state the lifecycle tests need

    public <Domain>ApiIntegrationTests()
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
}
```

---

## Environment variables

Every test class reads two environment variables. Use the `<DOMAIN>` prefix in uppercase:

| Variable | Purpose |
|----------|---------|
| `<DOMAIN>_API_BASE_URL` | Base URL of the live API. Falls back to a hardcoded Azure URL when absent. |
| `<DOMAIN>_API_HOST_KEY` | Azure Functions host key sent as the `x-functions-key` request header. Omit the header when the variable is not set. |

Examples: `CUSTOMER_API_BASE_URL`, `BAGS_API_BASE_URL`, `IDENTITY_API_HOST_KEY`.

---

## Test ordering

Tests within a class run in a fixed sequence using `TestPriorityAttribute` and `PriorityOrderer`. Copy both classes verbatim from the Customer test file into the `#region Test Ordering Infrastructure` block of each new file. Update the namespace to match the new domain.

Assign priorities starting at 1 with no gaps:

```csharp
[Fact, TestPriority(1)]
public async Task T01_Create<Entity>_Returns<ExpectedStatus>() { ... }

[SkippableFact, TestPriority(2)]
public async Task T02_Get<Entity>_Returns<ExpectedStatus>() { ... }
```

---

## Test naming convention

Test method names follow this pattern:

```
T<NN>_<Action><Entity>_<ExpectedOutcome>
```

- `<NN>` — two-digit zero-padded priority number matching `TestPriority`.
- `<Action>` — verb describing what the test does: `Create`, `Get`, `Update`, `Delete`, `Authorise`, `Settle`, `Reverse`, `Reinstate`.
- `<Entity>` — the domain entity being acted on.
- `<ExpectedOutcome>` — what a passing test proves: `ReturnsCreated`, `ReturnsNotFound`, `ReflectsUpdatedFields`, `PaginationWorks`.

---

## Lifecycle test sequence

Structure the ordered tests to exercise the full entity lifecycle in this sequence:

| Priority range | Purpose |
|----------------|---------|
| T01 | Create entity — capture the returned identifier(s) in static fields |
| T02 | Read entity — verify all fields returned from T01 persist correctly |
| T03 | Update entity — send a `PATCH` with changed fields, verify response |
| T04 | Read entity again — verify the update persisted |
| T05–TN-3 | Domain-specific operations in logical order (e.g. points authorise → settle → reverse) |
| TN-2 | Delete entity — assert `204 No Content` |
| TN-1 | Read deleted entity — assert `404 Not Found` |
| TN | Standalone tests — self-contained, create and delete their own data, no shared state |

---

## Standalone tests

Standalone tests (the final group, using `[Fact]` rather than `[SkippableFact]`) must not depend on shared static state. Each standalone test creates its own entity and deletes it at the end:

```csharp
[Fact, TestPriority(19)]
public async Task T19_Create<Entity>_MinimalFields()
{
    var request = new { /* only required fields */ };
    var response = await _client.PostAsJsonAsync("/api/v1/<entities>", request, JsonOptions);

    response.StatusCode.Should().Be(HttpStatusCode.Created);
    var body = await response.Content.ReadFromJsonAsync<Create<Entity>Response>(JsonOptions);
    body.Should().NotBeNull();
    // Assert minimum viable response shape

    // Cleanup
    await _client.Delete<Entity>Async($"/api/v1/<entities>/{body!.<Identifier>}");
}
```

Typical standalone tests to include:

- Minimal fields only (omit all optional fields).
- Cross-reference variant (e.g. creating with an `identityReference`, verifying it is stored).
- Partial update preserves untouched fields (create fresh entity → `PATCH` one field → assert all others unchanged → delete).

---

## Skippable tests and guard helpers

Ordered tests that depend on a previous step must use `[SkippableFact]` and skip if the prerequisite state is absent:

```csharp
[SkippableFact, TestPriority(2)]
public async Task T02_Get<Entity>_ReturnsEntity()
{
    Skip.If(_entityId is null, "T01 did not produce an entity ID");
    // ...
}
```

Add a private static helper for the most common skip condition (equivalent to `SkipIfNoLoyaltyNumber` in the Customer tests):

```csharp
private static void SkipIfNoEntityId()
{
    Skip.If(_entityId is null, "Previous test did not produce an entity ID");
}
```

Use `Skip.If(condition, reason)` for secondary skip conditions within a test (e.g. skipping a settle step when the authorise step did not produce a reference).

---

## HTTP calls

Use the following patterns consistently. Do not introduce additional HTTP client abstractions.

### GET

```csharp
var response = await _client.GetAsync($"/api/v1/<entities>/{identifier}");
```

### POST

```csharp
var response = await _client.PostAsJsonAsync("/api/v1/<entities>", request, JsonOptions);
```

### PATCH

```csharp
var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/<entities>/{identifier}")
{
    Content = JsonContent.Create(request, options: JsonOptions)
};
var response = await _client.SendAsync(patchRequest);
```

### DELETE

```csharp
var response = await _client.DeleteAsync($"/api/v1/<entities>/{identifier}");
```

---

## Assertions

Use FluentAssertions throughout. Structure every test as Arrange / Act / Assert with blank-line separation.

Common assertion patterns:

```csharp
// Status codes
response.StatusCode.Should().Be(HttpStatusCode.Created);
response.StatusCode.Should().Be(HttpStatusCode.OK);
response.StatusCode.Should().Be(HttpStatusCode.NoContent);
response.StatusCode.Should().Be(HttpStatusCode.NotFound);

// Deserialise response body
var body = await response.Content.ReadFromJsonAsync<CreateEntityResponse>(JsonOptions);
body.Should().NotBeNull();

// Field assertions
body!.EntityId.Should().NotBeEmpty();
body.SomeField.Should().Be(expectedValue);
body.SomeNullable.Should().BeNull();
body.NumericField.Should().BeGreaterThan(0);
body.Collection.Should().NotBeEmpty();
body.Collection.Should().HaveCount(n);
body.StringField.Should().StartWith("PREFIX").And.HaveLength(9);

// Assertions with failure messages (for PATCH preservation tests)
body.UnchangedField.Should().Be(original, "partial update should preserve <field>");
```

---

## Response DTOs

Define one sealed class per API response shape inside the `#region Response DTOs` block. Use `init`-only properties, match the camelCase JSON field names from the API spec, and initialise strings to `string.Empty`.

```csharp
public sealed class Create<Entity>Response
{
    public Guid EntityId { get; init; }
    // ... fields from the POST response body
}

public sealed class <Entity>Response
{
    public Guid EntityId { get; init; }
    // ... all fields from the GET response body
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
```

Rules:
- Use `Guid` for identifier fields.
- Use `DateOnly` for date-only fields (e.g. `dateOfBirth`).
- Use `DateTimeOffset` for timestamp fields (e.g. `createdAt`, `settledAt`).
- Use `decimal` for monetary amounts. Never use `double` or `float`.
- Use `int` for points or count fields.
- Use `string?` and `Guid?` for optional fields.
- Provide `IReadOnlyList<T>` for collection fields, initialised to `[]`.

---

## Fake data generation

Use `Bogus.Faker` for all test data. Do not hardcode values in lifecycle tests. Common patterns:

```csharp
_faker.Name.FirstName()
_faker.Name.LastName()
_faker.Date.Past(50, DateTime.Today.AddYears(-18))   // adult date of birth
_faker.PickRandom("en", "fr", "de", "es", "it")
_faker.Phone.PhoneNumber("+44##########")
_faker.Address.CountryCode()
_faker.Random.AlphaNumeric(6).ToUpper()              // booking reference
Guid.NewGuid()                                        // basket ID, identity reference
```

Standalone tests may use inline literals for simplicity (e.g. `preferredLanguage = "en"`).

---

## JSON serialisation

Use the shared `JsonOptions` instance defined at the top of the class. Never create a second `JsonSerializerOptions` instance within the same file.

```csharp
private static readonly JsonSerializerOptions JsonOptions = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
};
```

This matches the camelCase field names the APIs produce and suppresses null fields in request bodies.

---

## Shared static state

Capture identifiers and other cross-test data in `private static` fields. Declare them all at the top of the class, grouped after the `_faker` field. Assign them only inside the test that first produces them:

```csharp
// Shared state across ordered tests
private static Guid? _entityId;
private static string? _someReference;
private static string? _capturedFieldFromT01;
private static string? _updatedFieldFromT03;
```

Never read shared state without a preceding `Skip.If` guard.

---

## Running the tests

Run the full test suite from the repository root:

```bash
dotnet test src/API/Tests/ReservationSystem.Tests.csproj
```

Run tests for a single microservice:

```bash
dotnet test src/API/Tests/ReservationSystem.Tests.csproj \
  --filter "FullyQualifiedName~IntegrationTests.<Domain>"
```

Set environment variables before running to target a specific environment:

```bash
export <DOMAIN>_API_BASE_URL=https://your-api.azurewebsites.net
export <DOMAIN>_API_HOST_KEY=your-host-key
dotnet test src/API/Tests/ReservationSystem.Tests.csproj \
  --filter "FullyQualifiedName~IntegrationTests.<Domain>"
```

---

## Checklist for a new integration test suite

Before committing a new test file, confirm all of the following:

- [ ] Namespace is `ReservationSystem.Tests.IntegrationTests.<Domain>`.
- [ ] Class name is `<Domain>ApiIntegrationTests`.
- [ ] Class implements `IAsyncLifetime` with trivial `InitializeAsync` and `DisposeAsync` that disposes `_client`.
- [ ] `[TestCaseOrderer]` attribute references the correct namespace for `PriorityOrderer`.
- [ ] Environment variables follow the `<DOMAIN>_API_BASE_URL` / `<DOMAIN>_API_HOST_KEY` naming convention.
- [ ] `JsonOptions` is declared as a single static field with `CamelCase` policy and `WhenWritingNull`.
- [ ] HTTP client timeout is 30 seconds.
- [ ] All ordered lifecycle tests use `[SkippableFact]` (except T01 which uses `[Fact]`).
- [ ] Each `[SkippableFact]` guards on shared state before executing.
- [ ] All standalone tests use `[Fact]` and clean up after themselves.
- [ ] Test method names follow the `T<NN>_<Action><Entity>_<ExpectedOutcome>` convention.
- [ ] Response DTOs are sealed classes with `init`-only properties, in `#region Response DTOs`.
- [ ] `TestPriorityAttribute` and `PriorityOrderer` are copied verbatim into `#region Test Ordering Infrastructure` with the namespace updated.
- [ ] No `Thread.Sleep`, `Task.Delay`, or retry loops — tests assume the API responds within 30 seconds.
- [ ] No hardcoded GUIDs or identifiers in lifecycle tests — use `Bogus` or `Guid.NewGuid()`.
