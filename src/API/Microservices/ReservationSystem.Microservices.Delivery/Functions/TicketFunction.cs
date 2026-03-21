using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Delivery.Application.GetTicket;

namespace ReservationSystem.Microservices.Delivery.Functions;

public sealed class TicketFunction
{
    private readonly GetTicketHandler _getTicketHandler;
    private readonly ILogger<TicketFunction> _logger;

    public TicketFunction(GetTicketHandler getTicketHandler, ILogger<TicketFunction> logger)
    {
        _getTicketHandler = getTicketHandler;
        _logger = logger;
    }

    [Function("GetTicket")]
    public async Task<HttpResponseData> GetTicket(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/tickets/{ticketId:guid}")] HttpRequestData req,
        Guid ticketId,
        CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
