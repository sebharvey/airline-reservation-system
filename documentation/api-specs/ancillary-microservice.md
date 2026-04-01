# Ancillary Microservice — API Specification

> **Service owner:** Ancillary domain
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Ancillary microservice consolidates two capability areas: **seat ancillaries** (seatmap definitions, aircraft type records, fleet-wide seat pricing, and seat offer generation) and **bag ancillaries** (checked baggage policies, bag pricing, and bag offer generation).

`SeatOfferId` values are generated deterministically from `flightId + seatNumber + pricingRuleHash`. `BagOfferId` values are generated deterministically from `inventoryId + cabinCode + bagSequence`. Neither requires a dedicated offer storage table — offers are generated on demand and validated on retrieval.

> **Important:** The Ancillary microservice is an internal service. It is not called directly by channels (Web, App, NDC). All booking-path requests are routed through the **Retail API** orchestration layer. Admin endpoints are called from a future Contact Centre admin application.

---

## Security

### Authentication

Calls from the Retail API to the Ancillary microservice are authenticated using an **Azure Function Host Key**, passed in the `x-functions-key` HTTP header. The Ancillary microservice does not validate JWTs. See [Microservice Authentication — Host Keys](../api.md#microservice-authentication--host-keys) in `api.md` for the full mechanism.

### Required headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes (for request bodies) | Must be `application/json` |
| `x-functions-key` | Yes | Azure Function Host Key authenticating the caller |
| `X-Correlation-ID` | Yes | UUID propagated on every downstream call for distributed tracing |

---

## Seat ancillary

### Business rules

**Seat pricing** is fleet-wide, position-based, and route-agnostic:

| Position | Price (GBP) |
|----------|-------------|
| Window | £70.00 |
| Aisle | £50.00 |
| Middle | £20.00 |

Business Class (`J`) and First Class (`F`) seat selection is included in the fare at no ancillary charge. These cabins are excluded from `ancillary.SeatPricing`.

**Seatmap layout vs pricing vs availability** are three distinct concerns owned by different services:

- **Layout** — physical cabin and seat configuration. Owned by Ancillary MS via `GET /v1/seatmap/{aircraftType}`.
- **Pricing** — fleet-wide position-based prices and `SeatOfferId` generation. Owned by Ancillary MS via `GET /v1/seat-offers?flightId={flightId}`.
- **Availability** — real-time per-seat status (available, held, sold) for a specific flight. Owned by **Offer MS** via `GET /v1/flights/{flightId}/seat-availability`.

The Retail API merges all three datasets before returning the seatmap response to the channel.

**`isSelectable`** reflects only whether a seat is physically available for selection (not a structural block or crew seat). Real-time occupancy is overlaid from the Offer MS.

**Seat validation responsibility:** Before writing any row to `delivery.Manifest`, the orchestration layer must validate `SeatNumber` against the active seatmap by calling `GET /v1/seatmap/{aircraftType}`.

**Ancillary document requirement:** For every paid seat selection, the Retail API must create a `delivery.Document` record (type `SeatAncillary`) via the Delivery MS.

**Aircraft type code convention:** 4 characters — manufacturer prefix + 3-digit variant. Examples: `A351` (A350-1000), `B789` (B787-9), `A339` (A330-900).

### Data schema

#### `ancillary.AircraftType`

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `AircraftTypeCode` | CHAR(4) | No | | PK | e.g. `A351`, `B789`, `A339` |
| `Manufacturer` | VARCHAR(50) | No | | | e.g. `Airbus`, `Boeing` |
| `FriendlyName` | VARCHAR(100) | Yes | | | e.g. `Airbus A350-1000` |
| `TotalSeats` | SMALLINT | No | | | Total seat count across all cabins |
| `CabinCounts` | NVARCHAR(MAX) | Yes | | | JSON array, e.g. `[{"cabin":"J","count":32}]`. `ISJSON` check constraint |
| `IsActive` | BIT | No | `1` | | |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | Read-only — SQL trigger on insert |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | Read-only — SQL trigger on update |

#### `ancillary.Seatmap`

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `SeatmapId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `AircraftTypeCode` | CHAR(4) | No | | FK → `ancillary.AircraftType` | |
| `Version` | INT | No | `1` | | Incremented on each `PUT` |
| `IsActive` | BIT | No | `1` | | One active seatmap per aircraft type |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | Read-only — SQL trigger on insert |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | Read-only — SQL trigger on update |
| `CabinLayout` | NVARCHAR(MAX) | No | | | Full cabin and seat JSON. `ISJSON` check constraint |

> **Index:** `IX_Seatmap_AircraftType` on `(AircraftTypeCode)` WHERE `IsActive = 1`.
> **Known configurations:** A351 — 369 seats, rows 1–10 J (1-2-1), 20–28 W (2-3-2), 35–62 Y (3-3-3). B789 — 296 seats. A339 — 326 seats, Y layout 3-4-3.

#### `ancillary.SeatPricing`

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `SeatPricingId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `CabinCode` | CHAR(1) | No | | UK (with SeatPosition, CurrencyCode) | `W` or `Y` only — `J`/`F` carry no ancillary charge |
| `SeatPosition` | VARCHAR(10) | No | | UK (with CabinCode, CurrencyCode) | `Window`, `Aisle`, or `Middle` |
| `CurrencyCode` | CHAR(3) | No | `'GBP'` | UK (with CabinCode, SeatPosition) | ISO 4217 |
| `Price` | DECIMAL(10,2) | No | | | |
| `IsActive` | BIT | No | `1` | | |
| `ValidFrom` | DATETIME2 | No | | | Effective start |
| `ValidTo` | DATETIME2 | Yes | | | Null = open-ended |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | Read-only — SQL trigger on insert |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | Read-only — SQL trigger on update |

> **Constraint:** `UQ_SeatPricing_CabinPosition` on `(CabinCode, SeatPosition, CurrencyCode)`.
> **Seed data:** `('W','Window','GBP',70.00)` · `('W','Aisle','GBP',50.00)` · `('W','Middle','GBP',20.00)` · `('Y','Window','GBP',70.00)` · `('Y','Aisle','GBP',50.00)` · `('Y','Middle','GBP',20.00)`.

---

### Seat endpoints

#### GET /v1/seatmap/{aircraftType}

Retrieve the active seatmap definition and cabin layout for an aircraft type. Returns physical layout and seat attributes only — no pricing or availability.

**When to use:** Called by the Retail API as step 1 of seatmap assembly, and by the orchestration layer to validate a seat number before writing to `delivery.Manifest`.

| Parameter | Type | Description |
|-----------|------|-------------|
| `aircraftType` | string (path) | 4-character aircraft type code, e.g. `A351` |

**Response — `200 OK`**

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
      "rows": [
        {
          "rowNumber": 1,
          "seats": [
            { "seatNumber": "1A", "column": "A", "type": "Suite", "position": "Window", "attributes": ["ExtraLegroom", "BulkheadForward"], "isSelectable": true }
          ]
        }
      ]
    }
  ]
}
```

Seat `attributes` values: `ExtraLegroom`, `ExitRow`, `BulkheadForward`, `BulkheadAft`, `ReducedRecline`, `BlockedForCrew`, `LieFlat`.

**Errors:** `404` — no active seatmap for the given aircraft type.

---

#### GET /v1/seat-offers?flightId={flightId}

Generate and return priced seat offers for all selectable seats on a specific flight. Returns one `SeatOffer` per selectable seat with a deterministic `SeatOfferId`, current price, and attributes. No database write occurs.

**When to use:** Called by the Retail API as step 2 of seatmap assembly (after layout retrieval). Also called during post-sale seat selection and OLCI.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `flightId` | string (UUID, query) | Yes | `InventoryId` from `offer.FlightInventory` |

**Response — `200 OK`**

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
      "attributes": ["ExtraLegroom"],
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
      "attributes": ["ExtraLegroom"],
      "isSelectable": true,
      "isChargeable": true,
      "price": 70.00,
      "currencyCode": "GBP"
    }
  ]
}
```

