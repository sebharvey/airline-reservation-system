namespace ReservationSystem.Microservices.Ancillary.Application.Product.UpdateProductGroup;

public sealed record UpdateProductGroupCommand(Guid ProductGroupId, string Name, bool IsActive);
