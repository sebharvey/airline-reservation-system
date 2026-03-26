using System.Text.RegularExpressions;

namespace ReservationSystem.Microservices.Identity.Validation;

/// <summary>
/// Validates identity fields for account and authentication endpoints.
/// Returns a list of human-readable error messages; an empty list means valid.
/// </summary>
public static partial class IdentityValidator
{
    private const int MaxEmailLength = 254;
    private const int MinPasswordLength = 8;

    /// <summary>
    /// RFC 5321-compliant email: local@domain with at least one dot in the domain.
    /// </summary>
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailRegex();

    /// <summary>
    /// Validate fields for the POST /v1/accounts (create account) endpoint.
    /// </summary>
    public static List<string> ValidateCreateAccount(string? email, string? password)
    {
        var errors = new List<string>();
        errors.AddRange(ValidateEmail(email));
        errors.AddRange(ValidatePassword(password));
        return errors;
    }

    /// <summary>
    /// Validate fields for the POST /v1/auth/login endpoint.
    /// </summary>
    public static List<string> ValidateLogin(string? email, string? password)
    {
        var errors = new List<string>();
        errors.AddRange(ValidateEmail(email));

        if (string.IsNullOrWhiteSpace(password))
            errors.Add("The 'password' field is required.");

        return errors;
    }

    /// <summary>
    /// Validate the email field for POST /v1/auth/password/reset-request
    /// and POST /v1/accounts/{id}/email/change-request.
    /// </summary>
    public static List<string> ValidateEmailField(string? email) => ValidateEmail(email);

    /// <summary>
    /// Validate fields for the POST /v1/auth/password/reset endpoint.
    /// </summary>
    public static List<string> ValidateResetPassword(string? token, string? newPassword)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(token))
            errors.Add("The 'token' field is required.");

        errors.AddRange(ValidatePassword(newPassword, fieldName: "newPassword"));

        return errors;
    }

    /// <summary>
    /// Validate that a required token field is present.
    /// Used for refresh, logout, and verify-email-change endpoints.
    /// </summary>
    public static List<string> ValidateRequiredToken(string? token, string fieldName)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(token))
            errors.Add($"The '{fieldName}' field is required.");

        return errors;
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private static List<string> ValidateEmail(string? email, string fieldName = "email")
    {
        var errors = new List<string>();

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

        return errors;
    }

    private static List<string> ValidatePassword(string? password, string fieldName = "password")
    {
        var errors = new List<string>();

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

        return errors;
    }
}
