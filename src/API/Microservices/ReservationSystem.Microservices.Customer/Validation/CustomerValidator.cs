using System.Text.RegularExpressions;

namespace ReservationSystem.Microservices.Customer.Validation;

/// <summary>
/// Validates customer profile fields for the Create and Update endpoints.
/// Returns a list of human-readable error messages; an empty list means valid.
/// </summary>
public static partial class CustomerValidator
{
    private const int MaxGivenNameLength = 100;
    private const int MaxSurnameLength = 100;
    private const int MaxPhoneNumberLength = 30;
    private const int MaxRedemptionReferenceLength = 100;
    private const int MaxDescriptionLength = 500;
    private const int MaxReasonLength = 500;
    private const int MaxBookingReferenceLength = 10;
    private const int MinSearchQueryLength = 2;
    private const int MaxSearchQueryLength = 100;

    private static readonly IReadOnlySet<string> ValidTransactionTypes =
        new HashSet<string>(StringComparer.Ordinal) { "Earn", "Redeem", "Adjustment", "Expiry", "Reinstate" };

    /// <summary>
    /// BCP 47 language tag: two lowercase letters, hyphen, two uppercase letters (e.g. en-GB, fr-FR).
    /// </summary>
    [GeneratedRegex(@"^[a-z]{2}-[A-Z]{2}$")]
    private static partial Regex Bcp47Regex();

    /// <summary>
    /// ISO 3166-1 alpha-2 country code: two uppercase letters (e.g. GB, US, NG).
    /// </summary>
    [GeneratedRegex(@"^[A-Z]{2}$")]
    private static partial Regex Alpha2Regex();

    /// <summary>
    /// Phone number: optional leading +, then digits, spaces, hyphens, or parentheses.
    /// </summary>
    [GeneratedRegex(@"^\+?[\d\s\-\(\)]+$")]
    private static partial Regex PhoneRegex();

