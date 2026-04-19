# Delivery Microservice — API Specification

> **Service owner:** Delivery domain
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Delivery microservice is the airline's system of record for issued travel documents and departure operations. It manages three distinct record types: **Tickets** (`delivery.Ticket`) — financial/accounting documents, one per passenger per flight segment, equivalent to an e-ticket; **Manifest** (`delivery.Manifest`) — the operational passenger manifest used by ground handling, gate staff, crew briefings, and departure control systems; and **Documents** (`delivery.Document`) — ancillary EMD-equivalent records for post-sale ancillary purchases such as seat selections and additional bags.

Where the Order MS owns the commercial booking record, the Delivery MS owns the departure-facing operational record. It also generates boarding cards and BCBP (Bar Coded Boarding Pass) barcode strings compliant with IATA Resolution 792.

> **Important:** The Delivery microservice is an internal service. It is not called directly by channels (Web, App, NDC). All requests are routed through the **Retail API**, **Airport API**, or **Operations API** orchestration layers. See the [Security](#security) section for authentication details.

---

## Security

### Authentication

The Delivery microservice is called exclusively by orchestration APIs. It does not validate JWTs; JWT validation is the responsibility of the calling orchestration layer.

Calls from orchestration APIs to the Delivery microservice are authenticated using an **Azure Function Host Key**, passed in the `x-functions-key` HTTP header. See [Microservice Authentication — Host Keys](../api.md#microservice-authentication--host-keys) in `api.md` for the full mechanism, key storage, and retrieval details.

### Required Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes (for request bodies) | Must be `application/json` |
| `x-functions-key` | Yes | Azure Function Host Key authenticating the orchestration API as an authorised caller |
| `X-Correlation-ID` | Yes | UUID generated at the channel boundary; propagated on every downstream call for distributed tracing and log correlation |

### Data Protection

- PII (passenger names, passport numbers, contact details) must never appear in logs or telemetry. Log entries reference passengers by `BookingReference`, `ETicketNumber`, or `PassengerId` only.
- The Delivery MS does not store email addresses or credentials. These are owned by the Identity MS.
- The Delivery MS does not call other microservices. All data required for ticket issuance, manifest population, and document creation is passed explicitly by the calling orchestration layer.

---

## Business Rules

### E-Ticket Numbers

- E-ticket numbers follow the IATA format: a **3-digit airline code prefix** followed by a **10-digit serial number**, e.g. `932-1234567890` (Apex Air prefix: `932`).
- Each e-ticket covers **one passenger across all flight segments in the booking**. A return booking for two passengers generates **two** e-ticket numbers (one per passenger). Each ticket contains coupons — one coupon per flight segment, ordered by coupon number.
- The ticket is the **monetary unit**: total fare and taxes are stored at the ticket level. Coupon-level value is always **derived** from the fare construction and tax breakdown — never stored directly as an authoritative amount.
- E-ticket numbers are **immutable after issuance** — post-booking changes trigger **reissuance** of a new e-ticket number against the same order item. The old ticket is voided; the new one is created. Amendment of an existing e-ticket number is never permitted.
- `delivery.Ticket` rows are never deleted. Voiding sets `IsVoided = 1` and stamps `VoidedAt`. The voided row is retained permanently for audit.

### Accounting Events

- Creation of any `delivery.Ticket` row publishes a `TicketIssued` event to the event bus, consumed by the Accounting MS to record fare revenue.
- Voiding a `delivery.Ticket` row publishes a `TicketVoided` event to the event bus.
- Creation of any `delivery.Document` row publishes a `DocumentIssued` event to the event bus, consumed by the Accounting MS to record ancillary revenue.
- Voiding a `delivery.Document` row publishes a `DocumentVoided` event to the event bus.

### Seat Number Validation

The Delivery MS **trusts the seat number provided by its caller** — it does not validate seat numbers against the active seatmap. Seat validation (calling `GET /v1/seatmap/{aircraftType}` on the Seat MS) is exclusively the responsibility of the orchestration layer before calling any Delivery MS endpoint that accepts a seat number.

### Manifest Lifecycle

- Manifest rows are created when a ticket is issued.
- On cancellation or flight change, the relevant manifest rows are **hard-deleted** via `DELETE /v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}`.
- On IROPS rebooking, the old manifest rows are deleted and new ones are written for the replacement flight.
- Manifest rows are updated (not replaced) for seat changes, check-in status, SSR updates, and schedule delays.

### Optimistic Concurrency

`delivery.Ticket` and `delivery.Manifest` records implement Optimistic Concurrency Control (OCC) using an integer `Version` column. Every mutation must supply the caller's known version. If the `UPDATE` affects 0 rows (version mismatch), the operation is rejected with `409 Conflict`. The caller must re-fetch and retry. See [Optimistic Concurrency Control](../api.md#optimistic-concurrency-control) in `api.md` for the full specification.

### SSR Codes in Manifest

`SsrCodes` on `delivery.Manifest` rows are stored as a JSON array (e.g. `["VGML","WCHR"]`), not a CSV string. An empty array `[]` is stored when no SSRs are held. SSR codes are written at booking confirmation and updated via `PATCH /v1/manifest/{bookingRef}` following a self-serve SSR change.

### BCBP Barcode Format

Boarding cards include a barcode string compliant with **IATA Resolution 792** (Bar Coded Boarding Pass). The Delivery MS assembles this string at boarding card generation from data on the `delivery.Manifest` row and the confirmed order. The string is returned alongside human-readable fields; channels render it using their preferred barcode library (PDF417 for print, QR for mobile).

BCBP field order: format code + leg count, passenger name (surname/given padded to 20 chars), electronic ticket indicator + booking reference, origin IATA code, destination IATA code, operating carrier, flight number (4-char padded), Julian departure date, cabin code, seat number (4-char padded), sequence number, passenger status, conditional section with BCBP version, Julian issue date + issuer code, operating carrier (repeated), loyalty number, airline-specific data.

Example: `M1TAYLOR/ALEX        EAB1234 LHRJFKAX 0003 042J001A0001 156>518 W6042 AX 2A00000012345678 JAX7KLP2NZR901A`

---

## Data Schema

### `delivery.Ticket`

One row per issued e-ticket: one passenger across all flight segments in the booking. This is the accountable document and monetary unit of travel entitlement. Coupon-level value is always derived from the stored fare data — never stored directly. Additional detail is stored in the `TicketData` JSON column for extensibility.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `TicketId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `TicketNumber` | BIGINT IDENTITY | No | | UK | System-generated sequential number. Combined with airline prefix to form the IATA e-ticket number, e.g. `932-1000000001` |
| `BookingReference` | CHAR(6) | No | | | e.g. `AB1234` |
| `PassengerId` | VARCHAR(20) | No | | | PAX reference from the order, e.g. `PAX-1` |
| `TotalFareAmount` | DECIMAL(10,2) | No | `0` | | Passenger air fare in `Currency`. Excludes taxes. |
| `Currency` | CHAR(3) | No | `'GBP'` | | ISO 4217 collecting currency |
| `TotalTaxAmount` | DECIMAL(10,2) | No | `0` | | Sum of all taxes in `Currency` |
| `TotalAmount` | DECIMAL(10,2) | No | `0` | | `TotalFareAmount + TotalTaxAmount`. Computed column |
| `FareCalculation` | NVARCHAR(500) | No | `''` | | IATA linear fare calculation string, e.g. `LON BA NYC 500.00 BA LON 500.00 NUC1000.00 END ROE1.000000`. Used to derive per-coupon fare shares |
| `IsVoided` | BIT | No | `0` | | Set to `1` on voluntary change, cancellation, or IROPS reissuance. Never deleted |
| `VoidedAt` | DATETIME2 | Yes | | | Null until voided |
| `TicketData` | NVARCHAR(MAX) | No | | | JSON document. See TicketData JSON Structure |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on every update** |
| `Version` | INT | No | `1` | | Optimistic concurrency counter; incremented on every write |

> **Indexes:** `IX_Ticket_TicketNumber` (unique) on `(TicketNumber)`. `IX_Ticket_BookingReference` on `(BookingReference)`.
> **Constraints:** `CHK_TicketData` — `ISJSON(TicketData) = 1`. `CHK_Ticket_Ccy` — `LEN(RTRIM(Currency)) = 3`.
> **Immutability:** Rows are never deleted. Voiding sets `IsVoided = 1`. Reissuance creates a new row with a new `TicketNumber` and voids the old row in the same transaction.

#### TicketData JSON Structure

Financial columns already on `delivery.Ticket` (`totalFareAmount`, `currency`, `totalTaxAmount`, `totalAmount`, `fareCalculation`) and identity columns (`ticketNumber`, `bookingReference`, `passengerId`, `isVoided`, `voidedAt`, `createdAt`, `updatedAt`) are excluded from the JSON. The JSON carries operational and extensible detail: passenger identity, coupons (one per flight segment), SSR codes, form of payment, and change history. Per-coupon **fare share and tax amounts are always derived** from the stored columns — they are not embedded in the JSON.

```json
{
  "passenger": {
    "surname": "Taylor",
    "givenName": "Alex",
    "passengerTypeCode": "ADT",
    "frequentFlyer": {
      "carrier": "AX",
      "number": "AX12345678",
      "tier": "Gold"
    }
  },
  "formOfPayment": {
    "type": "CC",
    "cardType": "VI",
    "maskedPan": "4111111111111111",
    "expiryMmYy": "0830",
    "approvalCode": "AUTH123",
    "amount": 1100.00,
    "currency": "GBP"
  },
  "commission": { "type": "PERCENT", "rate": 0, "amount": 0 },
  "endorsementsRestrictions": "NON-END NON-REF",
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
      "status": "Open",
      "marketing": { "carrier": "AX", "flightNumber": "AX003" },
      "operating": { "carrier": "AX", "flightNumber": "AX003" },
      "origin": "LHR",
      "destination": "JFK",
      "departureDate": "2025-08-15",
      "departureTime": "11:00",
      "classOfService": "J",
      "cabin": "Business",
      "fareBasisCode": "JFLEXGB",
      "notValidBefore": "2025-08-15",
      "notValidAfter": null,
      "stopoverIndicator": "O",
      "baggageAllowance": { "type": "PC", "quantity": 2, "weightKg": null },
      "seat": "1A",
      "attributedTaxCodes": ["GB", "UB", "YQ"]
    },
    {
      "couponNumber": 2,
      "status": "Open",
      "marketing": { "carrier": "AX", "flightNumber": "AX004" },
      "operating": { "carrier": "AX", "flightNumber": "AX004" },
      "origin": "JFK",
      "destination": "LHR",
      "departureDate": "2025-08-25",
      "departureTime": "20:00",
      "classOfService": "J",
      "cabin": "Business",
      "fareBasisCode": "JFLEXGB",
      "notValidBefore": "2025-08-25",
      "notValidAfter": null,
      "stopoverIndicator": "O",
      "baggageAllowance": { "type": "PC", "quantity": 2, "weightKg": null },
      "seat": "2A",
      "attributedTaxCodes": ["US", "XY", "YC", "XA", "YQ"]
    }
  ],
  "ssrCodes": [
    { "code": "VGML", "description": "Vegetarian meal", "segmentRef": "SEG-1" }
  ],
  "changeHistory": [
    {
      "eventType": "Issued",
      "occurredAt": "2025-06-01T09:14:00Z",
      "actor": "RetailAPI",
      "detail": "Initial ticket issuance"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `passenger.surname` | string | Passenger surname |
| `passenger.givenName` | string | Passenger given name |
| `passenger.passengerTypeCode` | string | IATA type: `ADT`, `CHD`, `INF`, `YTH` |
| `passenger.frequentFlyer` | object \| null | Frequent flyer details if provided; `null` if not enrolled |
| `formOfPayment` | object \| null | Payment details. `null` if not captured at ticketing time |
| `commission` | object | Agent commission. `{ type: "PERCENT", rate: 0, amount: 0 }` if no commission |
| `endorsementsRestrictions` | string \| null | Fare endorsements, e.g. `NON-END NON-REF` |
| `tourCode` | string \| null | Tour operator code if applicable |
| `originalIssue` | object | Exchange reference fields. All `null` on initial issuance |
| `coupons` | array | One coupon object per flight segment, in itinerary order |
| `coupons[].couponNumber` | integer | 1-based sequence number within this ticket |
| `coupons[].status` | string | `Open` · `CheckedIn` · `Lifted` · `Flown` · `Refunded` · `Void` · `Exchanged` · `PrintExchange` |
| `coupons[].marketing` | object | Marketing carrier code and flight number |
| `coupons[].operating` | object | Operating carrier and flight number. Matches marketing if no codeshare |
| `coupons[].origin` / `destination` | string | IATA 3-letter airport codes |
| `coupons[].departureDate` / `departureTime` | string | ISO 8601 date and local time |
| `coupons[].classOfService` | string | Booking class code, e.g. `J` |
| `coupons[].cabin` | string | Cabin name, e.g. `Business` |
| `coupons[].fareBasisCode` | string | e.g. `JFLEXGB` |
| `coupons[].notValidBefore` / `notValidAfter` | string \| null | Validity window dates; `notValidAfter` is `null` until exchange |
| `coupons[].stopoverIndicator` | string | `O` = stopover permitted, `X` = transit only |
| `coupons[].baggageAllowance` | object \| null | `{ type, quantity, weightKg }`. `null` if not provided |
| `coupons[].seat` | string \| null | Seat number if assigned; `null` if not yet selected |
| `coupons[].attributedTaxCodes` | array | Tax codes from the ticket-level tax breakdown that apply to this coupon. Informational — do not use for amounts; use `GET /v1/tickets/{eTicketNumber}/coupons/{couponNumber}/value` for derived monetary values |
| `ssrCodes` | array | IATA SSR codes across all segments for this passenger. Each entry has `code`, `description`, and `segmentRef` |
| `changeHistory` | array | Append-only audit trail. Each entry: `eventType`, `occurredAt`, `actor`, `detail` |

---

### `delivery.TicketTax`

One row per tax line per ticket. Stores the authoritative amount for each tax code collected. For taxes that split per coupon (YQ/YR carrier surcharges), the total is divided equally across coupons and stored as separate rows — one per coupon — to allow `GetAttributedValue` to sum without double-counting.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `TicketTaxId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `TicketId` | UNIQUEIDENTIFIER | No | | FK → `delivery.Ticket(TicketId)` | Cascade-deleted when parent ticket row is deleted (never happens in practice — tickets are voided not deleted) |
| `TaxCode` | VARCHAR(4) | No | | | IATA tax code, e.g. `GB`, `YQ`, `US` |
| `Amount` | DECIMAL(10,2) | No | | | Tax amount in `Currency` |
| `Currency` | CHAR(3) | No | | | ISO 4217. Normally the same as the ticket's collecting currency; may differ for taxes quoted in a specific currency |

> **Indexes:** `IX_TicketTax_TicketId` on `(TicketId)`.

---

### `delivery.TicketTaxCoupon`

Junction table linking each `TicketTax` row to the coupon numbers it applies to. For departure/arrival taxes, one row per applicable coupon. For split-per-coupon surcharges (YQ/YR), each `TicketTax` row has exactly one `TicketTaxCoupon` row because the split produces one tax record per coupon.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `TicketTaxCouponId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `TicketTaxId` | UNIQUEIDENTIFIER | No | | FK → `delivery.TicketTax(TicketTaxId)` | |
| `CouponNumber` | TINYINT | No | | | 1–4 (IATA coupon limit per ticket) |

> **Constraints:** `CHK_TicketTaxCoupon_Number` — `CouponNumber BETWEEN 1 AND 4`. `UQ_TicketTaxCoupon` — unique on `(TicketTaxId, CouponNumber)`.

---

### `delivery.Manifest`

Operational source of truth for who is on a given flight. One row per passenger per flight segment. Created when a ticket is issued; updated on seat changes, check-in, SSR updates, and delays; hard-deleted on cancellation or flight removal.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `ManifestId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `TicketId` | UNIQUEIDENTIFIER | No | | FK → `delivery.Ticket(TicketId)` | |
| `InventoryId` | UNIQUEIDENTIFIER | No | | | Cross-schema ref to `offer.FlightInventory(InventoryId)`. Not a DB FK |
| `FlightNumber` | VARCHAR(10) | No | | | Denormalised, e.g. `AX003` |
| `DepartureDate` | DATE | No | | | Denormalised |
| `AircraftType` | CHAR(4) | No | | | Used for seatmap validation by the orchestration layer at write time |
| `SeatNumber` | VARCHAR(5) | No | | | e.g. `1A`, `22K`. Validated by the orchestration layer before passing to this service |
| `CabinCode` | CHAR(1) | No | | | `F` · `J` · `W` · `Y` |
| `BookingReference` | CHAR(6) | No | | | e.g. `AB1234` |
| `ETicketNumber` | VARCHAR(20) | No | | | Denormalised from `delivery.Ticket` |
| `PassengerId` | VARCHAR(20) | No | | | PAX reference from the order, e.g. `PAX-1` |
| `GivenName` | VARCHAR(100) | No | | | Denormalised for manifest readability |
| `Surname` | VARCHAR(100) | No | | | Denormalised for manifest readability |
| `SsrCodes` | NVARCHAR(500) | Yes | | | JSON array of IATA SSR codes, e.g. `["VGML","WCHR"]`. Empty array `[]` when no SSRs held |
| `DepartureTime` | TIME | No | | | Local departure time; updated by Operations API on delay |
| `ArrivalTime` | TIME | No | | | Local arrival time; updated by Operations API on delay |
| `CheckedIn` | BIT | No | `0` | | |
| `CheckedInAt` | DATETIME2 | Yes | | | Null until check-in completed |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on every update** |
| `Version` | INT | No | `1` | | Optimistic concurrency counter; incremented on every write |

> **Indexes:** `IX_Manifest_Seat` (unique) on `(InventoryId, SeatNumber)` — prevents double-assignment of a seat on a flight. `IX_Manifest_Pax` (unique) on `(InventoryId, ETicketNumber)` — prevents duplicate manifest entries for the same passenger. `IX_Manifest_Flight` on `(FlightNumber, DepartureDate)` — used for gate staff and IROPS manifest retrieval. `IX_Manifest_BookingReference` on `(BookingReference)`.

---

### `delivery.Document`

Ancillary accountable document records — EMD (Electronic Miscellaneous Document) equivalent. One row per ancillary sale. Enables the Accounting system to track non-fare revenue independently.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `DocumentId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `DocumentNumber` | VARCHAR(20) | No | | UK | e.g. `932-EMD-0001234`. System-generated |
| `DocumentType` | VARCHAR(30) | No | | | `SeatAncillary` · `BagAncillary` |
| `BookingReference` | CHAR(6) | No | | | Associated booking reference |
| `ETicketNumber` | VARCHAR(20) | No | | | Associated e-ticket; links ancillary to the travel segment it covers |
| `PassengerId` | VARCHAR(20) | No | | | PAX reference from the order |
| `SegmentRef` | VARCHAR(20) | No | | | Segment identifier the ancillary applies to, e.g. `SEG-1` |
| `PaymentId` | VARCHAR(20) | No | | | Associated payment identifier, e.g. `a1b2c3d4-e5f6-7890-abcd-ef1234567890` |
| `Amount` | DECIMAL(10,2) | No | | | Amount charged for this ancillary item |
| `CurrencyCode` | CHAR(3) | No | `'GBP'` | | ISO 4217 |
| `IsVoided` | BIT | No | `0` | | Set to `1` if the ancillary is refunded or reversed |
| `DocumentData` | NVARCHAR(MAX) | No | | | JSON document. See DocumentData JSON Structure |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on every update** |

> **Indexes:** `IX_Document_Number` (unique) on `(DocumentNumber)`. `IX_Document_BookingReference` on `(BookingReference)`. `IX_Document_ETicketNumber` on `(ETicketNumber)`.
> **Constraints:** `CHK_DocumentData` — `ISJSON(DocumentData) = 1`.

#### DocumentData JSON Structure

The `ancillaryDetail` object is a discriminated union — its shape depends on `documentType`. Typed columns already on `delivery.Document` are excluded from the JSON.

**SeatAncillary example:**
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

**BagAncillary example:**
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
    "taxes": [],
    "totalAmount": 60.00,
    "currencyCode": "GBP"
  },
  "voidHistory": []
}
```

| Field | Type | Description |
|-------|------|-------------|
| `emdType` | string | `EMD-A` (associated — tied to a specific e-ticket/flight coupon) or `EMD-S` (standalone). All current ancillaries are `EMD-A` |
| `rfic` | string | IATA Reason for Issuance Code — `A` (air transportation/seat) or `C` (baggage) |
| `rfisc` | string | IATA sub-code identifying the specific service |
| `serviceDescription` | string | Human-readable service description |
| `couponStatus` | string | `Open` → `Used` (set by Airport API at bag drop/boarding) · `Void` (set on refund) |
| `ancillaryDetail.type` | string | `SeatAncillary` or `BagAncillary` — discriminator |
| `bagTagNumber` | string | `null` at purchase; populated by Airport API when the bag is physically checked in |
| `voidHistory` | array | Append-only array of `{ occurredAt, actor, reason }` entries added when `IsVoided` is set to `1` |

---

## Endpoints

---

### POST /v1/tickets

Issue e-tickets for all passengers in a booking. Creates one `delivery.Ticket` row **per passenger** (covering all flight segments). Publishes a `TicketIssued` event for each ticket created.

**When to use:** Called by the Retail API immediately after payment authorisation, before inventory settlement and order confirmation. If ticketing fails, the Retail API must abort, void the payment authorisation, and return an error to the channel. The order must not be confirmed until e-ticket numbers have been successfully issued.

**Failure handling:** If ticket issuance fails, the Retail API must not proceed to inventory settlement or order confirmation. Payment authorisation must be voided and inventory released.

#### Request

```json
{
  "bookingReference": "AB1234",
  "passengers": [
    {
      "passengerId": "PAX-1",
      "surname": "Taylor",
      "givenName": "Alex",
      "passengerTypeCode": "ADT",
      "frequentFlyer": { "carrier": "AX", "number": "AX12345678", "tier": "Gold" },
      "fareConstruction": {
        "baseFare": 1000.00,
        "collectingCurrency": "GBP",
        "totalTaxes": 100.00,
        "fareCalculationLine": "LON AX NYC 500.00 AX LON 500.00 NUC1000.00 END ROE1.000000",
        "taxes": [
          { "code": "GB", "amount": 26.00, "currency": "GBP" },
          { "code": "UB", "amount": 24.00, "currency": "GBP" },
          { "code": "YQ", "amount": 40.00, "currency": "GBP" },
          { "code": "US", "amount": 10.00, "currency": "USD" }
        ]
      },
      "formOfPayment": {
        "type": "CC",
        "cardType": "VI",
        "maskedPan": "411111XXXXXX1111",
        "expiryMmYy": "0830",
        "approvalCode": "AUTH123",
        "amount": 1100.00,
        "currency": "GBP"
      },
      "endorsementsRestrictions": "NON-END NON-REF"
    },
    {
      "passengerId": "PAX-2",
      "surname": "Taylor",
      "givenName": "Jordan",
      "passengerTypeCode": "ADT",
      "fareConstruction": {
        "baseFare": 1000.00,
        "collectingCurrency": "GBP",
        "totalTaxes": 100.00,
        "fareCalculationLine": "LON AX NYC 500.00 AX LON 500.00 NUC1000.00 END ROE1.000000",
        "taxes": [
          { "code": "GB", "amount": 26.00, "currency": "GBP" },
          { "code": "UB", "amount": 24.00, "currency": "GBP" },
          { "code": "YQ", "amount": 40.00, "currency": "GBP" },
          { "code": "US", "amount": 10.00, "currency": "USD" }
        ]
      },
      "formOfPayment": {
        "type": "CC",
        "cardType": "VI",
        "maskedPan": "411111XXXXXX1111",
        "expiryMmYy": "0830",
        "approvalCode": "AUTH456",
        "amount": 1100.00,
        "currency": "GBP"
      }
    }
  ],
  "segments": [
    {
      "segmentId": "SEG-1",
      "flightNumber": "AX003",
      "operatingFlightNumber": null,
      "departureDate": "2025-08-15",
      "departureTime": "11:00",
      "origin": "LHR",
      "destination": "JFK",
      "cabinCode": "J",
      "cabinName": "Business",
      "fareBasisCode": "JFLEXGB",
      "stopoverIndicator": "O",
      "baggageAllowance": { "type": "PC", "quantity": 2, "weightKg": null },
      "seatAssignments": [
        { "passengerId": "PAX-1", "seatNumber": "1A" },
        { "passengerId": "PAX-2", "seatNumber": "1K" }
      ],
      "ssrCodes": [
        { "passengerId": "PAX-1", "code": "VGML", "description": "Vegetarian meal", "segmentRef": "SEG-1" }
      ]
    },
    {
      "segmentId": "SEG-2",
      "flightNumber": "AX004",
      "operatingFlightNumber": null,
      "departureDate": "2025-08-25",
      "departureTime": "20:00",
      "origin": "JFK",
      "destination": "LHR",
      "cabinCode": "J",
      "cabinName": "Business",
      "fareBasisCode": "JFLEXGB",
      "stopoverIndicator": "O",
      "baggageAllowance": { "type": "PC", "quantity": 2, "weightKg": null },
      "seatAssignments": [
        { "passengerId": "PAX-1", "seatNumber": "2A" },
        { "passengerId": "PAX-2", "seatNumber": "2K" }
      ],
      "ssrCodes": []
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bookingReference` | string | Yes | The 6-character booking reference |
| `passengers` | array | Yes | List of all passengers on the booking. One ticket is issued per passenger |
| `passengers[].passengerId` | string | Yes | PAX reference, e.g. `PAX-1` |
| `passengers[].surname` | string | Yes | Passenger's surname |
| `passengers[].givenName` | string | Yes | Passenger's given name |
| `passengers[].passengerTypeCode` | string | No | IATA type: `ADT` (default), `CHD`, `INF`, `YTH` |
| `passengers[].frequentFlyer` | object | No | `{ carrier, number, tier }`. Omit if not enrolled |
| `passengers[].fareConstruction` | object | **Yes** | Financial data for the ticket. Required for every passenger |
| `passengers[].fareConstruction.baseFare` | number | Yes | Passenger air fare in `collectingCurrency`, excluding taxes. Must be > 0 |
| `passengers[].fareConstruction.collectingCurrency` | string | Yes | ISO 4217, 3 characters |
| `passengers[].fareConstruction.totalTaxes` | number | Yes | Sum of all taxes in `collectingCurrency`. Must be ≥ 0. Must equal the sum of `taxes[].amount` within ±0.02 |
| `passengers[].fareConstruction.fareCalculationLine` | string | Yes | IATA linear fare calculation string. Must be parseable: city codes, carrier codes, NUC amounts, `NUCxxx`, `END`, `ROExxx` |
| `passengers[].fareConstruction.taxes` | array | Yes | Per-tax breakdown. Each entry: `{ code, amount, currency }`. `currency` may be an empty string to inherit `collectingCurrency` |
| `passengers[].formOfPayment` | object | No | Form of payment detail |
| `passengers[].commission` | object | No | Agent commission: `{ type, rate, amount }`. Defaults to zero if omitted |
| `passengers[].endorsementsRestrictions` | string | No | Free-text fare endorsements, e.g. `NON-END NON-REF` |
| `segments` | array | Yes | List of all flight segments. 1–4 segments (IATA coupon limit per ticket) |
| `segments[].segmentId` | string | Yes | Segment reference, e.g. `SEG-1` |
| `segments[].flightNumber` | string | Yes | Marketing flight number, e.g. `AX003` |
| `segments[].operatingFlightNumber` | string | No | Operating flight number if codeshare; `null` if same as marketing |
| `segments[].departureDate` | string (date) | Yes | ISO 8601 date |
| `segments[].departureTime` | string (time) | No | Local departure time, `HH:mm` |
| `segments[].origin` | string | Yes | IATA 3-letter airport code |
| `segments[].destination` | string | Yes | IATA 3-letter airport code |
| `segments[].cabinCode` | string | Yes | `F`, `J`, `W`, or `Y` |
| `segments[].cabinName` | string | No | Human-readable cabin name, e.g. `Business` |
| `segments[].fareBasisCode` | string | No | e.g. `JFLEXGB` |
| `segments[].stopoverIndicator` | string | No | `O` (stopover) or `X` (transit). Defaults to `O` |
| `segments[].baggageAllowance` | object | No | `{ type, quantity, weightKg }` |
| `segments[].seatAssignments` | array | No | Per-passenger seat assignments: `{ passengerId, seatNumber }` |
| `segments[].ssrCodes` | array | No | Per-passenger SSR codes: `{ passengerId, code, description, segmentRef }` |

> **Validation:** Returns `400 Bad Request` with FluentValidation error details if any required field is missing, `fareCalculationLine` cannot be parsed, or the tax breakdown sum does not match `totalTaxes` within ±0.02.

#### Response — `201 Created`

```json
{
  "tickets": [
    {
      "ticketId": "e1f2a3b4-c5d6-7890-abcd-ef1234567890",
      "eTicketNumber": "932-1000000001",
      "passengerId": "PAX-1",
      "segmentIds": ["SEG-1", "SEG-2"]
    },
    {
      "ticketId": "f2a3b4c5-d6e7-8901-bcde-f12345678901",
      "eTicketNumber": "932-1000000002",
      "passengerId": "PAX-2",
      "segmentIds": ["SEG-1", "SEG-2"]
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `tickets` | array | One entry per issued e-ticket — one per passenger |
| `tickets[].ticketId` | string (UUID) | Internal ticket record identifier |
| `tickets[].eTicketNumber` | string | IATA-format e-ticket number, e.g. `932-1000000001` |
| `tickets[].passengerId` | string | PAX reference identifying which passenger holds this ticket |
| `tickets[].segmentIds` | array | Segment IDs covered by this ticket (all segments in the booking) |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid fare calculation line, or tax breakdown mismatch |
| `409 Conflict` | Tickets have already been issued for this booking reference |
| `422 Unprocessable Entity` | Duplicate seat assignment detected — two passengers assigned to the same seat on the same segment |

---

### PATCH /v1/tickets/{eTicketNumber}/void

Void an issued e-ticket. Sets `IsVoided = 1` and stamps `VoidedAt`. The row is retained permanently — ticket rows are never deleted. Publishes a `TicketVoided` event to the event bus.

**When to use:** Called by the Retail API during voluntary flight change, voluntary cancellation, or IROPS processing (via Operations API) before reissuance or order cancellation. All tickets on the affected segment(s) must be voided before new tickets are issued.

**Reissuance pattern:** Void the old ticket via this endpoint, then call `POST /v1/tickets/reissue` to create new tickets. Both operations should be performed in close succession to minimise the window where a passenger has no valid ticket.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `eTicketNumber` | string | The IATA-format e-ticket number to void, e.g. `932-1234567890` |

#### Request

```json
{
  "reason": "VoluntaryChange",
  "actor": "RetailAPI",
  "version": 1
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `reason` | string | Yes | Reason for voiding: `VoluntaryChange`, `VoluntaryCancellation`, `IROPSRebook`, `NameCorrection`, `AdminVoid` |
| `actor` | string | Yes | Which system initiated the void, e.g. `RetailAPI`, `DisruptionAPI` |
| `version` | integer | Yes | Current `Version` value from the ticket record. Used for optimistic concurrency control — returns `409 Conflict` if the stored version does not match |

#### Response — `200 OK`

```json
{
  "eTicketNumber": "932-1234567890",
  "isVoided": true,
  "voidedAt": "2026-03-17T14:22:00Z"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `eTicketNumber` | string | The voided e-ticket number |
| `isVoided` | boolean | Always `true` on success |
| `voidedAt` | string (datetime) | ISO 8601 UTC timestamp when the void was applied |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | No ticket found for the given e-ticket number |
| `409 Conflict` | Version mismatch — ticket has been modified by another request. Re-fetch and retry |
| `422 Unprocessable Entity` | Ticket is already voided |

---

### POST /v1/tickets/reissue

Reissue e-tickets following a passenger name correction, seat change, flight change, or IROPS rebooking. Voids the original tickets and issues replacements in a single atomic operation. Publishes `TicketVoided` and `TicketIssued` events for each ticket pair.

**When to use:** Called by the Retail API after a name correction, post-sale seat change, voluntary flight change, or by the Operations API during IROPS rebooking. Reissuance is required whenever a change invalidates the data encoded in the existing ticket (passenger name is encoded in the BCBP barcode string; seat changes should trigger reissuance for consistency).

**Scenarios requiring reissuance:**
- PAX name correction (given name or surname changed)
- Post-sale seat change (seat number is encoded on the boarding pass)
- Voluntary flight change (new flight segment, new fare details)
- IROPS rebooking (replacement flight, potentially different seat and fare)
- Material schedule change exceeding the 60-minute threshold (departure/arrival times updated; Operations API only)

**Scenarios that do NOT require reissuance:**
- Passport/travel document updates
- Contact information updates
- SSR code additions or changes

#### Request

```json
{
  "bookingReference": "AB1234",
  "voidedETicketNumbers": ["932-1234567890", "932-1234567891"],
  "passengers": [
    {
      "passengerId": "PAX-1",
      "givenName": "Alex",
      "surname": "Taylor-Smith"
    }
  ],
  "segments": [
    {
      "segmentId": "SEG-1",
      "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "flightNumber": "AX003",
      "departureDate": "2025-08-15",
      "origin": "LHR",
      "destination": "JFK",
      "cabinCode": "J",
      "fareBasisCode": "JFLEXGB",
      "seatAssignments": [
        { "passengerId": "PAX-1", "seatNumber": "1A", "positionType": "Window", "deckCode": "M" }
      ],
      "ssrCodes": []
    }
  ],
  "reason": "NameCorrection",
  "actor": "RetailAPI"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bookingReference` | string | Yes | The 6-character booking reference |
| `voidedETicketNumbers` | array | Yes | List of e-ticket numbers to void as part of this reissuance |
| `passengers` | array | Yes | Updated passenger details for the reissued tickets |
| `segments` | array | Yes | Segment details for the new tickets. For a flight change, this contains the replacement segment. For a name correction, this contains the same segment with updated PAX name |
| `reason` | string | Yes | `NameCorrection`, `SeatChange`, `VoluntaryChange`, `IROPSRebook`, `ScheduleChange` |
| `actor` | string | Yes | Which system initiated the reissuance, e.g. `RetailAPI`, `DisruptionAPI` |

> **Atomicity:** The void and reissuance occur in a single database transaction. If the new ticket creation fails, the void is rolled back.

#### Response — `200 OK`

```json
{
  "voidedETicketNumbers": ["932-1234567890", "932-1234567891"],
  "tickets": [
    {
      "ticketId": "c5d6e7f8-a9b0-1234-efab-345678901234",
      "eTicketNumber": "932-1234567900",
      "passengerId": "PAX-1",
      "segmentId": "SEG-1",
      "flightNumber": "AX003",
      "departureDate": "2025-08-15"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `voidedETicketNumbers` | array | The e-ticket numbers that were voided as part of this operation |
| `tickets` | array | The newly issued e-tickets, in the same schema as `POST /v1/tickets` |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid format |
| `404 Not Found` | One or more `voidedETicketNumbers` not found |
| `422 Unprocessable Entity` | One or more tickets in `voidedETicketNumbers` are already voided |

---

### GET /v1/tickets/{eTicketNumber}

Retrieve full details for an issued e-ticket. Returns stored financial data and a derived fare-component breakdown parsed from the `FareCalculation` line.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `eTicketNumber` | string | IATA-format e-ticket number, e.g. `932-1000000001` |

#### Response — `200 OK`

```json
{
  "ticketId": "e1f2a3b4-c5d6-7890-abcd-ef1234567890",
  "eTicketNumber": "932-1000000001",
  "bookingReference": "AB1234",
  "passengerId": "PAX-1",
  "totalFareAmount": 1000.00,
  "currency": "GBP",
  "totalTaxAmount": 100.00,
  "totalAmount": 1100.00,
  "fareCalculation": "LON AX NYC 500.00 AX LON 500.00 NUC1000.00 END ROE1.000000",
  "fareComponents": [
    { "origin": "LON", "carrier": "AX", "destination": "NYC", "nucAmount": 500.00, "fareBasis": null },
    { "origin": "NYC", "carrier": "AX", "destination": "LON", "nucAmount": 500.00, "fareBasis": null }
  ],
  "taxBreakdown": [
    { "taxCode": "GB", "amount": 26.00, "currency": "GBP", "appliesToCouponNumbers": [1] },
    { "taxCode": "UB", "amount": 24.00, "currency": "GBP", "appliesToCouponNumbers": [1] },
    { "taxCode": "YQ", "amount": 20.00, "currency": "GBP", "appliesToCouponNumbers": [1] },
    { "taxCode": "YQ", "amount": 20.00, "currency": "GBP", "appliesToCouponNumbers": [2] },
    { "taxCode": "US", "amount": 10.00, "currency": "USD", "appliesToCouponNumbers": [2] }
  ],
  "isVoided": false,
  "voidedAt": null,
  "ticketData": { }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `ticketId` | string (UUID) | Internal ticket identifier |
| `eTicketNumber` | string | IATA-format e-ticket number |
| `bookingReference` | string | 6-character booking reference |
| `passengerId` | string | PAX reference |
| `totalFareAmount` | number | Stored passenger air fare in `currency` |
| `currency` | string | ISO 4217 collecting currency |
| `totalTaxAmount` | number | Stored total tax amount |
| `totalAmount` | number | `totalFareAmount + totalTaxAmount` |
| `fareCalculation` | string | Stored IATA linear fare calculation string |
| `fareComponents` | array \| null | Derived fare components parsed from `fareCalculation`. `null` if the string cannot be parsed |
| `fareComponents[].origin` / `destination` | string | City code (3-letter alpha, may be city not airport) |
| `fareComponents[].carrier` | string | 2-letter carrier code |
| `fareComponents[].nucAmount` | number | NUC amount for this component |
| `taxBreakdown` | array | Stored tax rows. YQ/YR appear as multiple rows (one per coupon) reflecting the even split |
| `taxBreakdown[].taxCode` | string | IATA tax code |
| `taxBreakdown[].amount` | number | Tax amount for the attributed coupons |
| `taxBreakdown[].currency` | string | Currency of this tax amount |
| `taxBreakdown[].appliesToCouponNumbers` | array | Coupon numbers this tax row covers |
| `isVoided` | boolean | Whether the ticket has been voided |
| `voidedAt` | string \| null | ISO 8601 UTC void timestamp, or `null` |
| `ticketData` | object | Full TicketData JSON. See TicketData JSON Structure |

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No ticket found for the given e-ticket number |

---

### GET /v1/tickets/{eTicketNumber}/coupons/{couponNumber}/value

Derive the monetary value attributed to a specific flight coupon. Returns the proportional fare share (weighted by NUC component amounts in the fare calculation) and the sum of taxes attributed to this coupon. All values are **derived** — they are never stored directly.

**When to use:** Called by the Accounting MS when recognising revenue for a specific flight leg, or by any consumer needing per-segment financial attribution without performing the IATA fare calculation locally.

**Attribution logic:**
- **Fare share** — `(couponNUC / totalNUC) × TotalFareAmount`, where `couponNUC` is the NUC amount for the matching fare component in `FareCalculation`. Rounded to 2 decimal places (HALF_UP).
- **Tax share** — sum of `TicketTax.Amount` for all tax rows whose `AppliedToCoupons` includes `couponNumber`. For split-per-coupon taxes (YQ/YR), the split is already stored as separate rows, so summation is correct without further division.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `eTicketNumber` | string | IATA-format e-ticket number |
| `couponNumber` | integer | Coupon sequence number (1-based, 1–4) |

#### Response — `200 OK`

```json
{
  "eTicketNumber": "932-1000000001",
  "couponNumber": 1,
  "fareShare": 500.00,
  "taxShare": 70.00,
  "total": 570.00,
  "currency": "GBP",
  "valueSource": "derived"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `eTicketNumber` | string | The e-ticket number, echoed back |
| `couponNumber` | integer | The coupon number, echoed back |
| `fareShare` | number | Proportional fare amount attributed to this coupon |
| `taxShare` | number | Sum of tax amounts attributed to this coupon |
| `total` | number | `fareShare + taxShare` |
| `currency` | string | ISO 4217 collecting currency |
| `valueSource` | string | Always `"derived"` — signals this value is computed, not stored |

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No ticket found for the given e-ticket number, or coupon number is out of range for this ticket |
| `422 Unprocessable Entity` | The ticket's `FareCalculation` string cannot be parsed or contains no component matching the requested coupon number |

---

### POST /v1/manifest

Write operational manifest entries for a booking. Creates one `delivery.Manifest` row per passenger per flight segment. Called after order confirmation once e-ticket numbers, seat assignments, and booking reference are known.

**When to use:** Called by the Retail API at order confirmation (after `POST /v1/orders` succeeds on the Order MS). Also called by the Operations API when writing manifest entries for a replacement flight following IROPS rebooking.

**Seat validation:** The orchestration layer must validate all seat numbers against the active seatmap (via `GET /v1/seatmap/{aircraftType}` on the Seat MS) before calling this endpoint. The Delivery MS trusts the seat numbers provided.

> **Idempotency:** Repeated calls with the same `bookingReference` + `inventoryId` + `passengerId` combination return the existing manifest rows without creating duplicates.

#### Request

```json
{
  "bookingReference": "AB1234",
  "entries": [
    {
      "ticketId": "e1f2a3b4-c5d6-7890-abcd-ef1234567890",
      "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "flightNumber": "AX003",
      "departureDate": "2025-08-15",
      "departureTime": "11:00",
      "arrivalTime": "14:10",
      "aircraftType": "A351",
      "seatNumber": "1A",
      "cabinCode": "J",
      "eTicketNumber": "932-1234567890",
      "passengerId": "PAX-1",
      "givenName": "Alex",
      "surname": "Taylor",
      "ssrCodes": ["VGML"]
    },
    {
      "ticketId": "f2a3b4c5-d6e7-8901-bcde-f12345678901",
      "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "flightNumber": "AX003",
      "departureDate": "2025-08-15",
      "departureTime": "11:00",
      "arrivalTime": "14:10",
      "aircraftType": "A351",
      "seatNumber": "1K",
      "cabinCode": "J",
      "eTicketNumber": "932-1234567891",
      "passengerId": "PAX-2",
      "givenName": "Jordan",
      "surname": "Taylor",
      "ssrCodes": []
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bookingReference` | string | Yes | The 6-character booking reference |
| `entries` | array | Yes | One entry per passenger per segment |
| `entries[].ticketId` | string (UUID) | Yes | The `TicketId` from the issued `delivery.Ticket` record |
| `entries[].inventoryId` | string (UUID) | Yes | Flight inventory identifier |
| `entries[].flightNumber` | string | Yes | e.g. `AX003` |
| `entries[].departureDate` | string (date) | Yes | ISO 8601 date |
| `entries[].departureTime` | string (time) | Yes | Local departure time, `HH:mm` format |
| `entries[].arrivalTime` | string (time) | Yes | Local arrival time, `HH:mm` format |
| `entries[].aircraftType` | string | Yes | 4-character aircraft type code, e.g. `A351` |
| `entries[].seatNumber` | string | Yes | Validated seat number, e.g. `1A` |
| `entries[].cabinCode` | string | Yes | `F`, `J`, `W`, or `Y` |
| `entries[].eTicketNumber` | string | Yes | IATA-format e-ticket number |
| `entries[].passengerId` | string | Yes | PAX reference |
| `entries[].givenName` | string | Yes | Passenger's given name |
| `entries[].surname` | string | Yes | Passenger's surname |
| `entries[].ssrCodes` | array | No | Array of IATA SSR code strings, e.g. `["VGML","WCHR"]`. Empty array if none |

#### Response — `201 Created`

```json
{
  "manifestEntries": [
    {
      "manifestId": "d6e7f8a9-b0c1-2345-fabc-456789012345",
      "bookingReference": "AB1234",
      "eTicketNumber": "932-1234567890",
      "passengerId": "PAX-1",
      "flightNumber": "AX003",
      "departureDate": "2025-08-15",
      "seatNumber": "1A"
    }
  ]
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid format |
| `409 Conflict` | Seat already assigned on this flight (`IX_Manifest_Seat` constraint violation) |
| `422 Unprocessable Entity` | Duplicate manifest entry for the same passenger on the same flight (`IX_Manifest_Pax` constraint violation) |

---

### PUT /v1/manifest

Update manifest entries following a post-booking seat change. Replaces seat assignment fields on existing manifest rows. Called by the Retail API after a post-sale seat change on a confirmed order.

**When to use:** Called by the Retail API after `PATCH /v1/orders/{bookingRef}/seats` succeeds on the Order MS, and after e-ticket reissuance via `POST /v1/tickets/reissue`. Updates seat numbers and any related seat attributes on the relevant manifest rows.

**Seat validation:** The orchestration layer must validate new seat numbers against the active seatmap before calling this endpoint.

#### Request

```json
{
  "bookingReference": "AB1234",
  "updates": [
    {
      "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "passengerId": "PAX-1",
      "eTicketNumber": "932-1234567900",
      "seatNumber": "3A",
      "version": 1
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bookingReference` | string | Yes | The booking reference |
| `updates` | array | Yes | One entry per manifest row to update |
| `updates[].inventoryId` | string (UUID) | Yes | Identifies the specific flight |
| `updates[].passengerId` | string | Yes | Identifies the passenger |
| `updates[].eTicketNumber` | string | Yes | The new e-ticket number (post-reissuance) |
| `updates[].seatNumber` | string | Yes | The new validated seat number |
| `updates[].version` | integer | Yes | Current `Version` for optimistic concurrency |

#### Response — `200 OK`

```json
{
  "updated": 1
}
```

| Field | Type | Description |
|-------|------|-------------|
| `updated` | integer | Number of manifest rows updated |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid format |
| `404 Not Found` | No manifest entry found for the given booking reference, inventory ID, and passenger ID combination |
| `409 Conflict` | New seat is already assigned to another passenger on this flight, or version mismatch |

---

### PATCH /v1/manifest/{bookingRef}

Update manifest entries for a booking. Used to record check-in status (OLCI) and to update SSR codes following a self-serve SSR change. Applies partial updates to matching manifest rows.

**When to use:**
- Called by the Retail API when a passenger completes online check-in — sets `CheckedIn = 1` and stamps `CheckedInAt`.
- Called by the Retail API when a passenger adds, changes, or removes SSR codes via the manage-booking flow — updates the `SsrCodes` JSON array.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "updates": [
    {
      "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "passengerId": "PAX-1",
      "checkedIn": true,
      "checkedInAt": "2025-08-14T09:30:00Z",
      "ssrCodes": ["VGML", "WCHR"],
      "version": 2
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `updates` | array | Yes | One entry per manifest row to update |
| `updates[].inventoryId` | string (UUID) | Yes | Identifies the specific flight |
| `updates[].passengerId` | string | Yes | Identifies the passenger |
| `updates[].checkedIn` | boolean | No | Set to `true` to mark as checked in. Once set to `true`, cannot be reversed |
| `updates[].checkedInAt` | string (datetime) | No | ISO 8601 UTC timestamp of check-in. Required if `checkedIn` is `true` |
| `updates[].ssrCodes` | array | No | Replacement full list of IATA SSR code strings. Replaces the existing array entirely — not appended. Empty array removes all SSRs |
| `updates[].version` | integer | Yes | Current `Version` for optimistic concurrency |

#### Response — `200 OK`

```json
{
  "updated": 1
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid format, or `checkedIn` is `false` when the row is already checked in |
| `404 Not Found` | No manifest entry found for the given booking reference, inventory ID, and passenger ID |
| `409 Conflict` | Version mismatch — re-fetch and retry |

---

### PATCH /v1/manifest/{bookingRef}/flight

Update departure and arrival times on all manifest entries for a booking on a specific flight. Used exclusively by the Operations API when a flight delay changes scheduled times.

**When to use:** Called by the Operations API as part of the flight delay propagation flow, after updating segment times on the Order MS. Updates `DepartureTime` and `ArrivalTime` on every manifest row for the affected booking and flight.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "flightNumber": "AX101",
  "departureDate": "2026-03-22",
  "newDepartureTime": "16:30",
  "newArrivalTime": "19:45"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `flightNumber` | string | Yes | The affected flight number |
| `departureDate` | string (date) | Yes | ISO 8601 date of the affected flight |
| `newDepartureTime` | string (time) | Yes | Updated local departure time, `HH:mm` format |
| `newArrivalTime` | string (time) | Yes | Updated local arrival time, `HH:mm` format |

#### Response — `200 OK`

```json
{
  "updated": 2
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid format |
| `404 Not Found` | No manifest entries found for the given booking reference and flight |

---

### DELETE /v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}

Remove all manifest entries for a specific flight and booking. Hard-deletes all matching rows. Used when a booking is cancelled, a flight change removes a segment, or IROPS rebooking replaces a flight with an alternative.

**When to use:**
- Called by the Retail API during voluntary cancellation (after voiding all tickets for the booking).
- Called by the Retail API during voluntary flight change (before writing manifest for the replacement flight).
- Called by the Operations API during IROPS rebooking (before writing manifest for the replacement flight).

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |
| `flightNumber` | string | The flight number, e.g. `AX003` |
| `departureDate` | string (date) | ISO 8601 departure date of the flight, e.g. `2025-08-15` |

#### Request

```
DELETE /v1/manifest/AB1234/flight/AX003/2025-08-15
```

#### Response — `200 OK`

```json
{
  "deleted": 2
}
```

| Field | Type | Description |
|-------|------|-------------|
| `deleted` | integer | Number of manifest rows deleted |

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No manifest entries found for the given booking reference, flight number, and departure date combination |

---

### GET /v1/manifest

Retrieve the full passenger manifest for a flight. Returns all manifest entries for every passenger on the specified flight, regardless of booking reference. Used by the Operations API for cancellation rebooking and by departure control systems (DCS) for check-in validation.

**When to use:** Called by the Operations API when processing a flight cancellation to retrieve all affected passengers. Also callable by the Airport API for gate management and ground handling operations.

#### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `flightNumber` | string | Yes | The flight number, e.g. `AX003` |
| `departureDate` | string (date) | Yes | ISO 8601 departure date |

#### Request

```
GET /v1/manifest?flightNumber=AX003&departureDate=2025-08-15
```

#### Response — `200 OK`

```json
{
  "flightNumber": "AX003",
  "departureDate": "2025-08-15",
  "totalPassengers": 186,
  "entries": [
    {
      "manifestId": "d6e7f8a9-b0c1-2345-fabc-456789012345",
      "bookingReference": "AB1234",
      "eTicketNumber": "932-1234567890",
      "passengerId": "PAX-1",
      "givenName": "Alex",
      "surname": "Taylor",
      "seatNumber": "1A",
      "cabinCode": "J",
      "ssrCodes": ["VGML"],
      "departureTime": "11:00",
      "arrivalTime": "14:10",
      "checkedIn": false,
      "checkedInAt": null
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `flightNumber` | string | Flight number, echoed back |
| `departureDate` | string (date) | Departure date, echoed back |
| `totalPassengers` | integer | Total number of manifest entries returned |
| `entries` | array | Full manifest entries for all passengers on the flight |
| `entries[].manifestId` | string (UUID) | Manifest row identifier |
| `entries[].bookingReference` | string | 6-character booking reference |
| `entries[].eTicketNumber` | string | IATA e-ticket number |
| `entries[].passengerId` | string | PAX reference |
| `entries[].givenName` | string | Passenger's given name |
| `entries[].surname` | string | Passenger's surname |
| `entries[].seatNumber` | string | Assigned seat number |
| `entries[].cabinCode` | string | Cabin class |
| `entries[].ssrCodes` | array | Array of IATA SSR code strings held for this passenger on this flight |
| `entries[].departureTime` | string (time) | Current scheduled departure time (may reflect a delay update) |
| `entries[].arrivalTime` | string (time) | Current scheduled arrival time |
| `entries[].checkedIn` | boolean | Whether the passenger has completed check-in |
| `entries[].checkedInAt` | string (datetime) | ISO 8601 UTC timestamp of check-in, or `null` |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing or invalid query parameters |
| `404 Not Found` | No manifest entries found for the given flight and date |

---

### POST /v1/documents

Issue an ancillary document (`delivery.Document` record) for a post-sale ancillary purchase. Triggers a `DocumentIssued` accounting event. Called by the Retail API after successful ancillary payment settlement.

**When to use:** Called by the Retail API for every paid seat selection or additional bag purchase — both during the bookflow and post-sale. Must be called after payment has been successfully settled. Business and First Class seat selections carry no charge but do not generate a document (no payment, no ancillary revenue to record).

**Document types:**
- `SeatAncillary` — charged seat selection (Premium Economy or Economy cabins only).
- `BagAncillary` — additional checked bag beyond the free allowance.

#### Request

```json
{
  "documentType": "BagAncillary",
  "bookingReference": "AB1234",
  "eTicketNumber": "932-1234567890",
  "passengerId": "PAX-1",
  "segmentRef": "SEG-1",
  "paymentId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "amount": 60.00,
  "currencyCode": "GBP",
  "documentData": {
    "emdType": "EMD-A",
    "rfic": "C",
    "rfisc": "0GO",
    "serviceDescription": "Additional Checked Bag — 23 kg",
    "couponStatus": "Open",
    "ancillaryDetail": {
      "type": "BagAncillary",
      "bagSequenceNumber": 1,
      "weightKg": 23,
      "dimensionsCm": { "length": 90, "width": 75, "depth": 43 },
      "bagTagNumber": null
    },
    "priceBreakdown": {
      "baseAmount": 60.00,
      "taxes": [],
      "totalAmount": 60.00,
      "currencyCode": "GBP"
    },
    "voidHistory": []
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `documentType` | string | Yes | `SeatAncillary` or `BagAncillary` |
| `bookingReference` | string | Yes | The 6-character booking reference |
| `eTicketNumber` | string | Yes | Associated e-ticket number linking the ancillary to its travel segment |
| `passengerId` | string | Yes | PAX reference |
| `segmentRef` | string | Yes | Segment identifier the ancillary applies to, e.g. `SEG-1` |
| `paymentId` | string | Yes | Payment identifier from the Payment MS, e.g. `a1b2c3d4-e5f6-7890-abcd-ef1234567890` |
| `amount` | number | Yes | Amount charged. Decimal, 2 places |
| `currencyCode` | string | Yes | ISO 4217 currency code |
| `documentData` | object | Yes | Full ancillary detail. See DocumentData JSON Structure |

#### Response — `201 Created`

```json
{
  "documentId": "e7f8a9b0-c1d2-3456-abcd-567890123456",
  "documentNumber": "932-EMD-0001234",
  "documentType": "BagAncillary",
  "bookingReference": "AB1234",
  "createdAt": "2026-03-17T14:25:00Z"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `documentId` | string (UUID) | Internal document record identifier |
| `documentNumber` | string | System-generated IATA EMD-style number, e.g. `932-EMD-0001234` |
| `documentType` | string | `SeatAncillary` or `BagAncillary` |
| `bookingReference` | string | Booking reference, echoed back |
| `createdAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid document type, or malformed `documentData` JSON |
| `404 Not Found` | The referenced `eTicketNumber` does not exist |
| `422 Unprocessable Entity` | The referenced `eTicketNumber` is voided — cannot issue an ancillary document against a voided ticket |

---

### PATCH /v1/documents/{documentNumber}/void

Void an ancillary document. Sets `IsVoided = 1` and appends to `voidHistory` in the `DocumentData` JSON. Publishes a `DocumentVoided` event to the event bus. Used on voluntary cancellation or IROPS when ancillary charges are refunded.

**When to use:** Called by the Retail API during voluntary cancellation where the booking included paid ancillaries (seats or bags). Also called by the Operations API when IROPS cancellations result in full refunds that include ancillary charges.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `documentNumber` | string | The system-generated document number, e.g. `932-EMD-0001234` |

#### Request

```json
{
  "reason": "VoluntaryCancellation",
  "actor": "RetailAPI"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `reason` | string | Yes | Reason for voiding: `VoluntaryCancellation`, `IROPSCancellation`, `AdminVoid` |
| `actor` | string | Yes | Which system initiated the void, e.g. `RetailAPI`, `DisruptionAPI` |

#### Response — `200 OK`

```json
{
  "documentNumber": "932-EMD-0001234",
  "isVoided": true,
  "voidedAt": "2026-03-17T14:30:00Z"
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | No document found for the given document number |
| `422 Unprocessable Entity` | Document is already voided |

---

### POST /v1/boarding-cards

Generate boarding cards and BCBP barcode strings for checked-in passengers. Returns one boarding card per passenger per flight. The Delivery MS assembles the BCBP string from data on the `delivery.Manifest` row.

**When to use:** Called by the Retail API as the final step of the online check-in (OLCI) flow, after check-in status has been recorded on the manifest via `PATCH /v1/manifest/{bookingRef}`.

**Pre-conditions:** All passengers in the request must have `CheckedIn = 1` on their manifest entries. The Delivery MS returns `422 Unprocessable Entity` if any passenger has not completed check-in.

**BCBP string format:** Compliant with IATA Resolution 792. Contains: format code, leg count, passenger name (surname/given padded to 20 chars), electronic ticket indicator + booking reference, origin, destination, operating carrier, flight number (4-char padded), Julian departure date, cabin code, seat number (4-char padded), sequence number, passenger status code, BCBP version indicator, Julian issue date + issuer code, operating carrier (repeated in conditional section), loyalty/frequent flyer number, airline-specific data.

#### Request

```json
{
  "bookingReference": "AB1234",
  "passengers": [
    {
      "passengerId": "PAX-1",
      "inventoryIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"]
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bookingReference` | string | Yes | The 6-character booking reference |
| `passengers` | array | Yes | Passengers for whom to generate boarding cards |
| `passengers[].passengerId` | string | Yes | PAX reference |
| `passengers[].inventoryIds` | array | Yes | Flight inventory IDs for which to generate boarding cards. Typically all segments for the departure day |

#### Response — `201 Created`

```json
{
  "boardingCards": [
    {
      "passengerId": "PAX-1",
      "flightNumber": "AX003",
      "departureDate": "2025-08-15",
      "seatNumber": "1A",
      "cabinCode": "J",
      "sequenceNumber": "0001",
      "bcbpString": "M1TAYLOR/ALEX        EAB1234 LHRJFKAX 0003 042J001A0001 156>518 W6042 AX 2A00000012345678 JAX7KLP2NZR901A",
      "passengerName": "TAYLOR/ALEX",
      "origin": "LHR",
      "destination": "JFK",
      "eTicketNumber": "932-1234567890"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `boardingCards` | array | One boarding card per passenger per flight |
| `boardingCards[].passengerId` | string | PAX reference |
| `boardingCards[].flightNumber` | string | Flight number |
| `boardingCards[].departureDate` | string (date) | ISO 8601 departure date |
| `boardingCards[].seatNumber` | string | Assigned seat number |
| `boardingCards[].cabinCode` | string | Cabin class |
| `boardingCards[].sequenceNumber` | string | 4-char padded check-in sequence number |
| `boardingCards[].bcbpString` | string | Full IATA Resolution 792 BCBP barcode string for rendering by the channel |
| `boardingCards[].passengerName` | string | Name formatted as `SURNAME/GIVEN` as encoded in the BCBP string |
| `boardingCards[].origin` | string | IATA 3-letter origin airport code |
| `boardingCards[].destination` | string | IATA 3-letter destination airport code |
| `boardingCards[].eTicketNumber` | string | IATA e-ticket number for this boarding card |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | No manifest entry found for a given passenger and inventory ID combination |
| `422 Unprocessable Entity` | One or more passengers have not completed check-in (`CheckedIn = 0`). All passengers must be checked in before boarding cards can be generated |

---

## Orchestration API Call Sequences

### Booking Confirmation (Retail API)

The Delivery MS is called twice during booking confirmation:

1. **`POST /v1/tickets`** — immediately after payment authorisation, before inventory settlement and order confirmation. Returns e-ticket numbers.
2. **`POST /v1/manifest`** — after `POST /v1/orders` succeeds on the Order MS. Writes manifest entries using the confirmed booking reference and issued e-ticket numbers.

Ancillary documents (`POST /v1/documents`) are called for each paid seat or bag selection after order confirmation and ancillary payment settlement.

### Voluntary Flight Change (Retail API)

1. **`PATCH /v1/tickets/{eTicketNumber}/void`** — called for each ticket on the changed segment.
2. **`DELETE /v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}`** — remove manifest entries for the original flight.
3. **`POST /v1/tickets/reissue`** — issue new tickets for the replacement flight.
4. **`POST /v1/manifest`** — write manifest entries for the replacement flight.

### Voluntary Cancellation (Retail API)

1. **`PATCH /v1/tickets/{eTicketNumber}/void`** — called for each ticket on the booking.
2. **`DELETE /v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}`** — remove all manifest entries.
3. **`PATCH /v1/documents/{documentNumber}/void`** — void any ancillary documents if a refund is due.

### Post-Sale Seat Change (Retail API)

1. **`POST /v1/tickets/reissue`** — reissue tickets with updated seat assignment.
2. **`PUT /v1/manifest`** — update manifest with new seat number and reissued e-ticket number.
3. **`POST /v1/documents`** — issue `SeatAncillary` document after payment settled.

### Online Check-In (Retail API)

1. **`PATCH /v1/manifest/{bookingRef}`** — set `checkedIn = true` and record `checkedInAt`.
2. **`POST /v1/boarding-cards`** — generate BCBP boarding cards for checked-in passengers.

### Flight Delay (Operations API)

1. **`PATCH /v1/manifest/{bookingRef}/flight`** — update departure/arrival times on all manifest entries for the affected booking and flight.
2. (Conditional) **`POST /v1/tickets/reissue`** — only if delay exceeds the 60-minute material schedule change threshold.

### Flight Cancellation — IROPS Rebooking (Operations API)

1. **`GET /v1/manifest`** — retrieve full passenger manifest for the cancelled flight.
2. **`DELETE /v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}`** — remove manifest entries for the cancelled flight (per booking).
3. **`POST /v1/tickets/reissue`** — issue new tickets for the replacement flight.
4. **`POST /v1/manifest`** — write manifest entries for the replacement flight.
5. **`PATCH /v1/documents/{documentNumber}/void`** — void ancillary documents where IROPS results in a full refund.

---

## Data Conventions

| Convention | Format | Example |
|------------|--------|---------|
| Timestamps | ISO 8601 UTC | `"2025-08-15T11:00:00Z"` |
| Dates | ISO 8601 | `"2025-08-15"` |
| Times | HH:mm (24-hour local) | `"11:00"` |
| Airport codes | IATA 3-letter | `"LHR"` |
| E-ticket numbers | IATA format | `"932-1234567890"` |
| Document numbers | IATA EMD-style | `"932-EMD-0001234"` |
| Cabin codes | Single character | `"J"`, `"W"`, `"Y"`, `"F"` |
| JSON field names | camelCase | `eTicketNumber`, `bookingReference` |
| UUIDs | RFC 4122 lowercase | `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"` |

---

## Invocation Examples

### Issue e-tickets at booking confirmation (Retail API → Delivery MS)

```bash
curl -X POST https://{delivery-ms-host}/v1/tickets \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "bookingReference": "AB1234",
    "passengers": [{ "passengerId": "PAX-1", "givenName": "Alex", "surname": "Taylor" }],
    "segments": [{ "segmentId": "SEG-1", "inventoryId": "3fa85f64-...", "flightNumber": "AX003", "departureDate": "2025-08-15", "origin": "LHR", "destination": "JFK", "cabinCode": "J", "fareBasisCode": "JFLEXGB", "seatAssignments": [], "ssrCodes": [] }]
  }'
```

### Void an e-ticket (Retail API → Delivery MS)

```bash
curl -X PATCH https://{delivery-ms-host}/v1/tickets/932-1234567890/void \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{ "reason": "VoluntaryCancellation", "actor": "RetailAPI", "version": 1 }'
```

### Write manifest entries (Retail API → Delivery MS)

```bash
curl -X POST https://{delivery-ms-host}/v1/manifest \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "bookingReference": "AB1234",
    "entries": [{ "ticketId": "e1f2...", "inventoryId": "3fa8...", "flightNumber": "AX003", "departureDate": "2025-08-15", "departureTime": "11:00", "arrivalTime": "14:10", "aircraftType": "A351", "seatNumber": "1A", "cabinCode": "J", "eTicketNumber": "932-1234567890", "passengerId": "PAX-1", "givenName": "Alex", "surname": "Taylor", "ssrCodes": [] }]
  }'
```

### Retrieve full flight manifest (Operations API → Delivery MS)

```bash
curl -X GET "https://{delivery-ms-host}/v1/manifest?flightNumber=AX003&departureDate=2025-08-15" \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 661f9500-f30c-52e5-b827-557766551111"
```

### Update check-in status at OLCI (Retail API → Delivery MS)

```bash
curl -X PATCH https://{delivery-ms-host}/v1/manifest/AB1234 \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "updates": [{ "inventoryId": "3fa8...", "passengerId": "PAX-1", "checkedIn": true, "checkedInAt": "2025-08-14T09:30:00Z", "ssrCodes": ["VGML"], "version": 1 }]
  }'
```

### Generate boarding cards (Retail API → Delivery MS)

```bash
curl -X POST https://{delivery-ms-host}/v1/boarding-cards \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "bookingReference": "AB1234",
    "passengers": [{ "passengerId": "PAX-1", "inventoryIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"] }]
  }'
```

### Update manifest times on delay (Operations API → Delivery MS)

```bash
curl -X PATCH https://{delivery-ms-host}/v1/manifest/AB1234/flight \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 661f9500-f30c-52e5-b827-557766551111" \
  -d '{
    "flightNumber": "AX101",
    "departureDate": "2026-03-22",
    "newDepartureTime": "16:30",
    "newArrivalTime": "19:45"
  }'
```

> **Note:** All calls to the Delivery microservice are authenticated using the `x-functions-key` header. The Delivery MS never receives or validates end-user JWTs. See [`api.md` — Microservice Authentication](../api.md#microservice-authentication--host-keys) for the full host key mechanism.

---

## Related Documentation

- [API Endpoint Reference](../api-reference.md) — Summary of all microservice endpoints
- [System Design](../system-overview.md) — Full domain design including booking confirmation, check-in, flight change, cancellation, and IROPS sequence diagrams
