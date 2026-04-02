using System.Text.RegularExpressions;

namespace ReservationSystem.Shared.Business.Validation;

/// <summary>
/// Shared field-level validation helpers for common passenger and user profile
/// fields that appear across the Identity, Customer, and Loyalty services.
///
/// Each method appends human-readable error messages to the supplied list so
/// callers can compose multiple checks without allocating intermediate lists:
/// <code>
///   var errors = new List&lt;string&gt;();
///   CommonFieldValidator.ValidateEmail(email, errors);
///   CommonFieldValidator.ValidatePassword(password, errors);
///   if (errors.Count > 0) return await req.BadRequestAsync(string.Join(" ", errors));
/// </code>
/// </summary>
public static partial class CommonFieldValidator
{
    // ── Constants ──────────────────────────────────────────────────────────────

    /// <summary>RFC 5321 maximum email address length.</summary>
    public const int MaxEmailLength = 254;

    /// <summary>Minimum required password length.</summary>
    public const int MinPasswordLength = 8;

    /// <summary>Maximum length for given name and surname fields.</summary>
    public const int MaxNameLength = 100;

    /// <summary>Maximum length for phone number fields.</summary>
    public const int MaxPhoneNumberLength = 30;

    // ── Compiled regex ─────────────────────────────────────────────────────────

    /// <summary>RFC 5321-compliant email: local@domain with at least one dot in the domain.</summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    /// <summary>BCP 47 language tag: two lowercase letters, hyphen, two uppercase letters (e.g. en-GB).</summary>
    [GeneratedRegex(@"^[a-z]{2}-[A-Z]{2}$")]
    private static partial Regex Bcp47Regex();

    /// <summary>ISO 3166-1 alpha-2 country code: two uppercase letters (e.g. GB, US).</summary>
    [GeneratedRegex(@"^[A-Z]{2}$")]
    private static partial Regex Alpha2Regex();

    /// <summary>Phone number: optional leading +, then digits, spaces, hyphens, or parentheses.</summary>
    [GeneratedRegex(@"^\+?[\d\s\-\(\)]+$")]
    private static partial Regex PhoneRegex();

    // ── Validation methods ─────────────────────────────────────────────────────

    /// <summary>
    /// Required email address: must be present, ≤ 254 chars, and match RFC 5321 format.
    /// </summary>
    public static void ValidateEmail(string? email, List<string> errors, string fieldName = "email")
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            errors.Add($"The '{fieldName}' field is required.");
        }
        else
        {
            if (email.Length > MaxEmailLength)
                errors.Add($"The '{fieldName}' field must not exceed {MaxEmailLength} characters.");

            if (!EmailRegex().IsMatch(email))
                errors.Add($"The '{fieldName}' field must be a valid email address.");
        }
    }

    /// <summary>
    /// Required password: must be present, ≥ 8 chars, and contain upper, lower, digit, and special character.
    /// </summary>
    public static void ValidatePassword(string? password, List<string> errors, string fieldName = "password")
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            errors.Add($"The '{fieldName}' field is required.");
        }
        else
        {
            if (password.Length < MinPasswordLength)
                errors.Add($"The '{fieldName}' field must be at least {MinPasswordLength} characters long.");

            if (!password.Any(char.IsUpper))
                errors.Add($"The '{fieldName}' field must contain at least one uppercase letter.");

            if (!password.Any(char.IsLower))
                errors.Add($"The '{fieldName}' field must contain at least one lowercase letter.");

            if (!password.Any(char.IsDigit))
                errors.Add($"The '{fieldName}' field must contain at least one digit.");

            if (!password.Any(c => !char.IsLetterOrDigit(c)))
                errors.Add($"The '{fieldName}' field must contain at least one special character.");
        }
    }

    /// <summary>
    /// Required name field (given name or surname): must be present and ≤ <paramref name="maxLength"/> chars.
    /// </summary>
    public static void ValidateRequiredName(string? name, List<string> errors, string fieldName, int maxLength = MaxNameLength)
    {
        if (string.IsNullOrWhiteSpace(name))
            errors.Add($"The '{fieldName}' field is required.");
        else if (name.Length > maxLength)
            errors.Add($"The '{fieldName}' field must not exceed {maxLength} characters.");
    }

    /// <summary>
    /// Optional name field: when provided, must not be blank and must be ≤ <paramref name="maxLength"/> chars.
    /// </summary>
    public static void ValidateOptionalName(string? name, List<string> errors, string fieldName, int maxLength = MaxNameLength)
    {
        if (name is null) return;

        if (string.IsNullOrWhiteSpace(name))
            errors.Add($"The '{fieldName}' field must not be empty when provided.");
        else if (name.Length > maxLength)
            errors.Add($"The '{fieldName}' field must not exceed {maxLength} characters.");
    }

    /// <summary>
    /// Date of birth: when provided, must not be in the future.
    /// </summary>
    public static void ValidateDateOfBirthNotFuture(DateOnly? dateOfBirth, List<string> errors, string fieldName = "dateOfBirth")
    {
        if (dateOfBirth.HasValue && dateOfBirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            errors.Add($"The '{fieldName}' field must not be a future date.");
    }

    /// <summary>
    /// Optional phone number: when provided, must not be blank, ≤ 30 chars, and match the expected format.
    /// </summary>
    public static void ValidateOptionalPhoneNumber(string? phoneNumber, List<string> errors, string fieldName = "phoneNumber")
    {
        if (phoneNumber is null) return;

        if (string.IsNullOrWhiteSpace(phoneNumber))
            errors.Add($"The '{fieldName}' field must not be empty when provided.");
        else if (phoneNumber.Length > MaxPhoneNumberLength)
            errors.Add($"The '{fieldName}' field must not exceed {MaxPhoneNumberLength} characters.");
        else if (!PhoneRegex().IsMatch(phoneNumber))
            errors.Add($"The '{fieldName}' field must be a valid phone number (e.g. '+447700900123').");
    }

    /// <summary>
    /// Required BCP 47 language tag: must be present and match xx-XX format (e.g. en-GB).
    /// </summary>
    public static void ValidateRequiredLanguageTag(string? language, List<string> errors, string fieldName = "preferredLanguage")
    {
        if (string.IsNullOrWhiteSpace(language))
            errors.Add($"The '{fieldName}' field is required.");
        else if (!Bcp47Regex().IsMatch(language))
            errors.Add($"The '{fieldName}' field must be a valid BCP 47 language tag in the format xx-XX (e.g. 'en-GB').");
    }

    /// <summary>
    /// Optional BCP 47 language tag: when provided, must match xx-XX format (e.g. en-GB).
    /// </summary>
    public static void ValidateOptionalLanguageTag(string? language, List<string> errors, string fieldName = "preferredLanguage")
    {
        if (language is not null && !Bcp47Regex().IsMatch(language))
            errors.Add($"The '{fieldName}' field must be a valid BCP 47 language tag in the format xx-XX (e.g. 'en-GB').");
    }

    /// <summary>
    /// Optional ISO 3166-1 alpha-2 country code: when provided, must be two uppercase letters (e.g. GB).
    /// </summary>
    public static void ValidateOptionalCountryCode(string? code, List<string> errors, string fieldName = "nationality")
    {
        if (code is not null && !Alpha2Regex().IsMatch(code))
            errors.Add($"The '{fieldName}' field must be a valid ISO 3166-1 alpha-2 country code (e.g. 'GB').");
    }
}
