namespace ReservationSystem.Microservices.Customer.Domain.Entities;

/// <summary>
/// Core domain entity representing a loyalty programme customer.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class Customer
{
    public Guid CustomerId { get; private set; }
    public string LoyaltyNumber { get; private set; } = string.Empty;
    public Guid? IdentityReference { get; private set; }
    public string GivenName { get; private set; } = string.Empty;
    public string Surname { get; private set; } = string.Empty;
    public DateOnly? DateOfBirth { get; private set; }
    public string? Nationality { get; private set; }
    public string PreferredLanguage { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string TierCode { get; private set; } = string.Empty;
    public int PointsBalance { get; private set; }
    public int TierProgressPoints { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Customer() { }

    public void UpdateProfile(
        string? givenName = null,
        string? surname = null,
        DateOnly? dateOfBirth = null,
        string? nationality = null,
        string? preferredLanguage = null,
        string? phoneNumber = null,
        string? tierCode = null,
        Guid? identityReference = null)
    {
        if (givenName is not null) GivenName = givenName;
        if (surname is not null) Surname = surname;
        if (dateOfBirth is not null) DateOfBirth = dateOfBirth;
        if (nationality is not null) Nationality = nationality;
        if (preferredLanguage is not null) PreferredLanguage = preferredLanguage;
        if (phoneNumber is not null) PhoneNumber = phoneNumber;
        if (tierCode is not null) TierCode = tierCode;
        if (identityReference is not null) IdentityReference = identityReference;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void DeductPoints(int points)
    {
        PointsBalance -= points;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddPoints(int points)
    {
        PointsBalance += points;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Factory method for creating a brand-new customer. Assigns a new CustomerId and timestamps.
    /// </summary>
    public static Customer Create(
        string loyaltyNumber,
        string givenName,
        string surname,
        string preferredLanguage,
        string tierCode,
        Guid? identityReference = null,
        DateOnly? dateOfBirth = null,
        string? nationality = null,
        string? phoneNumber = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(loyaltyNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(givenName);
        ArgumentException.ThrowIfNullOrWhiteSpace(surname);
        ArgumentException.ThrowIfNullOrWhiteSpace(preferredLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(tierCode);

        return new Customer
        {
            CustomerId = Guid.NewGuid(),
            LoyaltyNumber = loyaltyNumber,
            IdentityReference = identityReference,
            GivenName = givenName,
            Surname = surname,
            DateOfBirth = dateOfBirth,
            Nationality = nationality,
            PreferredLanguage = preferredLanguage,
            PhoneNumber = phoneNumber,
            TierCode = tierCode,
            PointsBalance = 0,
            TierProgressPoints = 0,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from a persistence store.
    /// </summary>
    public static Customer Reconstitute(
        Guid customerId,
        string loyaltyNumber,
        Guid? identityReference,
        string givenName,
        string surname,
        DateOnly? dateOfBirth,
        string? nationality,
        string preferredLanguage,
        string? phoneNumber,
        string tierCode,
        int pointsBalance,
        int tierProgressPoints,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        return new Customer
        {
            CustomerId = customerId,
            LoyaltyNumber = loyaltyNumber,
            IdentityReference = identityReference,
            GivenName = givenName,
            Surname = surname,
            DateOfBirth = dateOfBirth,
            Nationality = nationality,
            PreferredLanguage = preferredLanguage,
            PhoneNumber = phoneNumber,
            TierCode = tierCode,
            PointsBalance = pointsBalance,
            TierProgressPoints = tierProgressPoints,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
