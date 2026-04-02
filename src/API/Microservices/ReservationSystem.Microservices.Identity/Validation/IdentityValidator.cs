using ReservationSystem.Shared.Business.Validation;

namespace ReservationSystem.Microservices.Identity.Validation;

/// <summary>
/// Validates identity fields for account and authentication endpoints.
/// Returns a list of human-readable error messages; an empty list means valid.
/// </summary>
public static class IdentityValidator
{
    /// <summary>
    /// Validate fields for the POST /v1/accounts (create account) endpoint.
    /// </summary>
    public static List<string> ValidateCreateAccount(string? email, string? password)
    {
        var errors = new List<string>();
        CommonFieldValidator.ValidateEmail(email, errors);
        CommonFieldValidator.ValidatePassword(password, errors);
        return errors;
    }

    /// <summary>
    /// Validate fields for the POST /v1/auth/login endpoint.
    /// </summary>
    public static List<string> ValidateLogin(string? email, string? password)
    {
        var errors = new List<string>();
        CommonFieldValidator.ValidateEmail(email, errors);

        if (string.IsNullOrWhiteSpace(password))
            errors.Add("The 'password' field is required.");

        return errors;
    }

    /// <summary>
    /// Validate the email field for POST /v1/auth/password/reset-request
    /// and POST /v1/accounts/{id}/email/change-request.
    /// </summary>
    public static List<string> ValidateEmailField(string? email)
    {
        var errors = new List<string>();
        CommonFieldValidator.ValidateEmail(email, errors);
        return errors;
    }

    /// <summary>
    /// Validate fields for the POST /v1/auth/password/reset endpoint.
    /// </summary>
    public static List<string> ValidateResetPassword(string? token, string? newPassword)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(token))
            errors.Add("The 'token' field is required.");

        CommonFieldValidator.ValidatePassword(newPassword, errors, fieldName: "newPassword");

        return errors;
    }

    /// <summary>
    /// Validate the password field for the POST /v1/accounts/{id}/set-password endpoint.
    /// </summary>
    public static List<string> ValidatePasswordField(string? password)
    {
        var errors = new List<string>();
        CommonFieldValidator.ValidatePassword(password, errors, fieldName: "newPassword");
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
}