Non-selectable seats (`isSelectable: false`) are excluded from the array entirely.

**Errors:** `400` — missing/malformed `flightId`. `404` — no active seatmap for the flight's aircraft type.

---

#### GET /v1/seat-offers/{seatOfferId}

Retrieve and validate a specific seat offer by deterministic ID. Confirms the pricing rule is still active. Used by the Retail API when adding a seat to a basket and again at order confirmation.

No consumption state is tracked — the Offer MS manages seat hold/sell/release separately.

**Response — `200 OK`**

```json
{
  "seatOfferId": "so-3fa85f64-35A-v1",
  "flightId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "seatNumber": "35A",
  "cabinCode": "Y",
  "position": "Window",
  "type": "Standard",
  "attributes": ["ExtraLegroom"],
  "isSelectable": true,
  "isChargeable": true,
  "price": 70.00,
  "currencyCode": "GBP",
  "isValid": true
}
```

**Errors:** `400` — malformed `seatOfferId`. `404` — pricing rule no longer active or ID cannot be resolved.

---

#### GET /v1/aircraft-types

List all aircraft types including inactive. Admin endpoint.

**Response — `200 OK`:** Array of aircraft type objects — `aircraftTypeCode`, `manufacturer`, `friendlyName`, `totalSeats`, `cabinCounts`, `isActive`, `createdAt`, `updatedAt`.

