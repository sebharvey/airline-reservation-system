# Seat Microservice — API Specification

> **Service owner:** Seat domain
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Seat microservice is the system of record for aircraft seatmap definitions, fleet-wide seat pricing rules, and **seat offer generation**. It owns three capability areas: aircraft type records (`seat.AircraftType`), seatmap definitions with full cabin layout as JSON (`seat.Seatmap`), and fleet-wide position-based pricing rules (`seat.SeatPricing`).

`SeatOfferId` values are generated deterministically by the Seat MS from `flightId + seatNumber + pricingRuleHash` — no offer storage is required. The Seat MS generates priced seat offers on demand from active `SeatPricing` rules. Because pricing is stable and position-based (not per-flight or per-route), there is no risk of price drift between offer generation and purchase.

The Retail API assembles the full seatmap response for channels by merging three datasets: (1) the physical cabin layout from `GET /v1/seatmap/{aircraftType}`, (2) priced seat offers including `SeatOfferId` from `GET /v1/seat-offers?flightId={flightId}`, and (3) real-time availability status (available, held, sold) from `GET /v1/flights/{flightId}/seat-availability` on the **Offer MS**. The Seat MS does not call the Offer MS and has no knowledge of per-flight availability.

> **Important:** The Seat microservice is an internal service. It is not called directly by channels (Web, App, NDC). All booking-path requests are routed through the **Retail API** orchestration layer. Admin endpoints are called from a future Contact Centre admin application. See the [Security](#security) section for authentication details.

---

## Security

### Authentication

The Seat microservice sits behind the Retail API orchestration layer. Channels authenticate via the Retail API using OAuth 2.0 / OIDC; the Retail API validates JWTs before calling this service.

Calls from the Retail API to the Seat microservice are authenticated using an **Azure Function Host Key**, passed in the `x-functions-key` HTTP header. The Seat microservice does not validate JWTs; that responsibility belongs to the Retail API. See [Microservice Authentication — Host Keys](../api.md#microservice-authentication--host-keys) in `api.md` for the full mechanism, key storage, and retrieval details.

### Required Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes (for request bodies) | Must be `application/json` |
| `x-functions-key` | Yes (on all Retail API → Seat MS calls) | Azure Function Host Key authenticating the Retail API as an authorised caller |
| `X-Correlation-ID` | Yes | UUID generated at the channel boundary; propagated on every downstream call for distributed tracing and log correlation |

### Data Protection

- Seatmap and pricing data contains no PII. Standard logging practices apply.
- `SeatOfferId` values are session-scoped and must not be cached by channels beyond the current booking session.

---

## Business Rules

### Cabin Classes

All Apex Air aircraft are configured with up to four cabin classes. Cabin codes are single-character identifiers used uniformly across all services.

| Code | Name |
|------|------|
| `F` | First Class |
| `J` | Business Class |
| `W` | Premium Economy |
| `Y` | Economy |

### Seat Pricing

Seat pricing is fleet-wide, position-based, and route-agnostic. Prices apply uniformly across all aircraft types and routes.

| Position | Price (GBP) |
|----------|-------------|
| Window | £70.00 |
| Aisle | £50.00 |
| Middle | £20.00 |

**Business Class (J) and First Class (F) seat selection is included in the fare at no ancillary charge.** These cabins are excluded from `seat.SeatPricing`. When the Seat MS generates seat offers for a Business or First Class seat, the price is `0.00` and no pricing rule entry exists for these cabins.

### Seat Offer Generation

The Seat MS generates priced `SeatOffer` objects on demand from active `SeatPricing` rules.

- `SeatOfferId` values are deterministic identifiers derived from `flightId + seatNumber + pricingRuleHash`.
- No offer storage table is required — offers are generated and returned without a database write.
- `SeatOfferId` values are session-scoped and must not be persisted by channels beyond the current booking session.
- The Retail API calls `GET /v1/seat-offers/{seatOfferId}` at order confirmation to verify the price stored in the basket matches the current Seat MS pricing. If the underlying pricing rule has changed or been deactivated, the endpoint returns `404 Not Found` and the booking flow must be aborted.

### Seatmap Layout vs Pricing vs Availability

These are three distinct concerns owned by different services:

- **Layout** — physical cabin and seat configuration, attributes, and selectability. Owned by Seat MS via `GET /v1/seatmap/{aircraftType}`.
- **Pricing** — fleet-wide position-based prices and `SeatOfferId` generation. Owned by Seat MS via `GET /v1/seat-offers?flightId={flightId}`.
- **Availability** — real-time per-seat status (available, held, or sold) for a specific flight. Owned by **Offer MS** via `GET /v1/flights/{flightId}/seat-availability`. The Seat MS has no knowledge of and makes no calls to the Offer MS.

The Retail API merges all three datasets before returning the seatmap response to the channel.

### `isSelectable` vs Availability

The `isSelectable` flag on each seat in the `CabinLayout` JSON reflects only whether a seat is **physically available for selection** — i.e. not a structural no-fly zone, permanently blocked position, or crew seat. It does not reflect real-time occupancy. Real-time availability (whether a seat is currently sold or held on a specific flight) is provided by the Offer MS and overlaid by the Retail API.

### Where Seat Selection Occurs

Seat selection may occur at three points in the customer journey:

1. **During the bookflow** — optional step within the basket. Business and First Class seats carry no charge. Economy and Premium Economy seats are charged as ancillaries.
2. **Post-sale (manage booking)** — full ancillary charge applies.
3. **At online check-in (OLCI)** — seat assignment is **free of charge** at OLCI regardless of cabin. No payment is taken. Seat pricing is displayed for reference only.

### Seat Validation Responsibility

Before writing any row to `delivery.Manifest`, the orchestration layer (Retail API, Airport API, or Disruption API) must validate the `SeatNumber` against the active seatmap for the relevant `AircraftType` by calling `GET /v1/seatmap/{aircraftType}`. The Seat MS trusts the seat number provided by its caller. The Delivery MS also trusts the seat number provided to it — seat validation is exclusively the orchestration layer's responsibility.

### Ancillary Document Requirement

For every paid seat selection, the Retail API must create a `delivery.Document` record (type `SeatAncillary`) via the Delivery MS. This enables the Accounting system to account for seat ancillary revenue independently from the fare ticket. The Seat MS has no involvement in this step.

### Aircraft Type Code Convention

Aircraft type codes are 4 characters: manufacturer prefix + 3-digit variant number (third digit encodes the specific variant). Examples: A350-1000 → `A351`, A350-900 → `A359`, B787-9 → `B789`, A330-900 → `A339`. This convention is consistent with IATA SSIM aircraft designator standards and must be used uniformly across all services, databases, and API contracts.

---

## Data Schema

### `seat.AircraftType`

Root reference record for each aircraft type in the fleet.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `AircraftTypeCode` | CHAR(4) | No | | PK | 4-character code, e.g. `A351`, `B789`, `A339` |
| `Manufacturer` | VARCHAR(50) | No | | | e.g. `Airbus`, `Boeing` |
| `FriendlyName` | VARCHAR(100) | Yes | | | e.g. `Airbus A350-1000`, `Boeing 787-9` |
| `TotalSeats` | SMALLINT | No | | | Total seat count across all cabins |
| `CabinCounts` | NVARCHAR(MAX) | Yes | | | JSON array of cabin seat counts, e.g. `[{"cabin": "J", "count": 32}, {"cabin": "W", "count": 56}, {"cabin": "Y", "count": 281}]` |
| `IsActive` | BIT | No | `1` | | |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — generated by a SQL trigger on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — updated automatically by a SQL trigger on every row modification** |

> **Constraints:** `CHK_CabinCounts` — `CabinCounts IS NULL OR ISJSON(CabinCounts) = 1`; when provided, `CabinCounts` must be a valid JSON document.

### `seat.Seatmap`

One active seatmap per aircraft type, with the full cabin layout stored as a JSON column.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `SeatmapId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `AircraftTypeCode` | CHAR(4) | No | | FK → `seat.AircraftType(AircraftTypeCode)` | |
| `Version` | INT | No | `1` | | Incremented when the layout is replaced via PUT |
| `IsActive` | BIT | No | `1` | | Only one active seatmap per aircraft type at any time |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — generated by a SQL trigger on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — updated automatically by a SQL trigger on every row modification** |
| `CabinLayout` | NVARCHAR(MAX) | No | | | JSON document containing full cabin and seat definitions. See CabinLayout JSON Structure below |

> **Indexes:** `IX_Seatmap_AircraftType` on `(AircraftTypeCode)` WHERE `IsActive = 1`.
> **Constraints:** `CHK_CabinLayout` — `ISJSON(CabinLayout) = 1`.
> **One active seatmap per aircraft type.** When a new seatmap is created for an aircraft type that already has an active seatmap, the previous one must be deactivated (`IsActive = 0`) as part of the same transaction.

#### CabinLayout JSON Structure

The `CabinLayout` JSON contains the physical configuration of the aircraft — cabin zones, row ranges, column definitions, seat attributes, and facility locations. Pricing and availability are **never** embedded here.

Key fields per seat object within `CabinLayout`:

| Field | Type | Description |
|-------|------|-------------|
| `seatNumber` | string | Seat identifier, e.g. `"1A"`, `"22K"` |
| `column` | string | Column letter |
| `type` | string | Seat type: `Suite` (Business/First long-haul) or `Standard` |
| `position` | string | `Window`, `Aisle`, or `Middle` — used by Seat MS to look up pricing |
| `attributes` | array | Seat feature flags, e.g. `ExtraLegroom`, `ExitRow`, `BulkheadForward`, `BulkheadAft`, `ReducedRecline`, `BlockedForCrew`, `LieFlat` |
| `isSelectable` | boolean | Whether this seat is available for selection. `false` for crew seats, structural blocks, permanently closed positions |

Cabin-level fields include `cabinCode`, `cabinName`, `deckLevel`, `startRow`, `endRow`, `columns` array, and `layout` string (e.g. `"1-2-1"`, `"2-3-2"`, `"3-3-3"`, `"3-4-3"`). Facility locations (galleys, toilets, exits) are listed under `facilitiesForward`, `facilitiesAft`, and `facilitiesMidCabin` per cabin.

**Known aircraft configurations:**

| Aircraft | Total Seats | Business (J) | Premium Economy (W) | Economy (Y) | Layout (Y) |
|----------|-------------|--------------|----------------------|-------------|------------|
| A351 | 369 | Rows 1–10, 1-2-1 | Rows 20–28, 2-3-2 | Rows 35–62, 3-3-3 | 3-3-3 |
| B789 | 296 | Rows 1–10, 1-1-1 | Rows 20–27, 2-3-2 | Rows 33–55, 3-3-3 | 3-3-3 |
| A339 | 326 | Rows 1–10, 1-2-1 | Rows 20–27, 2-3-2 | Rows 33–55, 3-4-3 | 3-4-3 |

### `seat.SeatPricing`

Fleet-wide, position-based pricing rules. Business Class (J) and First Class (F) are excluded — seat selection is included in those fares at no charge.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `SeatPricingId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `CabinCode` | CHAR(1) | No | | UK (with SeatPosition, CurrencyCode) | `W` or `Y` only — Business (J) and First (F) carry no ancillary charge |
| `SeatPosition` | VARCHAR(10) | No | | UK (with CabinCode, CurrencyCode) | `Window`, `Aisle`, or `Middle` |
| `CurrencyCode` | CHAR(3) | No | `'GBP'` | UK (with CabinCode, SeatPosition) | ISO 4217 |
| `Price` | DECIMAL(10,2) | No | | | |
| `IsActive` | BIT | No | `1` | | |
| `ValidFrom` | DATETIME2 | No | | | Effective start of this pricing rule |
| `ValidTo` | DATETIME2 | Yes | | | Null = open-ended / currently active |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — generated by a SQL trigger on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — updated automatically by a SQL trigger on every row modification** |

> **Constraints:** `UQ_SeatPricing_CabinPosition` (unique) on `(CabinCode, SeatPosition, CurrencyCode)` — enforces one active price per cabin/position/currency combination.
> **Seed data:** `('W', 'Window', 'GBP', 70.00)` · `('W', 'Aisle', 'GBP', 50.00)` · `('W', 'Middle', 'GBP', 20.00)` · `('Y', 'Window', 'GBP', 70.00)` · `('Y', 'Aisle', 'GBP', 50.00)` · `('Y', 'Middle', 'GBP', 20.00)`.

> **`createdAt` / `updatedAt` are database-generated fields and must never be written by the application layer.** Both are set and maintained exclusively by SQL triggers. The application layer must re-read the persisted row after any INSERT or UPDATE and use the returned values in the API response. In-memory timestamps set before persistence are provisional only. These fields are always present in responses but are not valid in request bodies.

---

## Endpoints

---

### GET /v1/seatmap/{aircraftType}

Retrieve the active seatmap definition and cabin layout for an aircraft type. Returns physical layout, seat attributes, and cabin configuration only — **no pricing or availability**. Pricing is returned by `GET /v1/seat-offers?flightId={flightId}` and availability by the Offer MS.

**When to use:** Called by the Retail API as the first step in building the full seatmap response for a channel. Also called by the orchestration layer before writing to `delivery.Manifest` to validate a seat number against the active seatmap.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `aircraftType` | string | 4-character aircraft type code, e.g. `A351`, `B789`, `A339` |

#### Request

```
GET /v1/seatmap/A351
```

#### Response — `200 OK`

```json
{
  "seatmapId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "aircraftType": "A351",
  "version": 1,
  "totalSeats": 369,
  "cabins": [
    {
      "cabinCode": "J",
      "cabinName": "Business Class",
      "deckLevel": "Main",
      "startRow": 1,
      "endRow": 10,
      "columns": ["A", "D", "G", "K"],
      "layout": "1-2-1",
      "facilitiesForward": [
        { "type": "Galley", "position": "Forward" },
        { "type": "Toilet", "position": "Forward-Left" },
        { "type": "Exit", "position": "Forward-Left" }
      ],
      "facilitiesAft": [
        { "type": "Toilet", "position": "Aft-Left" },
        { "type": "Galley", "position": "Aft" }
      ],
      "rows": [
        {
          "rowNumber": 1,
          "seats": [
            {
              "seatNumber": "1A",
              "column": "A",
              "type": "Suite",
              "position": "Window",
              "attributes": ["ExtraLegroom", "BulkheadForward"],
              "isSelectable": true
            },
            {
              "seatNumber": "1D",
              "column": "D",
              "type": "Suite",
              "position": "Aisle",
              "attributes": ["ExtraLegroom", "BulkheadForward"],
              "isSelectable": true
            }
          ]
        }
      ]
    },
    {
      "cabinCode": "W",
      "cabinName": "Premium Economy",
      "deckLevel": "Main",
      "startRow": 20,
      "endRow": 28,
      "columns": ["A", "B", "D", "E", "F", "H", "K"],
      "layout": "2-3-2",
      "rows": []
    },
    {
      "cabinCode": "Y",
      "cabinName": "Economy",
      "deckLevel": "Main",
      "startRow": 35,
      "endRow": 62,
      "columns": ["A", "B", "C", "D", "E", "F", "G", "H", "K"],
      "layout": "3-3-3",
      "rows": []
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `seatmapId` | string (UUID) | The active seatmap record identifier |
| `aircraftType` | string | Aircraft type code, echoed back |
| `version` | integer | Seatmap version — incremented on each replacement |
| `totalSeats` | integer | Total selectable seat count across all cabins |
| `cabins` | array | Ordered array of cabin definitions |
| `cabins[].cabinCode` | string | Cabin code: `F`, `J`, `W`, or `Y` |
| `cabins[].cabinName` | string | Display name, e.g. `Business Class` |
| `cabins[].deckLevel` | string | `Main` or `Upper` |
| `cabins[].startRow` | integer | First row number in this cabin |
| `cabins[].endRow` | integer | Last row number in this cabin |
| `cabins[].columns` | array | Ordered list of column letters present in this cabin |
| `cabins[].layout` | string | Seat layout pattern, e.g. `"1-2-1"`, `"2-3-2"`, `"3-3-3"`, `"3-4-3"` |
| `cabins[].facilitiesForward` | array | Galley, toilet, and exit locations at the forward end of this cabin |
| `cabins[].facilitiesAft` | array | Galley, toilet, and exit locations at the aft end of this cabin |
| `cabins[].facilitiesMidCabin` | array | Mid-cabin exits and toilets (where present), each with an `atRow` field |
| `cabins[].rows` | array | Array of row objects, each containing a `seats` array |
| `seats[].seatNumber` | string | Seat identifier, e.g. `"1A"`, `"35K"` |
| `seats[].column` | string | Column letter |
| `seats[].type` | string | `Suite` or `Standard` |
| `seats[].position` | string | `Window`, `Aisle`, or `Middle` |
| `seats[].attributes` | array | Feature flags: `ExtraLegroom`, `ExitRow`, `BulkheadForward`, `BulkheadAft`, `ReducedRecline`, `BlockedForCrew`, `LieFlat` |
| `seats[].isSelectable` | boolean | Whether the seat is physically available for selection. Does not reflect real-time occupancy |

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No active seatmap found for the given aircraft type code |

---

### GET /v1/seat-offers?flightId={flightId}

Generate and return priced seat offers for all selectable seats on a specific flight. Returns one `SeatOffer` per selectable seat with a deterministic `SeatOfferId`, current price, and seat attributes. Used by the Retail API to build the pricing layer of the full seatmap response.

**When to use:** Called by the Retail API as the second step in building the full seatmap response, after retrieving the layout from `GET /v1/seatmap/{aircraftType}`. Also called during post-sale seat selection and OLCI (for reference pricing only at OLCI — no payment is taken at check-in).

> **Offer generation:** The Seat MS uses the `flightId` to resolve the `aircraftType` (via the Offer MS — the `flightId` corresponds to an `InventoryId` in `offer.FlightInventory`). It retrieves the active `CabinLayout` for that aircraft type, reads all active `SeatPricing` rules, and generates a `SeatOfferId` per selectable seat. Business and First Class seats are returned with `price: 0.00` and `isChargeable: false`. No database write occurs.

> **`SeatOfferId` generation:** The identifier is derived deterministically from `flightId + seatNumber + pricingRuleHash`. The `pricingRuleHash` encodes the active pricing rule at generation time, enabling validation at confirmation without storing offer state.

#### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `flightId` | string (UUID) | Yes | The `InventoryId` from `offer.FlightInventory` identifying the specific flight |

#### Request

```
GET /v1/seat-offers?flightId=3fa85f64-5717-4562-b3fc-2c963f66afa6
```

#### Response — `200 OK`

```json
{
  "flightId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "aircraftType": "A351",
  "seatOffers": [
    {
      "seatOfferId": "so-3fa85f64-1A-v1",
      "seatNumber": "1A",
      "cabinCode": "J",
      "position": "Window",
      "type": "Suite",
      "attributes": ["ExtraLegroom", "BulkheadForward"],
      "isSelectable": true,
      "isChargeable": false,
      "price": 0.00,
      "currencyCode": "GBP"
    },
    {
      "seatOfferId": "so-3fa85f64-35A-v1",
      "seatNumber": "35A",
      "cabinCode": "Y",
      "position": "Window",
      "type": "Standard",
      "attributes": ["ExtraLegroom", "BulkheadForward"],
      "isSelectable": true,
      "isChargeable": true,
      "price": 70.00,
      "currencyCode": "GBP"
    },
    {
      "seatOfferId": "so-3fa85f64-35B-v1",
      "seatNumber": "35B",
      "cabinCode": "Y",
      "position": "Middle",
      "type": "Standard",
      "attributes": ["ExtraLegroom", "BulkheadForward"],
      "isSelectable": true,
      "isChargeable": true,
      "price": 20.00,
      "currencyCode": "GBP"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `flightId` | string (UUID) | The flight inventory identifier, echoed back |
| `aircraftType` | string | Aircraft type code resolved for this flight |
| `seatOffers` | array | One entry per selectable seat on the aircraft |
| `seatOffers[].seatOfferId` | string | Deterministic seat offer identifier derived from `flightId + seatNumber + pricingRuleHash` |
| `seatOffers[].seatNumber` | string | Seat identifier, e.g. `"1A"`, `"35K"` |
| `seatOffers[].cabinCode` | string | Cabin class: `F`, `J`, `W`, or `Y` |
| `seatOffers[].position` | string | `Window`, `Aisle`, or `Middle` |
| `seatOffers[].type` | string | `Suite` or `Standard` |
| `seatOffers[].attributes` | array | Feature flags from the seatmap definition |
| `seatOffers[].isSelectable` | boolean | Whether this seat is physically selectable |
| `seatOffers[].isChargeable` | boolean | `false` for Business and First Class (included in fare); `true` for Premium Economy and Economy |
| `seatOffers[].price` | number | Current price. `0.00` for Business and First Class. Decimal, 2 places |
| `seatOffers[].currencyCode` | string | ISO 4217 currency code |

> **Non-selectable seats:** Seats with `isSelectable: false` in the `CabinLayout` (crew seats, structural blocks) are excluded from the `seatOffers` array entirely — no `SeatOfferId` is generated for them.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing or malformed `flightId` query parameter |
| `404 Not Found` | No active seatmap found for the aircraft type associated with the given flight |

---

### GET /v1/seat-offers/{seatOfferId}

Retrieve and validate a specific seat offer by its deterministic ID. Confirms the pricing rule that generated the ID is still active and returns the current price. Used by the Retail API when adding a seat to a basket or confirming a seat purchase at order confirmation.

**When to use:** Called by the Retail API when a passenger adds a seat to a basket (to validate the offer) and again at basket confirmation (to verify the price stored in the basket matches current pricing before settling payment).

> **Validation:** The Seat MS resolves the `seatOfferId` to its constituent parts (`flightId`, `seatNumber`, `pricingRuleHash`), retrieves the current active `SeatPricing` rule for the identified cabin/position/currency combination, and confirms the rule is still active and its hash matches. If the pricing rule has been deactivated or changed since the offer was generated, the endpoint returns `404 Not Found` and the Retail API must abort the booking flow and prompt the passenger to re-select.

> **No consumption state:** This endpoint does not mark the offer as consumed. Unlike flight `StoredOffer` records, seat offers are stateless. The Offer MS manages actual seat hold/sell/release on `offer.FlightInventory` — that is a separate operation from seat offer validation.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `seatOfferId` | string | The deterministic seat offer identifier returned from `GET /v1/seat-offers` |

#### Request

```
GET /v1/seat-offers/so-3fa85f64-35A-v1
```

#### Response — `200 OK`

```json
{
  "seatOfferId": "so-3fa85f64-35A-v1",
  "flightId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "seatNumber": "35A",
  "cabinCode": "Y",
  "position": "Window",
  "type": "Standard",
  "attributes": ["ExtraLegroom", "BulkheadForward"],
  "isSelectable": true,
  "isChargeable": true,
  "price": 70.00,
  "currencyCode": "GBP",
  "isValid": true
}
```

| Field | Type | Description |
|-------|------|-------------|
| `seatOfferId` | string | The seat offer identifier, echoed back |
| `flightId` | string (UUID) | The flight inventory identifier resolved from the offer ID |
| `seatNumber` | string | Seat identifier resolved from the offer ID |
| `cabinCode` | string | Cabin class resolved from the offer ID |
| `position` | string | `Window`, `Aisle`, or `Middle` |
| `type` | string | `Suite` or `Standard` |
| `attributes` | array | Feature flags from the seatmap definition |
| `isSelectable` | boolean | Whether this seat is physically selectable |
| `isChargeable` | boolean | `false` for Business and First Class; `true` for Premium Economy and Economy |
| `price` | number | Current price at validation time. Decimal, 2 places |
| `currencyCode` | string | ISO 4217 currency code |
| `isValid` | boolean | `true` if the underlying pricing rule is still active and the offer hash matches |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Malformed `seatOfferId` — cannot be resolved to constituent parts |
| `404 Not Found` | The pricing rule underlying this offer is no longer active, the hash does not match, or the `seatOfferId` cannot be resolved |

---

### GET /v1/aircraft-types

List all aircraft types. Returns all records including inactive ones. Admin endpoint — not channel-facing.

**When to use:** Called from the Contact Centre admin application to view the fleet configuration.

#### Response — `200 OK`

```json
{
  "aircraftTypes": [
    {
      "aircraftTypeCode": "A351",
      "manufacturer": "Airbus",
      "friendlyName": "Airbus A350-1000",
      "totalSeats": 369,
      "cabinCounts": [{"cabin": "J", "count": 32}, {"cabin": "W", "count": 56}, {"cabin": "Y", "count": 281}],
      "isActive": true,
      "createdAt": "2025-01-01T00:00:00Z",
      "updatedAt": "2025-01-01T00:00:00Z"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `aircraftTypeCode` | string | 4-character IATA-style code, e.g. `A351` |
| `manufacturer` | string | e.g. `Airbus`, `Boeing` |
| `friendlyName` | string | Human-readable name. May be `null` |
| `totalSeats` | integer | Total seat count across all cabins |
| `cabinCounts` | array | Array of `{cabin, count}` objects, e.g. `[{"cabin": "J", "count": 32}]`. May be `null` |
| `isActive` | boolean | Whether this aircraft type is currently active |
| `createdAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated on insert |
| `updatedAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated on every update |

---

### POST /v1/aircraft-types

Create a new aircraft type record. Admin endpoint — not channel-facing.

#### Request

```json
{
  "aircraftTypeCode": "A351",
  "manufacturer": "Airbus",
  "friendlyName": "Airbus A350-1000",
  "totalSeats": 369,
  "cabinCounts": [{"cabin": "J", "count": 32}, {"cabin": "W", "count": 56}, {"cabin": "Y", "count": 281}]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `aircraftTypeCode` | string | Yes | Exactly 4 characters, e.g. `A351`. Must be unique |
| `manufacturer` | string | Yes | Max 50 characters |
| `friendlyName` | string | No | Max 100 characters |
| `totalSeats` | integer | Yes | Must be > 0 |
| `cabinCounts` | array | No | Array of `{cabin, count}` objects, e.g. `[{"cabin": "J", "count": 32}]` |

#### Response — `201 Created`

Returns the full created aircraft type in the same schema as items in `GET /v1/aircraft-types`.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid code format, or `totalSeats` ≤ 0 |
| `409 Conflict` | An aircraft type with the given `aircraftTypeCode` already exists |

---

### GET /v1/aircraft-types/{aircraftTypeCode}

Retrieve an aircraft type by code. Admin endpoint — not channel-facing.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `aircraftTypeCode` | string | 4-character aircraft type code, e.g. `A351` |

#### Response — `200 OK`

Returns a single aircraft type object in the same schema as items in `GET /v1/aircraft-types`.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No aircraft type found for the given code |

---

### PUT /v1/aircraft-types/{aircraftTypeCode}

Update an aircraft type record. Replaces all mutable fields. Admin endpoint — not channel-facing.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `aircraftTypeCode` | string | 4-character aircraft type code |

#### Request

```json
{
  "manufacturer": "Airbus",
  "friendlyName": "Airbus A350-1000 (Updated)",
  "totalSeats": 369,
  "cabinCounts": [{"cabin": "J", "count": 32}, {"cabin": "W", "count": 56}, {"cabin": "Y", "count": 281}],
  "isActive": true
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `manufacturer` | string | Yes | Max 50 characters |
| `friendlyName` | string | No | Max 100 characters |
| `totalSeats` | integer | Yes | Must be > 0 |
| `cabinCounts` | array | No | Array of `{cabin, count}` objects, e.g. `[{"cabin": "J", "count": 32}]` |
| `isActive` | boolean | Yes | Whether this aircraft type is active |

#### Response — `200 OK`

Returns the full updated aircraft type in the same schema as `GET /v1/aircraft-types/{aircraftTypeCode}`.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid values |
| `404 Not Found` | No aircraft type found for the given code |

---

### DELETE /v1/aircraft-types/{aircraftTypeCode}

Delete an aircraft type. Only permitted if no active seatmaps reference it. Admin endpoint — not channel-facing.

> **Guard:** If any active seatmap references the given `aircraftTypeCode`, this endpoint returns `409 Conflict`. Deactivate or delete the associated seatmap first.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `aircraftTypeCode` | string | 4-character aircraft type code |

#### Response — `204 No Content`

No response body.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No aircraft type found for the given code |
| `409 Conflict` | One or more active seatmaps reference this aircraft type. Delete or deactivate them first |

---

### GET /v1/seatmaps

List all seatmap definitions. Returns all records including inactive ones. Admin endpoint — not channel-facing.

#### Response — `200 OK`

```json
{
  "seatmaps": [
    {
      "seatmapId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
      "aircraftTypeCode": "A351",
      "version": 1,
      "isActive": true,
      "totalSeats": 369,
      "createdAt": "2025-01-01T00:00:00Z",
      "updatedAt": "2025-01-01T00:00:00Z"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `seatmapId` | string (UUID) | Unique seatmap identifier |
| `aircraftTypeCode` | string | Associated aircraft type code |
| `version` | integer | Layout version number |
| `isActive` | boolean | Whether this is the currently active seatmap for the aircraft type |
| `totalSeats` | integer | Total seat count in this seatmap configuration |
| `createdAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated |
| `updatedAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated |

> Note: `CabinLayout` JSON is **not** returned in the list response. Use `GET /v1/seatmaps/{seatmapId}` or `GET /v1/seatmap/{aircraftType}` to retrieve the full layout.

---

### POST /v1/seatmaps

Create a new seatmap definition for an aircraft type. Admin endpoint — not channel-facing.

> **One active seatmap per aircraft type.** If an active seatmap already exists for the given `aircraftTypeCode`, it is automatically deactivated (`IsActive = 0`) as part of the same transaction when the new seatmap is created.

#### Request

```json
{
  "aircraftTypeCode": "A351",
  "cabinLayout": { }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `aircraftTypeCode` | string | Yes | Must reference an existing active `AircraftType` |
| `cabinLayout` | object | Yes | Full cabin layout JSON document. Must pass `ISJSON` validation. See CabinLayout JSON Structure in the Data Schema section |

#### Response — `201 Created`

```json
{
  "seatmapId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "aircraftTypeCode": "A351",
  "version": 1,
  "isActive": true,
  "createdAt": "2026-03-20T10:00:00Z",
  "updatedAt": "2026-03-20T10:00:00Z"
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid `cabinLayout` JSON, or `aircraftTypeCode` does not reference an existing aircraft type |

---

### GET /v1/seatmaps/{seatmapId}

Retrieve a seatmap definition by ID, including the full `CabinLayout` JSON. Admin endpoint — not channel-facing.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `seatmapId` | string (UUID) | The unique seatmap identifier |

#### Response — `200 OK`

Returns the full seatmap record including the `cabinLayout` object, in the same structure as the response from `GET /v1/seatmap/{aircraftType}` with the addition of `seatmapId`, `isActive`, `createdAt`, and `updatedAt` fields.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No seatmap found for the given `seatmapId` |

---

### PUT /v1/seatmaps/{seatmapId}

Replace the cabin layout of an existing seatmap. Increments the `Version` counter. Admin endpoint — not channel-facing.

**When to use:** Called when the physical layout of an aircraft type changes (e.g. a cabin reconfiguration). Replaces the `CabinLayout` JSON in full — partial updates are not supported.

> **Impact on active bookings:** Updating the seatmap does not retroactively affect passengers already assigned to seats. However, the validation step at manifest creation (orchestration layer calling `GET /v1/seatmap/{aircraftType}` to verify a seat number) will use the updated layout. If a seat is removed from the layout, any subsequent attempt to assign that seat number will fail validation.

> **Version increment:** Each successful PUT increments `Version` by 1. This allows consumers to detect layout changes and invalidate any cached seatmap data.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `seatmapId` | string (UUID) | The unique seatmap identifier |

#### Request

```json
{
  "cabinLayout": { }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `cabinLayout` | object | Yes | Replacement cabin layout JSON document. Must pass `ISJSON` validation |

#### Response — `200 OK`

Returns the updated seatmap record with the incremented `version` value.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Invalid `cabinLayout` JSON |
| `404 Not Found` | No seatmap found for the given `seatmapId` |

---

### DELETE /v1/seatmaps/{seatmapId}

Delete a seatmap definition. Admin endpoint — not channel-facing.

> **Warning:** Deleting the active seatmap for an aircraft type will cause `GET /v1/seatmap/{aircraftType}` and `GET /v1/seat-offers?flightId={flightId}` to return `404 Not Found` for flights on that aircraft type. Use only for seatmaps created in error. To retire a seatmap without deleting it, use `PUT /v1/seatmaps/{seatmapId}` to replace the layout or set `isActive: false` via a supporting mechanism.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `seatmapId` | string (UUID) | The unique seatmap identifier |

#### Response — `204 No Content`

No response body.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No seatmap found for the given `seatmapId` |

---

### GET /v1/seat-pricing

List all seat pricing rules. Returns all records including inactive ones. Admin endpoint — not channel-facing.

#### Response — `200 OK`

```json
{
  "pricing": [
    {
      "seatPricingId": "d4e5f6a7-b8c9-0123-defa-234567890123",
      "cabinCode": "Y",
      "seatPosition": "Window",
      "currencyCode": "GBP",
      "price": 70.00,
      "isActive": true,
      "validFrom": "2026-01-01T00:00:00Z",
      "validTo": null,
      "createdAt": "2025-12-01T00:00:00Z",
      "updatedAt": "2025-12-01T00:00:00Z"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `seatPricingId` | string (UUID) | Unique pricing rule identifier |
| `cabinCode` | string | `W` or `Y` — Business (J) and First (F) are excluded |
| `seatPosition` | string | `Window`, `Aisle`, or `Middle` |
| `currencyCode` | string | ISO 4217 currency code |
| `price` | number | Price for this position. Decimal, 2 places |
| `isActive` | boolean | Whether this rule is currently active |
| `validFrom` | string (datetime) | ISO 8601 UTC effective start |
| `validTo` | string (datetime) | ISO 8601 UTC end. `null` = open-ended / currently active |
| `createdAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated on insert |
| `updatedAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated on every update |

---

### POST /v1/seat-pricing

Create a new seat pricing rule. Admin endpoint — not channel-facing.

> **Uniqueness:** A `UNIQUE` constraint on `(CabinCode, SeatPosition, CurrencyCode)` enforces one active price per combination. Creating a rule for an existing active combination returns `409 Conflict`. Use `PUT /v1/seat-pricing/{seatPricingId}` to update an existing price.
>
> **Business and First Class exclusion:** `CabinCode` must be `W` or `Y`. Attempting to create a pricing rule for `J` or `F` returns `400 Bad Request` — those cabins carry no ancillary charge and must never have a pricing rule.

#### Request

```json
{
  "cabinCode": "Y",
  "seatPosition": "Window",
  "currencyCode": "GBP",
  "price": 70.00,
  "validFrom": "2026-01-01T00:00:00Z",
  "validTo": null
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `cabinCode` | string | Yes | Must be `W` or `Y`. `J` and `F` are rejected |
| `seatPosition` | string | Yes | `Window`, `Aisle`, or `Middle` |
| `currencyCode` | string | Yes | ISO 4217 currency code, e.g. `GBP` |
| `price` | number | Yes | Price for this position. Decimal, 2 places. Must be > 0 |
| `validFrom` | string (datetime) | Yes | ISO 8601 UTC effective start |
| `validTo` | string (datetime) | No | ISO 8601 UTC end. `null` = open-ended |

#### Response — `201 Created`

Returns the full created pricing rule in the same schema as items in `GET /v1/seat-pricing`.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid values, `cabinCode` is `J` or `F`, or `validFrom` after `validTo` |
| `409 Conflict` | An active pricing rule already exists for the given `cabinCode`, `seatPosition`, and `currencyCode` combination |

---

### GET /v1/seat-pricing/{seatPricingId}

Retrieve a seat pricing rule by ID. Admin endpoint — not channel-facing.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `seatPricingId` | string (UUID) | The unique pricing rule identifier |

#### Response — `200 OK`

Returns a single pricing rule in the same schema as items in `GET /v1/seat-pricing`.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No pricing rule found for the given `seatPricingId` |

---

### PUT /v1/seat-pricing/{seatPricingId}

Update a seat pricing rule. Replaces all mutable fields. Admin endpoint — not channel-facing.

**When to use:** Called from the Contact Centre admin application when changing the price for a seat position tier.

> **Impact on active bookings:** Price changes take effect immediately for new seat offer generation. Existing `SeatOfferId` values in active baskets encode the `pricingRuleHash` at generation time. At confirmation, `GET /v1/seat-offers/{seatOfferId}` will return the current price. If the price has changed, the Retail API detects the discrepancy and must re-present the updated price to the customer before proceeding.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `seatPricingId` | string (UUID) | The unique pricing rule identifier |

#### Request

```json
{
  "price": 75.00,
  "validFrom": "2026-04-01T00:00:00Z",
  "validTo": null,
  "isActive": true
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `price` | number | Yes | Updated price. Decimal, 2 places. Must be > 0 |
| `validFrom` | string (datetime) | Yes | ISO 8601 UTC effective start |
| `validTo` | string (datetime) | No | ISO 8601 UTC end. `null` = open-ended |
| `isActive` | boolean | Yes | Whether this rule is active |

#### Response — `200 OK`

Returns the full updated pricing rule in the same schema as `GET /v1/seat-pricing/{seatPricingId}`.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid values, or `validFrom` after `validTo` |
| `404 Not Found` | No pricing rule found for the given `seatPricingId` |

---

### DELETE /v1/seat-pricing/{seatPricingId}

Delete a seat pricing rule. Admin endpoint — not channel-facing.

> **Warning:** Deleting a pricing rule will cause `GET /v1/seat-offers/{seatOfferId}` to return `404 Not Found` for any `SeatOfferId` that was generated using it. Any basket containing such an offer will fail validation at order confirmation. To disable a rule without deleting it, use `PUT /v1/seat-pricing/{seatPricingId}` with `isActive: false`.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `seatPricingId` | string (UUID) | The unique pricing rule identifier |

#### Response — `204 No Content`

No response body.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No pricing rule found for the given `seatPricingId` |

---

## Retail API Integration Flows

### Bookflow and Post-Sale — Full Seatmap with Pricing and Availability

The Retail API assembles the channel-facing seatmap response by merging three separate data sources. This applies to both the bookflow seat selection step and post-sale manage-booking seat changes.

1. **Retail API → Seat MS:** `GET /v1/seatmap/{aircraftType}` — retrieve physical cabin layout, seat positions, attributes, and `isSelectable` flags.
2. **Retail API → Seat MS:** `GET /v1/seat-offers?flightId={flightId}` — retrieve `SeatOfferId`, price, and `isChargeable` per selectable seat.
3. **Retail API → Offer MS:** `GET /v1/flights/{flightId}/seat-availability` — retrieve availability status (available, held, sold) per seat.
4. **Retail API** merges the three datasets and returns the unified seatmap response to the channel.

**Adding a seat to the basket:**

5. **Retail API → Seat MS:** `GET /v1/seat-offers/{seatOfferId}` — validate each selected `SeatOfferId` before adding to the basket.
6. **Retail API → Offer MS:** `POST /v1/flights/{flightId}/seat-reservations` — reserve the seat against the basket in flight inventory.
7. **Retail API → Order MS:** `PUT /v1/basket/{basketId}/seats` — write seat offer IDs and passenger assignments to the basket.

**At basket confirmation (bookflow):**

8. **Retail API → Seat MS:** `GET /v1/seat-offers/{seatOfferId}` — re-validate prices before authorising payment.
9. **Retail API → Payment MS:** Authorise seat payment (`description=SeatAncillary`). Business/First Class: no payment required (`price: 0.00`).
10. **Retail API → Delivery MS:** Reissue e-tickets with updated seat assignments.
11. **Retail API → Delivery MS:** Write `delivery.Manifest` entries (Seat MS not involved — seat number validated in step 1 against seatmap).
12. **Retail API → Delivery MS:** Issue `delivery.Document` (type `SeatAncillary`) for charged seat selections.
13. **Retail API → Payment MS:** Settle seat payment after order confirmation.

### OLCI (Online Check-In) — Seat Selection

At OLCI, seat selection is **free of charge** regardless of cabin. The flow follows steps 1–4 above to display the seatmap, but:

- Pricing is displayed for reference only — no `SeatOfferId` is used to drive a payment.
- The Retail API calls `PATCH /v1/checkin/{bookingRef}/seats` (not the basket endpoints).
- No seat ancillary payment is authorised or settled.
- The Retail API still calls `GET /v1/seatmap/{aircraftType}` to validate the seat number before writing to `delivery.Manifest`.

---

## Data Conventions

| Convention | Format | Example |
|------------|--------|---------|
| Timestamps | ISO 8601 UTC | `"2026-03-20T10:00:00Z"` |
| Currency codes | ISO 4217 | `"GBP"` |
| Currency amounts | Decimal, 2 places | `70.00` |
| JSON field names | camelCase | `seatNumber`, `cabinCode` |
| UUIDs | RFC 4122 lowercase | `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"` |
| Aircraft type codes | 4-character IATA-style | `"A351"`, `"B789"`, `"A339"` |
| Cabin codes | Single character | `"F"`, `"J"`, `"W"`, `"Y"` |
| Seat positions | PascalCase | `"Window"`, `"Aisle"`, `"Middle"` |

---

## Invocation Examples

### Retrieve seatmap layout (Retail API → Seat MS, step 1 of seatmap assembly)

```bash
curl -X GET https://{seat-ms-host}/v1/seatmap/A351 \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

### Retrieve priced seat offers (Retail API → Seat MS, step 2 of seatmap assembly)

```bash
curl -X GET "https://{seat-ms-host}/v1/seat-offers?flightId=3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

### Validate a seat offer at basket confirmation (Retail API → Seat MS)

```bash
curl -X GET https://{seat-ms-host}/v1/seat-offers/so-3fa85f64-35A-v1 \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

### Create an aircraft type (admin)

```bash
curl -X POST https://{seat-ms-host}/v1/aircraft-types \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "aircraftTypeCode": "A351",
    "manufacturer": "Airbus",
    "friendlyName": "Airbus A350-1000",
    "totalSeats": 369
  }'
```

### Create a seat pricing rule (admin)

```bash
curl -X POST https://{seat-ms-host}/v1/seat-pricing \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "cabinCode": "Y",
    "seatPosition": "Window",
    "currencyCode": "GBP",
    "price": 70.00,
    "validFrom": "2026-01-01T00:00:00Z",
    "validTo": null
  }'
```

### Update a seat pricing rule (admin)

```bash
curl -X PUT https://{seat-ms-host}/v1/seat-pricing/d4e5f6a7-b8c9-0123-defa-234567890123 \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "price": 75.00,
    "validFrom": "2026-04-01T00:00:00Z",
    "validTo": null,
    "isActive": true
  }'
```

> **Note:** All calls to the Seat microservice are authenticated using the `x-functions-key` header. The Seat MS never receives or validates end-user JWTs. See [`api.md` — Microservice Authentication](../api.md#microservice-authentication--host-keys) for the full host key mechanism.

---

## Related Documentation

- [API Endpoint Reference](../api-reference.md) — Summary of all microservice endpoints
- [System Design](../system-overview.md) — Full domain design including seatmap assembly sequence diagrams, post-sale seat selection flow, and OLCI seat assignment flow
