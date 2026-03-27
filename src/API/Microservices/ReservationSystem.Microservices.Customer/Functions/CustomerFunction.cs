using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Models;
using ReservationSystem.Microservices.Customer.Application.AddPoints;
using ReservationSystem.Microservices.Customer.Domain.Entities;
using ReservationSystem.Microservices.Customer.Application.AuthorisePoints;
using ReservationSystem.Microservices.Customer.Application.CreateCustomer;
using ReservationSystem.Microservices.Customer.Application.DeleteCustomer;
using ReservationSystem.Microservices.Customer.Application.GetCustomer;
using ReservationSystem.Microservices.Customer.Application.GetTransactions;
using ReservationSystem.Microservices.Customer.Application.ReinstatePoints;
using ReservationSystem.Microservices.Customer.Application.ReversePoints;
using ReservationSystem.Microservices.Customer.Application.SettlePoints;
using ReservationSystem.Microservices.Customer.Application.SearchCustomers;
using ReservationSystem.Microservices.Customer.Application.TransferPoints;
using ReservationSystem.Microservices.Customer.Application.UpdateCustomer;
using ReservationSystem.Microservices.Customer.Application.GetPreferences;
using ReservationSystem.Microservices.Customer.Application.UpdatePreferences;
using ReservationSystem.Microservices.Customer.Models.Mappers;
using ReservationSystem.Microservices.Customer.Models.Requests;
using ReservationSystem.Microservices.Customer.Models.Responses;
using ReservationSystem.Microservices.Customer.Validation;
using System.Net;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.OpenApi.Models;

namespace ReservationSystem.Microservices.Customer.Functions;

/// <summary>
/// HTTP-triggered functions for the Customer resource.
/// Translates HTTP concerns into application-layer calls and back again.
/// </summary>
public sealed class CustomerFunction
{
    private readonly CreateCustomerHandler _createHandler;
    private readonly GetCustomerHandler _getHandler;
    private readonly UpdateCustomerHandler _updateHandler;
    private readonly DeleteCustomerHandler _deleteHandler;
    private readonly GetTransactionsHandler _getTransactionsHandler;
    private readonly AuthorisePointsHandler _authorisePointsHandler;
    private readonly SettlePointsHandler _settlePointsHandler;
    private readonly ReversePointsHandler _reversePointsHandler;
    private readonly ReinstatePointsHandler _reinstatePointsHandler;
    private readonly AddPointsHandler _addPointsHandler;
    private readonly SearchCustomersHandler _searchHandler;
    private readonly GetCustomerByIdentityHandler _getByIdentityHandler;
    private readonly TransferPointsHandler _transferPointsHandler;
    private readonly GetPreferencesHandler _getPreferencesHandler;
    private readonly UpdatePreferencesHandler _updatePreferencesHandler;
    private readonly ILogger<CustomerFunction> _logger;

    public CustomerFunction(
        CreateCustomerHandler createHandler,
        GetCustomerHandler getHandler,
        UpdateCustomerHandler updateHandler,
        DeleteCustomerHandler deleteHandler,
        GetTransactionsHandler getTransactionsHandler,
        AuthorisePointsHandler authorisePointsHandler,
        SettlePointsHandler settlePointsHandler,
        ReversePointsHandler reversePointsHandler,
        ReinstatePointsHandler reinstatePointsHandler,
        AddPointsHandler addPointsHandler,
        SearchCustomersHandler searchHandler,
        GetCustomerByIdentityHandler getByIdentityHandler,
        TransferPointsHandler transferPointsHandler,
        GetPreferencesHandler getPreferencesHandler,
        UpdatePreferencesHandler updatePreferencesHandler,
        ILogger<CustomerFunction> logger)
    {
        _createHandler = createHandler;
        _getHandler = getHandler;
        _updateHandler = updateHandler;
        _deleteHandler = deleteHandler;
        _getTransactionsHandler = getTransactionsHandler;
        _authorisePointsHandler = authorisePointsHandler;
        _settlePointsHandler = settlePointsHandler;
        _reversePointsHandler = reversePointsHandler;
        _reinstatePointsHandler = reinstatePointsHandler;
        _addPointsHandler = addPointsHandler;
        _searchHandler = searchHandler;
        _getByIdentityHandler = getByIdentityHandler;
        _transferPointsHandler = transferPointsHandler;
        _getPreferencesHandler = getPreferencesHandler;
        _updatePreferencesHandler = updatePreferencesHandler;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers
    // -------------------------------------------------------------------------

    [Function("CreateCustomer")]
    [OpenApiOperation(operationId: "CreateCustomer", tags: new[] { "Customers" }, Summary = "Create a new customer profile")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(CreateCustomerRequest), Required = true, Description = "Customer details")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.Created, contentType: "application/json", bodyType: typeof(CreateCustomerResponse), Description = "Created")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<CreateCustomerRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var validationErrors = CustomerValidator.ValidateCreate(
            request!.GivenName, request.Surname, request.PreferredLanguage, request.DateOfBirth);