---

#### POST /v1/aircraft-types

Create a new aircraft type. Admin endpoint.

**Request:** `aircraftTypeCode` (required, 4 chars), `manufacturer` (required), `friendlyName`, `totalSeats` (required, > 0), `cabinCounts`.

**Response:** `201 Created` — full aircraft type object. **Errors:** `400`, `409` (code already exists).

---

#### GET /v1/aircraft-types/{aircraftTypeCode}

Retrieve an aircraft type by code. Admin endpoint. **Errors:** `404`.

---

#### PUT /v1/aircraft-types/{aircraftTypeCode}

Update an aircraft type. Admin endpoint. **Request:** `manufacturer`, `friendlyName`, `totalSeats`, `cabinCounts`, `isActive`. **Errors:** `400`, `404`.

---

#### DELETE /v1/aircraft-types/{aircraftTypeCode}

Delete an aircraft type. Only permitted if no active seatmaps reference it. Admin endpoint. **Errors:** `404`, `409` (active seatmaps exist).

---

#### GET /v1/seatmaps

List all seatmap definitions (excluding `CabinLayout`). Admin endpoint.

---

#### POST /v1/seatmaps

Create a new seatmap. If an active seatmap already exists for the aircraft type, it is deactivated in the same transaction. Admin endpoint.

**Request:** `aircraftTypeCode` (required), `cabinLayout` (required, valid JSON). **Response:** `201 Created`. **Errors:** `400`.

---

#### GET /v1/seatmaps/{seatmapId}

Retrieve a seatmap by ID including full `CabinLayout`. Admin endpoint. **Errors:** `404`.

---

#### PUT /v1/seatmaps/{seatmapId}

Replace the cabin layout of an existing seatmap. Increments `Version`. Admin endpoint.

**Request:** `cabinLayout` (required). **Errors:** `400`, `404`.

---

#### DELETE /v1/seatmaps/{seatmapId}

Delete a seatmap. Admin endpoint. **Errors:** `404`.

---

#### GET /v1/seat-pricing

List all seat pricing rules including inactive. Admin endpoint.

**Response:** Array of pricing rule objects — `seatPricingId`, `cabinCode`, `seatPosition`, `currencyCode`, `price`, `isActive`, `validFrom`, `validTo`, `createdAt`, `updatedAt`.

---

#### POST /v1/seat-pricing

Create a new seat pricing rule. `CabinCode` must be `W` or `Y` — `J`/`F` are rejected. Admin endpoint.

**Request:** `cabinCode`, `seatPosition`, `currencyCode`, `price` (> 0), `validFrom`, `validTo`. **Errors:** `400`, `409` (combination already exists).

---

#### GET /v1/seat-pricing/{seatPricingId}

Retrieve a seat pricing rule by ID. Admin endpoint. **Errors:** `404`.

---

#### PUT /v1/seat-pricing/{seatPricingId}

Update a seat pricing rule. Admin endpoint. **Request:** `price`, `validFrom`, `validTo`, `isActive`. **Errors:** `400`, `404`.

---

#### DELETE /v1/seat-pricing/{seatPricingId}

Delete a seat pricing rule. Invalidates any `SeatOfferId` generated from it. Admin endpoint. **Errors:** `404`.

---

### Seat — Retail API integration flows

**Full seatmap assembly (bookflow and post-sale):**

1. Retail API → Ancillary MS: `GET /v1/seatmap/{aircraftType}` — layout
2. Retail API → Ancillary MS: `GET /v1/seat-offers?flightId={flightId}` — pricing and `SeatOfferId` per seat
3. Retail API → Offer MS: `GET /v1/flights/{flightId}/seat-availability` — availability status
4. Retail API merges and returns unified seatmap to channel

**Adding a seat to the basket:**

5. Retail API → Ancillary MS: `GET /v1/seat-offers/{seatOfferId}` — validate offer
6. Retail API → Offer MS: `POST /v1/flights/{flightId}/seat-reservations` — reserve seat
7. Retail API → Order MS: `PUT /v1/basket/{basketId}/seats`

**At basket confirmation:**

