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
using ReservationSystem.Orchestration.Loyalty.Models.Requests;
using ReservationSystem.Orchestration.Loyalty.Models.Responses;
using System.Net;

namespace ReservationSystem.Orchestration.Loyalty.Functions;

/// <summary>
/// HTTP-triggered functions for staff-facing loyalty customer management.
/// All routes require a valid staff JWT token with the "User" role claim,
/// enforced by <see cref="Middleware.StaffTokenMiddleware"/>.
/// Function names are prefixed with "Admin" to trigger staff middleware.
/// </summary>
public sealed class CustomerManagementFunction
{
    private readonly SearchCustomersHandler _searchHandler;
    private readonly GetTransactionsHandler _getTransactionsHandler;
    private readonly CustomerServiceClient _customerServiceClient;
    private readonly ILogger<CustomerManagementFunction> _logger;

    public CustomerManagementFunction(
        SearchCustomersHandler searchHandler,
        GetTransactionsHandler getTransactionsHandler,
        CustomerServiceClient customerServiceClient,
        ILogger<CustomerManagementFunction> logger)
    {
        _searchHandler = searchHandler;
        _getTransactionsHandler = getTransactionsHandler;
        _customerServiceClient = customerServiceClient;
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
            TierCode = customer.TierCode,
            PointsBalance = customer.PointsBalance,
            TierProgressPoints = customer.TierProgressPoints,
            IsActive = customer.IsActive,
            CreatedAt = customer.CreatedAt,
            UpdatedAt = customer.UpdatedAt,
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
}

/// <summary>Search request body for the admin customer search endpoint.</summary>
public sealed class AdminSearchRequest
{
    public string? Query { get; init; }
}
