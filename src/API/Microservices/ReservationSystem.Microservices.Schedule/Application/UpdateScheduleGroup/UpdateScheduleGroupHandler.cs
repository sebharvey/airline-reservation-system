using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;
using ReservationSystem.Microservices.Schedule.Models.Responses;

namespace ReservationSystem.Microservices.Schedule.Application.UpdateScheduleGroup;

/// <summary>
/// Updates an existing schedule group and returns the updated entity as a <see cref="ScheduleGroupItem"/>.
/// Returns null when the schedule group does not exist.
/// </summary>
public sealed class UpdateScheduleGroupHandler
{
    private readonly IScheduleGroupRepository _groupRepository;
    private readonly IFlightScheduleRepository _scheduleRepository;
    private readonly ILogger<UpdateScheduleGroupHandler> _logger;

    public UpdateScheduleGroupHandler(
        IScheduleGroupRepository groupRepository,
        IFlightScheduleRepository scheduleRepository,
        ILogger<UpdateScheduleGroupHandler> logger)
    {
        _groupRepository = groupRepository;
        _scheduleRepository = scheduleRepository;
        _logger = logger;
    }

    public async Task<ScheduleGroupItem?> HandleAsync(
        UpdateScheduleGroupCommand command,
        CancellationToken cancellationToken = default)
    {
        var group = await _groupRepository.GetByIdAsync(command.ScheduleGroupId, cancellationToken);
        if (group is null)
        {
            _logger.LogWarning("Update requested for unknown schedule group {ScheduleGroupId}", command.ScheduleGroupId);
            return null;
        }

        group.Update(command.Name, command.SeasonStart, command.SeasonEnd, command.IsActive);
        await _groupRepository.UpdateAsync(group, cancellationToken);

        var schedules = await _scheduleRepository.GetByGroupAsync(command.ScheduleGroupId, cancellationToken);

        _logger.LogInformation("Updated schedule group {ScheduleGroupId} '{Name}'", group.ScheduleGroupId, group.Name);

        return new ScheduleGroupItem
        {
            ScheduleGroupId = group.ScheduleGroupId,
            Name = group.Name,
            SeasonStart = group.SeasonStart.ToString("yyyy-MM-dd"),
            SeasonEnd = group.SeasonEnd.ToString("yyyy-MM-dd"),
            IsActive = group.IsActive,
            ScheduleCount = schedules.Count,
            CreatedBy = group.CreatedBy,
            CreatedAt = group.CreatedAt.ToString("o")
        };
    }
}