    /// <summary>
    /// Validate fields for the POST /v1/customers (create) endpoint.
    /// </summary>
    public static List<string> ValidateCreate(
        string? givenName,
        string? surname,
        string? preferredLanguage,
        DateOnly? dateOfBirth)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(givenName))
            errors.Add("The 'givenName' field is required.");
        else if (givenName.Length > MaxGivenNameLength)
            errors.Add($"The 'givenName' field must not exceed {MaxGivenNameLength} characters.");

        if (string.IsNullOrWhiteSpace(surname))
            errors.Add("The 'surname' field is required.");
        else if (surname.Length > MaxSurnameLength)
            errors.Add($"The 'surname' field must not exceed {MaxSurnameLength} characters.");

        if (string.IsNullOrWhiteSpace(preferredLanguage))
            errors.Add("The 'preferredLanguage' field is required.");
        else if (!Bcp47Regex().IsMatch(preferredLanguage))
            errors.Add("The 'preferredLanguage' field must be a valid BCP 47 language tag in the format xx-XX (e.g. 'en-GB').");

        if (dateOfBirth.HasValue && dateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            errors.Add("The 'dateOfBirth' field must not be a future date.");

        return errors;
    }

    /// <summary>
    /// Validate fields for the PATCH /v1/customers/{loyaltyNumber} (update) endpoint.
    /// Only supplied (non-null) fields are validated.
    /// </summary>
    public static List<string> ValidateUpdate(
        string? givenName,
        string? surname,
        string? preferredLanguage,
        string? nationality,
        string? phoneNumber,
        DateOnly? dateOfBirth)
    {
        var errors = new List<string>();

        if (givenName is not null)
        {
            if (string.IsNullOrWhiteSpace(givenName))
                errors.Add("The 'givenName' field must not be empty when provided.");
            else if (givenName.Length > MaxGivenNameLength)
                errors.Add($"The 'givenName' field must not exceed {MaxGivenNameLength} characters.");
        }

        if (surname is not null)
        {
            if (string.IsNullOrWhiteSpace(surname))
                errors.Add("The 'surname' field must not be empty when provided.");
            else if (surname.Length > MaxSurnameLength)
                errors.Add($"The 'surname' field must not exceed {MaxSurnameLength} characters.");
        }

        if (preferredLanguage is not null && !Bcp47Regex().IsMatch(preferredLanguage))
            errors.Add("The 'preferredLanguage' field must be a valid BCP 47 language tag in the format xx-XX (e.g. 'en-GB').");

        if (nationality is not null && !Alpha2Regex().IsMatch(nationality))
            errors.Add("The 'nationality' field must be a valid ISO 3166-1 alpha-2 country code (e.g. 'GB').");

        if (phoneNumber is not null)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                errors.Add("The 'phoneNumber' field must not be empty when provided.");
            else if (phoneNumber.Length > MaxPhoneNumberLength)
                errors.Add($"The 'phoneNumber' field must not exceed {MaxPhoneNumberLength} characters.");
            else if (!PhoneRegex().IsMatch(phoneNumber))
                errors.Add("The 'phoneNumber' field must be a valid phone number (e.g. '+447700900123').");
        }

        if (dateOfBirth.HasValue && dateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            errors.Add("The 'dateOfBirth' field must not be a future date.");

        return errors;
    }

    /// <summary>
    /// Validate fields for POST /v1/customers/{loyaltyNumber}/points/authorise.
    /// </summary>
    public static List<string> ValidateAuthorisePoints(int points, Guid basketId)
    {
        var errors = new List<string>();

        if (points <= 0)
            errors.Add("The 'points' field must be a positive integer.");

        if (basketId == Guid.Empty)
            errors.Add("The 'basketId' field must be a valid non-empty GUID.");

        return errors;
    }

    /// <summary>
    /// Validate fields for POST /v1/customers/{loyaltyNumber}/points/settle.
    /// </summary>
    public static List<string> ValidateSettlePoints(string? redemptionReference)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(redemptionReference))
            errors.Add("The 'redemptionReference' field is required.");
        else if (redemptionReference.Length > MaxRedemptionReferenceLength)
            errors.Add($"The 'redemptionReference' field must not exceed {MaxRedemptionReferenceLength} characters.");

        return errors;
    }

    /// <summary>
    /// Validate fields for POST /v1/customers/{loyaltyNumber}/points/reverse.
    /// </summary>
    public static List<string> ValidateReversePoints(string? redemptionReference)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(redemptionReference))
            errors.Add("The 'redemptionReference' field is required.");
        else if (redemptionReference.Length > MaxRedemptionReferenceLength)
            errors.Add($"The 'redemptionReference' field must not exceed {MaxRedemptionReferenceLength} characters.");

        return errors;
    }

    /// <summary>
    /// Validate fields for POST /v1/customers/{loyaltyNumber}/points/reinstate.
    /// </summary>
    public static List<string> ValidateReinstatePoints(int points, string? bookingReference, string? reason)
    {
        var errors = new List<string>();

        if (points <= 0)
            errors.Add("The 'points' field must be a positive integer.");

        if (string.IsNullOrWhiteSpace(bookingReference))
            errors.Add("The 'bookingReference' field is required.");
        else if (bookingReference.Length > MaxBookingReferenceLength)
            errors.Add($"The 'bookingReference' field must not exceed {MaxBookingReferenceLength} characters.");

        if (string.IsNullOrWhiteSpace(reason))
            errors.Add("The 'reason' field is required.");
        else if (reason.Length > MaxReasonLength)
            errors.Add($"The 'reason' field must not exceed {MaxReasonLength} characters.");

        return errors;
    }

    /// <summary>
    /// Validate fields for POST /v1/customers/{loyaltyNumber}/points/add.
    /// </summary>
    public static List<string> ValidateAddPoints(int points, string? transactionType, string? description)
    {
        var errors = new List<string>();

        if (points == 0)
            errors.Add("The 'points' field must not be zero.");

        if (string.IsNullOrWhiteSpace(transactionType))
        {
            errors.Add("The 'transactionType' field is required.");
        }
        else if (!ValidTransactionTypes.Contains(transactionType))
        {
            errors.Add($"The 'transactionType' field must be one of: {string.Join(", ", ValidTransactionTypes)}.");
        }

        if (string.IsNullOrWhiteSpace(description))
            errors.Add("The 'description' field is required.");
        else if (description.Length > MaxDescriptionLength)
            errors.Add($"The 'description' field must not exceed {MaxDescriptionLength} characters.");

        return errors;
    }

    /// <summary>
    /// Validate fields for POST /v1/customers/search.
    /// </summary>
    public static List<string> ValidateSearch(string? query)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(query))
        {
            errors.Add("The 'query' field is required.");
        }
        else
        {
            if (query.Trim().Length < MinSearchQueryLength)
                errors.Add($"The 'query' field must be at least {MinSearchQueryLength} characters.");

            if (query.Length > MaxSearchQueryLength)
                errors.Add($"The 'query' field must not exceed {MaxSearchQueryLength} characters.");
        }

        return errors;
    }
}
