using ReservationSystem.Orchestration.Operations.Infrastructure.ExternalServices;
using ReservationSystem.Orchestration.Operations.Models.Responses;

namespace ReservationSystem.Orchestration.Operations.Application.CreateSchedule;

public sealed class CreateScheduleHandler
{
    private readonly ScheduleServiceClient _scheduleServiceClient;
    private readonly OfferServiceClient _offerServiceClient;

    public CreateScheduleHandler(
        ScheduleServiceClient scheduleServiceClient,
        OfferServiceClient offerServiceClient)
    {
        _scheduleServiceClient = scheduleServiceClient;
        _offerServiceClient = offerServiceClient;
    }

    public Task<CreateScheduleResponse> HandleAsync(CreateScheduleCommand command, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }
}
