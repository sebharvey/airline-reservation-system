using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bogus;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ReservationSystem.Tests.IntegrationTests.TerminalCustomer;

/// <summary>
/// Integration tests for the Terminal Customer flow.
/// Exercises a cross-service scenario: register a loyalty customer via the Loyalty API,
/// create a staff user via the User MS, authenticate via the Admin API to obtain a staff JWT,
/// search for the customer via the Loyalty API admin search endpoint, validate the result,
/// and finally delete the customer via the Customer MS.
/// </summary>
[TestCaseOrderer("ReservationSystem.Tests.IntegrationTests.TerminalCustomer.PriorityOrderer", "ReservationSystem.Tests")]
public class TerminalCustomerIntegrationTests : IAsyncLifetime
{
    // Service base URLs
    private static readonly string LoyaltyApiBaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("LOYALTY_API_BASE_URL"))
            ? "https://reservation-system-db-api-loyalty-gufra2fxfdd2eka6.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("LOYALTY_API_BASE_URL")!;

    private static readonly string AdminApiBaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ADMIN_API_BASE_URL"))
            ? "https://reservation-system-db-api-admin-ageucwaad3axbxhm.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("ADMIN_API_BASE_URL")!;

    private static readonly string UserMsBaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("USER_MS_BASE_URL"))
            ? "https://reservation-system-db-microservice-user-frhedyd4dcc6aya8.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("USER_MS_BASE_URL")!;

    private static readonly string CustomerMsBaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CUSTOMER_API_BASE_URL"))
            ? "https://reservation-system-db-microservice-customer-axdydza6brbkc0ck.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("CUSTOMER_API_BASE_URL")!;

    // Host keys for microservice-to-microservice calls
    private static readonly string? UserMsHostKey = Environment.GetEnvironmentVariable("USER_MS_HOST_KEY");
    private static readonly string? CustomerMsHostKey = Environment.GetEnvironmentVariable("CUSTOMER_API_HOST_KEY");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _loyaltyClient;
    private readonly HttpClient _adminClient;
    private readonly HttpClient _userMsClient;
    private readonly HttpClient _customerMsClient;
    private readonly Faker _faker;

    // Shared state across ordered tests
    private static string? _loyaltyNumber;
    private static string? _givenName;
    private static string? _surname;
    private static string? _staffUsername;
    private static string? _staffPassword;
    private static Guid? _staffUserId;
    private static string? _staffAccessToken;

    public TerminalCustomerIntegrationTests()
    {
        _loyaltyClient = new HttpClient { BaseAddress = new Uri(LoyaltyApiBaseUrl) };
        _loyaltyClient.Timeout = TimeSpan.FromSeconds(30);

        _adminClient = new HttpClient { BaseAddress = new Uri(AdminApiBaseUrl) };
        _adminClient.Timeout = TimeSpan.FromSeconds(30);

        _userMsClient = new HttpClient { BaseAddress = new Uri(UserMsBaseUrl) };
        _userMsClient.Timeout = TimeSpan.FromSeconds(30);

        if (!string.IsNullOrEmpty(UserMsHostKey))
        {
            _userMsClient.DefaultRequestHeaders.Add("x-functions-key", UserMsHostKey);
        }

        _customerMsClient = new HttpClient { BaseAddress = new Uri(CustomerMsBaseUrl) };
        _customerMsClient.Timeout = TimeSpan.FromSeconds(30);

        if (!string.IsNullOrEmpty(CustomerMsHostKey))
        {
            _customerMsClient.DefaultRequestHeaders.Add("x-functions-key", CustomerMsHostKey);
        }

        _faker = new Faker();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _loyaltyClient.Dispose();
        _adminClient.Dispose();
        _userMsClient.Dispose();
        _customerMsClient.Dispose();
        return Task.CompletedTask;
    }

    // -------------------------------------------------------------------------
    // T01: Register a loyalty customer via Loyalty API
    // -------------------------------------------------------------------------

    [Fact, TestPriority(1)]
    public async Task T01_RegisterLoyaltyCustomer_ReturnsCreatedWithLoyaltyNumber()
    {
        // Arrange
        _givenName = _faker.Name.FirstName();
        _surname = _faker.Name.LastName();
        var email = _faker.Internet.Email(_givenName, _surname).ToLowerInvariant();
        var password = _faker.Internet.Password(16, false, "\\w", "!A1a");

        var request = new
        {
            givenName = _givenName,
            surname = _surname,
            dateOfBirth = DateOnly.FromDateTime(_faker.Date.Past(50, DateTime.Today.AddYears(-18))).ToString("yyyy-MM-dd"),
            email,
            password,
            preferredLanguage = "en-GB"
        };

        // Act
        var response = await _loyaltyClient.PostAsJsonAsync("/api/v1/register", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.LoyaltyNumber.Should().StartWith("AX").And.HaveLength(9);
        body.GivenName.Should().Be(_givenName);
        body.TierCode.Should().Be("Blue");

        _loyaltyNumber = body.LoyaltyNumber;
    }

    // -------------------------------------------------------------------------
    // T02: Create a staff user via User MS
    // -------------------------------------------------------------------------

    [SkippableFact, TestPriority(2)]
    public async Task T02_CreateStaffUser_ReturnsCreatedWithUserId()
    {
        SkipIfNoLoyaltyNumber();

        // Arrange
        _staffUsername = $"teststaff_{_faker.Random.AlphaNumeric(8)}";
        _staffPassword = _faker.Internet.Password(16, false, "\\w", "!A1a");

        var request = new
        {
            username = _staffUsername,
            email = _faker.Internet.Email().ToLowerInvariant(),
            password = _staffPassword,
            firstName = _faker.Name.FirstName(),
            lastName = _faker.Name.LastName()
        };

        // Act
        var response = await _userMsClient.PostAsJsonAsync("/api/v1/users", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<AddUserResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.UserId.Should().NotBeEmpty();

        _staffUserId = body.UserId;
    }

    // -------------------------------------------------------------------------
    // T03: Authenticate via Admin API to get a staff JWT
    // -------------------------------------------------------------------------

    [SkippableFact, TestPriority(3)]
    public async Task T03_AdminLogin_ReturnsAccessToken()
    {
        Skip.If(string.IsNullOrEmpty(_staffUsername), "No staff user from previous step");

        // Arrange
        var request = new
        {
            username = _staffUsername,
            password = _staffPassword
        };

        // Act
        var response = await _adminClient.PostAsJsonAsync("/api/v1/auth/login", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AdminLoginResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.UserId.Should().NotBeEmpty();
        body.TokenType.Should().Be("Bearer");

        _staffAccessToken = body.AccessToken;
    }

    // -------------------------------------------------------------------------
    // T04: Search for the loyalty customer via Loyalty API admin search endpoint
    // -------------------------------------------------------------------------

    [SkippableFact, TestPriority(4)]
    public async Task T04_AdminSearchCustomers_FindsRegisteredCustomer()
    {
        SkipIfNoLoyaltyNumber();
        Skip.If(string.IsNullOrEmpty(_staffAccessToken), "No staff access token from previous step");

        // Arrange
        var request = new { query = _surname };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/customers/search")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _staffAccessToken);
        httpRequest.Headers.Add("X-Correlation-ID", Guid.NewGuid().ToString());

        // Act
        var response = await _loyaltyClient.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<List<CustomerSummaryResponse>>(JsonOptions);

        body.Should().NotBeNull();
        body.Should().NotBeEmpty();

        var match = body!.FirstOrDefault(c => c.LoyaltyNumber == _loyaltyNumber);
        match.Should().NotBeNull("the registered customer should appear in search results");
        match!.GivenName.Should().Be(_givenName);
        match.Surname.Should().Be(_surname);
        match.TierCode.Should().Be("Blue");
        match.IsActive.Should().BeTrue();
    }

    // -------------------------------------------------------------------------
    // T05: Delete the loyalty customer via Customer MS
    // -------------------------------------------------------------------------

    [SkippableFact, TestPriority(5)]
    public async Task T05_DeleteCustomer_ReturnsNoContent()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _customerMsClient.DeleteAsync($"/api/v1/customers/{_loyaltyNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    // -------------------------------------------------------------------------
    // T06: Verify customer no longer exists after deletion
    // -------------------------------------------------------------------------

    [SkippableFact, TestPriority(6)]
    public async Task T06_GetDeletedCustomer_ReturnsNotFound()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _customerMsClient.GetAsync($"/api/v1/customers/{_loyaltyNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private static void SkipIfNoLoyaltyNumber()
    {
        Skip.If(string.IsNullOrEmpty(_loyaltyNumber), "Previous test did not produce a loyalty number");
    }
}

#region Response DTOs

public sealed class RegisterResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public string GivenName { get; init; } = string.Empty;
    public string TierCode { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public sealed class AddUserResponse
{
    public Guid UserId { get; init; }
}

public sealed class AdminLoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public Guid UserId { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public string TokenType { get; init; } = string.Empty;
}

public sealed class CustomerSummaryResponse
{
    public Guid CustomerId { get; init; }
    public string LoyaltyNumber { get; init; } = string.Empty;
    public Guid? IdentityReference { get; init; }
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public DateOnly? DateOfBirth { get; init; }
    public string? Nationality { get; init; }
    public string PreferredLanguage { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string TierCode { get; init; } = string.Empty;
    public int PointsBalance { get; init; }
    public int TierProgressPoints { get; init; }
    public bool IsActive { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

#endregion

#region Test Ordering Infrastructure

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestPriorityAttribute : Attribute
{
    public int Priority { get; }
    public TestPriorityAttribute(int priority) => Priority = priority;
}

public sealed class PriorityOrderer : ITestCaseOrderer
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
        {
            foreach (var testCase in list)
            {
                yield return testCase;
            }
        }
    }
}

#endregion
