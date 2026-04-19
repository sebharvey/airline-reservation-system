using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionTime;

public sealed class AdminDisruptionTimeHandler
{
    public Task<AdminDisruptionTimeResponse> HandleAsync(
        AdminDisruptionTimeCommand command,
        CancellationToken ct)
    {
        throw new NotImplementedException("Flight time change disruption handling is not yet implemented.");
    }
}
