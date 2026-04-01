namespace ReservationSystem.Microservices.Exceptions.Models.Responses;

public sealed class GetExceptionsResponse
{
    public required int Count { get; init; }
    public required DateTimeOffset QueryFrom { get; init; }
    public required DateTimeOffset QueryTo { get; init; }
    public required IReadOnlyList<ExceptionEntry> Exceptions { get; init; }
}

public sealed class ExceptionEntry
{
    public required DateTimeOffset Timestamp { get; init; }
    public required string ProblemId { get; init; }
    public required string ExceptionType { get; init; }
    public required string Message { get; init; }
    public required string? Method { get; init; }
    public required string? Assembly { get; init; }
    public required string? OuterType { get; init; }
    public required string? OuterMessage { get; init; }
    public required string? OperationName { get; init; }
    public required string? OperationId { get; init; }
    public required string? CloudRoleName { get; init; }
    public required string CallStack { get; init; }
    public required int SeverityLevel { get; init; }
}
