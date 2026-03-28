using ReservationSystem.Shared.Common.Validation;

namespace ReservationSystem.Microservices.Customer.Validation;

/// <summary>
/// Validates customer profile fields for the Create and Update endpoints.
/// Returns a list of human-readable error messages; an empty list means valid.
/// </summary>
public static class CustomerValidator
{
    private const int MaxRedemptionReferenceLength = 100;
    private const int MaxDescriptionLength = 500;
    private const int MaxReasonLength = 500;
    private const int MaxBookingReferenceLength = 10;
    private const int MinSearchQueryLength = 2;
    private const int MaxSearchQueryLength = 100;

    private static readonly IReadOnlySet<string> ValidTransactionTypes =
        new HashSet<string>(StringComparer.Ordinal) { "Earn", "Redeem", "Adjustment", "Expiry", "Reinstate" };

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
        CommonFieldValidator.ValidateRequiredName(givenName, errors, "givenName");
        CommonFieldValidator.ValidateRequiredName(surname, errors, "surname");
        CommonFieldValidator.ValidateRequiredLanguageTag(preferredLanguage, errors);
        CommonFieldValidator.ValidateDateOfBirthNotFuture(dateOfBirth, errors);
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
        CommonFieldValidator.ValidateOptionalName(givenName, errors, "givenName");
        CommonFieldValidator.ValidateOptionalName(surname, errors, "surname");
        CommonFieldValidator.ValidateOptionalLanguageTag(preferredLanguage, errors);
        CommonFieldValidator.ValidateOptionalCountryCode(nationality, errors);
        CommonFieldValidator.ValidateOptionalPhoneNumber(phoneNumber, errors);
        CommonFieldValidator.ValidateDateOfBirthNotFuture(dateOfBirth, errors);
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

        if (!string.IsNullOrWhiteSpace(query))
        {
            if (query.Trim().Length < MinSearchQueryLength)
                errors.Add($"The 'query' field must be at least {MinSearchQueryLength} characters.");

            if (query.Length > MaxSearchQueryLength)
                errors.Add($"The 'query' field must not exceed {MaxSearchQueryLength} characters.");
        }

        return errors;
    }

    /// <summary>
    /// Validate fields for POST /v1/customers/{loyaltyNumber}/points/transfer.
    /// </summary>
    public static List<string> ValidateTransferPoints(
        string? senderLoyaltyNumber,
        string? recipientLoyaltyNumber,
        int points)
    {
        var errors = new List<string>();

        if (points <= 0)
            errors.Add("The 'points' field must be a positive integer.");

        if (string.IsNullOrWhiteSpace(recipientLoyaltyNumber))
            errors.Add("The 'recipientLoyaltyNumber' field is required.");
        else if (string.Equals(senderLoyaltyNumber, recipientLoyaltyNumber, StringComparison.OrdinalIgnoreCase))
            errors.Add("The sender and recipient loyalty numbers must be different.");

        return errors;
    }
}
