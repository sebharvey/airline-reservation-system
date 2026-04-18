using Microsoft.Extensions.Logging;
using ReservationSystem.Microservices.Offer.Domain.Entities;
using ReservationSystem.Microservices.Offer.Domain.Repositories;

namespace ReservationSystem.Microservices.Offer.Application.UpdateFareFamily;

public sealed class UpdateFareFamilyHandler
{
    private readonly IOfferRepository _repository;
    private readonly ILogger<UpdateFareFamilyHandler> _logger;

    public UpdateFareFamilyHandler(IOfferRepository repository, ILogger<UpdateFareFamilyHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<FareFamily> HandleAsync(UpdateFareFamilyCommand command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
            throw new ArgumentException("Fare family name is required.");

        var fareFamily = await _repository.GetFareFamilyByIdAsync(command.FareFamilyId, ct)
            ?? throw new KeyNotFoundException($"FareFamily '{command.FareFamilyId}' not found.");

        fareFamily.Update(command.Name.Trim(), command.Description?.Trim(), command.DisplayOrder);

        await _repository.UpdateFareFamilyAsync(fareFamily, ct);

        _logger.LogInformation("Updated FareFamily {FareFamilyId} ({Name})", fareFamily.FareFamilyId, fareFamily.Name);

        return fareFamily;
    }
}
