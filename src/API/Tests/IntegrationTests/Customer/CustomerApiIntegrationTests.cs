using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bogus;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ReservationSystem.Tests.IntegrationTests.Customer;

/// <summary>
/// Integration tests for the Customer Microservice API.
/// Tests run sequentially against the live API, exercising the full customer lifecycle:
/// create, read, update, points operations (add, authorise, settle, reverse, reinstate),
/// transactions, and delete.
/// </summary>
[TestCaseOrderer("ReservationSystem.Tests.IntegrationTests.Customer.PriorityOrderer", "ReservationSystem.Tests")]
public class CustomerApiIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("CUSTOMER_API_BASE_URL"))
            ? "https://reservation-system-db-microservice-customer-axdydza6brbkc0ck.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("CUSTOMER_API_BASE_URL")!;

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
    private static DateTimeOffset? _createdAt;
    private static string? _updatedGivenName;
    private static string? _updatedSurname;
    private static DateOnly? _updatedDateOfBirth;
    private static string? _updatedNationality;
    private static string? _updatedPhoneNumber;
    private static string? _updatedPreferredLanguage;
    private static string? _redemptionReference;

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
        _preferredLanguage = "en-GB";

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
        body.PreferredLanguage.Trim().Should().Be(_preferredLanguage);
        body.PointsBalance.Should().Be(0);
        body.IsActive.Should().BeTrue();

        _createdAt = body.CreatedAt;
    }

    [SkippableFact, TestPriority(3)]
    public async Task T03_UpdateCustomer_WithFullBody_ReturnsUpdatedProfile()
    {
        SkipIfNoLoyaltyNumber();

        // Arrange - mirror the manual test: send the full customer object with updated fields,
        // including tier change (Gold) and deactivation, as done in real-life testing.
        _updatedGivenName = _faker.Name.FirstName();
        _updatedSurname = _faker.Name.LastName();
        _updatedDateOfBirth = DateOnly.FromDateTime(_faker.Date.Past(50, DateTime.Today.AddYears(-18)));
        _updatedNationality = _faker.Address.CountryCode().Trim();  // TODO: check - This must only be a two letter code
        _updatedPhoneNumber = _faker.Phone.PhoneNumber("0#########");
        _updatedPreferredLanguage = "de-DE";

        var request = new
        {
            customerId = _customerId!.Value,
            loyaltyNumber = _loyaltyNumber,
            givenName = _updatedGivenName,
            surname = _updatedSurname,
            dateOfBirth = _updatedDateOfBirth.Value.ToString("yyyy-MM-dd"),
            nationality = _updatedNationality,
            preferredLanguage = _updatedPreferredLanguage,
            phoneNumber = _updatedPhoneNumber,
            tierCode = "Gold",
            pointsBalance = 0,
            tierProgressPoints = 0,
            isActive = false,
            createdAt = _createdAt
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
        body.DateOfBirth.Should().Be(_updatedDateOfBirth);
        body.Nationality.Should().Be(_updatedNationality);
        body.PreferredLanguage.Trim().Should().Be(_updatedPreferredLanguage);
        body.PhoneNumber.Should().Be(_updatedPhoneNumber);
        body.TierCode.Should().Be("Gold");
        body.IsActive.Should().BeFalse();
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
        body.DateOfBirth.Should().Be(_updatedDateOfBirth);
        body.Nationality.Should().Be(_updatedNationality);
        body.PreferredLanguage.Trim().Should().Be(_updatedPreferredLanguage);
        body.PhoneNumber.Should().Be(_updatedPhoneNumber);
        body.TierCode.Should().Be("Gold");
        body.IsActive.Should().BeFalse();
    }

    [SkippableFact, TestPriority(5)]
    public async Task T05_GetTransactions_InitiallyEmpty()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.GetAsync($"/api/v1/customers/{_loyaltyNumber}/transactions");

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
    public async Task T06_AddPoints_InitialBalance()
    {
        SkipIfNoLoyaltyNumber();

        // Arrange
        var request = new
        {
            points = 5000,
            transactionType = "Adjustment",
            description = "Added initial points balance for testing"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/customers/{_loyaltyNumber}/points/add", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AddPointsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.LoyaltyNumber.Should().Be(_loyaltyNumber);
        body.PointsAdded.Should().Be(5000);
        body.NewPointsBalance.Should().Be(5000);
        body.TransactionId.Should().NotBeEmpty();
    }

    [SkippableFact, TestPriority(7)]
    public async Task T07_AddPoints_SecondCredit()
    {
        SkipIfNoLoyaltyNumber();

        // Arrange
        var request = new
        {
            points = 5000,
            transactionType = "Adjustment",
            description = "Add some more"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/customers/{_loyaltyNumber}/points/add", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AddPointsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.LoyaltyNumber.Should().Be(_loyaltyNumber);
        body.PointsAdded.Should().Be(5000);
        body.NewPointsBalance.Should().Be(10000);
        body.TransactionId.Should().NotBeEmpty();
    }

    [SkippableFact, TestPriority(8)]
    public async Task T08_GetTransactions_ReturnsTwoAdjustments()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.GetAsync($"/api/v1/customers/{_loyaltyNumber}/transactions");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TransactionsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.LoyaltyNumber.Should().Be(_loyaltyNumber);
        body.TotalCount.Should().Be(2);
        body.Transactions.Should().HaveCount(2);

        foreach (var txn in body.Transactions)
        {
            txn.TransactionId.Should().NotBeEmpty();
            txn.TransactionType.Should().Be("Adjustment");
            txn.PointsDelta.Should().Be(5000);
            txn.Description.Should().NotBeNullOrEmpty();
        }
    }

    [SkippableFact, TestPriority(9)]
    public async Task T09_AuthorisePoints_CreatesRedemptionHold()
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
        body.PointsHeld.Should().Be(500);

        _redemptionReference = body.RedemptionReference;
    }

    [SkippableFact, TestPriority(10)]
    public async Task T10_SettlePoints_DeductsHeldPoints()
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
        // The API generates a new redemption reference in the settle response
        body!.RedemptionReference.Should().NotBeNullOrEmpty();
        body.PointsDeducted.Should().Be(500);
        body.NewPointsBalance.Should().Be(9500);
        body.TransactionId.Should().NotBeEmpty();
    }

    [SkippableFact, TestPriority(11)]
    public async Task T11_ReversePoints_ReleasesHeldPoints()
    {
        SkipIfNoLoyaltyNumber();
        Skip.If(string.IsNullOrEmpty(_redemptionReference), "No redemption reference from authorise step");

        // Arrange - reverse uses the original authorisation reference
        var request = new
        {
            redemptionReference = _redemptionReference,
            reason = "Customer request"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/customers/{_loyaltyNumber}/points/reverse", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ReversePointsResponse>(JsonOptions);

        body.Should().NotBeNull();
        // The API generates a new redemption reference in the reverse response
        body!.RedemptionReference.Should().NotBeNullOrEmpty();
        body.PointsReleased.Should().Be(500);
        body.NewPointsBalance.Should().Be(10000);
    }

    [SkippableFact, TestPriority(12)]
    public async Task T12_ReinstatePoints_CreditsCancelledBookingPoints()
    {
        SkipIfNoLoyaltyNumber();

        // Arrange
        var request = new
        {
            points = 500,
            bookingReference = "AB1234",
            reason = "Flight cancellation refund"
        };

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/customers/{_loyaltyNumber}/points/reinstate", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ReinstatePointsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.LoyaltyNumber.Should().Be(_loyaltyNumber);
        body.PointsReinstated.Should().Be(500);
        body.NewPointsBalance.Should().Be(10500);
        body.TransactionId.Should().NotBeEmpty();
    }

    [SkippableFact, TestPriority(13)]
    public async Task T13_GetCustomer_FinalBalanceCheck()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.GetAsync($"/api/v1/customers/{_loyaltyNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);

        body.Should().NotBeNull();
        // 5000 + 5000 (adds) - 500 (settle) + 500 (reverse) + 500 (reinstate) = 10500
        body!.PointsBalance.Should().Be(10500);
    }

    [SkippableFact, TestPriority(14)]
    public async Task T14_DeleteCustomer_ReturnsNoContent()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/customers/{_loyaltyNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [SkippableFact, TestPriority(15)]
    public async Task T15_GetDeletedCustomer_ReturnsNotFound()
    {
        SkipIfNoLoyaltyNumber();

        // Act
        var response = await _client.GetAsync($"/api/v1/customers/{_loyaltyNumber}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, TestPriority(16)]
    public async Task T16_GetNonExistentCustomer_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/customers/AX0000000");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact, TestPriority(17)]
    public async Task T17_CreateCustomer_MinimalFields()
    {
        // Arrange - only required fields (no dateOfBirth)
        var request = new
        {
            givenName = _faker.Name.FirstName(),
            surname = _faker.Name.LastName(),
            preferredLanguage = "en-GB"
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

    [Fact, TestPriority(18)]
    public async Task T18_CreateCustomer_WithIdentityId()
    {
        // Arrange
        var identityId = Guid.NewGuid();
        var request = new
        {
            givenName = _faker.Name.FirstName(),
            surname = _faker.Name.LastName(),
            dateOfBirth = DateOnly.FromDateTime(_faker.Date.Past(30, DateTime.Today.AddYears(-18))).ToString("yyyy-MM-dd"),
            preferredLanguage = "fr",
            identityId = identityId
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/customers", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var createBody = await response.Content.ReadFromJsonAsync<CreateCustomerResponse>(JsonOptions);
        createBody.Should().NotBeNull();

        // Verify identity id was stored
        var getResponse = await _client.GetAsync($"/api/v1/customers/{createBody!.LoyaltyNumber}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var getBody = await getResponse.Content.ReadFromJsonAsync<CustomerResponse>(JsonOptions);
        getBody!.IdentityId.Should().Be(identityId);

        // Cleanup
        await _client.DeleteAsync($"/api/v1/customers/{createBody.LoyaltyNumber}");
    }

    [Fact, TestPriority(19)]
    public async Task T19_UpdateCustomer_PartialUpdate_PreservesOtherFields()
    {
        // Arrange - create a fresh customer
        var originalName = _faker.Name.FirstName();
        var originalSurname = _faker.Name.LastName();
        var createRequest = new
        {
            givenName = originalName,
            surname = originalSurname,
            preferredLanguage = "de-DE"
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
        body.PreferredLanguage.Trim().Should().Be("de-DE", "partial update should preserve preferred language");
        body.PhoneNumber.Should().Be("+353851234567");

        // Cleanup
        await _client.DeleteAsync($"/api/v1/customers/{created.LoyaltyNumber}");
    }

    [Fact, TestPriority(20)]
    public async Task T20_GetTransactions_PaginationWorks()
    {
        // Arrange - create a customer and add enough transactions to paginate
        var createRequest = new
        {
            givenName = _faker.Name.FirstName(),
            surname = _faker.Name.LastName(),
            preferredLanguage = "en-GB"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/v1/customers", createRequest, JsonOptions);
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateCustomerResponse>(JsonOptions);
        created.Should().NotBeNull();

        // Add multiple transactions
        for (var i = 0; i < 3; i++)
        {
            await _client.PostAsJsonAsync(
                $"/api/v1/customers/{created!.LoyaltyNumber}/points/add",
                new { points = 100, transactionType = "Adjustment", description = $"Transaction {i + 1}" },
                JsonOptions);
        }

        // Act - request page with small page size
        var response = await _client.GetAsync($"/api/v1/customers/{created!.LoyaltyNumber}/transactions?page=1&pageSize=2");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<TransactionsResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.PageSize.Should().Be(2);
        body.TotalCount.Should().Be(3);
        body.Transactions.Count.Should().Be(2);

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
    public Guid? IdentityId { get; init; }
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

public sealed class AddPointsResponse
{
    public string LoyaltyNumber { get; init; } = string.Empty;
    public int PointsAdded { get; init; }
    public int NewPointsBalance { get; init; }
    public Guid TransactionId { get; init; }
    public DateTimeOffset AddedAt { get; init; }
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
