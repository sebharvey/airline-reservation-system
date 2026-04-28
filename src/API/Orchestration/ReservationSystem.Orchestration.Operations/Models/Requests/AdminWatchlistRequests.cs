namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class AdminCreateWatchlistEntryRequest
{
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string DateOfBirth { get; init; } = string.Empty;
    public string PassportNumber { get; init; } = string.Empty;
    public string? Notes { get; init; }
}

public sealed class AdminUpdateWatchlistEntryRequest
{
    public string GivenName { get; init; } = string.Empty;
    public string Surname { get; init; } = string.Empty;
    public string DateOfBirth { get; init; } = string.Empty;
    public string PassportNumber { get; init; } = string.Empty;
    public string? Notes { get; init; }
}
