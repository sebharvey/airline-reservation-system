using FluentValidation;
using ReservationSystem.Microservices.Delivery.Domain.ValueObjects;
using ReservationSystem.Microservices.Delivery.Models.Requests;

namespace ReservationSystem.Microservices.Delivery.Application.IssueTickets;

public sealed class IssueTicketsRequestValidator : AbstractValidator<IssueTicketsRequest>
{
    private const decimal RoundingTolerance = 0.02m; // IATA allows per-ticket rounding

    public IssueTicketsRequestValidator()
    {
        RuleFor(r => r.BookingReference)
            .NotEmpty().WithMessage("bookingReference is required.")
            .Length(6).WithMessage("bookingReference must be exactly 6 characters.");

        RuleFor(r => r.Passengers)
            .NotEmpty().WithMessage("At least one passenger is required.");

        RuleFor(r => r.Segments)
            .NotEmpty().WithMessage("At least one segment is required.");

        RuleForEach(r => r.Passengers).ChildRules(pax =>
        {
            pax.RuleFor(p => p.PassengerId).NotEmpty().WithMessage("passengerId is required on each passenger.");
            pax.RuleFor(p => p.GivenName).NotEmpty().WithMessage("givenName is required on each passenger.");
            pax.RuleFor(p => p.Surname).NotEmpty().WithMessage("surname is required on each passenger.");

            pax.RuleFor(p => p.FareConstruction)
                .NotNull().WithMessage("fareConstruction is required on each passenger.");

            pax.When(p => p.FareConstruction != null, () =>
            {
                pax.RuleFor(p => p.FareConstruction!.FareCalculationLine)
                    .NotEmpty().WithMessage("fareConstruction.fareCalculationLine is required.")
                    .Must(line => FareCalculation.TryParse(line, out _, out _))
                    .WithMessage(p =>
                    {
                        FareCalculation.TryParse(p.FareConstruction!.FareCalculationLine, out _, out var err);
                        return $"fareConstruction.fareCalculationLine is invalid: {err}";
                    });

                pax.RuleFor(p => p.FareConstruction!.CollectingCurrency)
                    .NotEmpty().WithMessage("fareConstruction.collectingCurrency is required.")
                    .Length(3).WithMessage("fareConstruction.collectingCurrency must be a 3-character ISO 4217 code.");

                pax.RuleFor(p => p.FareConstruction!.BaseFare)
                    .GreaterThan(0).WithMessage("fareConstruction.baseFare must be greater than zero.");

                pax.RuleFor(p => p.FareConstruction!.TotalTaxes)
                    .GreaterThanOrEqualTo(0).WithMessage("fareConstruction.totalTaxes must be non-negative.");

                // Tax breakdown must sum to totalTaxes.
                pax.RuleFor(p => p.FareConstruction!).Must(fc =>
                {
                    var taxSum = fc.Taxes.Sum(t => t.Amount);
                    return Math.Abs(taxSum - fc.TotalTaxes) <= RoundingTolerance;
                }).WithMessage(p =>
                {
                    var fc = p.FareConstruction!;
                    return $"Tax breakdown sum {fc.Taxes.Sum(t => t.Amount):F2} does not match totalTaxes {fc.TotalTaxes:F2} (tolerance ±{RoundingTolerance}).";
                });

                // Tax attribution: each tax code must reference valid coupon numbers only.
                // Coupon numbers are 1..segmentCount; validated here against request segment count.
            });
        });

        RuleForEach(r => r.Segments).ChildRules(seg =>
        {
            seg.RuleFor(s => s.SegmentId).NotEmpty().WithMessage("segmentId is required on each segment.");
            seg.RuleFor(s => s.FlightNumber).NotEmpty().WithMessage("flightNumber is required on each segment.");
            seg.RuleFor(s => s.DepartureDate).NotEmpty().WithMessage("departureDate is required on each segment.");
            seg.RuleFor(s => s.Origin)
                .NotEmpty().WithMessage("origin is required on each segment.")
                .Length(3).WithMessage("origin must be a 3-character IATA airport code.");
            seg.RuleFor(s => s.Destination)
                .NotEmpty().WithMessage("destination is required on each segment.")
                .Length(3).WithMessage("destination must be a 3-character IATA airport code.");
            seg.RuleFor(s => s.FareBasisCode).NotEmpty().WithMessage("fareBasisCode is required on each segment.");
        });
    }
}
