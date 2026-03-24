# Delivery domain

## Overview

The Delivery microservice is the airline's system of record for issued travel documents — e-tickets, boarding passes, and the flight manifest.

- Owns the operational record used by gate and ground staff — check-in status, seat assignments, and APIS data.
- Where the Order MS owns the commercial booking record, the Delivery MS owns the departure-facing operational record.

## Three record types

- **Tickets** — the accountable document store; one row per e-ticket (one passenger, one flight segment). This is the financial record of the issued travel entitlement. Each row carries full passenger and flight data as a JSON payload for self-containment.
- **Manifest** — the operational source of truth on who is on a given flight; one row per passenger per flight segment. Populated when a ticket is issued and updated on check-in, seat changes, SSR updates, and delays. Used by gate staff, ground handling, crew briefings, and IROPS processing.
- **Documents** — additional accountable document records analogous to Electronic Miscellaneous Documents (EMDs); one row per ancillary sale (seat purchase, bag purchase). Enables the Accounting system to track non-fare revenue items independently of the fare ticket.

Creation of any `delivery.Ticket` or `delivery.Document` row triggers an event (`TicketIssued`, `DocumentIssued`) to the Accounting system via the event bus, containing the full record.

Seat number integrity is enforced at the orchestration layer: the calling API validates `SeatNumber` against the active seatmap via the Seat MS before calling the Delivery MS. The Delivery MS trusts the seat number provided by its caller.

## Data schema — `delivery.Ticket`

Each row represents one issued e-ticket: one passenger on one flight segment. This is the accountable document store — the financial record of a travel entitlement. Additional detail (SSR codes, seat attributes, APIS data) is stored in a JSON `TicketData` field for extensibility.

| Column | Type | Nullable | Default | Key | Notes |
|---|---|---|---|---|---|
| TicketId | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| ETicketNumber | VARCHAR(20) | No | | UK | e.g. `932-1234567890`; IATA format, unique per issued ticket |
| InventoryId | UNIQUEIDENTIFIER | No | | | Cross-schema ref to `offer.FlightInventory(InventoryId)`; not enforced as DB FK |
| FlightNumber | VARCHAR(10) | No | | | Denormalised, e.g. `AX003` |
| DepartureDate | DATE | No | | | Denormalised for query convenience |
| BookingReference | CHAR(6) | No | | | e.g. `AB1234` |
| PassengerId | VARCHAR(20) | No | | | PAX reference from the order, e.g. `PAX-1` |
| GivenName | VARCHAR(100) | No | | | Denormalised for document readability |
| Surname | VARCHAR(100) | No | | | Denormalised for document readability |
| CabinCode | CHAR(1) | No | | | `F` · `J` · `W` · `Y` |
| FareBasisCode | VARCHAR(20) | No | | | Revenue management fare basis code, e.g. `JFLEXGB` |
| IsVoided | BIT | No | `0` | | Set to `1` on voluntary change, cancellation, or IROPS reissuance |
| VoidedAt | DATETIME2 | Yes | | | Null until voided |
| TicketData | NVARCHAR(MAX) | No | | | JSON document containing full ticket detail: SSR codes (as array), APIS data, seat assignment, change history |
| CreatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| UpdatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| Version | INT | No | `1` | | Optimistic concurrency version counter; incremented on every write |

### `TicketData` JSON structure

Scalar identifiers and status fields that exist as typed columns on `delivery.Ticket` (`eTicketNumber`, `inventoryId`, `flightNumber`, `departureDate`, `bookingReference`, `passengerId`, `givenName`, `surname`, `cabinCode`, `fareBasisCode`, `isVoided`, `voidedAt`, `createdAt`, `updatedAt`) are excluded from the JSON document — the table columns are the single source of truth for those values. The JSON carries the extensible detail: seat assignment, SSR codes, APIS data, and change history.

