namespace ReservationSystem.Orchestration.Operations.Models.Requests;

public sealed class AdminCreateFareFamilyRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
}

public sealed class AdminUpdateFareFamilyRequest
{
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public int DisplayOrder { get; init; }
}
