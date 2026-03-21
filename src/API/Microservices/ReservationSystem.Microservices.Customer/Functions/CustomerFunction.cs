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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
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
        throw new NotImplementedException();
    }
}
