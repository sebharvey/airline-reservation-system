using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ReservationSystem.Microservices.Delivery.Application.RebookManifest;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;

namespace ReservationSystem.Tests.Microservices.Delivery.Application.RebookManifest;

public sealed class RebookManifestHandlerTests
{
    private readonly Mock<ITicketRepository> _ticketRepo = new();
    private readonly Mock<IManifestRepository> _manifestRepo = new();
    private readonly RebookManifestHandler _handler;

    public RebookManifestHandlerTests()
    {
        _handler = new RebookManifestHandler(
            _ticketRepo.Object,
            _manifestRepo.Object,
            NullLogger<RebookManifestHandler>.Instance);
    }

    private static RebookManifestCommand MakeCommand(IReadOnlyList<(int PaxId, string ETicketNumber)> passengers) =>
        new(
            BookingReference:  "ABC123",
            FromFlightNumber:  "AX100",
            FromDepartureDate: new DateOnly(2026, 6, 20),
            ToInventoryId:     Guid.NewGuid(),
            ToSegmentId:       2,
            ToFlightNumber:    "AX101",
            ToOrigin:          "LHR",
            ToDestination:     "JFK",
            ToDepartureDate:   new DateOnly(2026, 6, 21),
            ToDepartureTime:   new TimeOnly(10, 0),
            ToArrivalTime:     new TimeOnly(13, 0),
            ToCabinCode:       "Y",
            Passengers:        passengers);

    [Fact]
    public async Task HandleAsync_MatchesTicketByIntPaxId_CallsRepoRebook()
    {
        var ticket = Ticket.Create("ABC123", 1);
        _ticketRepo
            .Setup(r => r.GetByBookingReferenceAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ticket });
        _manifestRepo
            .Setup(r => r.RebookByBookingAndFlightAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
                It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<int, ManifestPassengerRebook>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var command = MakeCommand([(PaxId: 1, ETicketNumber: "932-1000000001")]);
        var result = await _handler.HandleAsync(command);

        Assert.Equal(1, result);
        _manifestRepo.Verify(r => r.RebookByBookingAndFlightAsync(
            "ABC123", "AX100", new DateOnly(2026, 6, 20),
            It.IsAny<Guid>(), 2, "AX101", "LHR", "JFK",
            new DateOnly(2026, 6, 21), new TimeOnly(10, 0), new TimeOnly(13, 0), "Y",
            It.Is<IReadOnlyDictionary<int, ManifestPassengerRebook>>(d => d.ContainsKey(1)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaxIdDoesNotMatchAnyTicket_ReturnsZero()
    {
        var ticket = Ticket.Create("ABC123", 2);
        _ticketRepo
            .Setup(r => r.GetByBookingReferenceAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ticket });

        var command = MakeCommand([(PaxId: 1, ETicketNumber: "932-1000000001")]);
        var result = await _handler.HandleAsync(command);

        Assert.Equal(0, result);
        _manifestRepo.Verify(r => r.RebookByBookingAndFlightAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
            It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateOnly>(),
            It.IsAny<TimeOnly>(), It.IsAny<TimeOnly>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<int, ManifestPassengerRebook>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_VoidedTicketNotMatched_ReturnsZero()
    {
        var ticket = Ticket.Create("ABC123", 1);
        ticket.Void();
        _ticketRepo
            .Setup(r => r.GetByBookingReferenceAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ticket });

        var command = MakeCommand([(PaxId: 1, ETicketNumber: "932-1000000001")]);
        var result = await _handler.HandleAsync(command);

        Assert.Equal(0, result);
    }
}
