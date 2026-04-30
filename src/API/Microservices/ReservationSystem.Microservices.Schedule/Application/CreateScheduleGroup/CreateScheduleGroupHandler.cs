using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Domain.Entities;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;
using ReservationSystem.Microservices.Schedule.Models.Responses;

namespace ReservationSystem.Microservices.Schedule.Application.CreateScheduleGroup;

/// <summary>
/// Creates a new schedule group and returns the created entity as a <see cref="ScheduleGroupItem"/>.
/// </summary>
public sealed class CreateScheduleGroupHandler
{
    private readonly IScheduleGroupRepository _groupRepository;
    private readonly ILogger<CreateScheduleGroupHandler> _logger;

    public CreateScheduleGroupHandler(
        IScheduleGroupRepository groupRepository,
        ILogger<CreateScheduleGroupHandler> logger)
    {
        _groupRepository = groupRepository;
        _logger = logger;
    }

    public async Task<ScheduleGroupItem> HandleAsync(
        CreateScheduleGroupCommand command,
        CancellationToken cancellationToken = default)
    {
        var group = ScheduleGroup.Create(command.Name, command.SeasonStart, command.SeasonEnd, command.IsActive, command.CreatedBy);
        await _groupRepository.AddAsync(group, cancellationToken);

        _logger.LogInformation("Created schedule group {ScheduleGroupId} '{Name}'", group.ScheduleGroupId, group.Name);

        return new ScheduleGroupItem
        {
            ScheduleGroupId = group.ScheduleGroupId,
            Name = group.Name,
            SeasonStart = group.SeasonStart.ToString("yyyy-MM-dd"),
            SeasonEnd = group.SeasonEnd.ToString("yyyy-MM-dd"),
            IsActive = group.IsActive,
            ScheduleCount = 0,
            CreatedBy = group.CreatedBy,
            CreatedAt = group.CreatedAt.ToString("o")
        };
    }
}
