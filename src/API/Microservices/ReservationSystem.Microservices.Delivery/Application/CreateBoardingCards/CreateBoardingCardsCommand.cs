namespace ReservationSystem.Microservices.Delivery.Application.CreateBoardingCards;

public sealed class CreateBoardingCardsCommand
{
    public string BookingReference { get; init; } = string.Empty;
    public List<BoardingCardPassengerCommand> Passengers { get; init; } = [];
}

public sealed class BoardingCardPassengerCommand
{
    public string PassengerId { get; init; } = string.Empty;
    public List<string> InventoryIds { get; init; } = [];
}
