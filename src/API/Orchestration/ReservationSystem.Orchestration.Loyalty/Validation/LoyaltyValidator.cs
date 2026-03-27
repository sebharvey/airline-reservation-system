using ReservationSystem.Shared.Common.Validation;

namespace ReservationSystem.Orchestration.Loyalty.Validation;

/// <summary>
/// Validates request fields for the Loyalty orchestration layer.
/// Returns a list of human-readable error messages; an empty list means valid.
/// </summary>
public static class LoyaltyValidator
{
    /// <summary>
    /// Validate fields for the POST /api/v1/register (member registration) endpoint.
    /// </summary>
    public static List<string> ValidateRegister(
        string? email,
        string? password,
        string? givenName,
        string? surname,
        DateOnly? dateOfBirth,
        string? phoneNumber,
        string? preferredLanguage)
    {
        var errors = new List<string>();
        CommonFieldValidator.ValidateEmail(email, errors);
        CommonFieldValidator.ValidatePassword(password, errors);
        CommonFieldValidator.ValidateRequiredName(givenName, errors, "givenName");
        CommonFieldValidator.ValidateRequiredName(surname, errors, "surname");
        CommonFieldValidator.ValidateDateOfBirthNotFuture(dateOfBirth, errors);
        CommonFieldValidator.ValidateOptionalPhoneNumber(phoneNumber, errors);
        CommonFieldValidator.ValidateOptionalLanguageTag(preferredLanguage, errors);
        return errors;
    }

    /// <summary>
    /// Validate fields for the POST /api/v1/customers/{loyaltyNumber}/points/transfer endpoint.
    /// </summary>
    public static List<string> ValidateTransferPoints(
        string? senderLoyaltyNumber,
        string? recipientLoyaltyNumber,
        string? recipientEmail,
        int points)
    {
        var errors = new List<string>();

        if (points <= 0)
            errors.Add("The 'points' field must be a positive integer.");

        if (string.IsNullOrWhiteSpace(recipientLoyaltyNumber))
            errors.Add("The 'recipientLoyaltyNumber' field is required.");
        else if (string.Equals(senderLoyaltyNumber, recipientLoyaltyNumber, StringComparison.OrdinalIgnoreCase))
            errors.Add("The sender and recipient loyalty numbers must be different.");

        CommonFieldValidator.ValidateEmail(recipientEmail, errors);

        return errors;
    }

    /// <summary>
    /// Validate fields for the PATCH /api/v1/customers/{loyaltyNumber}/profile (update profile) endpoint.
    /// Only supplied (non-null) fields are validated.
    /// </summary>
    public static List<string> ValidateUpdateProfile(
        string? givenName,
        string? surname,
        DateOnly? dateOfBirth,
        string? nationality,
        string? phoneNumber,
        string? preferredLanguage)
    {
        var errors = new List<string>();
        CommonFieldValidator.ValidateOptionalName(givenName, errors, "givenName");
        CommonFieldValidator.ValidateOptionalName(surname, errors, "surname");
        CommonFieldValidator.ValidateDateOfBirthNotFuture(dateOfBirth, errors);
        CommonFieldValidator.ValidateOptionalCountryCode(nationality, errors);
        CommonFieldValidator.ValidateOptionalPhoneNumber(phoneNumber, errors);
        CommonFieldValidator.ValidateOptionalLanguageTag(preferredLanguage, errors);
        return errors;
    }
}