8. Retail API → Ancillary MS: `GET /v1/seat-offers/{seatOfferId}` — re-validate prices
9. Retail API → Payment MS: authorise seat payment (`description=SeatAncillary`). Business/First: no payment (`price: 0.00`)
10. Retail API → Delivery MS: reissue e-tickets, write manifest, issue `delivery.Document` (type `SeatAncillary`)
11. Retail API → Payment MS: settle seat payment

**OLCI seat selection:** Pricing displayed for reference only — no `SeatOfferId` drives a payment. No ancillary charge regardless of cabin.

---

## Bag ancillary

### Business rules

**Free bag allowance** by cabin (uniform across all routes):

| Cabin | Free Bags | Max Weight per Bag |
|-------|-----------|--------------------|
| First (F) | 2 | 32 kg |
| Business (J) | 2 | 32 kg |
| Premium Economy (W) | 2 | 23 kg |
| Economy (Y) | 1 | 23 kg |

**Additional bag pricing** (per bag, per segment, fleet-wide):

| Sequence | Description | Price |
|----------|-------------|-------|
| 1 | 1st additional bag | £60.00 |
| 2 | 2nd additional bag | £80.00 |
| 99 | 3rd additional bag and beyond (catch-all) | £100.00 |

**Bag offer generation:** `BagOfferId` is computed deterministically from `inventoryId + cabinCode + bagSequence`. No offer record is stored. The Retail API calls `GET /v1/bags/offers/{bagOfferId}` at order confirmation to validate the price stored in the basket.

**Where bags can be purchased:** During the bookflow (as a basket ancillary), post-sale via manage booking, or at OLCI (immediate payment required).

**Ancillary document requirement:** For every bag purchase, the Retail API must create a `delivery.Document` record (type `BagAncillary`) via the Delivery MS.

### Data schema

#### `ancillary.BagPolicy`

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `PolicyId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `CabinCode` | CHAR(1) | No | | UK | `F` · `J` · `W` · `Y` |
| `FreeBagsIncluded` | TINYINT | No | | | |
| `MaxWeightKgPerBag` | TINYINT | No | | | |
| `IsActive` | BIT | No | `1` | | |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | Read-only — SQL trigger on insert |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | Read-only — SQL trigger on update |

> **Constraint:** `UNIQUE` on `(CabinCode)` — one active policy per cabin. Update existing row; do not insert duplicates.
> **Seed data:** `('F',2,32)` · `('J',2,32)` · `('W',2,23)` · `('Y',1,23)`.

#### `ancillary.BagPricing`

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `PricingId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `BagSequence` | TINYINT | No | | UK (with CurrencyCode) | `1`, `2`, `99` (catch-all) |
| `CurrencyCode` | CHAR(3) | No | `'GBP'` | UK (with BagSequence) | ISO 4217 |
| `Price` | DECIMAL(10,2) | No | | | |
| `IsActive` | BIT | No | `1` | | |
| `ValidFrom` | DATETIME2 | No | | | Effective start |
| `ValidTo` | DATETIME2 | Yes | | | Null = open-ended |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | Read-only — SQL trigger on insert |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | Read-only — SQL trigger on update |

> **Constraint:** `UQ_BagPricing_Sequence` on `(BagSequence, CurrencyCode)`.
> **Seed data:** `(1,'GBP',60.00)` · `(2,'GBP',80.00)` · `(99,'GBP',100.00)`.

---

### Bag endpoints

#### GET /v1/bags/offers?inventoryId={inventoryId}&cabinCode={cabinCode}

Generate and return the free bag policy and priced bag offers for a specific flight and cabin. Returns the free allowance and one `BagOfferId` per purchasable additional bag tier. No database write occurs.

**When to use:** Called by the Retail API during the bookflow, post-sale manage booking, and OLCI flows.

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `inventoryId` | string (UUID, query) | Yes | `InventoryId` from `offer.FlightInventory` |
| `cabinCode` | string (query) | Yes | `F`, `J`, `W`, or `Y` |

**Response — `200 OK`**

```json
{
  "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cabinCode": "Y",
  "policy": {
    "freeBagsIncluded": 1,
    "maxWeightKgPerBag": 23
  },
  "bagOffers": [
    { "bagOfferId": "bo-3fa85f64-Y-1-v1", "bagSequence": 1, "description": "1st additional checked bag", "price": 60.00, "currencyCode": "GBP" },
    { "bagOfferId": "bo-3fa85f64-Y-2-v1", "bagSequence": 2, "description": "2nd additional checked bag", "price": 80.00, "currencyCode": "GBP" },
    { "bagOfferId": "bo-3fa85f64-Y-99-v1", "bagSequence": 99, "description": "3rd additional checked bag and beyond", "price": 100.00, "currencyCode": "GBP" }
  ]
}
```

