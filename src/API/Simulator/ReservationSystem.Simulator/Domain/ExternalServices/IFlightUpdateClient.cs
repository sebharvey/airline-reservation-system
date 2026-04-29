using ReservationSystem.Simulator.Models;

namespace ReservationSystem.Simulator.Domain.ExternalServices;

public interface IFlightUpdateClient
{
    Task<string> LoginAsync(string username, string password, CancellationToken ct = default);

    Task<List<FlightInventoryItem>> GetInventoryAsync(string departureDate, string jwtToken, CancellationToken ct = default);

    Task SetOperationalDataAsync(Guid inventoryId, string? departureGate, string? aircraftRegistration, string jwtToken, CancellationToken ct = default);

    Task<FlightManifestResponse> GetManifestAsync(string flightNumber, string departureDate, string jwtToken, CancellationToken ct = default);

    Task AdminCheckInAsync(string bookingRef, AdminCheckInRequest request, string jwtToken, CancellationToken ct = default);
}
