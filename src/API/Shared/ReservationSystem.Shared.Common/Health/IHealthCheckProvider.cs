namespace ReservationSystem.Shared.Common.Health;

/// <summary>
/// Represents a single named check that verifies one service or dependency is reachable.
/// Register an implementation via <see cref="HealthCheckExtensions.AddHealthCheck"/> in each API's Program.cs.
/// </summary>
public interface IHealthCheckProvider
{
    string Name { get; }
    Task CheckAsync(CancellationToken cancellationToken);
}