```json
{
  "seatAssignment": {
    "seatNumber": "1A",
    "positionType": "Window",
    "deckCode": "M"
  },
  "ssrCodes": [
    { "code": "VGML", "description": "Vegetarian meal", "segmentRef": "SEG-1" },
    { "code": "WCHR", "description": "Wheelchair to ramp", "segmentRef": "SEG-1" }
  ],
  "apisData": {
    "documentType": "PASSPORT",
    "documentNumber": "PA1234567",
    "issuingCountry": "GBR",
    "expiryDate": "2030-01-01",
    "nationality": "GBR",
    "dateOfBirth": "1985-03-12",
    "gender": "Male",
    "residenceCountry": "GBR"
  },
  "changeHistory": [
    {
      "eventType": "Issued",
      "occurredAt": "2025-06-01T09:14:00Z",
      "actor": "RetailAPI",
      "detail": "Initial ticket issuance"
    },
    {
      "eventType": "SeatChanged",
      "occurredAt": "2025-07-10T11:45:00Z",
      "actor": "RetailAPI",
      "detail": "Seat changed from 3C to 1A via manage-booking"
    },
    {
      "eventType": "IROPSReissued",
      "occurredAt": "2025-08-14T04:22:00Z",
      "actor": "DisruptionAPI",
      "detail": "Reissued onto AX005 LHR-JFK 2025-08-15 following cancellation of AX003; prior ticket 932-1234567890 voided"
    }
  ]
}
```

> `seatAssignment.positionType`: `Window` · `Aisle` · `Middle`. `deckCode`: `M` (main) · `U` (upper). `ssrCodes` is an empty array `[]` when no SSRs are held. `apisData` may be `null` if APIS has not yet been provided by the passenger (collection is triggered at check-in for routes that require it). `changeHistory` is append-only — a new entry is added on every mutation.

