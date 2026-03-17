namespace ReservationSystem.Shared.Common.Http;

/// <summary>
/// Standard error envelope returned in all non-2xx responses.
///
/// Every API must use this shape so clients and the frontend can handle errors
/// uniformly without inspecting individual response structures.
///
/// Canonical JSON shape (camelCase via <see cref="Json.SharedJsonOptions.CamelCase"/>):
/// <code>
/// {
///   "error":         "Offer not found",
///   "errorCode":     "NOT_FOUND",
///   "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
///   "details":       { "offerId": "abc-123" }
/// }
/// </code>
///
/// Usage — create via the static factory and pass to an HTTP extension:
/// <code>
///   return await req.NotFoundAsync("Offer not found", correlationId);
///
///   // or with structured details:
///   var error = ApiError.From(
///       "Seat not available",
///       errorCode:     "SEAT_UNAVAILABLE",
///       correlationId: correlationId,
///       details:       new { seatNumber = "14A", flightId = id });
///   return await req.OkJsonAsync(error); // caller picks status code
/// </code>
/// </summary>
public sealed record ApiError
{
    /// <summary>Human-readable error description, safe to display to end-users.</summary>
    public string Error { get; init; } = string.Empty;

    /// <summary>
    /// Machine-readable error code, stable across releases.
    /// Clients should branch on this value, not on <see cref="Error"/>.
    /// Convention: SCREAMING_SNAKE_CASE, e.g. NOT_FOUND, SEAT_UNAVAILABLE.
    /// </summary>
    public string? ErrorCode { get; init; }

    /// <summary>
    /// The <c>X-Correlation-ID</c> from the inbound request (or a freshly
    /// generated GUID if the caller did not supply one). Include in every error
    /// response so support engineers can locate the matching log line.
    /// </summary>
    public string? CorrelationId { get; init; }

    /// <summary>
    /// Optional structured data providing additional context for debugging or
    /// for programmatic error handling by clients (e.g. field-level validation
    /// failures, unavailable resource identifiers). Must be JSON-serialisable.
    /// </summary>
    public object? Details { get; init; }

    // -------------------------------------------------------------------------
    // Factory
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates an <see cref="ApiError"/> from individual fields.
    /// All parameters beyond <paramref name="error"/> are optional.
    /// </summary>
    public static ApiError From(
        string error,
        string? errorCode = null,
        string? correlationId = null,
        object? details = null)
        => new()
        {
            Error         = error,
            ErrorCode     = errorCode,
            CorrelationId = correlationId,
            Details       = details
        };
}
