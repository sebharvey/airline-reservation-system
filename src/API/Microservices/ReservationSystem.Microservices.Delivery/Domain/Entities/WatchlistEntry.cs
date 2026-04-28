namespace ReservationSystem.Microservices.Delivery.Domain.Entities;

public sealed class WatchlistEntry
{
    public Guid WatchlistId { get; private set; }
    public string GivenName { get; private set; } = string.Empty;
    public string Surname { get; private set; } = string.Empty;
    public DateOnly DateOfBirth { get; private set; }
    public string PassportNumber { get; private set; } = string.Empty;
    public string AddedBy { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private WatchlistEntry() { }

    public static WatchlistEntry Create(
        string givenName,
        string surname,
        DateOnly dateOfBirth,
        string passportNumber,
        string addedBy,
        string? notes)
    {
        return new WatchlistEntry
        {
            WatchlistId = Guid.NewGuid(),
            GivenName = givenName.Trim().ToUpperInvariant(),
            Surname = surname.Trim().ToUpperInvariant(),
            DateOfBirth = dateOfBirth,
            PassportNumber = passportNumber.Trim().ToUpperInvariant(),
            AddedBy = addedBy,
            Notes = notes?.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    public static WatchlistEntry Reconstitute(
        Guid watchlistId,
        string givenName,
        string surname,
        DateOnly dateOfBirth,
        string passportNumber,
        string addedBy,
        string? notes,
        DateTime createdAt,
        DateTime updatedAt)
    {
        return new WatchlistEntry
        {
            WatchlistId = watchlistId,
            GivenName = givenName,
            Surname = surname,
            DateOfBirth = dateOfBirth,
            PassportNumber = passportNumber,
            AddedBy = addedBy,
            Notes = notes,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt,
        };
    }

    public void Update(string givenName, string surname, DateOnly dateOfBirth, string passportNumber, string? notes)
    {
        GivenName = givenName.Trim().ToUpperInvariant();
        Surname = surname.Trim().ToUpperInvariant();
        DateOfBirth = dateOfBirth;
        PassportNumber = passportNumber.Trim().ToUpperInvariant();
        Notes = notes?.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
