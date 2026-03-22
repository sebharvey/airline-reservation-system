using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ReservationSystem.Tests.IntegrationTests.Identity;

/// <summary>
/// Integration tests for the Identity Microservice API.
/// Tests run sequentially against the live API, exercising the full auth lifecycle:
/// create account, verify email, login, refresh token, and logout.
/// </summary>
[TestCaseOrderer("ReservationSystem.Tests.IntegrationTests.Identity.IdentityPriorityOrderer", "ReservationSystem.Tests")]
public class IdentityApiIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("IDENTITY_API_BASE_URL"))
            ? "https://reservation-system-db-microservice-identity-dwdegsahhngkbvgv.uksouth-01.azurewebsites.net"
            : Environment.GetEnvironmentVariable("IDENTITY_API_BASE_URL")!;

    private static readonly string? HostKey = Environment.GetEnvironmentVariable("IDENTITY_API_HOST_KEY");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _client;

    // Shared state across ordered tests
    private static Guid? _userAccountId;
    private static string? _accessToken;
    private static string? _refreshToken;

    // Use a unique email per test run to avoid conflicts with existing accounts
    private static readonly string TestEmail = $"test.user.{Guid.NewGuid():N}@example.com";
    private const string TestPassword = "Password1!";

    public IdentityApiIntegrationTests()
    {
        _client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _client.Timeout = TimeSpan.FromSeconds(30);

        if (!string.IsNullOrEmpty(HostKey))
        {
            _client.DefaultRequestHeaders.Add("x-functions-key", HostKey);
        }
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact, IdentityTestPriority(1)]
    public async Task T01_Login_WithNonExistentEmail_ReturnsUnauthorized()
    {
        // Arrange
        var request = new
        {
            email = "bad-email@example.com",
            password = TestPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Error.Should().Be("Invalid credentials.");
    }

    [Fact, IdentityTestPriority(2)]
    public async Task T02_Login_WithBadPassword_ReturnsUnauthorized()
    {
        // Arrange
        var request = new
        {
            email = "bad-email@example.com",
            password = "BadPassword!"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Error.Should().Be("Invalid credentials.");
    }

    [Fact, IdentityTestPriority(3)]
    public async Task T03_CreateAccount_ReturnsCreatedWithUserAccountId()
    {
        // Arrange
        var request = new
        {
            email = TestEmail,
            password = TestPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/accounts", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<CreateAccountResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.UserAccountId.Should().NotBeEmpty();

        _userAccountId = body.UserAccountId;
    }

    [SkippableFact, IdentityTestPriority(4)]
    public async Task T04_Login_BeforeEmailVerified_ReturnsForbidden()
    {
        SkipIfNoUserAccountId();

        // Arrange
        var request = new
        {
            email = TestEmail,
            password = TestPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Error.Should().Be("Account is locked.");
    }

    [SkippableFact, IdentityTestPriority(5)]
    public async Task T05_VerifyEmail_ReturnsOk()
    {
        SkipIfNoUserAccountId();

        // Act
        var response = await _client.PostAsJsonAsync(
            $"/api/v1/accounts/{_userAccountId}/verify-email", new { }, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact, IdentityTestPriority(6)]
    public async Task T06_Login_AfterEmailVerified_ReturnsTokens()
    {
        SkipIfNoUserAccountId();

        // Arrange
        var request = new
        {
            email = TestEmail,
            password = TestPassword
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
        body.UserAccountId.Should().Be(_userAccountId!.Value);

        _accessToken = body.AccessToken;
        _refreshToken = body.RefreshToken;
    }

    [SkippableFact, IdentityTestPriority(7)]
    public async Task T07_RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        SkipIfNoUserAccountId();
        Skip.If(string.IsNullOrEmpty(_refreshToken), "No refresh token from login step");

        // Arrange
        var request = new { refreshToken = _refreshToken };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<RefreshTokenResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();

        // Capture the rotated refresh token for the logout step
        _refreshToken = body.RefreshToken;
    }

    [SkippableFact, IdentityTestPriority(8)]
    public async Task T08_Logout_WithValidRefreshToken_ReturnsOk()
    {
        SkipIfNoUserAccountId();
        Skip.If(string.IsNullOrEmpty(_refreshToken), "No refresh token from refresh step");

        // Arrange
        var request = new { refreshToken = _refreshToken };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/logout", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [SkippableFact, IdentityTestPriority(9)]
    public async Task T09_RefreshToken_AfterLogout_ReturnsUnauthorized()
    {
        SkipIfNoUserAccountId();
        Skip.If(string.IsNullOrEmpty(_refreshToken), "No refresh token to test against");

        // Arrange - use the same refresh token that was just invalidated by logout
        var request = new { refreshToken = _refreshToken };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>(JsonOptions);

        body.Should().NotBeNull();
        body!.Error.Should().Be("Invalid or expired refresh token.");
    }

    private static void SkipIfNoUserAccountId()
    {
        Skip.If(!_userAccountId.HasValue, "Previous test did not produce a user account ID");
    }
}

#region Response DTOs

public sealed class CreateAccountResponse
{
    public Guid UserAccountId { get; init; }
}

public sealed class LoginResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
    public Guid UserAccountId { get; init; }
}

public sealed class RefreshTokenResponse
{
    public string AccessToken { get; init; } = string.Empty;
    public string RefreshToken { get; init; } = string.Empty;
}

public sealed class ErrorResponse
{
    public string Error { get; init; } = string.Empty;
}

#endregion

#region Test Ordering Infrastructure

[AttributeUsage(AttributeTargets.Method)]
public sealed class IdentityTestPriorityAttribute : Attribute
{
    public int Priority { get; }
    public IdentityTestPriorityAttribute(int priority) => Priority = priority;
}

public sealed class IdentityPriorityOrderer : ITestCaseOrderer
{
    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> testCases)
        where TTestCase : ITestCase
    {
        var sortedCases = new SortedDictionary<int, List<TTestCase>>();

        foreach (var testCase in testCases)
        {
            var priority = testCase.TestMethod.Method
                .GetCustomAttributes(typeof(IdentityTestPriorityAttribute).AssemblyQualifiedName)
                .FirstOrDefault()
                ?.GetNamedArgument<int>(nameof(IdentityTestPriorityAttribute.Priority)) ?? 0;

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