        if (validationErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", validationErrors));

        var command = CustomerMapper.ToCommand(request);
        var customer = await _createHandler.HandleAsync(command, cancellationToken);
        var response = CustomerMapper.ToCreateResponse(customer);

        return await req.CreatedAsync($"/v1/customers/{customer.LoyaltyNumber}", response);
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/search  (POST keeps PII out of URL logs)
    // -------------------------------------------------------------------------

    [Function("SearchCustomers")]
    [OpenApiOperation(operationId: "SearchCustomers", tags: new[] { "Customers" }, Summary = "Search loyalty customers")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SearchCustomersRequest), Required = true, Description = "Search query — contains match on name, exact match on loyalty number (max 50 results)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(CustomerResponse[]), Description = "OK – always returns an array, empty if no matches found")]
    public async Task<HttpResponseData> Search(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/search")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<SearchCustomersRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var searchErrors = CustomerValidator.ValidateSearch(request!.Query);
        if (searchErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", searchErrors));

        var query = new SearchCustomersQuery(request.Query.Trim());
        var customers = await _searchHandler.HandleAsync(query, cancellationToken);
        var results = customers.Select(CustomerMapper.ToResponse).ToList();

        return await req.OkJsonAsync(results);
    }

    // -------------------------------------------------------------------------
    // GET /v1/customers/{loyaltyNumber}
    // -------------------------------------------------------------------------

    [Function("GetCustomer")]
    [OpenApiOperation(operationId: "GetCustomer", tags: new[] { "Customers" }, Summary = "Get a customer by loyalty number")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Customer loyalty number")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(CustomerResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetByLoyaltyNumber(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/customers/{loyaltyNumber}")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var query = new GetCustomerQuery(loyaltyNumber);
        var customer = await _getHandler.HandleAsync(query, cancellationToken);

        if (customer is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        var response = CustomerMapper.ToResponse(customer);
        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // GET /v1/customers/by-identity/{identityId}
    // -------------------------------------------------------------------------

    [Function("GetCustomerByIdentity")]
    [OpenApiOperation(operationId: "GetCustomerByIdentity", tags: new[] { "Customers" }, Summary = "Get a customer by identity ID")]
    [OpenApiParameter(name: "identityId", In = ParameterLocation.Path, Required = true, Type = typeof(Guid), Description = "Identity account ID")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(CustomerResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetByIdentityId(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/customers/by-identity/{identityId:guid}")] HttpRequestData req,
        Guid identityId,
        CancellationToken cancellationToken)
    {
        var query = new GetCustomerByIdentityQuery(identityId);
        var customer = await _getByIdentityHandler.HandleAsync(query, cancellationToken);

        if (customer is null)
            return await req.NotFoundAsync($"Customer not found for identity ID '{identityId}'.");

        var response = CustomerMapper.ToResponse(customer);
        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // PATCH /v1/customers/{loyaltyNumber}
    // -------------------------------------------------------------------------

    [Function("UpdateCustomer")]
    [OpenApiOperation(operationId: "UpdateCustomer", tags: new[] { "Customers" }, Summary = "Update a customer profile")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Customer loyalty number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdateCustomerRequest), Required = true, Description = "Customer update details")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(CustomerResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/customers/{loyaltyNumber}")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdateCustomerRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var validationErrors = CustomerValidator.ValidateUpdate(
            request!.GivenName, request.Surname, request.PreferredLanguage,
            request.Nationality, request.PhoneNumber, request.DateOfBirth);

        if (validationErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", validationErrors));

        var command = CustomerMapper.ToCommand(loyaltyNumber, request);

        Domain.Entities.Customer? customer;

        try
        {
            customer = await _updateHandler.HandleAsync(command, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Conflict updating customer {LoyaltyNumber}", loyaltyNumber);
            return await req.BadRequestAsync(ex.Message);
        }

        if (customer is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        var response = CustomerMapper.ToResponse(customer);
        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // GET /v1/customers/{loyaltyNumber}/transactions
    // -------------------------------------------------------------------------

    [Function("GetCustomerTransactions")]
    [OpenApiOperation(operationId: "GetCustomerTransactions", tags: new[] { "Points" }, Summary = "Get loyalty transactions for a customer")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Customer loyalty number")]
    [OpenApiParameter(name: "page", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page number (default 1)")]
    [OpenApiParameter(name: "pageSize", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page size (default 20)")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(TransactionsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetTransactions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/customers/{loyaltyNumber}/transactions")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var customerQuery = new GetCustomerQuery(loyaltyNumber);
        var customer = await _getHandler.HandleAsync(customerQuery, cancellationToken);

        if (customer is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        var paged = PagedRequest.From(req);
        var query = new GetTransactionsQuery(loyaltyNumber, paged.Page, paged.PageSize);
        var (transactions, totalCount) = await _getTransactionsHandler.HandleAsync(query, cancellationToken);
        var response = CustomerMapper.ToResponse(loyaltyNumber, transactions, paged.Page, paged.PageSize, totalCount);

        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/{loyaltyNumber}/points/authorise
    // -------------------------------------------------------------------------

    [Function("AuthorisePoints")]
    [OpenApiOperation(operationId: "AuthorisePoints", tags: new[] { "Points" }, Summary = "Authorise a points transaction")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Customer loyalty number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AuthorisePointsRequest), Required = true, Description = "Points authorisation request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AuthorisePointsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> AuthorisePoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/authorise")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AuthorisePointsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var authoriseErrors = CustomerValidator.ValidateAuthorisePoints(request!.Points, request.BasketId);
        if (authoriseErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", authoriseErrors));

        var command = CustomerMapper.ToCommand(loyaltyNumber, request);
        var transaction = await _authorisePointsHandler.HandleAsync(command, cancellationToken);

        if (transaction is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        var response = CustomerMapper.ToAuthoriseResponse(transaction);
        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/{loyaltyNumber}/points/settle
    // -------------------------------------------------------------------------

    [Function("SettlePoints")]
    [OpenApiOperation(operationId: "SettlePoints", tags: new[] { "Points" }, Summary = "Settle an authorised points transaction")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Customer loyalty number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(SettlePointsRequest), Required = true, Description = "Points settlement request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(SettlePointsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> SettlePoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/settle")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<SettlePointsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var settleErrors = CustomerValidator.ValidateSettlePoints(request!.RedemptionReference);
        if (settleErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", settleErrors));

        var command = CustomerMapper.ToCommand(loyaltyNumber, request);
        var transaction = await _settlePointsHandler.HandleAsync(command, cancellationToken);

        if (transaction is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        var response = CustomerMapper.ToSettleResponse(transaction);
        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/{loyaltyNumber}/points/reverse
    // -------------------------------------------------------------------------

    [Function("ReversePoints")]
    [OpenApiOperation(operationId: "ReversePoints", tags: new[] { "Points" }, Summary = "Reverse a points transaction")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Customer loyalty number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ReversePointsRequest), Required = true, Description = "Points reversal request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ReversePointsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> ReversePoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/reverse")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<ReversePointsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var reverseErrors = CustomerValidator.ValidateReversePoints(request!.RedemptionReference);
        if (reverseErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", reverseErrors));

        var command = CustomerMapper.ToCommand(loyaltyNumber, request);
        var transaction = await _reversePointsHandler.HandleAsync(command, cancellationToken);

        if (transaction is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        var response = CustomerMapper.ToReverseResponse(transaction);
        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/{loyaltyNumber}/points/reinstate
    // -------------------------------------------------------------------------

    [Function("ReinstatePoints")]
    [OpenApiOperation(operationId: "ReinstatePoints", tags: new[] { "Points" }, Summary = "Reinstate reversed points")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Customer loyalty number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(ReinstatePointsRequest), Required = true, Description = "Points reinstatement request")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ReinstatePointsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> ReinstatePoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/reinstate")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<ReinstatePointsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var reinstateErrors = CustomerValidator.ValidateReinstatePoints(request!.Points, request.BookingReference, request.Reason);
        if (reinstateErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", reinstateErrors));

        var command = CustomerMapper.ToCommand(loyaltyNumber, request);
        var transaction = await _reinstatePointsHandler.HandleAsync(command, cancellationToken);

        if (transaction is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        var response = CustomerMapper.ToReinstateResponse(loyaltyNumber, transaction);
        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/{loyaltyNumber}/points/add
    // -------------------------------------------------------------------------

    [Function("AddPoints")]
    [OpenApiOperation(operationId: "AddPoints", tags: new[] { "Points" }, Summary = "Add points to a customer loyalty account")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Customer loyalty number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(AddPointsRequest), Required = true, Description = "Points to add")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(AddPointsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> AddPoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/add")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<AddPointsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var addErrors = CustomerValidator.ValidateAddPoints(request!.Points, request.TransactionType, request.Description);
        if (addErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", addErrors));

        var command = CustomerMapper.ToCommand(loyaltyNumber, request);

        LoyaltyTransaction? transaction;

        try
        {
            transaction = await _addPointsHandler.HandleAsync(command, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid transaction type in AddPoints request for {LoyaltyNumber}", loyaltyNumber);
            return await req.BadRequestAsync(ex.Message);
        }

        if (transaction is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        var response = CustomerMapper.ToAddPointsResponse(loyaltyNumber, transaction);
        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/{loyaltyNumber}/points/transfer
    // -------------------------------------------------------------------------

    [Function("TransferPoints")]
    [OpenApiOperation(operationId: "TransferPoints", tags: new[] { "Points" }, Summary = "Transfer points from one loyalty account to another")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Sender's customer loyalty number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(TransferPointsRequest), Required = true, Description = "Transfer request — recipient loyalty number and points to transfer")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(TransferPointsResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Bad Request — validation failure or insufficient points")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found — sender or recipient not found")]
    public async Task<HttpResponseData> TransferPoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/transfer")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<TransferPointsRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var transferErrors = CustomerValidator.ValidateTransferPoints(
            loyaltyNumber, request!.RecipientLoyaltyNumber, request.Points);

        if (transferErrors.Count > 0)
            return await req.BadRequestAsync(string.Join(" ", transferErrors));

        var command = CustomerMapper.ToCommand(loyaltyNumber, request);

        TransferPointsResponse? result;

        try
        {
            result = await _transferPointsHandler.HandleAsync(command, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Insufficient points for transfer from {LoyaltyNumber}", loyaltyNumber);
            return await req.BadRequestAsync(ex.Message);
        }

        if (result is null)
            return await req.NotFoundAsync("Sender or recipient account not found.");

        return await req.OkJsonAsync(result);
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/customers/{loyaltyNumber}
    // -------------------------------------------------------------------------

    [Function("DeleteCustomer")]
    [OpenApiOperation(operationId: "DeleteCustomer", tags: new[] { "Customers" }, Summary = "Delete a customer")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Customer loyalty number")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Deleted")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> Delete(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "v1/customers/{loyaltyNumber}")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var command = new DeleteCustomerCommand(loyaltyNumber);
        var deleted = await _deleteHandler.HandleAsync(command, cancellationToken);

        if (!deleted)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return req.NoContent();
    }

    // -------------------------------------------------------------------------
    // GET /v1/customers/{loyaltyNumber}/preferences
    // -------------------------------------------------------------------------

    [Function("GetCustomerPreferences")]
    [OpenApiOperation(operationId: "GetCustomerPreferences", tags: new[] { "Preferences" }, Summary = "Get preference settings for a customer")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Customer loyalty number")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(CustomerPreferencesResponse), Description = "OK")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> GetPreferences(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/customers/{loyaltyNumber}/preferences")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var query = new GetPreferencesQuery(loyaltyNumber);
        var preferences = await _getPreferencesHandler.HandleAsync(query, cancellationToken);

        if (preferences is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return await req.OkJsonAsync(CustomerMapper.ToPreferencesResponse(preferences));
    }

    // -------------------------------------------------------------------------
    // PUT /v1/customers/{loyaltyNumber}/preferences
    // -------------------------------------------------------------------------

    [Function("UpdateCustomerPreferences")]
    [OpenApiOperation(operationId: "UpdateCustomerPreferences", tags: new[] { "Preferences" }, Summary = "Replace preference settings for a customer")]
    [OpenApiParameter(name: "loyaltyNumber", In = ParameterLocation.Path, Required = true, Type = typeof(string), Description = "Customer loyalty number")]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(UpdatePreferencesRequest), Required = true, Description = "Preference flags")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NoContent, Description = "Updated")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.NotFound, Description = "Not Found")]
    public async Task<HttpResponseData> UpdatePreferences(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "v1/customers/{loyaltyNumber}/preferences")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var (request, error) = await req.TryDeserializeBodyAsync<UpdatePreferencesRequest>(_logger, cancellationToken);
        if (error is not null) return error;

        var command = CustomerMapper.ToCommand(loyaltyNumber, request!);
        var updated = await _updatePreferencesHandler.HandleAsync(command, cancellationToken);

        if (!updated)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        return req.NoContent();
    }
}