> **Indexes:** `IX_Ticket_ETicketNumber` (unique) on `(ETicketNumber)`. `IX_Ticket_BookingReference` on `(BookingReference)`. `IX_Ticket_Flight` on `(FlightNumber, DepartureDate)`.
> **Constraints:** `CHK_TicketData` — `ISJSON(TicketData) = 1`.
> **Immutability principle:** Ticket rows are never deleted; voiding sets `IsVoided = 1`. Re-issuance creates a new row with a new `ETicketNumber`; the old row is voided in the same transaction.
> **Event on creation:** Each new `delivery.Ticket` row triggers a `TicketIssued` event to the Accounting system event bus, carrying the full ticket record for financial accounting.
> **Cross-schema integrity:** `InventoryId` references `offer.FlightInventory` but is not declared as a foreign key, as the Delivery and Offer domains are logically separated. Referential integrity is the responsibility of the Retail API orchestration layer.
> **Concurrency:** `Version` is used for optimistic concurrency control — see [api.md — Optimistic Concurrency Control](api.md#optimistic-concurrency-control).

## Data schema — `delivery.Manifest`

The operational source of truth for who is on a given flight. One row per passenger per flight segment. Populated when a ticket is issued; updated on check-in, seat changes, SSR updates, and schedule changes. Used by departure control systems, gate staff, ground handling, and crew briefing.

| Column | Type | Nullable | Default | Key | Notes |
|---|---|---|---|---|---|
| ManifestId | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| TicketId | UNIQUEIDENTIFIER | No | | FK → `delivery.Ticket(TicketId)` | Links manifest row to the issued ticket |
| InventoryId | UNIQUEIDENTIFIER | No | | | Cross-schema ref to `offer.FlightInventory(InventoryId)`; not enforced as DB FK |
| FlightNumber | VARCHAR(10) | No | | | Denormalised, e.g. `AX003` |
| DepartureDate | DATE | No | | | Denormalised for query convenience |
| AircraftType | CHAR(4) | No | | | Used for seatmap validation at write time |
| SeatNumber | VARCHAR(5) | No | | | e.g. `1A`, `22K` — must exist on active seatmap for `AircraftType` |
| CabinCode | CHAR(1) | No | | | `F` · `J` · `W` · `Y` |
| BookingReference | CHAR(6) | No | | | e.g. `AB1234` |
| ETicketNumber | VARCHAR(20) | No | | | e.g. `932-1234567890`; denormalised from `delivery.Ticket` |
| PassengerId | VARCHAR(20) | No | | | PAX reference from the order, e.g. `PAX-1` |
| GivenName | VARCHAR(100) | No | | | Denormalised for manifest readability |
| Surname | VARCHAR(100) | No | | | Denormalised for manifest readability |
| SsrCodes | NVARCHAR(500) | Yes | | | JSON array of IATA SSR codes, e.g. `["VGML","WCHR"]`; written at booking confirmation and updated on SSR change |
| DepartureTime | TIME | No | | | Local departure time; updated by Disruption API on delay |
| ArrivalTime | TIME | No | | | Local arrival time; updated by Disruption API on delay |
| CheckedIn | BIT | No | `0` | | |
| CheckedInAt | DATETIME2 | Yes | | | Null until check-in is completed |
| CreatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| UpdatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| Version | INT | No | `1` | | Optimistic concurrency version counter; incremented on every write |

> **Indexes:** `IX_Manifest_Seat` (unique) on `(InventoryId, SeatNumber)` — prevents double-assignment of a seat on a flight. `IX_Manifest_Pax` (unique) on `(InventoryId, ETicketNumber)` — prevents duplicate manifest entries for the same passenger. `IX_Manifest_Flight` on `(FlightNumber, DepartureDate)` — used for gate staff and IROPS manifest retrieval. `IX_Manifest_BookingReference` on `(BookingReference)` — used for customer servicing and check-in lookups.
> **Lifecycle:** Manifest rows are created when a ticket is issued; removed (hard-deleted) when a booking is cancelled or a flight change removes the segment. On IROPS rebooking, the old manifest row is deleted and a new one written for the replacement flight.
> **SsrCodes:** Stored as a JSON array (e.g. `["VGML","WCHR"]`) rather than a CSV string, enabling clean serialisation/deserialisation and future query support via SQL Server JSON functions.
> **Seatmap validation:** The orchestration layer is responsible for validating `SeatNumber` against the active seatmap (via Seat MS) before calling the Delivery MS. The Delivery MS trusts the seat number provided by its caller.
> **Concurrency:** `Version` is used for optimistic concurrency control — see [api.md — Optimistic Concurrency Control](api.md#optimistic-concurrency-control).

## Data schema — `delivery.Document`

Accountable document records for non-fare ancillary sales — analogous to Electronic Miscellaneous Documents (EMDs) in legacy airline systems. One row per ancillary item purchased (seat selection, additional bag). Creation of any `delivery.Document` row triggers a `DocumentIssued` event to the Accounting system event bus.

| Column | Type | Nullable | Default | Key | Notes |
|---|---|---|---|---|---|
| DocumentId | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| DocumentNumber | VARCHAR(20) | No | | UK | System-generated reference, e.g. `932-EMD-0001234`; unique per document |
| DocumentType | VARCHAR(30) | No | | | `SeatAncillary` · `BagAncillary` |
| BookingReference | CHAR(6) | No | | | Associated booking reference |
| ETicketNumber | VARCHAR(20) | No | | | Associated e-ticket (links the ancillary to the travel segment it covers) |
| PassengerId | VARCHAR(20) | No | | | PAX reference from the order, e.g. `PAX-1` |
| SegmentRef | VARCHAR(20) | No | | | Segment identifier the ancillary applies to (e.g. `SEG-1`) |
| PaymentReference | VARCHAR(20) | No | | | Associated payment reference, e.g. `AXPAY-0002` |
| Amount | DECIMAL(10,2) | No | | | Amount charged for this ancillary item |
| CurrencyCode | CHAR(3) | No | `'GBP'` | | ISO 4217 currency code |
| IsVoided | BIT | No | `0` | | Set to `1` if the ancillary is refunded or reversed |
| DocumentData | NVARCHAR(MAX) | No | | | JSON document containing full ancillary detail (seat position, bag sequence, price breakdown, etc.) |
| CreatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| UpdatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |

### `DocumentData` JSON structure

Typed columns already present on `delivery.Document` (`documentNumber`, `documentType`, `bookingReference`, `eTicketNumber`, `passengerId`, `segmentRef`, `paymentReference`, `amount`, `currencyCode`, `isVoided`, `createdAt`, `updatedAt`) are excluded from the JSON. The JSON carries the IATA EMD service detail and price breakdown that varies by ancillary type, plus a coupon status and void history.

The top-level `ancillaryDetail` object is a discriminated union — its shape depends on `documentType`.

**`SeatAncillary` example**

```json
{
  "emdType": "EMD-A",
  "rfic": "A",
  "rfisc": "0B5",
  "serviceDescription": "Seat Selection — Business Window",
  "couponStatus": "Open",
  "ancillaryDetail": {
    "type": "SeatAncillary",
    "seatNumber": "1A",
    "positionType": "Window",
    "deckCode": "M",
    "cabinCode": "J",
    "seatAttributes": ["ExtraLegroom", "LieFlat"]
  },
  "priceBreakdown": {
    "baseAmount": 45.00,
    "taxes": [
      { "code": "GB", "description": "UK Air Passenger Duty", "amount": 0.00 }
    ],
    "totalAmount": 45.00,
    "currencyCode": "GBP"
  },
  "voidHistory": []
}
```

**`BagAncillary` example**

```json
{
  "emdType": "EMD-A",
  "rfic": "C",
  "rfisc": "0GO",
  "serviceDescription": "Additional Checked Bag — 23 kg",
  "couponStatus": "Open",
  "ancillaryDetail": {
    "type": "BagAncillary",
    "bagSequenceNumber": 2,
    "weightKg": 23,
    "dimensionsCm": { "length": 90, "width": 75, "depth": 43 },
    "bagTagNumber": null
  },
  "priceBreakdown": {
    "baseAmount": 60.00,
    "taxes": [
      { "code": "GB", "description": "UK Air Passenger Duty", "amount": 0.00 }
    ],
    "totalAmount": 60.00,
    "currencyCode": "GBP"
  },
  "voidHistory": []
}
```

> `emdType`: `EMD-A` (associated — tied to a specific e-ticket/flight coupon) or `EMD-S` (standalone). All current ancillaries are `EMD-A`. `rfic` is the IATA Reason for Issuance Code (`A` = air transportation, `C` = baggage). `rfisc` is the sub-code identifying the specific service. `couponStatus`: `Open` → `Used` (set by Airport API at bag drop / boarding) · `Void` (set on refund). `bagTagNumber` is null at purchase and populated by the Airport API when the bag is checked in. `voidHistory` is an append-only array of `{ occurredAt, actor, reason }` entries added when `IsVoided` is set to `1`.

> **Indexes:** `IX_Document_Number` (unique) on `(DocumentNumber)`. `IX_Document_BookingReference` on `(BookingReference)`. `IX_Document_ETicketNumber` on `(ETicketNumber)`.
> **Constraints:** `CHK_DocumentData` — `ISJSON(DocumentData) = 1`.
> **Event on creation:** Each new `delivery.Document` row triggers a `DocumentIssued` event to the Accounting system event bus, carrying the full document record so the Accounting MS can record the ancillary revenue.
> **Scope:** `delivery.Document` covers seat and bag ancillaries sold through the reservation system. Upgrade documents and future ancillary types (e.g. lounge access, excess baggage waivers) should also be recorded here when introduced.
