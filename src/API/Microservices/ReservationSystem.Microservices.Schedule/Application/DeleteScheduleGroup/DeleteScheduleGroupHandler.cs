using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;

namespace ReservationSystem.Microservices.Schedule.Application.DeleteScheduleGroup;

/// <summary>
/// Deletes a schedule group by ID.
/// Returns true when successfully deleted, false when the group does not exist.
/// </summary>
public sealed class DeleteScheduleGroupHandler
{
    private readonly IScheduleGroupRepository _groupRepository;
    private readonly ILogger<DeleteScheduleGroupHandler> _logger;

    public DeleteScheduleGroupHandler(
        IScheduleGroupRepository groupRepository,
        ILogger<DeleteScheduleGroupHandler> logger)
    {
        _groupRepository = groupRepository;
        _logger = logger;
    }

    public async Task<bool> HandleAsync(
        DeleteScheduleGroupCommand command,
        CancellationToken cancellationToken = default)
    {
        var group = await _groupRepository.GetByIdAsync(command.ScheduleGroupId, cancellationToken);
        if (group is null)
        {
            _logger.LogWarning("Delete requested for unknown schedule group {ScheduleGroupId}", command.ScheduleGroupId);
            return false;
        }

        await _groupRepository.DeleteAsync(command.ScheduleGroupId, cancellationToken);

        _logger.LogInformation("Deleted schedule group {ScheduleGroupId}", command.ScheduleGroupId);

        return true;
    }
}
