using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.ImportSsim;

public sealed class ImportSsimHandler
{
    private readonly ScheduleServiceClient _scheduleServiceClient;

    public ImportSsimHandler(ScheduleServiceClient scheduleServiceClient)
    {
        _scheduleServiceClient = scheduleServiceClient;
    }

    public async Task<ImportSsimResponse> HandleAsync(
        ImportSsimCommand command,
        CancellationToken cancellationToken = default)
    {
        return await _scheduleServiceClient.ImportSsimAsync(command.SsimText, command.CreatedBy, cancellationToken);
    }
}
