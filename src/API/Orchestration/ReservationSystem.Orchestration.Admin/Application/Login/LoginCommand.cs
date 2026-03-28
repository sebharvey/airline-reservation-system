namespace ReservationSystem.Orchestration.Admin.Application.Login;

public sealed record LoginCommand(
    string Username,
    string Password);
