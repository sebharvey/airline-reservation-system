namespace ReservationSystem.Simulator.Domain.ExternalServices;

public interface IAdminApiClient
{
    Task<string> LoginAsync(CancellationToken ct = default);
}
