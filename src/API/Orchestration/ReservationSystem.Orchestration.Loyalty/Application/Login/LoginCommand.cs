namespace ReservationSystem.Orchestration.Loyalty.Application.Login;

public sealed record LoginCommand(
    string Email,
    string Password);
