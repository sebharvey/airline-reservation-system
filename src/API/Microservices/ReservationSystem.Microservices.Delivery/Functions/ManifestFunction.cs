using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Application.CreateManifest;
using ReservationSystem.Microservices.Delivery.Application.GetManifest;
using ReservationSystem.Microservices.Delivery.Application.GetManifestTickets;
using ReservationSystem.Microservices.Delivery.Application.IssueTicket;
using ReservationSystem.Microservices.Delivery.Application.ReissueTicket;
using ReservationSystem.Microservices.Delivery.Application.UpdateManifest;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class ManifestFunction
{
    private readonly CreateManifestHandler _createManifestHandler;
    private readonly GetManifestHandler _getManifestHandler;
    private readonly UpdateManifestHandler _updateManifestHandler;
    private readonly IssueTicketHandler _issueTicketHandler;
    private readonly ReissueTicketHandler _reissueTicketHandler;
    private readonly GetManifestTicketsHandler _getManifestTicketsHandler;
    private readonly ILogger<ManifestFunction> _logger;

    public ManifestFunction(
        CreateManifestHandler createManifestHandler,
        GetManifestHandler getManifestHandler,
        UpdateManifestHandler updateManifestHandler,
        IssueTicketHandler issueTicketHandler,
        ReissueTicketHandler reissueTicketHandler,
        GetManifestTicketsHandler getManifestTicketsHandler,
        ILogger<ManifestFunction> logger)
    {
        _createManifestHandler = createManifestHandler;
        _getManifestHandler = getManifestHandler;
        _updateManifestHandler = updateManifestHandler;
        _issueTicketHandler = issueTicketHandler;
        _reissueTicketHandler = reissueTicketHandler;
        _getManifestTicketsHandler = getManifestTicketsHandler;
        _logger = logger;
    }

    [Function("CreateManifest")]
    public async Task<HttpResponseData> CreateManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/manifests")] HttpRequestData req,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    [Function("GetManifest")]
    public async Task<HttpResponseData> GetManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/manifests/{manifestId:guid}")] HttpRequestData req,
        Guid manifestId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    [Function("UpdateManifest")]
    public async Task<HttpResponseData> UpdateManifest(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "v1/manifests/{manifestId:guid}")] HttpRequestData req,
        Guid manifestId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    [Function("IssueTicket")]
    public async Task<HttpResponseData> IssueTicket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/manifests/{manifestId:guid}/tickets")] HttpRequestData req,
        Guid manifestId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    [Function("ReissueTicket")]
    public async Task<HttpResponseData> ReissueTicket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "v1/manifests/{manifestId:guid}/tickets/{ticketId:guid}/reissue")] HttpRequestData req,
        Guid manifestId,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    [Function("GetManifestTickets")]
    public async Task<HttpResponseData> GetManifestTickets(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/manifests/{manifestId:guid}/tickets")] HttpRequestData req,
        Guid manifestId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
