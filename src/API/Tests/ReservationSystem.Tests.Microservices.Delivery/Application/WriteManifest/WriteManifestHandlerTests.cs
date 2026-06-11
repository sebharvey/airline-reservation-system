using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ReservationSystem.Microservices.Delivery.Application.WriteManifest;
using ReservationSystem.Microservices.Delivery.Domain.Entities;
using ReservationSystem.Microservices.Delivery.Domain.Repositories;
using ReservationSystem.Microservices.Delivery.Models.Requests;

namespace ReservationSystem.Tests.Microservices.Delivery.Application.WriteManifest;

public sealed class WriteManifestHandlerTests
{
    private readonly Mock<ITicketRepository> _ticketRepo = new();
    private readonly Mock<IManifestRepository> _manifestRepo = new();
    private readonly WriteManifestHandler _handler;

    public WriteManifestHandlerTests()
    {
        _handler = new WriteManifestHandler(
            _ticketRepo.Object,
            _manifestRepo.Object,
            NullLogger<WriteManifestHandler>.Instance);
    }

    private static Ticket MakeTicket(int passengerId) =>
        Ticket.Create("ABC123", passengerId);

    private static WriteManifestRequest MakeRequest(int paxId, string eTicketNumber = "125-1234567890") =>
        new()
        {
            BookingReference = "ABC123",
            OrderId          = Guid.NewGuid(),
            InventoryId      = Guid.NewGuid(),
            SegmentId        = 1,
            FlightNumber     = "BA0100",
            Origin           = "LHR",
            Destination      = "JFK",
            DepartureDate    = "2026-06-20",
            AircraftType     = "B77W",
            DepartureTime    = "10:30",
            ArrivalTime      = "13:15",
            Entries          =
            [
                new ManifestEntryRequest
                {
                    PaxId         = paxId,
                    PassengerId   = $"PAX-{paxId}",
                    GivenName     = "John",
                    Surname       = "Smith",
                    ETicketNumber = eTicketNumber,
                    CabinCode     = "Y"
                }
            ]
        };

    [Fact]
    public async Task HandleAsync_MatchesTicketByPaxId_WritesManifest()
    {
        var ticket = MakeTicket(passengerId: 1);
        _ticketRepo
            .Setup(r => r.GetByBookingReferenceAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ticket });
        _manifestRepo
            .Setup(r => r.CreateAsync(It.IsAny<Manifest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _handler.HandleAsync(MakeRequest(paxId: 1));

        Assert.Equal(1, result.Written);
        Assert.Equal(0, result.Skipped);
        _manifestRepo.Verify(r => r.CreateAsync(It.IsAny<Manifest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_PaxIdDoesNotMatchAnyTicket_SkipsEntry()
    {
        var ticket = MakeTicket(passengerId: 2);
        _ticketRepo
            .Setup(r => r.GetByBookingReferenceAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ticket });

        var result = await _handler.HandleAsync(MakeRequest(paxId: 1));

        Assert.Equal(0, result.Written);
        Assert.Equal(1, result.Skipped);
        _manifestRepo.Verify(r => r.CreateAsync(It.IsAny<Manifest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task HandleAsync_VoidedTicketNotMatched_SkipsEntry()
    {
        var ticket = MakeTicket(passengerId: 1);
        ticket.Void();
        _ticketRepo
            .Setup(r => r.GetByBookingReferenceAsync("ABC123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { ticket });

        var result = await _handler.HandleAsync(MakeRequest(paxId: 1));

        Assert.Equal(0, result.Written);
        Assert.Equal(1, result.Skipped);
    }
}
