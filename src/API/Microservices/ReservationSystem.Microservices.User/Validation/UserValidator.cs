using ReservationSystem.Shared.Business.Validation;

namespace ReservationSystem.Microservices.User.Validation;

/// <summary>
/// Validates user fields for account creation and login endpoints.
/// Returns a list of human-readable error messages; an empty list means valid.
/// </summary>
public static class UserValidator
{
    /// <summary>
    /// Validate fields for POST /v1/users (add user).
    /// </summary>
    public static List<string> ValidateAddUser(
        string? username,
        string? email,
        string? password,
        string? firstName,
        string? lastName)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(username))
            errors.Add("The 'username' field is required.");
        else if (username.Length > 100)
            errors.Add("The 'username' field must not exceed 100 characters.");

        CommonFieldValidator.ValidateEmail(email, errors);
        CommonFieldValidator.ValidatePassword(password, errors);

        if (string.IsNullOrWhiteSpace(firstName))
            errors.Add("The 'firstName' field is required.");
        else if (firstName.Length > 100)
            errors.Add("The 'firstName' field must not exceed 100 characters.");

        if (string.IsNullOrWhiteSpace(lastName))
            errors.Add("The 'lastName' field is required.");
        else if (lastName.Length > 100)
            errors.Add("The 'lastName' field must not exceed 100 characters.");

        return errors;
    }

    /// <summary>
    /// Validate fields for POST /v1/users/login.
    /// </summary>
    public static List<string> ValidateLogin(string? username, string? password)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(username))
            errors.Add("The 'username' field is required.");

        if (string.IsNullOrWhiteSpace(password))
            errors.Add("The 'password' field is required.");

        return errors;
    }

    /// <summary>
    /// Validate fields for PATCH /v1/users/{userId} (update user).
    /// At least one field must be supplied.
    /// </summary>
    public static List<string> ValidateUpdateUser(
        string? firstName,
        string? lastName,
        string? email)
    {
        var errors = new List<string>();

        if (firstName is null && lastName is null && email is null)
        {
            errors.Add("At least one field must be supplied.");
            return errors;
        }

        if (firstName is not null && string.IsNullOrWhiteSpace(firstName))
            errors.Add("The 'firstName' field must not be empty when supplied.");
        else if (firstName is not null && firstName.Length > 100)
            errors.Add("The 'firstName' field must not exceed 100 characters.");

        if (lastName is not null && string.IsNullOrWhiteSpace(lastName))
            errors.Add("The 'lastName' field must not be empty when supplied.");
        else if (lastName is not null && lastName.Length > 100)
            errors.Add("The 'lastName' field must not exceed 100 characters.");

        if (email is not null)
            CommonFieldValidator.ValidateEmail(email, errors);

        return errors;
    }

    /// <summary>
    /// Validate fields for POST /v1/users/{userId}/reset-password.
    /// </summary>
    public static List<string> ValidateResetPassword(string? newPassword)
    {
        var errors = new List<string>();
        CommonFieldValidator.ValidatePassword(newPassword, errors);
        return errors;
    }
}
