using ReservationSystem.Microservices.Offer.Domain.Entities;

namespace ReservationSystem.Microservices.Offer.Models.Mappers;

public static class FareFamilyMapper
{
    public static object ToResponse(FareFamily f) => new
    {
        fareFamilyId = f.FareFamilyId,
        name         = f.Name,
        description  = f.Description,
        displayOrder = f.DisplayOrder,
        createdAt    = f.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
        updatedAt    = f.UpdatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ")
    };

    public static IEnumerable<object> ToResponseList(IReadOnlyList<FareFamily> families) =>
        families.Select(ToResponse);
}
