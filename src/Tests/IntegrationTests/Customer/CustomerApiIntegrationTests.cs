using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bogus;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ReservationSystem.IntegrationTests.Customer;

/// <summary>
/// Integration tests for the Customer Microservice API.
/// Tests run sequentially against the live API, exercising the full customer lifecycle:
/// create, read, update, points operations, transactions, and delete.
/// </summary>
[TestCaseOrderer("ReservationSystem.IntegrationTests.Customer.PriorityOrderer", "ReservationSystem.IntegrationTests.Customer")]
public class CustomerApiIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl = Environment.GetEnvironmentVariable("CUSTOMER_API_BASE_URL")
        ?? "https://reservation-system-db-microservice-customer-axdydza6brbkc0ck.uksouth-01.azurewebsites.net";

    private static readonly string? HostKey = Environment.GetEnvironmentVariable("CUSTOMER_API_HOST_KEY");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _client;
    private readonly Faker _faker;

    // Shared state across ordered tests
    private static string? _loyaltyNumber;
    private static Guid? _customerId;
    private static string? _givenName;
    private static string? _surname;
    private static DateOnly? _dateOfBirth;
    private static string? _preferredLanguage;
    private static string? _updatedGivenName;
    private static string? _updatedSurname;
    private static string? _updatedPhoneNumber;
    private static string? _updatedNationality;
    private static string? _redemptionReference;
    private static string? _secondRedemptionReference;

    public CustomerApiIntegrationTests()
    {
        _client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _client.Timeout = TimeSpan.FromSeconds(30);

        if (!string.IsNullOrEmpty(HostKey))
        {
            _client.DefaultRequestHeaders.Add("x-functions-key", HostKey);
        }

        _faker = new Faker();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact, TestPriority(1)]
    public async Task T01_CreateCustomer_ReturnsCreatedWithLoyaltyNumber()
    {
        // Arrange
        _givenName = _faker.Name.FirstName();
        _surname = _faker.Name.LastName();
        _dateOfBirth = DateOnly.FromDateTime(_faker.Date.Past(50, DateTime.Today.AddYears(-18)));
        _preferredLanguage = _faker.PickRandom("en", "fr", "de", "es", "it");

        var request = new
        {
            givenName = _givenName,
            surname = _surname,
            dateOfBirth = _dateOfBirth.Value.ToString("yyyy-MM-dd"),
            preferredLanguage = _preferredLanguage
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/customers", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateCustomerResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.CustomerId.Should().NotBeEmpty();
        body.LoyaltyNumber.Should().StartWith("AX").And.HaveLength(9);
        body.TierCode.Should().NotBeNullOrEmpty();

        _customerId = body.CustomerId;
        _loyaltyNumber = body.LoyaltyNumber;
    }

    [SkippableFact, TestPriority(2)]
    public async Task T02_GetCustomer_ReturnsCreatedCustomerDetails()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.GetAsync($"/api/v1/customers/{_loyaltyNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.CustomerId.Should().Be(_customerId!.Value);
        body.LoyaltyNumber.Should().Be(_loyaltyNumber);
        body.GivenName.Should().Be(_givenName);
        body.Surname.Should().Be(_surname);
        body.PreferredLanguage.Should().Be(_preferredLanguage);
        body.PointsBalance.Should().Be(0);
        body.IsActive.Should().BeTrue();
    }

    [SkippableFact, TestPriority(3)]
    public async Task T03_UpdateCustomer_ReturnsUpdatedProfile()
    {
        SkipIfNoLoyaltyNumber();

        // Arrange
        _updatedGivenName = _faker.Name.FirstName();
        _updatedSurname = _faker.Name.LastName();
        _updatedPhoneNumber = _faker.Phone.PhoneNumber("+44##########");
        _updatedNationality = _faker.Address.CountryCode();

        var request = new
        {
            givenName = _updatedGivenName,
            surname = _updatedSurname,
            phoneNumber = _updatedPhoneNumber,
            nationality = _updatedNationality
        };

        // Act
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/customers/{_loyaltyNumber}")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        var response = await _client.SendAsync(patchRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.GivenName.Should().Be(_updatedGivenName);
        body.Surname.Should().Be(_updatedSurname);
        body.PhoneNumber.Should().Be(_updatedPhoneNumber);
        body.Nationality.Should().Be(_updatedNationality);
        body.PreferredLanguage.Should().Be(_preferredLanguage);
    }

    [SkippableFact, TestPriority(4)]
    public async Task T04_GetCustomer_ReflectsUpdatedFields()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.GetAsync($"/api/v1/customers/{_loyaltyNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.GivenName.Should().Be(_updatedGivenName);
        body.Surname.Should().Be(_updatedSurname);
        body.PhoneNumber.Should().Be(_updatedPhoneNumber);
        body.Nationality.Should().Be(_updatedNationality);
    }

    [SkippableFact, TestPriority(5)]
    public async Task T05_GetTransactions_InitiallyEmpty()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.GetAsync($"/api/v1/customers/{_loyaltyNumber}/transactions?page=1&pageSize=20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TransactionsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.LoyaltyNumber.Should().Be(_loyaltyNumber);
        body.Page.Should().Be(1);
        body.TotalCount.Should().Be(0);
        body.Transactions.Should().BeEmpty();
    }

    [SkippableFact, TestPriority(6)]
    public async Task T06_ReinstatePoints_AddsPointsToBalance()
    {
        SkipIfNoLoyaltyNumber();

        // Arrange
        var request = new
        {
            points = 5000,
            bookingReference = _faker.Random.AlphaNumeric(6).ToUpper(),
            reason = "Integration test - initial points credit"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/customers/{_loyaltyNumber}/points/reinstate", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ReinstatePointsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.LoyaltyNumber.Should().Be(_loyaltyNumber);
        body.PointsReinstated.Should().Be(5000);
        body.NewPointsBalance.Should().Be(5000);
        body.TransactionId.Should().NotBeEmpty();
    }

    [SkippableFact, TestPriority(7)]
    public async Task T07_GetCustomer_VerifyPointsBalanceAfterReinstatement()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.GetAsync($"/api/v1/customers/{_loyaltyNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PointsBalance.Should().Be(5000);
    }

    [SkippableFact, TestPriority(8)]
    public async Task T08_AuthorisePoints_CreatesRedemptionHold()
    {
        SkipIfNoLoyaltyNumber();

        // Arrange
        var request = new
        {
            points = 1000,
            basketId = Guid.NewGuid()
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/customers/{_loyaltyNumber}/points/authorise", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthorisePointsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.RedemptionReference.Should().NotBeNullOrEmpty();
        body.PointsAuthorised.Should().Be(1000);
        body.PointsHeld.Should().BeGreaterOrEqualTo(1000);

        _redemptionReference = body.RedemptionReference;
    }

    [SkippableFact, TestPriority(9)]
    public async Task T09_SettlePoints_DeductsHeldPoints()
    {
        SkipIfNoLoyaltyNumber();
        Skip.If(string.IsNullOrEmpty(_redemptionReference), "No redemption reference from authorise step");

        // Arrange
        var request = new
        {
            redemptionReference = _redemptionReference
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/customers/{_loyaltyNumber}/points/settle", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SettlePointsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.RedemptionReference.Should().Be(_redemptionReference);
        body.PointsDeducted.Should().Be(1000);
        body.NewPointsBalance.Should().Be(4000);
        body.TransactionId.Should().NotBeEmpty();
    }

    [SkippableFact, TestPriority(10)]
    public async Task T10_AuthorisePoints_SecondHoldForReversal()
    {
        SkipIfNoLoyaltyNumber();

        // Arrange
        var request = new
        {
            points = 500,
            basketId = Guid.NewGuid()
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/customers/{_loyaltyNumber}/points/authorise", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthorisePointsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.RedemptionReference.Should().NotBeNullOrEmpty();
        body.PointsAuthorised.Should().Be(500);

        _secondRedemptionReference = body.RedemptionReference;
    }

    [SkippableFact, TestPriority(11)]
    public async Task T11_ReversePoints_ReleasesHeldPoints()
    {
        SkipIfNoLoyaltyNumber();
        Skip.If(string.IsNullOrEmpty(_secondRedemptionReference), "No second redemption reference from authorise step");

        // Arrange
        var request = new
        {
            redemptionReference = _secondRedemptionReference,
            reason = "Integration test - reversing authorisation"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/customers/{_loyaltyNumber}/points/reverse", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ReversePointsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.RedemptionReference.Should().Be(_secondRedemptionReference);
        body.PointsReleased.Should().Be(500);
        body.NewPointsBalance.Should().Be(4000);
    }

    [SkippableFact, TestPriority(12)]
    public async Task T12_ReinstatePoints_SecondCreditForTransactionHistory()
    {
        SkipIfNoLoyaltyNumber();

        // Arrange
        var request = new
        {
            points = 2500,
            bookingReference = _faker.Random.AlphaNumeric(6).ToUpper(),
            reason = "Integration test - bonus points credit"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/customers/{_loyaltyNumber}/points/reinstate", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ReinstatePointsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PointsReinstated.Should().Be(2500);
        body.NewPointsBalance.Should().Be(6500);
    }

    [SkippableFact, TestPriority(13)]
    public async Task T13_GetTransactions_ReturnsTransactionHistory()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.GetAsync($"/api/v1/customers/{_loyaltyNumber}/transactions?page=1&pageSize=20");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TransactionsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.LoyaltyNumber.Should().Be(_loyaltyNumber);
        body.TotalCount.Should().BeGreaterThan(0);
        body.Transactions.Should().NotBeEmpty();

        foreach (var txn in body.Transactions)
        {
            txn.TransactionId.Should().NotBeEmpty();
            txn.TransactionType.Should().NotBeNullOrEmpty();
            txn.Description.Should().NotBeNullOrEmpty();
        }
    }

    [SkippableFact, TestPriority(14)]
    public async Task T14_GetTransactions_PaginationWorks()
    {
        SkipIfNoLoyaltyNumber();

        // Act - request page with small page size
        var response = await _client.GetAsync($"/api/v1/customers/{_loyaltyNumber}/transactions?page=1&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TransactionsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PageSize.Should().Be(2);
        body.Transactions.Count.Should().BeLessOrEqualTo(2);
    }

    [SkippableFact, TestPriority(15)]
    public async Task T15_GetCustomer_FinalBalanceCheck()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.GetAsync($"/api/v1/customers/{_loyaltyNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PointsBalance.Should().Be(6500);
        body.IsActive.Should().BeTrue();
    }

    [SkippableFact, TestPriority(16)]
    public async Task T16_DeleteCustomer_ReturnsNoContent()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/customers/{_loyaltyNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [SkippableFact, TestPriority(17)]
    public async Task T17_GetDeletedCustomer_ReturnsNotFound()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.GetAsync($"/api/v1/customers/{_loyaltyNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, TestPriority(18)]
    public async Task T18_GetNonExistentCustomer_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/customers/AX0000000");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, TestPriority(19)]
    public async Task T19_CreateCustomer_MinimalFields()
    {
        // Arrange - only required fields
        var request = new
        {
            givenName = _faker.Name.FirstName(),
            surname = _faker.Name.LastName(),
            preferredLanguage = "en"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/customers", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateCustomerResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.LoyaltyNumber.Should().StartWith("AX");

        // Cleanup - delete the customer we just created
        await _client.DeleteAsync($"/api/v1/customers/{body.LoyaltyNumber}");
    }

    [Fact, TestPriority(20)]
    public async Task T20_CreateCustomer_WithIdentityReference()
    {
        // Arrange
        var identityRef = Guid.NewGuid();
        var request = new
        {
            givenName = _faker.Name.FirstName(),
            surname = _faker.Name.LastName(),
            dateOfBirth = DateOnly.FromDateTime(_faker.Date.Past(30, DateTime.Today.AddYears(-18))).ToString("yyyy-MM-dd"),
            preferredLanguage = "fr",
            identityReference = identityRef
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/customers", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await response.Content.ReadFromJsonAsync<CreateCustomerResponse>(JsonOptions);
        createBody.Should().NotBeNull();

        // Verify identity reference was stored
        var getResponse = await _client.GetAsync($"/api/v1/customers/{createBody!.LoyaltyNumber}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResponse.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        getBody!.IdentityReference.Should().Be(identityRef);

        // Cleanup
        await _client.DeleteAsync($"/api/v1/customers/{createBody.LoyaltyNumber}");
    }

    [Fact, TestPriority(21)]
    public async Task T21_UpdateCustomer_PartialUpdate_PreservesOtherFields()
    {
        // Arrange - create a fresh customer
        var originalName = _faker.Name.FirstName();
        var originalSurname = _faker.Name.LastName();
        var createRequest = new
        {
            givenName = originalName,
            surname = originalSurname,
            preferredLanguage = "de"
        };

        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", createRequest, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateCustomerResponse>(JsonOptions);

        // Act - only update phone number
        var updateRequest = new { phoneNumber = "+353851234567" };
        var patchRequest = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/customers/{created!.LoyaltyNumber}")
        {
            Content = JsonContent.Create(updateRequest, options: JsonOptions)
        };
        var updateResponse = await _client.SendAsync(patchRequest);

        // Assert
        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await updateResponse.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);

        body!.GivenName.Should().Be(originalName, "partial update should preserve given name");
        body.Surname.Should().Be(originalSurname, "partial update should preserve surname");
        body.PreferredLanguage.Should().Be("de", "partial update should preserve preferred language");
        body.PhoneNumber.Should().Be("+353851234567");

        // Cleanup
        await _client.DeleteAsync($"/api/v1/customers/{created.LoyaltyNumber}");
    }

    private static void SkipIfNoLoyaltyNumber()
    {
        Skip.If(string.IsNullOrEmpty(_loyaltyNumber), "Previous test did not produce a loyalty number");
    }
}

#region Response DTOs

public sealed class CreateCustomerResponse
{
    public Guid CustomerId { get; init; }
    public string LoyaltyNumber { get; init; } = string.Empty;
    public string TierCode { get; init; } = string.Empty;
}

public sealed class CustomerResponse
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

public sealed class TransactionsResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<TransactionResponse> Transactions { get; init; } = [];
}

public sealed class TransactionResponse
{
    public Guid TransactionId { get; init; }
    public string TransactionType { get; init; } = string.Empty;
    public int PointsDelta { get; init; }
    public int BalanceAfter { get; init; }
    public string? BookingReference { get; init; }
    public string? FlightNumber { get; init; }
    public string Description { get; init; } = string.Empty;
    public DateTimeOffset TransactionDate { get; init; }
}

public sealed class AuthorisePointsResponse
{
    public string RedemptionReference { get; init; } = string.Empty;
    public int PointsAuthorised { get; init; }
    public int PointsHeld { get; init; }
    public DateTimeOffset AuthorisedAt { get; init; }
}

public sealed class SettlePointsResponse
{
    public string RedemptionReference { get; init; } = string.Empty;
    public int PointsDeducted { get; init; }
    public int NewPointsBalance { get; init; }
    public Guid TransactionId { get; init; }
    public DateTimeOffset SettledAt { get; init; }
}

public sealed class ReversePointsResponse
{
    public string RedemptionReference { get; init; } = string.Empty;
    public int PointsReleased { get; init; }
    public int NewPointsBalance { get; init; }
    public DateTimeOffset ReversedAt { get; init; }
}

public sealed class ReinstatePointsResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public int PointsReinstated { get; init; }
    public int NewPointsBalance { get; init; }
    public Guid TransactionId { get; init; }
    public DateTimeOffset ReinstatedAt { get; init; }
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
