# Delivery domain

## Overview

The Delivery microservice is the airline's system of record for issued travel documents — e-tickets, boarding passes, and the flight manifest.

- Owns the operational record used by gate and ground staff — check-in status, seat assignments, and APIS data.
- Where the Order MS owns the commercial booking record, the Delivery MS owns the departure-facing operational record.

## Three record types

- **Tickets** — the accountable document store; one row per e-ticket (one passenger covering all flight segments on the booking). This is the financial record of the issued travel entitlement. Each row carries the full IATA ticket in a self-contained JSON payload including passenger, fare, payment, and per-segment coupon detail.
- **Manifest** — the operational source of truth on who is on a given flight; one row per passenger per flight segment. Populated when a ticket is issued and updated on check-in, seat changes, SSR updates, and delays. Used by gate staff, ground handling, crew briefings, and IROPS processing.
- **Documents** — additional accountable document records analogous to Electronic Miscellaneous Documents (EMDs); one row per ancillary sale (seat purchase, bag purchase). Enables the Accounting system to track non-fare revenue items independently of the fare ticket.

Creation of any `delivery.Ticket` or `delivery.Document` row triggers an event (`TicketIssued`, `DocumentIssued`) to the Accounting system via the event bus, containing the full record.

Seat number integrity is enforced at the orchestration layer: the calling API validates `SeatNumber` against the active seatmap via the Seat MS before calling the Delivery MS. The Delivery MS trusts the seat number provided by its caller.

## Data schema — `delivery.Ticket`

Each row represents one issued e-ticket: one passenger on one flight segment. This is the accountable document store — the financial record of a travel entitlement. Additional detail (SSR codes, seat attributes, APIS data) is stored in a JSON `TicketData` field for extensibility.

| Column | Type | Nullable | Default | Key | Notes |
|---|---|---|---|---|---|
| TicketId | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| TicketNumber | BIGINT | No | IDENTITY(1000000001,1) | UK | Database-generated auto-increment; the numeric second part of the IATA e-ticket number. The full formatted string (e.g. `932-1000000001`) is assembled at the API layer by prepending the airline accounting code. |
| BookingReference | CHAR(6) | No | | | e.g. `AB1234` |
| PassengerId | VARCHAR(20) | No | | | PAX reference from the order, e.g. `PAX-1` |
| IsVoided | BIT | No | `0` | | Set to `1` on voluntary change, cancellation, or IROPS reissuance |
| VoidedAt | DATETIME2 | Yes | | | Null until voided |
| TicketData | NVARCHAR(MAX) | No | | | JSON document containing full ticket detail: passenger, fare, payment, per-segment coupon detail, SSR codes, APIS data, seat assignment, change history |
| CreatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| UpdatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| Version | INT | No | `1` | | Optimistic concurrency version counter; incremented on every write |

### `TicketData` JSON structure

`TicketData` holds the full IATA electronic ticket representation. The document is self-contained: it carries all fare, payment, passenger, and coupon detail needed to reconstruct the complete ticket without additional lookups. `TicketData` is the authoritative source for passenger name, flight, cabin, and fare basis information. Typed columns on `delivery.Ticket` (`eTicketNumber`, `bookingReference`, `passengerId`, `isVoided`, `voidedAt`, `createdAt`, `updatedAt`) remain the authoritative values for those fields.

One ticket covers one passenger and all of their flight segments. Each segment is represented as a numbered coupon inside the `coupons` array.

