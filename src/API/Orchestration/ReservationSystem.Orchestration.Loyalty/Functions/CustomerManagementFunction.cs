using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Models;
using ReservationSystem.Orchestration.Loyalty.Application.SearchCustomers;
using ReservationSystem.Orchestration.Loyalty.Application.GetTransactions;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Loyalty.Infrastructure.ExternalServices.Dto;
using ReservationSystem.Orchestration.Loyalty.Models.Requests;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;
using System.Net;
using System.Text.Json;

namespace ReservationSystem.Orchestration.Loyalty.Functions;

/// <summary>
/// HTTP-triggered functions for staff-facing loyalty customer management.
/// All routes require a valid staff JWT token with the "User" role claim,
/// enforced by <see cref="ReservationSystem.Shared.Business.Middleware.TerminalAuthenticationMiddleware"/>.
/// Function names are prefixed with "Admin" to trigger staff middleware.
/// </summary>
public sealed class CustomerManagementFunction
{
    private readonly SearchCustomersHandler _searchHandler;
    private readonly GetTransactionsHandler _getTransactionsHandler;
    private readonly CustomerServiceClient _customerServiceClient;
    private readonly IdentityServiceClient _identityServiceClient;
    private readonly ILogger<CustomerManagementFunction> _logger;

    public CustomerManagementFunction(
        SearchCustomersHandler searchHandler,
        GetTransactionsHandler getTransactionsHandler,
        CustomerServiceClient customerServiceClient,
        IdentityServiceClient identityServiceClient,
        ILogger<CustomerManagementFunction> logger)
    {
        _searchHandler = searchHandler;
        _getTransactionsHandler = getTransactionsHandler;
        _customerServiceClient = customerServiceClient;
        _identityServiceClient = identityServiceClient;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/customers/search
    // -------------------------------------------------------------------------

    [Function("AdminSearchCustomers")]
    [OpenApiOperation(operationId: "AdminSearchCustomers", tags: new[] { "Admin Customers" }, Summary = "Search loyalty customers (staff)")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminSearchRequest), Required = false, Description = "Optional search query")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(IReadOnlyList<CustomerSummaryResponse>), Description = "OK")]
    public async Task<HttpResponseData> SearchCustomers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/customers/search")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        string? query = null;

        var (body, _) = await req.TryDeserializeBodyAsync<AdminSearchRequest>(_logger, cancellationToken);
        if (body is not null)
            query = body.Query;

        var result = await _searchHandler.HandleAsync(new SearchCustomersQuery(query), cancellationToken);
        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/customers/{loyaltyNumber}
    // -------------------------------------------------------------------------

    [Function("AdminGetCustomer")]
    [OpenApiOperation(operationId: "AdminGetCustomer", tags: new[] { "Admin Customers" }, Summary = "Get customer details (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AdminCustomerResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetCustomer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/customers/{loyaltyNumber}")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var customer = await _customerServiceClient.GetCustomerAsync(loyaltyNumber, cancellationToken);

        if (customer is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        AdminIdentityResponse? identity = null;
        if (customer.IdentityId.HasValue)
        {
            var identityAccount = await _identityServiceClient.GetAccountByIdAsync(customer.IdentityId.Value, cancellationToken);
            if (identityAccount is not null)
            {
                identity = new AdminIdentityResponse
                {
                    UserAccountId = identityAccount.UserAccountId,
                    Email = identityAccount.Email,
                    IsEmailVerified = identityAccount.IsEmailVerified,
                    IsLocked = identityAccount.IsLocked,
                    FailedLoginAttempts = identityAccount.FailedLoginAttempts,
                    LastLoginAt = identityAccount.LastLoginAt,
                    PasswordChangedAt = identityAccount.PasswordChangedAt,
                    CreatedAt = identityAccount.CreatedAt,
                    UpdatedAt = identityAccount.UpdatedAt,
                };
            }
        }

        return await req.OkJsonAsync(new AdminCustomerResponse
        {
            LoyaltyNumber = customer.LoyaltyNumber,
            GivenName = customer.GivenName,
            Surname = customer.Surname,
            DateOfBirth = customer.DateOfBirth,
            Gender = customer.Gender,
            Nationality = customer.Nationality,
            PreferredLanguage = customer.PreferredLanguage,
            PhoneNumber = customer.PhoneNumber,
            AddressLine1 = customer.AddressLine1,
            AddressLine2 = customer.AddressLine2,
            City = customer.City,
            StateOrRegion = customer.StateOrRegion,
            PostalCode = customer.PostalCode,
            CountryCode = customer.CountryCode,
            PassportNumber = customer.PassportNumber,
            PassportIssueDate = customer.PassportIssueDate,
            PassportIssuer = customer.PassportIssuer,
            PassportExpiryDate = customer.PassportExpiryDate,
            KnownTravellerNumber = customer.KnownTravellerNumber,
            TierCode = customer.TierCode,
            PointsBalance = customer.PointsBalance,
            TierProgressPoints = customer.TierProgressPoints,
            IsActive = customer.IsActive,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt,
            Identity = identity,
        });
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/admin/customers/{loyaltyNumber}
    // -------------------------------------------------------------------------

    [Function("AdminUpdateCustomer")]
    [OpenApiOperation(operationId: "AdminUpdateCustomer", tags: new[] { "Admin Customers" }, Summary = "Update customer details (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminUpdateCustomerRequest), Required = true)]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Updated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateCustomer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/customers/{loyaltyNumber}")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminUpdateCustomerRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        bool found;

        try
        {
            found = await _customerServiceClient.UpdateCustomerAsync(loyaltyNumber, request!, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }

        if (!found)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/customers/{loyaltyNumber}/transactions
    // -------------------------------------------------------------------------

    [Function("AdminGetCustomerTransactions")]
    [OpenApiOperation(operationId: "AdminGetCustomerTransactions", tags: new[] { "Admin Customers" }, Summary = "Get customer transactions (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiParameter(name: "page", In = ParameterLocation.Query, Required = false, Type = typeof(int))]
    [OpenApiParameter(name: "pageSize", In = ParameterLocation.Query, Required = false, Type = typeof(int))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(TransactionsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetCustomerTransactions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/customers/{loyaltyNumber}/transactions")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var paged = PagedRequest.From(req);
        var result = await _getTransactionsHandler.HandleAsync(
            new GetTransactionsQuery(loyaltyNumber, paged.Page, paged.PageSize), cancellationToken);

        if (result is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/customers/{loyaltyNumber}/points
    // -------------------------------------------------------------------------

    [Function("AdminAddPoints")]
    [OpenApiOperation(operationId: "AdminAddPoints", tags: new[] { "Admin Customers" }, Summary = "Assign points to a customer account (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminAddPointsRequest), Required = true)]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Points assigned")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> AddPoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/customers/{loyaltyNumber}/points")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminAddPointsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request!.Points == 0)
            return await req.BadRequestAsync("Points must not be zero.");

        if (string.IsNullOrWhiteSpace(request.Description))
            return await req.BadRequestAsync("Description is required.");

        try
        {
            await _customerServiceClient.AddPointsAsync(
                loyaltyNumber, request.Points, "Adjustment", request.Description, cancellationToken);
        }
        catch (InvalidOperationException)
        {
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/admin/customers/{loyaltyNumber}
    // -------------------------------------------------------------------------

    [Function("AdminDeleteCustomer")]
    [OpenApiOperation(operationId: "AdminDeleteCustomer", tags: new[] { "Admin Customers" }, Summary = "Delete customer account and transactions (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DeleteCustomer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/customers/{loyaltyNumber}")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var found = await _customerServiceClient.DeleteCustomerAsync(loyaltyNumber, cancellationToken);

        if (!found)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/admin/customers/{loyaltyNumber}/identity
    // -------------------------------------------------------------------------

    [Function("AdminUpdateCustomerIdentity")]
    [OpenApiOperation(operationId: "AdminUpdateCustomerIdentity", tags: new[] { "Admin Customers" }, Summary = "Update identity account email or locked status (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminUpdateIdentityRequest), Required = true)]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Updated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Conflict, Description = "Conflict – email already registered")]
    public async Task<HttpResponseData> UpdateCustomerIdentity(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/customers/{loyaltyNumber}/identity")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminUpdateIdentityRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (request!.Email is null && request.IsLocked is null)
            return await req.BadRequestAsync("At least one of 'email' or 'isLocked' must be provided.");

        var customer = await _customerServiceClient.GetCustomerAsync(loyaltyNumber, cancellationToken);
        if (customer is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        if (!customer.IdentityId.HasValue)
            return await req.NotFoundAsync($"Customer '{loyaltyNumber}' has no linked identity account.");

        try
        {
            await _identityServiceClient.UpdateAccountAsync(
                customer.IdentityId.Value, request.Email, request.IsLocked, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"Identity account not found for customer '{loyaltyNumber}'.");
        }
        catch (InvalidOperationException)
        {
            return await req.ConflictAsync("The email address is already registered to another account.");
        }

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/customers/{loyaltyNumber}/identity/set-password
    // -------------------------------------------------------------------------

    [Function("AdminSetCustomerPassword")]
    [OpenApiOperation(operationId: "AdminSetCustomerPassword", tags: new[] { "Admin Customers" }, Summary = "Set a password on a customer identity account (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminSetPasswordRequest), Required = true)]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Password set")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> SetCustomerPassword(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/customers/{loyaltyNumber}/identity/set-password")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminSetPasswordRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.NewPassword))
            return await req.BadRequestAsync("The 'newPassword' field is required.");

        var customer = await _customerServiceClient.GetCustomerAsync(loyaltyNumber, cancellationToken);
        if (customer is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        if (!customer.IdentityId.HasValue)
            return await req.NotFoundAsync($"Customer '{loyaltyNumber}' has no linked identity account.");

        try
        {
            await _identityServiceClient.SetPasswordAsync(customer.IdentityId.Value, request.NewPassword, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"Identity account not found for customer '{loyaltyNumber}'.");
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/customers/{loyaltyNumber}/identity/verify-email
    // -------------------------------------------------------------------------

    [Function("AdminVerifyCustomerEmail")]
    [OpenApiOperation(operationId: "AdminVerifyCustomerEmail", tags: new[] { "Admin Customers" }, Summary = "Mark a customer email address as verified (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Email marked as verified")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> VerifyCustomerEmail(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/customers/{loyaltyNumber}/identity/verify-email")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var customer = await _customerServiceClient.GetCustomerAsync(loyaltyNumber, cancellationToken);
        if (customer is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        if (!customer.IdentityId.HasValue)
            return await req.NotFoundAsync($"Customer '{loyaltyNumber}' has no linked identity account.");

        try
        {
            await _identityServiceClient.VerifyEmailAsync(customer.IdentityId.Value, cancellationToken);
        }
        catch (KeyNotFoundException)
        {
            return await req.NotFoundAsync($"Identity account not found for customer '{loyaltyNumber}'.");
        }

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/customers/{loyaltyNumber}/orders
    // -------------------------------------------------------------------------

    [Function("AdminGetCustomerOrders")]
    [OpenApiOperation(operationId: "AdminGetCustomerOrders", tags: new[] { "Admin Customers" }, Summary = "Get orders linked to a customer loyalty account (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AdminCustomerOrdersResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetCustomerOrders(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/customers/{loyaltyNumber}/orders")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var result = await _customerServiceClient.GetCustomerOrdersAsync(loyaltyNumber, cancellationToken);

        if (result is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return await req.OkJsonAsync(new AdminCustomerOrdersResponse
        {
            LoyaltyNumber = result.LoyaltyNumber,
            Orders = result.Orders.Select(o => new AdminCustomerOrderItem
            {
                CustomerOrderId = o.CustomerOrderId,
                OrderId = o.OrderId,
                BookingReference = o.BookingReference,
                CreatedAt = o.CreatedAt,
            }).ToList()
        });
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/admin/customers/{loyaltyNumber}/status
    // -------------------------------------------------------------------------

    [Function("AdminSetAccountStatus")]
    [OpenApiOperation(operationId: "AdminSetAccountStatus", tags: new[] { "Admin Customers" }, Summary = "Activate or deactivate a customer account (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminSetAccountStatusRequest), Required = true)]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Status updated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> SetAccountStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/admin/customers/{loyaltyNumber}/status")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminSetAccountStatusRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var found = await _customerServiceClient.UpdateCustomerAsync(
            loyaltyNumber, new { isActive = request!.IsActive }, cancellationToken);

        if (!found)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // GET /v1/admin/customers/{loyaltyNumber}/notes
    // -------------------------------------------------------------------------

    [Function("AdminGetCustomerNotes")]
    [OpenApiOperation(operationId: "AdminGetCustomerNotes", tags: new[] { "Admin Customers" }, Summary = "Get contact-centre notes for a customer (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AdminCustomerNotesResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetCustomerNotes(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/customers/{loyaltyNumber}/notes")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var result = await _customerServiceClient.GetNotesAsync(loyaltyNumber, cancellationToken);

        if (result is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return await req.OkJsonAsync(new AdminCustomerNotesResponse
        {
            LoyaltyNumber = result.LoyaltyNumber,
            Notes = result.Notes.Select(n => new AdminCustomerNoteItem
            {
                NoteId = n.NoteId,
                NoteText = n.NoteText,
                CreatedBy = n.CreatedBy,
                CreatedAt = n.CreatedAt,
                UpdatedAt = n.UpdatedAt,
            }).ToList()
        });
    }

    // -------------------------------------------------------------------------
    // POST /v1/admin/customers/{loyaltyNumber}/notes
    // -------------------------------------------------------------------------

    [Function("AdminAddCustomerNote")]
    [OpenApiOperation(operationId: "AdminAddCustomerNote", tags: new[] { "Admin Customers" }, Summary = "Add a contact-centre note to a customer (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminAddNoteRequest), Required = true)]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(AdminCustomerNoteItem), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> AddCustomerNote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/admin/customers/{loyaltyNumber}/notes")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminAddNoteRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.NoteText))
            return await req.BadRequestAsync("noteText is required.");

        var createdBy = ExtractUsername(req);

        CustomerNoteDto? note;
        try
        {
            note = await _customerServiceClient.AddNoteAsync(loyaltyNumber, request.NoteText, createdBy, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }

        if (note is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        var item = new AdminCustomerNoteItem
        {
            NoteId = note.NoteId,
            NoteText = note.NoteText,
            CreatedBy = note.CreatedBy,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt,
        };

        return await req.CreatedAsync($"/v1/admin/customers/{loyaltyNumber}/notes/{note.NoteId}", item);
    }

    // -------------------------------------------------------------------------
    // PUT /v1/admin/customers/{loyaltyNumber}/notes/{noteId}
    // -------------------------------------------------------------------------

    [Function("AdminUpdateCustomerNote")]
    [OpenApiOperation(operationId: "AdminUpdateCustomerNote", tags: new[] { "Admin Customers" }, Summary = "Update a contact-centre note (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiParameter(name: "noteId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AdminUpdateNoteRequest), Required = true)]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Updated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdateCustomerNote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/admin/customers/{loyaltyNumber}/notes/{noteId:guid}")] HttpRequestData req,
        string loyaltyNumber,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AdminUpdateNoteRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        if (string.IsNullOrWhiteSpace(request!.NoteText))
            return await req.BadRequestAsync("noteText is required.");

        bool updated;
        try
        {
            updated = await _customerServiceClient.UpdateNoteAsync(loyaltyNumber, noteId, request.NoteText, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return await req.BadRequestAsync(ex.Message);
        }

        if (!updated)
            return await req.NotFoundAsync($"Note '{noteId}' not found for customer '{loyaltyNumber}'.");

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/admin/customers/{loyaltyNumber}/notes/{noteId}
    // -------------------------------------------------------------------------

    [Function("AdminDeleteCustomerNote")]
    [OpenApiOperation(operationId: "AdminDeleteCustomerNote", tags: new[] { "Admin Customers" }, Summary = "Delete a contact-centre note (staff)")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string))]
    [OpenApiParameter(name: "noteId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid))]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> DeleteCustomerNote(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/admin/customers/{loyaltyNumber}/notes/{noteId:guid}")] HttpRequestData req,
        string loyaltyNumber,
        Guid noteId,
        CancellationToken cancellationToken)
    {
        var deleted = await _customerServiceClient.DeleteNoteAsync(loyaltyNumber, noteId, cancellationToken);

        if (!deleted)
            return await req.NotFoundAsync($"Note '{noteId}' not found for customer '{loyaltyNumber}'.");

        return req.NoContent();
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static string ExtractUsername(HttpRequestData req)
    {
        try
        {
            if (!req.Headers.TryGetValues("Authorization", out var values))
                return "Staff";

            var bearer = values.FirstOrDefault();
            if (bearer?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) != true)
                return "Staff";

            var token = bearer[7..];
            var parts = token.Split('.');
            if (parts.Length != 3)
                return "Staff";

            var padded = parts[1].Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("unique_name", out var uniqueName) && uniqueName.GetString() is { } un && !string.IsNullOrWhiteSpace(un))
                return un;

            if (root.TryGetProperty("sub", out var sub) && sub.GetString() is { } s && !string.IsNullOrWhiteSpace(s))
                return s;
        }
        catch { }

        return "Staff";
    }
}

/// <summary>Search request body for the admin customer search endpoint.</summary>
public sealed class AdminSearchRequest
{
    public string? Query { get; init; }
}
