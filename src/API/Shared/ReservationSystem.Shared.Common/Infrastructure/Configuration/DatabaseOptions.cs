namespace ReservationSystem.Shared.Common.Infrastructure.Configuration;

/// <summary>
/// Strongly-typed options for SQL database connectivity.
/// Bound from the "Database" section of application configuration at startup.
/// </summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Azure SQL connection string.</summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>
    /// Command timeout in seconds. Defaults to 30.
    /// Increase for long-running queries or batch operations.
    /// </summary>
    public int CommandTimeoutSeconds { get; init; } = 30;
}
