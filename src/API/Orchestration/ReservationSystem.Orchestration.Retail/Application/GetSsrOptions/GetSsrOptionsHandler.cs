using Microsoft.EntityFrameworkCore;
using ReservationSystem.Orchestration.Retail.Infrastructure.Persistence;
using ReservationSystem.Orchestration.Retail.Models.Responses;

namespace ReservationSystem.Orchestration.Retail.Application.GetSsrOptions;

public sealed class GetSsrOptionsHandler
{
    private readonly RetailDbContext _context;

    public GetSsrOptionsHandler(RetailDbContext context)
    {
        _context = context;
    }

    public async Task<GetSsrOptionsResponse> HandleAsync(GetSsrOptionsQuery query, CancellationToken cancellationToken)
    {
        var options = await _context.SsrCatalogue
            .Where(e => e.IsActive)
            .OrderBy(e => e.Category)
            .ThenBy(e => e.SsrCode)
            .Select(e => new SsrOptionDto(e.SsrCode, e.Label, e.Category))
            .ToListAsync(cancellationToken);

        return new GetSsrOptionsResponse(options.AsReadOnly());
    }
}
