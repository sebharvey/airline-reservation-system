namespace ReservationSystem.Microservices.Customer.Domain.Entities;

/// <summary>
/// Core domain entity representing a loyalty programme customer.
/// Has no dependency on infrastructure, persistence, or serialisation concerns.
/// </summary>
public sealed class Customer
{
    public Guid CustomerId { get; private set; }
    public string LoyaltyNumber { get; private set; } = string.Empty;
    public Guid? IdentityId { get; private set; }
    public string GivenName { get; private set; } = string.Empty;
    public string Surname { get; private set; } = string.Empty;
    public DateOnly? DateOfBirth { get; private set; }
    public string? Gender { get; private set; }
    public string? Nationality { get; private set; }
    public string PreferredLanguage { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string? City { get; private set; }
    public string? StateOrRegion { get; private set; }
    public string? PostalCode { get; private set; }
    public string? CountryCode { get; private set; }
    public string? PassportNumber { get; private set; }
    public DateOnly? PassportIssueDate { get; private set; }
    public string? PassportIssuer { get; private set; }
    public string? KnownTravellerNumber { get; private set; }
    public string TierCode { get; private set; } = string.Empty;
    public int PointsBalance { get; private set; }
    public int TierProgressPoints { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private Customer() { }

    public void UpdateProfile(
        string? givenName = null,
        string? surname = null,
        DateOnly? dateOfBirth = null,
        string? gender = null,
        string? nationality = null,
        string? preferredLanguage = null,
        string? phoneNumber = null,
        string? addressLine1 = null,
        string? addressLine2 = null,
        string? city = null,
        string? stateOrRegion = null,
        string? postalCode = null,
        string? countryCode = null,
        string? passportNumber = null,
        DateOnly? passportIssueDate = null,
        string? passportIssuer = null,
        string? knownTravellerNumber = null,
        string? tierCode = null,
        Guid? identityId = null,
        bool? isActive = null)
    {
        if (givenName is not null) GivenName = givenName;
        if (surname is not null) Surname = surname;
        if (dateOfBirth is not null) DateOfBirth = dateOfBirth;
        if (gender is not null) Gender = gender;
        if (nationality is not null) Nationality = nationality;
        if (preferredLanguage is not null) PreferredLanguage = preferredLanguage;
        if (phoneNumber is not null) PhoneNumber = phoneNumber;
        if (addressLine1 is not null) AddressLine1 = addressLine1;
        if (addressLine2 is not null) AddressLine2 = addressLine2;
        if (city is not null) City = city;
        if (stateOrRegion is not null) StateOrRegion = stateOrRegion;
        if (postalCode is not null) PostalCode = postalCode;
        if (countryCode is not null) CountryCode = countryCode;
        if (passportNumber is not null) PassportNumber = passportNumber;
        if (passportIssueDate is not null) PassportIssueDate = passportIssueDate;
        if (passportIssuer is not null) PassportIssuer = passportIssuer;
        if (knownTravellerNumber is not null) KnownTravellerNumber = knownTravellerNumber;
        if (tierCode is not null) TierCode = tierCode;
        if (identityId is not null) IdentityId = identityId;
        if (isActive is not null) IsActive = isActive.Value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DeductPoints(int points)
    {
        PointsBalance -= points;
        UpdatedAt = DateTime.UtcNow;
    }

    public void AddPoints(int points)
    {
        PointsBalance += points;
        UpdatedAt = DateTime.UtcNow;
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
        Guid? identityId = null,
        DateOnly? dateOfBirth = null,
        string? gender = null,
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
            IdentityId = identityId,
            GivenName = givenName,
            Surname = surname,
            DateOfBirth = dateOfBirth,
            Gender = gender,
            Nationality = nationality,
            PreferredLanguage = preferredLanguage,
            PhoneNumber = phoneNumber,
            TierCode = tierCode,
            PointsBalance = 0,
            TierProgressPoints = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Factory method for reconstituting an entity from a persistence store.
    /// </summary>
    public static Customer Reconstitute(
        Guid customerId,
        string loyaltyNumber,
        Guid? identityId,
        string givenName,
        string surname,
        DateOnly? dateOfBirth,
        string? gender,
        string? nationality,
        string preferredLanguage,
        string? phoneNumber,
        string? addressLine1,
        string? addressLine2,
        string? city,
        string? stateOrRegion,
        string? postalCode,
        string? countryCode,
        string? passportNumber,
        DateOnly? passportIssueDate,
        string? passportIssuer,
        string? knownTravellerNumber,
        string tierCode,
        int pointsBalance,
        int tierProgressPoints,
        bool isActive,
        DateTime createdAt,
        DateTime updatedAt)
    {
        return new Customer
        {
            CustomerId = customerId,
            LoyaltyNumber = loyaltyNumber,
            IdentityId = identityId,
            GivenName = givenName,
            Surname = surname,
            DateOfBirth = dateOfBirth,
            Gender = gender,
            Nationality = nationality,
            PreferredLanguage = preferredLanguage,
            PhoneNumber = phoneNumber,
            AddressLine1 = addressLine1,
            AddressLine2 = addressLine2,
            City = city,
            StateOrRegion = stateOrRegion,
            PostalCode = postalCode,
            CountryCode = countryCode,
            PassportNumber = passportNumber,
            PassportIssueDate = passportIssueDate,
            PassportIssuer = passportIssuer,
            KnownTravellerNumber = knownTravellerNumber,
            TierCode = tierCode,
            PointsBalance = pointsBalance,
            TierProgressPoints = tierProgressPoints,
            IsActive = isActive,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }
}