```json
{
  "passenger": {
    "surname": "HARVEY",
    "givenName": "SEBASTIAN MR",
    "passengerTypeCode": "ADT",
    "frequentFlyer": {
      "carrier": "VS",
      "number": "123456789",
      "tier": "GOLD"
    }
  },
  "fareConstruction": {
    "pricingCurrency": "GBP",
    "collectingCurrency": "GBP",
    "baseFare": 1800.00,
    "equivalentFarePaid": 1800.00,
    "nucAmount": 2299.74,
    "roeApplied": 0.782853,
    "fareCalculationLine": "LON VS NYC 900.00 VS LON 900.00 NUC 1800.00 END ROE 0.782853",
    "taxes": [
      { "code": "GB", "amount": 86.00,  "currency": "GBP", "description": "Air Passenger Duty" },
      { "code": "UB", "amount": 28.40,  "currency": "GBP", "description": "Passenger Service Charge LHR" },
      { "code": "US", "amount": 19.80,  "currency": "USD", "description": "US International Departure Tax" },
      { "code": "YQ", "amount": 410.00, "currency": "GBP", "description": "Carrier-imposed surcharge (fuel)" },
      { "code": "YR", "amount": 22.00,  "currency": "GBP", "description": "Carrier-imposed surcharge (other)" }
    ],
    "totalTaxes": 566.20,
    "totalAmount": 2366.20
  },
  "formOfPayment": {
    "type": "CC",
    "cardType": "VI",
    "maskedPan": "411111XXXXXX1111",
    "expiryMmYy": "0928",
    "approvalCode": "A12345",
    "amount": 2366.20,
    "currency": "GBP"
  },
  "commission": {
    "type": "PERCENT",
    "rate": 0.00,
    "amount": 0.00
  },
  "endorsementsRestrictions": "NON-ENDO/NON-REF/PENLTY APPLIES",
  "tourCode": null,
  "originalIssue": {
    "ticketNumber": null,
    "issueDate": null,
    "issuingLocation": null,
    "fareAmount": null
  },
  "coupons": [
    {
      "couponNumber": 1,
      "status": "O",
      "marketing":  { "carrier": "VS", "flightNumber": "VS003" },
      "operating":  { "carrier": "VS", "flightNumber": "VS003" },
      "origin": "LHR",
      "destination": "JFK",
      "departureDate": "2025-12-01",
      "departureTime": "11:00",
      "classOfService": "J",
      "cabin": "BUSINESS",
      "fareBasisCode": "JLE6MGBR",
      "notValidBefore": "2025-12-01",
      "notValidAfter": null,
      "stopoverIndicator": "O",
      "baggageAllowance": { "type": "PIECE", "quantity": 2, "weightKg": 32 },
      "seat": "11A",
      "fareComponent": { "amount": 900.00, "currency": "GBP" }
    },
    {
      "couponNumber": 2,
      "status": "O",
      "marketing":  { "carrier": "VS", "flightNumber": "VS004" },
      "operating":  { "carrier": "VS", "flightNumber": "VS004" },
      "origin": "JFK",
      "destination": "LHR",
      "departureDate": "2025-12-15",
      "departureTime": "22:20",
      "classOfService": "J",
      "cabin": "BUSINESS",
      "fareBasisCode": "JLE6MGBR",
      "notValidBefore": "2025-12-15",
      "notValidAfter": null,
      "stopoverIndicator": "S",
      "baggageAllowance": { "type": "PIECE", "quantity": 2, "weightKg": 32 },
      "seat": "12A",
      "fareComponent": { "amount": 900.00, "currency": "GBP" }
    }
  ],
  "ssrCodes": [
    { "code": "VGML", "description": "Vegetarian meal", "segmentRef": "SEG-1" }
  ],
  "apisData": null,
  "changeHistory": [
    {
      "eventType": "Issued",
      "occurredAt": "2025-06-01T09:14:00Z",
      "actor": "RetailAPI",
      "detail": "Initial ticket issuance"
    },
    {
      "eventType": "IROPSReissued",
      "occurredAt": "2025-08-14T04:22:00Z",
      "actor": "DisruptionAPI",
      "detail": "Reissued onto VS005 LHR-JFK 2025-08-15 following cancellation of VS003; prior ticket 932-1234567890 voided"
    }
  ]
}
```

> **`passenger.passengerTypeCode`:** `ADT` · `CHD` · `INF` · `YTH` · `MIL`. Defaults to `ADT` if not supplied.
> **`passenger.frequentFlyer`:** `null` when no frequent flyer number is held.
> **`fareConstruction`:** `null` when fare breakdown is not available (e.g. award tickets or manual issuance). `taxes` is an empty array `[]` when no taxes apply. `roeApplied` is the IATA Rate of Exchange used to convert `nucAmount` to `baseFare`.
> **`formOfPayment.type`:** `CC` (credit card) · `DC` (debit card) · `CASH` · `MPD` · `INV`. `cardType`: `VI` (Visa) · `MC` (Mastercard) · `AX` (Amex) · `DC` (Diners). `maskedPan`: first 6 digits + `X` per masked digit + last 4 digits. `approvalCode` holds the payment service reference.
> **`commission`:** Defaults to `PERCENT / 0.00 / 0.00` for direct channel bookings with no agency commission.
> **`originalIssue`:** `null` values on initial issuance; populated on reissuance with the voided ticket's number, issue date, location, and fare amount.
> **`coupons.status`:** `O` (Open for use) · `A` (Airport control) · `C` (Checked in) · `B` (Boarded) · `F` (Flown / Used) · `R` (Refunded) · `E` (Exchanged / Reissued) · `V` (Void) · `S` (Suspended) · `L` (Lifted / Used at gate) · `I` (Irregular operations). Initial status is `O`.
> **`coupons.stopoverIndicator`:** `O` (connection — transit time < 24 h) · `S` (stopover — transit time ≥ 24 h).
> **`ssrCodes`:** Empty array `[]` when no SSRs are held.
> **`apisData`:** `null` at booking; populated at check-in for routes requiring Advance Passenger Information. Shape: `{ documentType, documentNumber, issuingCountry, expiryDate, nationality, dateOfBirth, gender, residenceCountry }`.
> **`changeHistory`:** Append-only. A new entry is added on every mutation (seat change, reissuance, IROPS rebooking). `actor` identifies the system component that made the change.

