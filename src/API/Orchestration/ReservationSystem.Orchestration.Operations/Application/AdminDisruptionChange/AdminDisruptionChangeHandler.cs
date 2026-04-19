using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.AdminDisruptionChange;

public sealed class AdminDisruptionChangeHandler
{
    public Task<AdminDisruptionChangeResponse> HandleAsync(
        AdminDisruptionChangeCommand command,
        CancellationToken ct)
    {
        throw new NotImplementedException("Aircraft change disruption handling is not yet implemented.");
    }
}
