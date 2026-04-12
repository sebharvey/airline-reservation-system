using ReservationSystem.Microservices.Offer.Domain.Entities;

namespace ReservationSystem.Microservices.Offer.Domain.Services;

/// <summary>
/// Stateless domain service for dynamic fare pricing.
/// Encapsulates the occupancy-based interpolation algorithm used at both
/// search time (StoredOffer creation) and reprice time (StoredOffer update).
/// </summary>
public static class FarePricer
{
    /// <summary>
    /// Returns the proportion of total cabin seats that are sold or held (0 = empty, 1 = full).
    /// </summary>
    public static double ComputeOccupancyRatio(CabinInventory cabin) =>
        cabin.TotalSeats > 0
            ? Math.Clamp((double)(cabin.SeatsSold + cabin.SeatsHeld) / cabin.TotalSeats, 0.0, 1.0)
            : 0.0;

    /// <summary>
    /// Linearly interpolates the base fare between <paramref name="min"/> and
    /// <paramref name="max"/> at the given <paramref name="occupancyRatio"/>.
    /// Returns <paramref name="min"/> when <paramref name="max"/> is absent,
    /// rounded to 2 decimal places.
    /// </summary>
    public static decimal ComputeDynamicPrice(decimal? min, decimal? max, double occupancyRatio)
    {
        var minVal = min ?? 0m;
        if (max is null || max <= minVal) return minVal;
        return Math.Round(minVal + (max.Value - minVal) * (decimal)occupancyRatio, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Same interpolation as <see cref="ComputeDynamicPrice"/> but for integer points values.
    /// </summary>
    public static int? ComputeDynamicPoints(int? min, int? max, double occupancyRatio)
    {
        if (min is null) return null;
        if (max is null || max <= min) return min;
        return (int)Math.Round(min.Value + (max.Value - min.Value) * occupancyRatio, MidpointRounding.AwayFromZero);
    }
}
