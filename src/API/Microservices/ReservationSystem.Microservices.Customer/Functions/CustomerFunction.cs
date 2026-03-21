using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Shared.Common.Http;
using ReservationSystem.Shared.Common.Json;
using ReservationSystem.Microservices.Customer.Application.AuthorisePoints;
using ReservationSystem.Microservices.Customer.Application.CreateCustomer;
using ReservationSystem.Microservices.Customer.Application.DeleteCustomer;
using ReservationSystem.Microservices.Customer.Application.GetCustomer;
using ReservationSystem.Microservices.Customer.Application.GetTransactions;
using ReservationSystem.Microservices.Customer.Application.ReinstatePoints;
using ReservationSystem.Microservices.Customer.Application.ReversePoints;
using ReservationSystem.Microservices.Customer.Application.SettlePoints;
using ReservationSystem.Microservices.Customer.Application.UpdateCustomer;
using ReservationSystem.Microservices.Customer.Models.Mappers;
using ReservationSystem.Microservices.Customer.Models.Requests;
using System.Net;
using System.Text.Json;

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
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers
    // -------------------------------------------------------------------------

    [Function("CreateCustomer")]
    public async Task<HttpResponseData> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        CreateCustomerRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateCustomerRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in CreateCustomer request");
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var loyaltyNumber = GenerateLoyaltyNumber();
        var command = CustomerMapper.ToCommand(loyaltyNumber, request);
        var customer = await _createHandler.HandleAsync(command, cancellationToken);
        var response = CustomerMapper.ToCreateResponse(customer);

        return await req.CreatedAsync($"/v1/customers/{customer.LoyaltyNumber}", response);
    }

    // -------------------------------------------------------------------------
    // GET /v1/customers/{loyaltyNumber}
    // -------------------------------------------------------------------------

    [Function("GetCustomer")]
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
    // PATCH /v1/customers/{loyaltyNumber}
    // -------------------------------------------------------------------------

    [Function("UpdateCustomer")]
    public async Task<HttpResponseData> Update(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/customers/{loyaltyNumber}")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        UpdateCustomerRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<UpdateCustomerRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in UpdateCustomer request for {LoyaltyNumber}", loyaltyNumber);
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = CustomerMapper.ToCommand(loyaltyNumber, request);
        var customer = await _updateHandler.HandleAsync(command, cancellationToken);

        if (customer is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        var response = CustomerMapper.ToResponse(customer);
        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // GET /v1/customers/{loyaltyNumber}/transactions
    // -------------------------------------------------------------------------

    [Function("GetCustomerTransactions")]
    public async Task<HttpResponseData> GetTransactions(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/customers/{loyaltyNumber}/transactions")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        var customerQuery = new GetCustomerQuery(loyaltyNumber);
        var customer = await _getHandler.HandleAsync(customerQuery, cancellationToken);

        if (customer is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        int page = 1;
        int pageSize = 20;

        var pageParam = req.Query["page"];
        if (!string.IsNullOrEmpty(pageParam) && int.TryParse(pageParam, out var parsedPage))
            page = parsedPage;

        var pageSizeParam = req.Query["pageSize"];
        if (!string.IsNullOrEmpty(pageSizeParam) && int.TryParse(pageSizeParam, out var parsedPageSize))
            pageSize = parsedPageSize;

        var query = new GetTransactionsQuery(loyaltyNumber, page, pageSize);
        var (transactions, totalCount) = await _getTransactionsHandler.HandleAsync(query, cancellationToken);
        var response = CustomerMapper.ToResponse(loyaltyNumber, transactions, page, pageSize, totalCount);

        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // POST /v1/customers/{loyaltyNumber}/points/authorise
    // -------------------------------------------------------------------------

    [Function("AuthorisePoints")]
    public async Task<HttpResponseData> AuthorisePoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/authorise")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        AuthorisePointsRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<AuthorisePointsRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in AuthorisePoints request for {LoyaltyNumber}", loyaltyNumber);
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

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
    public async Task<HttpResponseData> SettlePoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/settle")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        SettlePointsRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<SettlePointsRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in SettlePoints request for {LoyaltyNumber}", loyaltyNumber);
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

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
    public async Task<HttpResponseData> ReversePoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/reverse")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        ReversePointsRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<ReversePointsRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in ReversePoints request for {LoyaltyNumber}", loyaltyNumber);
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

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
    public async Task<HttpResponseData> ReinstatePoints(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/customers/{loyaltyNumber}/points/reinstate")] HttpRequestData req,
        string loyaltyNumber,
        CancellationToken cancellationToken)
    {
        ReinstatePointsRequest? request;

        try
        {
            request = await JsonSerializer.DeserializeAsync<ReinstatePointsRequest>(
                req.Body, SharedJsonOptions.CamelCase, cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Invalid JSON in ReinstatePoints request for {LoyaltyNumber}", loyaltyNumber);
            return await req.BadRequestAsync("Invalid JSON in request body.");
        }

        if (request is null)
            return await req.BadRequestAsync("Request body is required.");

        var command = CustomerMapper.ToCommand(loyaltyNumber, request);
        var transaction = await _reinstatePointsHandler.HandleAsync(command, cancellationToken);

        if (transaction is null)
            return await req.NotFoundAsync($"Customer not found for loyalty number '{loyaltyNumber}'.");

        var response = CustomerMapper.ToReinstateResponse(transaction);
        return await req.OkJsonAsync(response);
    }

    // -------------------------------------------------------------------------
    // DELETE /v1/customers/{loyaltyNumber}
    // -------------------------------------------------------------------------

    [Function("DeleteCustomer")]
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
    // Helpers
    // -------------------------------------------------------------------------

    private static string GenerateLoyaltyNumber()
    {
        var random = Random.Shared.Next(1000000, 9999999);
        return $"AX{random}";
    }
}