**Errors:** `400` — missing/invalid parameters. `404` — no active `BagPolicy` for the given cabin code.

---

#### GET /v1/bags/offers/{bagOfferId}

Retrieve and validate a bag offer by deterministic ID. Confirms the pricing rule is still active. No consumption state is tracked.

**Response — `200 OK`**

```json
{
  "bagOfferId": "bo-3fa85f64-Y-1-v1",
  "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cabinCode": "Y",
  "bagSequence": 1,
  "description": "1st additional checked bag",
  "price": 60.00,
  "currencyCode": "GBP",
  "isValid": true
}
```

**Errors:** `400` — malformed `bagOfferId`. `404` — pricing rule no longer active or ID cannot be resolved.

---

#### GET /v1/bag-policies

List all bag allowance policies including inactive. Admin endpoint.

---

#### POST /v1/bag-policies

Create a new bag allowance policy. Admin endpoint.

**Request:** `cabinCode` (required), `freeBagsIncluded` (required, ≥ 0), `maxWeightKgPerBag` (required, > 0). **Errors:** `400`, `409` (policy for cabin already exists).

---

#### GET /v1/bag-policies/{policyId}

Retrieve a bag policy by ID. Admin endpoint. **Errors:** `404`.

---

#### PUT /v1/bag-policies/{policyId}

Update a bag allowance policy. Admin endpoint. **Request:** `freeBagsIncluded`, `maxWeightKgPerBag`, `isActive`. **Errors:** `400`, `404`.

---

#### DELETE /v1/bag-policies/{policyId}

Delete a bag allowance policy. Admin endpoint. **Errors:** `404`.

---

#### GET /v1/bag-pricing

List all bag pricing rules including inactive. Admin endpoint.

---

#### POST /v1/bag-pricing

Create a new bag pricing rule. Admin endpoint.

**Request:** `bagSequence` (required, `1`/`2`/`99`), `currencyCode` (required), `price` (required, > 0), `validFrom` (required), `validTo`. **Errors:** `400`, `409` (sequence/currency combination already exists).

---

#### GET /v1/bag-pricing/{pricingId}

Retrieve a bag pricing rule by ID. Admin endpoint. **Errors:** `404`.

---

#### PUT /v1/bag-pricing/{pricingId}

Update a bag pricing rule. Admin endpoint. **Request:** `price`, `validFrom`, `validTo`, `isActive`. **Errors:** `400`, `404`.

---

#### DELETE /v1/bag-pricing/{pricingId}

Delete a bag pricing rule. Invalidates any `BagOfferId` generated from it. Admin endpoint. **Errors:** `404`.

---

### Bag — Retail API integration flows

**Bookflow bag selection:**

1. Retail API → Ancillary MS: `GET /v1/bags/offers?inventoryId={id}&cabinCode={code}` — per segment
2. Retail API → Ancillary MS: `GET /v1/bags/offers/{bagOfferId}` — validate each selected offer
3. Retail API → Order MS: `PUT /v1/basket/{basketId}/bags`
4. At confirmation: Retail API → Payment MS (authorise), Order MS (create bag order items), Delivery MS (issue `BagAncillary` document), Payment MS (settle)

**Post-sale bag addition:**

1. Same offer retrieval and validation as above
2. Retail API → Payment MS: authorise + settle bag payment
3. Retail API → Order MS: `PATCH /v1/orders/{bookingRef}/bags`
4. Retail API → Delivery MS: issue `delivery.Document` (type `BagAncillary`)

---

## Data conventions

| Convention | Format | Example |
|------------|--------|---------|
| Timestamps | ISO 8601 UTC | `"2026-03-20T10:00:00Z"` |
| Currency codes | ISO 4217 | `"GBP"` |
| Monetary amounts | `DECIMAL`, 2 places | `70.00` |
| JSON field names | camelCase | `seatNumber`, `bagSequence` |
| UUIDs | RFC 4122 lowercase | `"3fa85f64-5717-4562-b3fc-2c963f66afa6"` |
| Aircraft type codes | 4-character | `"A351"`, `"B789"`, `"A339"` |
| Cabin codes | Single character | `"F"`, `"J"`, `"W"`, `"Y"` |
| `createdAt` / `updatedAt` | Database-generated | Never written by application code |

---

## Related documentation

- [API Endpoint Reference](../api-reference.md) — summary of all Ancillary MS endpoints
- [Ancillary Domain Design](../design/ancillary.md) — business rules, sequence flows, full data schemas
- [System Overview](../system-overview.md) — architecture and domain model
