using System.Text.RegularExpressions;

namespace ReservationSystem.Orchestration.Loyalty.Validation;

/// <summary>
/// Validates request fields for the Loyalty orchestration layer.
/// Returns a list of human-readable error messages; an empty list means valid.
/// </summary>
public static partial class LoyaltyValidator
{
    private const int MaxEmailLength = 254;
    private const int MinPasswordLength = 8;
    private const int MaxGivenNameLength = 100;
    private const int MaxSurnameLength = 100;
    private const int MaxPhoneNumberLength = 30;

    /// <summary>
    /// RFC 5321-compliant email: local@domain with at least one dot in the domain.
    /// </summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    /// <summary>
    /// BCP 47 language tag: two lowercase letters, hyphen, two uppercase letters (e.g. en-GB, fr-FR).
    /// </summary>
    [GeneratedRegex(@"^[a-z]{2}-[A-Z]{2}$")]
    private static partial Regex Bcp47Regex();

    /// <summary>
    /// ISO 3166-1 alpha-2 country code: two uppercase letters (e.g. GB, US).
    /// </summary>
    [GeneratedRegex(@"^[A-Z]{2}$")]
    private static partial Regex Alpha2Regex();

    /// <summary>
    /// Phone number: optional leading +, then digits, spaces, hyphens, or parentheses.
    /// </summary>
    [GeneratedRegex(@"^\+?[\d\s\-\(\)]+$")]
    private static partial Regex PhoneRegex();

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

        // Email
        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add("The 'email' field is required.");
        }
        else
        {
            if (email.Length > MaxEmailLength)
                errors.Add($"The 'email' field must not exceed {MaxEmailLength} characters.");

            if (!EmailRegex().IsMatch(email))
                errors.Add("The 'email' field must be a valid email address.");
        }

        // Password
        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add("The 'password' field is required.");
        }
        else
        {
            if (password.Length < MinPasswordLength)
                errors.Add($"The 'password' field must be at least {MinPasswordLength} characters long.");

            if (!password.Any(char.IsUpper))
                errors.Add("The 'password' field must contain at least one uppercase letter.");

            if (!password.Any(char.IsLower))
                errors.Add("The 'password' field must contain at least one lowercase letter.");

            if (!password.Any(char.IsDigit))
                errors.Add("The 'password' field must contain at least one digit.");

            if (!password.Any(c => !char.IsLetterOrDigit(c)))
                errors.Add("The 'password' field must contain at least one special character.");
        }

        // Given name
        if (string.IsNullOrWhiteSpace(givenName))
            errors.Add("The 'givenName' field is required.");
        else if (givenName.Length > MaxGivenNameLength)
            errors.Add($"The 'givenName' field must not exceed {MaxGivenNameLength} characters.");

        // Surname
        if (string.IsNullOrWhiteSpace(surname))
            errors.Add("The 'surname' field is required.");
        else if (surname.Length > MaxSurnameLength)
            errors.Add($"The 'surname' field must not exceed {MaxSurnameLength} characters.");

        // Date of birth
        if (dateOfBirth.HasValue && dateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            errors.Add("The 'dateOfBirth' field must not be a future date.");

        // Phone number (optional)
        if (phoneNumber is not null)
        {
            if (string.IsNullOrWhiteSpace(phoneNumber))
                errors.Add("The 'phoneNumber' field must not be empty when provided.");
            else if (phoneNumber.Length > MaxPhoneNumberLength)
                errors.Add($"The 'phoneNumber' field must not exceed {MaxPhoneNumberLength} characters.");
            else if (!PhoneRegex().IsMatch(phoneNumber))
                errors.Add("The 'phoneNumber' field must be a valid phone number (e.g. '+447700900123').");
        }

        // Preferred language (optional)
        if (preferredLanguage is not null && !Bcp47Regex().IsMatch(preferredLanguage))
            errors.Add("The 'preferredLanguage' field must be a valid BCP 47 language tag in the format xx-XX (e.g. 'en-GB').");

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

        if (dateOfBirth.HasValue && dateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            errors.Add("The 'dateOfBirth' field must not be a future date.");

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

        if (preferredLanguage is not null && !Bcp47Regex().IsMatch(preferredLanguage))
            errors.Add("The 'preferredLanguage' field must be a valid BCP 47 language tag in the format xx-XX (e.g. 'en-GB').");

        return errors;
    }
}
