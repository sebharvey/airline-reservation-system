using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReservationSystem.Microservices.Schedule.Application.ImportSchedules;
using ReservationSystem.Microservices.Schedule.Domain.Entities;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;

namespace ReservationSystem.Tests.Microservices.Schedule.Application;

public sealed class ImportSchedulesHandlerTests
{
    private readonly Mock<IFlightScheduleRepository> _scheduleRepository = new();
    private readonly Mock<IScheduleGroupRepository> _groupRepository = new();
    private readonly ImportSchedulesHandler _handler;

    public ImportSchedulesHandlerTests()
    {
        _handler = new ImportSchedulesHandler(
            _scheduleRepository.Object,
            _groupRepository.Object,
            NullLogger<ImportSchedulesHandler>.Instance);
    }

    [Fact]
    public async Task HandleAsync_WhenGroupNotFound_ThrowsArgumentException()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var command = new ImportSchedulesCommand(groupId, []);

        _groupRepository
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ScheduleGroup?)null);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _handler.HandleAsync(command));

        _scheduleRepository.Verify(
            r => r.ReplaceByGroupAsync(It.IsAny<Guid>(), It.IsAny<IReadOnlyList<FlightSchedule>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WithValidGroup_ReplacesSchedulesAndReturnsCount()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var group = ScheduleGroup.Create("Summer 2026", DateTime.UtcNow, DateTime.UtcNow.AddMonths(6), true, "admin");

        var scheduleDefinitions = new List<ScheduleDefinition>
        {
            new(
                FlightNumber: "AX001",
                Origin: "LHR",
                Destination: "JFK",
                DepartureTime: new TimeSpan(10, 0, 0),
                ArrivalTime: new TimeSpan(13, 0, 0),
                ArrivalDayOffset: 0,
                DaysOfWeek: 0b1111111,
                AircraftType: "A351",
                ValidFrom: DateTime.UtcNow,
                ValidTo: DateTime.UtcNow.AddMonths(6),
                CreatedBy: "admin")
        };

        var command = new ImportSchedulesCommand(groupId, scheduleDefinitions);

        _groupRepository
            .Setup(r => r.GetByIdAsync(groupId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        _scheduleRepository
            .Setup(r => r.ReplaceByGroupAsync(groupId, It.IsAny<IReadOnlyList<FlightSchedule>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        // Act
        var (schedules, deleted) = await _handler.HandleAsync(command);

        // Assert
        Assert.Single(schedules);
        Assert.Equal(5, deleted);
        Assert.Equal("AX001", schedules[0].FlightNumber);

        _scheduleRepository.Verify(
            r => r.ReplaceByGroupAsync(groupId, It.Is<IReadOnlyList<FlightSchedule>>(l => l.Count == 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
