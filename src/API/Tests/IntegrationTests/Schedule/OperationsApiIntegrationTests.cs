using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace ReservationSystem.Tests.IntegrationTests.Schedule;

/// <summary>
/// Integration tests for the Operations API — Schedule creation endpoint.
/// Tests run sequentially against the live API, exercising the full schedule
/// creation flow: create schedule → generate inventory → verify counts.
/// </summary>
[TestCaseOrderer("ReservationSystem.Tests.IntegrationTests.Schedule.PriorityOrderer", "ReservationSystem.Tests")]
public class OperationsApiIntegrationTests : IAsyncLifetime
{
    private static readonly string BaseUrl =
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPERATIONS_API_BASE_URL"))
            ? "https://localhost:7070"
            : Environment.GetEnvironmentVariable("OPERATIONS_API_BASE_URL")!;

    private static readonly string? HostKey = Environment.GetEnvironmentVariable("OPERATIONS_API_HOST_KEY");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _client;

    // Shared state across ordered tests
    private static Guid? _scheduleId;
    private static int? _flightsCreated;

    public OperationsApiIntegrationTests()
    {
        _client = new HttpClient { BaseAddress = new Uri(BaseUrl) };
        _client.Timeout = TimeSpan.FromSeconds(120);

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

    // -------------------------------------------------------------------------
    // Validation tests
    // -------------------------------------------------------------------------

    [Fact, TestPriority(1)]
    public async Task T01_CreateSchedule_MissingFlightNumber_ReturnsBadRequest()
    {
        var request = new
        {
            origin = "LHR",
            destination = "JFK",
            departureTime = "09:30",
            arrivalTime = "13:45",
            daysOfWeek = 127,
            aircraftType = "A351",
            validFrom = "2026-06-01",
            validTo = "2026-06-07",
            cabins = new[]
            {
                new { cabinCode = "Y", totalSeats = 220, fares = new[] { MakeMinimalFare() } }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/schedules", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact, TestPriority(2)]
    public async Task T02_CreateSchedule_MissingCabins_ReturnsBadRequest()
    {
        var request = new
        {
            flightNumber = "AX999",
            origin = "LHR",
            destination = "JFK",
            departureTime = "09:30",
            arrivalTime = "13:45",
            daysOfWeek = 127,
            aircraftType = "A351",
            validFrom = "2026-06-01",
            validTo = "2026-06-07",
            cabins = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/api/v1/schedules", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact, TestPriority(3)]
    public async Task T03_CreateSchedule_InvalidDaysOfWeek_ReturnsBadRequest()
    {
        var request = new
        {
            flightNumber = "AX999",
            origin = "LHR",
            destination = "JFK",
            departureTime = "09:30",
            arrivalTime = "13:45",
            daysOfWeek = 0,
            aircraftType = "A351",
            validFrom = "2026-06-01",
            validTo = "2026-06-07",
            cabins = new[]
            {
                new { cabinCode = "Y", totalSeats = 220, fares = new[] { MakeMinimalFare() } }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/schedules", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -------------------------------------------------------------------------
    // Happy path: single cabin, single fare, narrow date range
    // -------------------------------------------------------------------------

    [SkippableFact, TestPriority(10)]
    public async Task T10_CreateSchedule_SingleCabin_ReturnsCreatedWithScheduleId()
    {
        // Arrange — weekdays only (Mon–Fri = 31), 1 week = 5 operating dates, 1 cabin → 5 inventory records
        var request = new
        {
            flightNumber = "AX901",
            origin = "LHR",
            destination = "CDG",
            departureTime = "07:15",
            arrivalTime = "09:30",
            arrivalDayOffset = 0,
            daysOfWeek = 31,
            aircraftType = "A339",
            validFrom = "2026-09-07",
            validTo = "2026-09-11",
            cabins = new[]
            {
                new
                {
                    cabinCode = "Y",
                    totalSeats = 280,
                    fares = new[]
                    {
                        new
                        {
                            fareBasisCode = "YFLEX",
                            fareFamily = "Economy Flex",
                            currencyCode = "GBP",
                            baseFareAmount = 180.00m,
                            taxAmount = 45.00m,
                            isRefundable = true,
                            isChangeable = true,
                            changeFeeAmount = 0.00m,
                            cancellationFeeAmount = 0.00m,
                            pointsPrice = 8000,
                            pointsTaxes = 45.00m
                        }
                    }
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/schedules", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CreateScheduleResponseDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.ScheduleId.Should().NotBeEmpty();
        body.FlightsCreated.Should().Be(5, "Mon-Fri in week 2026-09-07 to 2026-09-11 = 5 days, 1 cabin each");

        _scheduleId = body.ScheduleId;
        _flightsCreated = body.FlightsCreated;

        // Verify Location header
        response.Headers.Location.Should().NotBeNull();
        response.Headers.Location!.ToString().Should().Contain($"/v1/schedules/{body.ScheduleId}");
    }

    // -------------------------------------------------------------------------
    // Happy path: multiple cabins, multiple fares
    // -------------------------------------------------------------------------

    [SkippableFact, TestPriority(20)]
    public async Task T20_CreateSchedule_MultipleCabins_ReturnsCorrectInventoryCount()
    {
        // Arrange — daily (127), 3 days (Mon 2026-09-14 to Wed 2026-09-16), 2 cabins → 6 inventory records
        var request = new
        {
            flightNumber = "AX902",
            origin = "LHR",
            destination = "JFK",
            departureTime = "09:30",
            arrivalTime = "13:45",
            arrivalDayOffset = 0,
            daysOfWeek = 127,
            aircraftType = "A351",
            validFrom = "2026-09-14",
            validTo = "2026-09-16",
            cabins = new object[]
            {
                new
                {
                    cabinCode = "J",
                    totalSeats = 30,
                    fares = new[]
                    {
                        new
                        {
                            fareBasisCode = "JFLEX",
                            fareFamily = "Business Flex",
                            currencyCode = "GBP",
                            baseFareAmount = 2500.00m,
                            taxAmount = 450.00m,
                            isRefundable = true,
                            isChangeable = true,
                            changeFeeAmount = 0.00m,
                            cancellationFeeAmount = 0.00m,
                            pointsPrice = 75000,
                            pointsTaxes = 450.00m
                        },
                        new
                        {
                            fareBasisCode = "JSAVER",
                            fareFamily = "Business Saver",
                            currencyCode = "GBP",
                            baseFareAmount = 1800.00m,
                            taxAmount = 450.00m,
                            isRefundable = false,
                            isChangeable = true,
                            changeFeeAmount = 150.00m,
                            cancellationFeeAmount = 300.00m,
                            pointsPrice = 55000,
                            pointsTaxes = 450.00m
                        }
                    }
                },
                new
                {
                    cabinCode = "Y",
                    totalSeats = 220,
                    fares = new[]
                    {
                        new
                        {
                            fareBasisCode = "YFLEX",
                            fareFamily = "Economy Flex",
                            currencyCode = "GBP",
                            baseFareAmount = 650.00m,
                            taxAmount = 180.00m,
                            isRefundable = true,
                            isChangeable = true,
                            changeFeeAmount = 0.00m,
                            cancellationFeeAmount = 0.00m,
                            pointsPrice = 25000,
                            pointsTaxes = 180.00m
                        }
                    }
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/schedules", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<CreateScheduleResponseDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.ScheduleId.Should().NotBeEmpty();
        body.FlightsCreated.Should().Be(6, "3 days x 2 cabins = 6 inventory records");
    }

    // -------------------------------------------------------------------------
    // Happy path: SSIM import with default createdBy
    // -------------------------------------------------------------------------

    [SkippableFact, TestPriority(30)]
    public async Task T30_ImportSsim_DefaultCreatedBy_ReturnsOkWithOneSchedule()
    {
        // Arrange — one Type 3 record, AX901 LHR→JFK, daily for a single day
        using var content = new StringContent(DefaultSsim, System.Text.Encoding.UTF8, "text/plain");

        // Act — no ?createdBy= query param; server defaults to "ssim-import"
        var response = await _client.PostAsync("/api/v1/schedules/ssim", content);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ImportSsimResponseDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.Count.Should().Be(1);
        body.Schedules.Should().HaveCount(1);
        body.Schedules[0].ScheduleId.Should().NotBeEmpty();
        body.Schedules[0].FlightNumber.Should().Be("AX901");
        body.Schedules[0].Origin.Should().Be("LHR");
        body.Schedules[0].Destination.Should().Be("JFK");
    }

    // -------------------------------------------------------------------------
    // Happy path: import-inventory from stored schedules
    // -------------------------------------------------------------------------

    [SkippableFact, TestPriority(40)]
    public async Task T40_ImportSchedulesToInventory_NoCabins_ReturnsBadRequest()
    {
        var request = new
        {
            cabins = Array.Empty<object>()
        };

        var response = await _client.PostAsJsonAsync("/api/v1/schedules/import-inventory", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact, TestPriority(41)]
    public async Task T41_ImportSchedulesToInventory_MissingFares_ReturnsBadRequest()
    {
        var request = new
        {
            cabins = new[]
            {
                new { cabinCode = "Y", totalSeats = 220, fares = Array.Empty<object>() }
            }
        };

        var response = await _client.PostAsJsonAsync("/api/v1/schedules/import-inventory", request, JsonOptions);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [SkippableFact, TestPriority(50)]
    public async Task T50_ImportSchedulesToInventory_ValidCabins_ReturnsOkWithCounts()
    {
        // Arrange — cabin and fare definitions to apply to all stored schedules.
        // Assumes T30 has already imported at least one SSIM schedule (AX901 LHR→JFK).
        var request = new
        {
            cabins = new[]
            {
                new
                {
                    cabinCode = "Y",
                    totalSeats = 220,
                    fares = new[]
                    {
                        new
                        {
                            fareBasisCode = "YFLEX",
                            fareFamily = "Economy Flex",
                            currencyCode = "GBP",
                            baseFareAmount = 650.00m,
                            taxAmount = 180.00m,
                            isRefundable = true,
                            isChangeable = true,
                            changeFeeAmount = 0.00m,
                            cancellationFeeAmount = 0.00m,
                            pointsPrice = 25000,
                            pointsTaxes = 180.00m
                        }
                    }
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/schedules/import-inventory", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ImportSchedulesToInventoryResponseDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.SchedulesProcessed.Should().BeGreaterThanOrEqualTo(0);
        body.InventoriesCreated.Should().BeGreaterThanOrEqualTo(0);
        body.InventoriesSkipped.Should().BeGreaterThanOrEqualTo(0);
        body.FaresCreated.Should().Be(body.InventoriesCreated,
            "one fare should be created per newly created inventory record");
    }

    [SkippableFact, TestPriority(51)]
    public async Task T51_ImportSchedulesToInventory_Idempotent_AllSkipped()
    {
        // Running the same import again should skip all existing inventory.
        var request = new
        {
            cabins = new[]
            {
                new
                {
                    cabinCode = "Y",
                    totalSeats = 220,
                    fares = new[]
                    {
                        new
                        {
                            fareBasisCode = "YFLEX",
                            fareFamily = "Economy Flex",
                            currencyCode = "GBP",
                            baseFareAmount = 650.00m,
                            taxAmount = 180.00m,
                            isRefundable = true,
                            isChangeable = true,
                            changeFeeAmount = 0.00m,
                            cancellationFeeAmount = 0.00m,
                            pointsPrice = 25000,
                            pointsTaxes = 180.00m
                        }
                    }
                }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/schedules/import-inventory", request, JsonOptions);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ImportSchedulesToInventoryResponseDto>(JsonOptions);
        body.Should().NotBeNull();
        body!.InventoriesCreated.Should().Be(0, "all inventory records already exist from T50");
        body.FaresCreated.Should().Be(0, "no new inventories were created so no fares should be created");
    }

    // Default single-record SSIM file used by T30.
    // Type 3 record: AX901, LHR→JFK, daily (1234567), 2026-09-07 only, A351 equipment.
    // Field positions are 0-indexed per SsimParser:
    //   0     record type '3'
    //   2-3   carrier 'AX'
    //   5-8   flight number '0901'  → AX901
    //   10    service type 'Y'
    //   12-19 period start '20260907'
    //   21-28 period end   '20260907'
    //   31-37 days-of-week '1234567' (daily)
    //   39-41 origin 'LHR'
    //   42-45 departure time '0800'
    //   46-48 UTC offset '+00'
    //   49-52 arrival time '1110'
    //   53    arrival day offset '0' (same day)
    //   55-57 destination 'JFK'
    //   61-63 equipment '351' → A351
    private const string DefaultSsim =
        "1IATA  AX              20260907 20260907AX  SCHED  20260328\r\n" +
        "2AX  01W20260907 20260907\r\n" +
        "3 AX 0901 Y 20260907 20260907  1234567 LHR0800+0011100 JFK   351 AX                                                                                                                              \r\n" +
        "5       1\r\n";

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static object MakeMinimalFare() => new
    {
        fareBasisCode = "YFLEX",
        fareFamily = "Economy Flex",
        currencyCode = "GBP",
        baseFareAmount = 180.00m,
        taxAmount = 45.00m,
        isRefundable = true,
        isChangeable = true,
        changeFeeAmount = 0.00m,
        cancellationFeeAmount = 0.00m,
        pointsPrice = 8000,
        pointsTaxes = 45.00m
    };

    private static void SkipIfNoScheduleId() =>
        Skip.If(_scheduleId is null, "Skipped because schedule creation did not succeed.");
}

#region Response DTOs

internal sealed class CreateScheduleResponseDto
{
    public Guid ScheduleId { get; init; }
    public int FlightsCreated { get; init; }
}

internal sealed class ImportSsimResponseDto
{
    public int Count { get; init; }
    public IReadOnlyList<ImportedScheduleItemDto> Schedules { get; init; } = [];
}

internal sealed class ImportSchedulesToInventoryResponseDto
{
    public int SchedulesProcessed { get; init; }
    public int InventoriesCreated { get; init; }
    public int InventoriesSkipped { get; init; }
    public int FaresCreated { get; init; }
}

internal sealed class ImportedScheduleItemDto
{
    public Guid ScheduleId { get; init; }
    public string FlightNumber { get; init; } = string.Empty;
    public string Origin { get; init; } = string.Empty;
    public string Destination { get; init; } = string.Empty;
    public string ValidFrom { get; init; } = string.Empty;
    public string ValidTo { get; init; } = string.Empty;
    public int OperatingDateCount { get; init; }
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
