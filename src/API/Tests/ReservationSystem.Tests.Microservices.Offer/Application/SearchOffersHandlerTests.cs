using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservationSystem.Microservices.Offer.Application.SearchOffers;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Tests.Microservices.Offer.Application;

public sealed class SearchOffersHandlerTests
{
    private readonly Mock<IOfferRepository> _repository = new();
    private readonly SearchOffersHandler _handler;

    public SearchOffersHandlerTests()
    {
        _handler = new SearchOffersHandler(_repository.Object, NullLogger<SearchOffersHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenNoInventoryAvailable_ReturnsNull()
    {
        // Arrange
        var command = new SearchOffersCommand("LHR", "JFK", "2099-12-25", 2, "Revenue");

        _repository
            .Setup(r => r.SearchAvailableInventoryAsync("LHR", "JFK", new DateOnly(2099, 12, 25), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.Null(result);
        _repository.Verify(
            r => r.SearchAvailableInventoryAsync("LHR", "JFK", new DateOnly(2099, 12, 25), 2, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenInventoryExistsButNoFareRulesMatch_ReturnsNull()
    {
        // Arrange
        var command = new SearchOffersCommand("LHR", "JFK", "2099-12-25", 2, "Revenue");

        var inventory = FlightInventory.Create(
            flightNumber: "AX001",
            departureDate: new DateOnly(2099, 12, 25),
            departureTime: new TimeOnly(10, 0),
            arrivalTime: new TimeOnly(13, 0),
            arrivalDayOffset: 0,
            origin: "LHR",
            destination: "JFK",
            aircraftType: "A351",
            cabins: [("Y", 180), ("J", 40)]);

        _repository
            .Setup(r => r.SearchAvailableInventoryAsync("LHR", "JFK", new DateOnly(2099, 12, 25), 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync([inventory]);

        _repository
            .Setup(r => r.GetApplicableFareRulesForFlightsAsync(
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<IReadOnlyList<string>>(),
                It.IsAny<DateOnly>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var result = await _handler.HandleAsync(command);

        // Assert
        Assert.Null(result);
        _repository.Verify(
            r => r.GetApplicableFareRulesForFlightsAsync(
                It.Is<IReadOnlyList<string>>(l => l.Contains("AX001")),
                It.IsAny<IReadOnlyList<string>>(),
                new DateOnly(2099, 12, 25),
                false,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
