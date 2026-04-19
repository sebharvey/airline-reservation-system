using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.CreateFareFamily;

public sealed class CreateFareFamilyHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<CreateFareFamilyHandler> _logger;

    public CreateFareFamilyHandler(IOfferRepository repository, ILogger<CreateFareFamilyHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<FareFamily> HandleAsync(CreateFareFamilyCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Fare family name is required.");

        var fareFamily = FareFamily.Create(command.Name.Trim(), command.Description?.Trim(), command.DisplayOrder);

        await _repository.CreateFareFamilyAsync(fareFamily, ct);

        _logger.LogInformation("Created FareFamily {FareFamilyId} ({Name})", fareFamily.FareFamilyId, fareFamily.Name);

        return fareFamily;
    }
}
