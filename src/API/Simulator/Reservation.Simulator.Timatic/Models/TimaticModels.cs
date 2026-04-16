// Description: Request and response models for all three IATA Timatic AutoCheck simulator endpoints.

namespace Reservation.Simulator.Timatic.Models;

// ── Document Check ─────────────────────────────────────────────────────────────

public sealed record DocumentCheckRequest(
    string TransactionIdentifier,
    string AirlineCode,
    string JourneyType,
    DocumentPaxInfo PaxInfo,
    IReadOnlyList<ItinerarySegment> Itinerary
);

public sealed record DocumentPaxInfo(
    string DocumentType,
    string Nationality,
    string DocumentIssuerCountry,
    string DocumentNumber,
    string DocumentExpiryDate,
    string DateOfBirth,
    string Gender,
    string ResidentCountry
);

public sealed record ItinerarySegment(
    string DepartureAirport,
    string ArrivalAirport,
    string Airline,
    string FlightNumber,
    string DepartureDate
);

public sealed record DocumentCheckResponse(
    string TransactionIdentifier,
    string Status,
    bool PassportRequired,
    bool VisaRequired,
    bool HealthDocRequired,
    bool TransitVisaRequired,
    IReadOnlyList<object> Requirements,
    IReadOnlyList<Advisory> Advisories,
    string DataAsOf
);

public sealed record Advisory(
    string Type,
    string Description,
    string Url,
    bool Mandatory
);

// ── APIS Check ─────────────────────────────────────────────────────────────────

public sealed record ApisCheckRequest(
    string TransactionIdentifier,
    string AirlineCode,
    string FlightNumber,
    string DepartureDate,
    string DepartureAirport,
    string ArrivalAirport,
    ApisPaxInfo PaxInfo
);

public sealed record ApisPaxInfo(
    string Surname,
    string GivenNames,
    string DateOfBirth,
    string Gender,
    string Nationality,
    string DocumentType,
    string DocumentNumber,
    string DocumentIssuerCountry,
    string DocumentExpiryDate
);

public sealed record ApisCheckResponse(
    string TransactionIdentifier,
    string ApisStatus,
    bool CarrierLiabilityConfirmed,
    string FineRisk,
    IReadOnlyList<object> Warnings,
    string AuditRef,
    string ProcessedAt
);

// ── Realtime Check ─────────────────────────────────────────────────────────────

public sealed record RealtimeCheckRequest(
    string TransactionIdentifier,
    string AirlineCode,
    string FlightNumber,
    string DepartureAirport,
    string ArrivalAirport,
    MrzData MrzData,
    string AgentId,
    string CheckTimestamp
);

public sealed record MrzData(
    string Line1,
    string Line2
);

public sealed record RealtimeCheckResponse(
    string TransactionIdentifier,
    string Decision,
    bool ConditionsMet,
    bool CarrierLiabilityConfirmed,
    ParsedDocument ParsedDocument,
    string AuditRef,
    string ProcessedAt
);

public sealed record ParsedDocument(
    string Surname,
    string GivenNames,
    string Nationality,
    string DateOfBirth,
    string DocumentExpiryDate,
    string DocumentNumber
);