> **Indexes:** `UQ_Ticket_Number` (unique) on `(TicketNumber)`. `IX_Ticket_BookingReference` on `(BookingReference)`.
> **Constraints:** `CHK_TicketData` — `ISJSON(TicketData) = 1`.
> **Immutability principle:** Ticket rows are never deleted; voiding sets `IsVoided = 1`. Re-issuance creates a new row with a new `ETicketNumber`; the old row is voided in the same transaction.
> **Event on creation:** Each new `delivery.Ticket` row triggers a `TicketIssued` event to the Accounting system event bus, carrying the full ticket record for financial accounting.
> **Concurrency:** `Version` is used for optimistic concurrency control — see [api.md — Optimistic Concurrency Control](api.md#optimistic-concurrency-control).

## Coupon status

Each flight coupon within `TicketData` carries a **status code** that controls what operations are permitted on it. Status codes are governed by IATA Resolution 722 and interline agreements.

| Status code | Meaning |
|---|---|
| `O` | **Open for use** — coupon is valid and available for check-in |
| `A` | **Airport control** — coupon has been lifted at the gate or check-in desk; DCS has taken control |
| `C` | **Checked in** — passenger has checked in; set by the Delivery MS when the manifest is updated |
| `B` | **Boarded** — boarding pass issued |
| `F` | **Flown / Used** — segment has been operated and coupon consumed |
| `R` | **Refunded** — coupon has been refunded |
| `E` | **Exchanged / Reissued** — coupon was used as part of an exchange or reissuance |
| `V` | **Void** — ticket was voided (typically same day as issuance) |
| `S` | **Suspended** — coupon placed on hold pending resolution |
| `L` | **Lifted / Used at gate** — variant of airport control in some PSS implementations |
| `I` | **Irregular operations** — IROP status in some PSS implementations |

Coupon status transitions follow a strict state machine. In normal operations an `O` coupon advances to `C` at online check-in, then to `A` or `B` at the gate, and finally to `F` once the segment is flown. Reversals and corrections are controlled and auditable via `changeHistory`.

```
O → C  (checked in — set by Delivery MS on PATCH /v1/manifest/{bookingRef})
C → B  (boarded — set by Airport API on boarding scan)
B → F  (flown — set by Airport API on departure)
O → V  (voided — set on same-day cancellation)
O → R  (refunded — set on voluntary refund)
* → E  (exchanged — set on reissuance; prior coupon voided)
* → S  (suspended — set for IROPS hold)
```

> **Status is set automatically by the Delivery MS** when a manifest is patched with `checkedIn: true`. The matching coupon (identified by `marketing.flightNumber`, `origin`, and `destination`) is updated from `O` to `C` and a `CouponStatusUpdated` entry is appended to `changeHistory`.

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
| PaymentId | UNIQUEIDENTIFIER | No | | | Associated payment identifier (GUID) |
| Amount | DECIMAL(10,2) | No | | | Amount charged for this ancillary item |
| CurrencyCode | CHAR(3) | No | `'GBP'` | | ISO 4217 currency code |
| IsVoided | BIT | No | `0` | | Set to `1` if the ancillary is refunded or reversed |
| DocumentData | NVARCHAR(MAX) | No | | | JSON document containing full ancillary detail (seat position, bag sequence, price breakdown, etc.) |
| CreatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| UpdatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |

### `DocumentData` JSON structure

Typed columns already present on `delivery.Document` (`documentNumber`, `documentType`, `bookingReference`, `eTicketNumber`, `passengerId`, `segmentRef`, `paymentId`, `amount`, `currencyCode`, `isVoided`, `createdAt`, `updatedAt`) are excluded from the JSON. The JSON carries the IATA EMD service detail and price breakdown that varies by ancillary type, plus a coupon status and void history.

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
