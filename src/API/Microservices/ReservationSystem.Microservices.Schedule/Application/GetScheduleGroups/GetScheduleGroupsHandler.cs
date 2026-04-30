using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Schedule.Domain.Repositories;
using ReservationSystem.Microservices.Schedule.Models.Responses;

namespace ReservationSystem.Microservices.Schedule.Application.GetScheduleGroups;

/// <summary>
/// Returns all schedule groups with their flight schedule counts.
/// </summary>
public sealed class GetScheduleGroupsHandler
{
    private readonly IScheduleGroupRepository _groupRepository;
    private readonly IFlightScheduleRepository _scheduleRepository;
    private readonly ILogger<GetScheduleGroupsHandler> _logger;

    public GetScheduleGroupsHandler(
        IScheduleGroupRepository groupRepository,
        IFlightScheduleRepository scheduleRepository,
        ILogger<GetScheduleGroupsHandler> logger)
    {
        _groupRepository = groupRepository;
        _scheduleRepository = scheduleRepository;
        _logger = logger;
    }

    public async Task<GetScheduleGroupsResponse> HandleAsync(
        GetScheduleGroupsQuery query,
        CancellationToken cancellationToken = default)
    {
        var groups = await _groupRepository.GetAllAsync(cancellationToken);
        var allSchedules = await _scheduleRepository.GetAllAsync(cancellationToken);
        var countByGroup = allSchedules.GroupBy(s => s.ScheduleGroupId)
            .ToDictionary(g => g.Key, g => g.Count());

        var response = new GetScheduleGroupsResponse
        {
            Count = groups.Count,
            Groups = groups.Select(g => new ScheduleGroupItem
            {
                ScheduleGroupId = g.ScheduleGroupId,
                Name = g.Name,
                SeasonStart = g.SeasonStart.ToString("yyyy-MM-dd"),
                SeasonEnd = g.SeasonEnd.ToString("yyyy-MM-dd"),
                IsActive = g.IsActive,
                ScheduleCount = countByGroup.GetValueOrDefault(g.ScheduleGroupId, 0),
                CreatedBy = g.CreatedBy,
                CreatedAt = g.CreatedAt.ToString("o")
            }).ToList().AsReadOnly()
        };

        _logger.LogInformation("GetScheduleGroups returned {Count} schedule group records", groups.Count);

        return response;
    }
}
