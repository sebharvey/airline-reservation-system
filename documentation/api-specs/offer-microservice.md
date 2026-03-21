# Offer Microservice — API Specification

> **Service owner:** Offer domain
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Offer microservice is the system of record for flight inventory, fare definitions, stored offer snapshots, and per-flight seat availability. It owns four capability areas: **Flight Inventory** (`offer.FlightInventory`) — seat capacity per flight, cabin, and date; **Fares** (`offer.Fare`) — fare definitions, pricing, and conditions linked to inventory records; **Stored Offers** (`offer.StoredOffer`) — point-in-time pricing snapshots presented to customers, ensuring price integrity through to order creation; and **Seat Availability** — per-seat status (available, held, sold) overlaid by the Retail API onto the Seat MS layout and pricing.

> **Important:** The Offer microservice operates on individual flight **segments only**. It has no concept of multi-segment connecting itineraries. Connecting itinerary assembly (pairing legs, enforcing minimum connect time, combining prices) is exclusively the responsibility of the Retail API orchestration layer.

> **Important:** The Offer microservice is an internal service. It is not called directly by channels (Web, App, NDC). All requests are routed through the **Retail API**, **Operations API**, **Schedule MS**, or **Disruption API** orchestration layers. See the [Security](#security) section for authentication details.

> **Implementation note:** The `src/API/Microservices/ReservationSystem.Microservices.Offer` project was scaffolded from the template and contains generic CRUD stubs that do not reflect the real Offer domain. When building this microservice, reuse that project as the starting point but **remove all placeholder CRUD operations and replace them with the real Offer domain implementation** as defined in this specification. Preserve the project structure, DI wiring, `host.json`, shared library references, and build pipeline.

---

## Security

### Authentication

The Offer microservice is called exclusively by orchestration APIs and the Schedule MS. It does not validate JWTs; that responsibility belongs to the calling layer.

Calls from orchestration APIs to the Offer microservice are authenticated using an **Azure Function Host Key**, passed in the `x-functions-key` HTTP header. See [Microservice Authentication — Host Keys](../api.md#microservice-authentication--host-keys) in `api.md` for the full mechanism, key storage, and retrieval details.

### Required Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes (for request bodies) | Must be `application/json` |
| `x-functions-key` | Yes | Azure Function Host Key authenticating the caller |
| `X-Correlation-ID` | Yes | UUID generated at the channel boundary; propagated on every downstream call |

### Data Protection

- No PII is stored in the Offer MS. Flight inventory and pricing data only.
- `OfferId` values are single-use and expire after 60 minutes. They must not be persisted by channels beyond the current booking session.

---

## Business Rules

### Stored Offer Pattern — Price Integrity

The core principle of the Offer MS is that **prices are locked at search time, not at payment time**.

- When a customer searches for flights, the Offer MS persists one `StoredOffer` snapshot per available cabin per flight result. Each snapshot captures the exact fare, taxes, conditions, and points pricing at the moment of presentation.
- The channel presents the `OfferId` to the customer. The customer selects an offer and passes its `OfferId` to the basket.
- The Retail API calls `GET /v1/offers/{offerId}` to retrieve the stored snapshot. It validates `IsConsumed = 0` and `ExpiresAt > now` before proceeding. If either check fails, the offer is rejected and the customer must re-search.
- At basket creation, the Retail API calls `POST /v1/inventory/hold` to hold seats against the basket.
- On order confirmation, `POST /v1/inventory/sell` converts held seats to sold. The fare charged is exactly the fare snapshotted at search time — no re-pricing occurs.
- `IsConsumed` is set to `1` atomically when the Order MS retrieves and locks the offer. This prevents the same offer being used on multiple concurrent orders.

### Offer Expiry

- `ExpiresAt` is set to `CreatedAt + 60 minutes`, matching the basket expiry window. This ensures a basket cannot reference an offer that has already expired.
- A background job periodically purges expired unconsumed offers from `offer.StoredOffer` to keep the table lean. Expired consumed offers are retained for audit.
- The Order MS validates offer expiry before consuming it. Expired offers must be rejected.

### Inventory Integrity

The invariant `SeatsAvailable + SeatsSold + SeatsHeld = TotalSeats` must be maintained by the Offer MS on every inventory mutation. There is no database-level check constraint — the application layer is solely responsible for keeping these counts consistent.

| Operation | SeatsAvailable | SeatsHeld | SeatsSold |
|-----------|---------------|-----------|-----------|
| `POST /v1/inventory/hold` | -N | +N | unchanged |
| `POST /v1/inventory/sell` | unchanged | -N | +N |
| `POST /v1/inventory/release` (from held) | +N | -N | unchanged |
| `POST /v1/inventory/release` (from sold) | +N | unchanged | -N |
| `PATCH /v1/inventory/cancel` | → 0 | unchanged | unchanged |

### Connecting Itineraries — Offer MS Scope Boundary

The Offer MS has no knowledge of multi-leg itineraries. When the Retail API handles a connecting itinerary search (`POST /v1/search/connecting`), it calls `POST /v1/search` twice — once per leg — pairs the results, applies the 60-minute minimum connect time (MCT) filter at LHR, and returns composite itinerary options to the channel. Each leg is a separate stored offer with its own `OfferId`; both are placed in the basket together.

### Seat Availability vs Seat Pricing

The Offer MS owns **real-time seat availability** (available, held, or sold) per flight. It does not own seat pricing or seatmap layout. The full seatmap response for a channel is assembled by the Retail API from three sources: Seat MS layout, Seat MS pricing/`SeatOfferId`, and Offer MS availability status.

### Code Share (Future Scope)

Code share is not in scope for the initial release. However, `offer.FlightInventory` must be designed to accommodate future `OperatingCarrier` and `OperatingFlightNumber` columns. API responses for `POST /v1/search` should include optional `operatingCarrier` and `operatingFlightNumber` fields (omitted/`null` for own-metal flights) so channels can handle them from launch.

### Cancellation via Disruption API

When the Disruption API cancels a flight via `PATCH /v1/inventory/cancel`, `Status` is set to `Cancelled` and `SeatsAvailable` to `0`. Cancelled inventory is excluded from all search results. The Disruption API calls this endpoint synchronously before returning `202 Accepted` to the Flight Operations System.

---

## Data Schema

### `offer.FlightInventory`

One row per flight per cabin per operating date. Created by the Operations API (via `POST /v1/flights`) during schedule generation.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `InventoryId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `FlightNumber` | VARCHAR(10) | No | | | e.g. `AX001` |
| `DepartureDate` | DATE | No | | | Operating date |
| `DepartureTime` | TIME | No | | | Local departure time at origin; sourced from schedule definition |
| `ArrivalTime` | TIME | No | | | Local arrival time at destination; sourced from schedule definition |
| `ArrivalDayOffset` | TINYINT | No | `0` | | `0` = same calendar day; `1` = arrives next day |
| `Origin` | CHAR(3) | No | | | IATA airport code |
| `Destination` | CHAR(3) | No | | | IATA airport code |
| `AircraftType` | VARCHAR(4) | No | | | IATA-style 4-char code, e.g. `A351`, `B789` |
| `CabinCode` | CHAR(1) | No | | | `F` · `J` · `W` · `Y` |
| `TotalSeats` | SMALLINT | No | | | Physical seat count for this cabin on this flight |
| `SeatsAvailable` | SMALLINT | No | | | Decremented on hold; incremented on release |
| `SeatsSold` | SMALLINT | No | `0` | | Incremented on sell; decremented on release from sold |
| `SeatsHeld` | SMALLINT | No | `0` | | Seats held in active baskets, not yet ticketed |
| `Status` | VARCHAR(20) | No | `'Active'` | | `Active` · `Cancelled` |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on every update** |

> **Indexes:** `IX_FlightInventory_Flight` on `(FlightNumber, DepartureDate, CabinCode)` WHERE `Status = 'Active'`.
> **Invariant:** `SeatsAvailable + SeatsSold + SeatsHeld = TotalSeats` must be maintained by the application layer on every mutation.
> **Cancellation:** On `PATCH /v1/inventory/cancel`, `Status` = `Cancelled` and `SeatsAvailable` = `0`. Cancelled inventory is excluded from search results.

---

### `offer.Fare`

One row per fare basis per inventory record. Multiple fares can exist per `InventoryId` (one per fare family / booking class). Created by the Operations API (via `POST /v1/flights/{inventoryId}/fares`) during schedule generation.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `FareId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `InventoryId` | UNIQUEIDENTIFIER | No | | FK → `offer.FlightInventory(InventoryId)` | |
| `FareBasisCode` | VARCHAR(20) | No | | | Revenue management fare basis code, e.g. `YLOWUK`, `JFLEXGB` |
| `FareFamily` | VARCHAR(50) | Yes | | | Commercial product name, e.g. `Economy Light`, `Business Flex` |
| `CabinCode` | CHAR(1) | No | | | `F` · `J` · `W` · `Y` |
| `BookingClass` | CHAR(1) | No | | | Revenue management booking class. When generating fares from a schedule, defaults from `CabinCode` (`F`→`F`, `J`→`J`, `W`→`W`, `Y`→`Y`) |
| `CurrencyCode` | CHAR(3) | No | `'GBP'` | | ISO 4217 |
| `BaseFareAmount` | DECIMAL(10,2) | No | | | Carrier base fare, excluding taxes |
| `TaxAmount` | DECIMAL(10,2) | No | | | Total taxes and surcharges |
| `TotalAmount` | DECIMAL(10,2) | No | | | `BaseFareAmount + TaxAmount`. Stored explicitly for query efficiency |
| `IsRefundable` | BIT | No | `0` | | Whether the fare permits a refund on voluntary cancellation |
| `IsChangeable` | BIT | No | `0` | | Whether the fare permits a voluntary flight change |
| `ChangeFeeAmount` | DECIMAL(10,2) | No | `0.00` | | Fee charged on voluntary flight change. `0.00` for fully flexible fares |
| `CancellationFeeAmount` | DECIMAL(10,2) | No | `0.00` | | Fee deducted from refund on voluntary cancellation. `0.00` for fully refundable fares |
| `PointsPrice` | INT | Yes | | | Points required for an award booking. `NULL` if not available for points redemption |
| `PointsTaxes` | DECIMAL(10,2) | Yes | | | Cash taxes payable on an award booking. `NULL` if `PointsPrice` is `NULL` |
| `ValidFrom` | DATETIME2 | No | | | Fare sale window start |
| `ValidTo` | DATETIME2 | No | | | Fare sale window end. Expired fares are excluded from search results |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on every update** |

> **Points pricing:** A fare with a non-null `PointsPrice` can be redeemed for points. The cash `TotalAmount` represents the revenue price for standard bookings. When a customer searches in points mode, the Offer MS returns `PointsPrice` and `PointsTaxes` as the primary pricing fields. Fares with `PointsPrice = NULL` are revenue-only and will not appear in points-mode searches.
> **Change and cancellation fees:** `ChangeFeeAmount` and `CancellationFeeAmount` are stored so the Retail API can calculate `totalDue = changeFee + addCollect` and `refundableAmount = totalPaid − cancellationFee` without a separate fee lookup. Non-changeable fares use `0.00` for `ChangeFeeAmount` — the change is simply not permitted.

---

### `offer.StoredOffer`

One row per offer presented to a customer at search time. Fully denormalised snapshot — self-contained even if the underlying `Fare` is later updated or withdrawn.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `OfferId` | UNIQUEIDENTIFIER | No | NEWID() | PK | Returned to channel at search time; passed to basket and Order MS to lock pricing |
| `InventoryId` | UNIQUEIDENTIFIER | No | | FK → `offer.FlightInventory(InventoryId)` | |
| `FareId` | UNIQUEIDENTIFIER | No | | FK → `offer.Fare(FareId)` | |
| `FlightNumber` | VARCHAR(10) | No | | | Denormalised snapshot |
| `DepartureDate` | DATE | No | | | Denormalised snapshot |
| `Origin` | CHAR(3) | No | | | Denormalised snapshot, IATA code |
| `Destination` | CHAR(3) | No | | | Denormalised snapshot, IATA code |
| `FareBasisCode` | VARCHAR(20) | No | | | Denormalised snapshot |
| `FareFamily` | VARCHAR(50) | Yes | | | Denormalised snapshot |
| `CurrencyCode` | CHAR(3) | No | `'GBP'` | | ISO 4217 |
| `BaseFareAmount` | DECIMAL(10,2) | No | | | Price at time offer was created |
| `TaxAmount` | DECIMAL(10,2) | No | | | Taxes at time offer was created |
| `TotalAmount` | DECIMAL(10,2) | No | | | Total at time offer was created |
| `IsRefundable` | BIT | No | `0` | | Fare conditions at time of offer creation |
| `IsChangeable` | BIT | No | `0` | | Fare conditions at time of offer creation |
| `ChangeFeeAmount` | DECIMAL(10,2) | No | `0.00` | | Snapshotted change fee at time of offer creation |
| `CancellationFeeAmount` | DECIMAL(10,2) | No | `0.00` | | Snapshotted cancellation fee at time of offer creation |
| `PointsPrice` | INT | Yes | | | Points required for this offer. `NULL` for revenue-only offers |
| `PointsTaxes` | DECIMAL(10,2) | Yes | | | Cash taxes payable on award booking. `NULL` if `PointsPrice` is `NULL` |
| `BookingType` | VARCHAR(10) | No | `'Revenue'` | | `Revenue` · `Reward`. Set at offer creation based on search mode |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on insert** |
| `ExpiresAt` | DATETIME2 | No | | | Set to `CreatedAt + 60 minutes`. Offer rejected by Order MS if `now > ExpiresAt` |
| `IsConsumed` | BIT | No | `0` | | Set to `1` atomically when retrieved and locked by Order MS. Prevents duplicate use |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on every update** |

> **Indexes:** `IX_StoredOffer_Expiry` on `(ExpiresAt)` WHERE `IsConsumed = 0` — used by background cleanup job to purge expired unconsumed offers.
> **Denormalisation:** Flight and fare fields are deliberately denormalised so the snapshot is fully self-contained. If `offer.Fare` is later updated or withdrawn, the stored offer retains the exact price and conditions presented to the customer.
> **`ExpiresAt` alignment:** Set to `CreatedAt + 60 minutes`, matching the basket expiry window. A basket cannot reference an offer that has already expired.
> **`IsConsumed`:** Set atomically to `1` when the Order MS retrieves and locks the offer. This is a single-use guarantee — a concurrent second basket attempting to use the same `OfferId` will receive a rejection.

---

## Endpoints

---

### POST /v1/flights

Create a new flight inventory record for a specific operating date and cabin. Called by the Operations API during schedule generation — once per operating date per cabin. Initialises `SeatsAvailable = TotalSeats`, `SeatsHeld = 0`, `SeatsSold = 0`. Returns `inventoryId` for use in the subsequent fare creation calls.

**When to use:** Called by the Operations API as part of the schedule creation orchestration. After the Schedule MS returns the list of `operatingDates` and `cabinFareDefinitions`, the Operations API calls `POST /v1/flights` for each `operatingDate × cabin` combination, then calls `POST /v1/flights/{inventoryId}/fares` for each fare within that cabin.

> **Inventory is immediately live:** Created records are immediately available for offer search with no additional activation step.

#### Request

```json
{
  "flightNumber": "AX001",
  "departureDate": "2026-04-01",
  "departureTime": "09:30",
  "arrivalTime": "13:45",
  "arrivalDayOffset": 0,
  "origin": "LHR",
  "destination": "JFK",
  "aircraftType": "A351",
  "cabinCode": "J",
  "totalSeats": 30
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `flightNumber` | string | Yes | e.g. `AX001`. Max 10 characters |
| `departureDate` | string (date) | Yes | ISO 8601 operating date |
| `departureTime` | string (time) | Yes | Local departure time at origin, `HH:mm` format |
| `arrivalTime` | string (time) | Yes | Local arrival time at destination, `HH:mm` format |
| `arrivalDayOffset` | integer | No | `0` = same calendar day (default); `1` = next day at destination |
| `origin` | string | Yes | IATA 3-letter airport code. Exactly 3 characters, uppercase |
| `destination` | string | Yes | IATA 3-letter airport code. Exactly 3 characters, uppercase |
| `aircraftType` | string | Yes | 4-character aircraft type code, e.g. `A351` |
| `cabinCode` | string | Yes | `F`, `J`, `W`, or `Y` |
| `totalSeats` | integer | Yes | Physical seat count for this cabin. Must be > 0 |

#### Response — `201 Created`

```json
{
  "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "flightNumber": "AX001",
  "departureDate": "2026-04-01",
  "cabinCode": "J",
  "totalSeats": 30,
  "seatsAvailable": 30,
  "seatsHeld": 0,
  "seatsSold": 0,
  "status": "Active"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `inventoryId` | string (UUID) | Unique inventory record identifier; used in subsequent fare creation and inventory mutation calls |
| `flightNumber` | string | Echoed back |
| `departureDate` | string (date) | Echoed back |
| `cabinCode` | string | Echoed back |
| `totalSeats` | integer | Total seats for this cabin |
| `seatsAvailable` | integer | Initialised to `totalSeats` |
| `seatsHeld` | integer | Initialised to `0` |
| `seatsSold` | integer | Initialised to `0` |
| `status` | string | `Active` |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid IATA code format, invalid cabin code, or `totalSeats` ≤ 0 |
| `409 Conflict` | An inventory record already exists for this `flightNumber`, `departureDate`, and `cabinCode` combination |

---

### POST /v1/flights/{inventoryId}/fares

Add a fare definition to an existing flight inventory record. Called by the Operations API once per fare per cabin per operating date during schedule generation. Links the fare to the inventory record via `InventoryId`.

**When to use:** Called by the Operations API immediately after `POST /v1/flights` for each fare defined in the cabin's `CabinFares` configuration. A single `InventoryId` may have multiple fares (e.g. `JFLEXGB` and `JSAVERGB` for the same Business Class inventory record).

> **Pricing at creation time:** Fare pricing is fixed at schedule creation time and written directly to `offer.Fare`. There is no dynamic fare calculation — the prices supplied by the Operations API are the prices stored and served.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `inventoryId` | string (UUID) | The inventory record identifier returned from `POST /v1/flights` |

#### Request

```json
{
  "fareBasisCode": "JFLEXGB",
  "fareFamily": "Business Flex",
  "bookingClass": "J",
  "currencyCode": "GBP",
  "baseFareAmount": 2500.00,
  "taxAmount": 450.00,
  "isRefundable": true,
  "isChangeable": true,
  "changeFeeAmount": 0.00,
  "cancellationFeeAmount": 0.00,
  "pointsPrice": 75000,
  "pointsTaxes": 450.00,
  "validFrom": "2026-01-01T00:00:00Z",
  "validTo": "2026-10-31T23:59:59Z"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `fareBasisCode` | string | Yes | Revenue management fare basis code, e.g. `JFLEXGB`. Max 20 characters |
| `fareFamily` | string | No | Commercial product name, e.g. `Business Flex`. Max 50 characters |
| `bookingClass` | string | No | Single character booking class. Defaults to the `CabinCode` of the parent inventory record if not supplied |
| `currencyCode` | string | Yes | ISO 4217 currency code, e.g. `GBP` |
| `baseFareAmount` | number | Yes | Base fare before taxes. Decimal, 2 places. Must be ≥ 0 |
| `taxAmount` | number | Yes | Total taxes and surcharges. Decimal, 2 places. Must be ≥ 0 |
| `isRefundable` | boolean | Yes | Whether the fare permits a refund on voluntary cancellation |
| `isChangeable` | boolean | Yes | Whether the fare permits a voluntary flight change |
| `changeFeeAmount` | number | Yes | Fee charged on voluntary flight change. `0.00` for fully flexible or non-changeable fares |
| `cancellationFeeAmount` | number | Yes | Fee deducted from refund on cancellation. `0.00` for fully refundable or non-refundable fares |
| `pointsPrice` | integer | No | Points required for an award booking on this fare. Omit or `null` for revenue-only fares |
| `pointsTaxes` | number | No | Cash taxes payable on award booking. Required if `pointsPrice` is supplied. Decimal, 2 places |
| `validFrom` | string (datetime) | Yes | ISO 8601 UTC fare sale window start |
| `validTo` | string (datetime) | Yes | ISO 8601 UTC fare sale window end |

#### Response — `201 Created`

```json
{
  "fareId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fareBasisCode": "JFLEXGB",
  "totalAmount": 2950.00
}
```

| Field | Type | Description |
|-------|------|-------------|
| `fareId` | string (UUID) | Unique fare record identifier |
| `inventoryId` | string (UUID) | Parent inventory record, echoed back |
| `fareBasisCode` | string | Fare basis code, echoed back |
| `totalAmount` | number | `baseFareAmount + taxAmount` as stored |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid amounts, or `validFrom` after `validTo` |
| `404 Not Found` | No inventory record found for the given `inventoryId` |
| `409 Conflict` | A fare with the same `fareBasisCode` already exists for this `inventoryId` |

---

### POST /v1/search

Search flight inventory for a single segment (origin, destination, date, cabin, pax count) and return priced, stored-offer-snapshotted offers. Called once per leg by the Retail API for both direct (`POST /v1/search/slice`) and connecting (`POST /v1/search/connecting`) searches.

**When to use:** Called by the Retail API to find available flights and create stored offer snapshots. For each matching `FlightInventory` record, the Offer MS finds all active, non-expired fares, creates a `StoredOffer` row per fare (locking the price at search time), and returns the offer details with `OfferId`. `OfferId` values are single-use with a 60-minute TTL.

**Offer creation logic:**
1. Query `offer.FlightInventory` for records matching `origin`, `destination`, `departureDate`, `cabinCode` WHERE `Status = 'Active'` AND `SeatsAvailable >= paxCount`.
2. For each matching inventory record, query `offer.Fare` WHERE `InventoryId = {inventoryId}` AND `ValidFrom <= now` AND `ValidTo >= now`.
3. For each fare, insert a `StoredOffer` row with `ExpiresAt = now + 60 minutes`, `IsConsumed = 0`, and all fare fields denormalised.
4. Return one offer object per `StoredOffer` created.

**For points-mode searches:** Only return fares where `PointsPrice IS NOT NULL`. Return `pointsPrice` and `pointsTaxes` as the primary pricing fields.

> **Connecting itinerary note:** The Offer MS has no concept of connecting itineraries. The Retail API calls this endpoint twice for a connecting search (once per leg), applying the 60-minute MCT filter itself. Each leg produces independent stored offers.

#### Request

```json
{
  "origin": "LHR",
  "destination": "JFK",
  "departureDate": "2026-08-15",
  "cabinCode": "J",
  "paxCount": 2,
  "bookingType": "Revenue"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `origin` | string | Yes | IATA 3-letter airport code for departure |
| `destination` | string | Yes | IATA 3-letter airport code for arrival |
| `departureDate` | string (date) | Yes | ISO 8601 departure date |
| `cabinCode` | string | Yes | `F`, `J`, `W`, or `Y` |
| `paxCount` | integer | Yes | Number of passengers. Must be ≥ 1. Only inventory with `SeatsAvailable >= paxCount` is returned |
| `bookingType` | string | No | `Revenue` (default) or `Reward`. If `Reward`, only fares with `PointsPrice IS NOT NULL` are returned |

#### Response — `200 OK`

```json
{
  "origin": "LHR",
  "destination": "JFK",
  "departureDate": "2026-08-15",
  "offers": [
    {
      "offerId": "9ab12345-6789-0abc-def0-123456789abc",
      "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "flightNumber": "AX001",
      "departureDate": "2026-08-15",
      "departureTime": "09:30",
      "arrivalTime": "13:45",
      "arrivalDayOffset": 0,
      "origin": "LHR",
      "destination": "JFK",
      "aircraftType": "A351",
      "cabinCode": "J",
      "fareBasisCode": "JFLEXGB",
      "fareFamily": "Business Flex",
      "bookingClass": "J",
      "currencyCode": "GBP",
      "baseFareAmount": 2500.00,
      "taxAmount": 450.00,
      "totalAmount": 2950.00,
      "isRefundable": true,
      "isChangeable": true,
      "changeFeeAmount": 0.00,
      "cancellationFeeAmount": 0.00,
      "pointsPrice": 75000,
      "pointsTaxes": 450.00,
      "bookingType": "Revenue",
      "seatsAvailable": 28,
      "expiresAt": "2026-03-21T15:30:00Z"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `offers` | array | One entry per stored offer created. May be empty if no matching inventory or fares found |
| `offers[].offerId` | string (UUID) | The `OfferId` to pass to the basket. Single-use, expires in 60 minutes |
| `offers[].inventoryId` | string (UUID) | Parent inventory record identifier |
| `offers[].flightNumber` | string | Flight number |
| `offers[].departureDate` | string (date) | ISO 8601 departure date |
| `offers[].departureTime` | string (time) | Local departure time at origin |
| `offers[].arrivalTime` | string (time) | Local arrival time at destination |
| `offers[].arrivalDayOffset` | integer | `0` = same day; `1` = next day at destination |
| `offers[].origin` | string | IATA origin code |
| `offers[].destination` | string | IATA destination code |
| `offers[].aircraftType` | string | Aircraft type code |
| `offers[].cabinCode` | string | Cabin class |
| `offers[].fareBasisCode` | string | Fare basis code |
| `offers[].fareFamily` | string | Commercial fare family name |
| `offers[].bookingClass` | string | Booking class character |
| `offers[].currencyCode` | string | ISO 4217 currency code |
| `offers[].baseFareAmount` | number | Base fare at search time |
| `offers[].taxAmount` | number | Taxes at search time |
| `offers[].totalAmount` | number | Total price at search time |
| `offers[].isRefundable` | boolean | Refundability condition at search time |
| `offers[].isChangeable` | boolean | Changeability condition at search time |
| `offers[].changeFeeAmount` | number | Change fee at search time |
| `offers[].cancellationFeeAmount` | number | Cancellation fee at search time |
| `offers[].pointsPrice` | integer | Points price. `null` for revenue-only fares |
| `offers[].pointsTaxes` | number | Cash taxes on award booking. `null` if `pointsPrice` is `null` |
| `offers[].bookingType` | string | `Revenue` or `Reward` |
| `offers[].seatsAvailable` | integer | Seats available at search time (informational — actual hold is done separately via `POST /v1/inventory/hold`) |
| `offers[].expiresAt` | string (datetime) | ISO 8601 UTC expiry of this offer. `now + 60 minutes` |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid IATA codes, invalid cabin code, or `paxCount` < 1 |

> An empty `offers` array is a valid `200 OK` response — it indicates no matching inventory or fares were found for the search criteria.

---

### GET /v1/offers/{offerId}

Retrieve a stored offer snapshot by ID. Validates `IsConsumed = 0` and `ExpiresAt > now` before returning. Used by the Retail API at basket creation to confirm the offer is still valid and to lock pricing.

**When to use:** Called by the Retail API when a customer adds an offer to a basket, immediately before calling `POST /v1/inventory/hold`. If the offer is expired or already consumed, the customer must be redirected to re-search.

> **Consumption:** This endpoint does **not** set `IsConsumed = 1`. Consumption happens atomically when the Order MS creates the confirmed order. This endpoint is read-only validation — retrieving the offer multiple times (e.g. basket updates) does not consume it.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `offerId` | string (UUID) | The stored offer identifier returned from `POST /v1/search` |

#### Response — `200 OK`

Returns the full stored offer snapshot in the same schema as individual items in the `POST /v1/search` response, plus:

```json
{
  "offerId": "9ab12345-6789-0abc-def0-123456789abc",
  "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "flightNumber": "AX001",
  "departureDate": "2026-08-15",
  "departureTime": "09:30",
  "arrivalTime": "13:45",
  "arrivalDayOffset": 0,
  "origin": "LHR",
  "destination": "JFK",
  "aircraftType": "A351",
  "cabinCode": "J",
  "fareBasisCode": "JFLEXGB",
  "fareFamily": "Business Flex",
  "bookingClass": "J",
  "currencyCode": "GBP",
  "baseFareAmount": 2500.00,
  "taxAmount": 450.00,
  "totalAmount": 2950.00,
  "isRefundable": true,
  "isChangeable": true,
  "changeFeeAmount": 0.00,
  "cancellationFeeAmount": 0.00,
  "pointsPrice": 75000,
  "pointsTaxes": 450.00,
  "bookingType": "Revenue",
  "isConsumed": false,
  "expiresAt": "2026-03-21T15:30:00Z"
}
```

| Additional Field | Type | Description |
|-------|------|-------------|
| `isConsumed` | boolean | Always `false` on a successful response (consumed offers return `410 Gone`) |

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No offer found for the given `offerId` |
| `410 Gone` | Offer exists but is expired (`ExpiresAt <= now`) or already consumed (`IsConsumed = 1`). Customer must re-search |

---

### GET /v1/flights/{flightId}/seat-availability

Retrieve current seat availability status for a flight. Returns one entry per selectable seat with availability status (`available`, `held`, or `sold`) based on `offer.FlightInventory` and seat reservations. Does **not** return pricing — pricing is owned by the Seat MS.

**When to use:** Called by the Retail API as part of the three-source seatmap assembly: (1) Seat MS layout, (2) Seat MS pricing/`SeatOfferId`, (3) this endpoint for availability. The Retail API merges all three before returning the seatmap to the channel.

**Availability derivation:** The Offer MS maps each selectable seat number (obtained from the seatmap held internally or passed in a future lookup mechanism) against the current reservation state for the flight. A seat is:
- `available` — no reservation against it
- `held` — reserved in an active basket not yet confirmed
- `sold` — sold in a confirmed order

> **Seat MS independence:** The Offer MS does not call the Seat MS. Seat availability is derived from reservation state stored within the Offer MS domain. Physical seat layout (which seats exist on the aircraft) is the Seat MS's responsibility. The Retail API merges both views.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `flightId` | string (UUID) | The `InventoryId` from `offer.FlightInventory` identifying the specific flight and cabin |

#### Response — `200 OK`

```json
{
  "flightId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "flightNumber": "AX001",
  "departureDate": "2026-08-15",
  "cabinCode": "J",
  "seatAvailability": [
    {
      "seatOfferId": "so-3fa85f64-1A-v1",
      "seatNumber": "1A",
      "status": "available"
    },
    {
      "seatOfferId": "so-3fa85f64-1D-v1",
      "seatNumber": "1D",
      "status": "held"
    },
    {
      "seatOfferId": "so-3fa85f64-1G-v1",
      "seatNumber": "1G",
      "status": "sold"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `flightId` | string (UUID) | Echoed back |
| `flightNumber` | string | Flight number for this inventory record |
| `departureDate` | string (date) | ISO 8601 departure date |
| `cabinCode` | string | Cabin class for this inventory record |
| `seatAvailability` | array | One entry per selectable seat in this cabin on this flight |
| `seatAvailability[].seatOfferId` | string | Deterministic `SeatOfferId` generated by the Offer MS using the same derivation as the Seat MS (`flightId + seatNumber`), enabling the Retail API to join availability to Seat MS offer data |
| `seatAvailability[].seatNumber` | string | Seat number, e.g. `1A` |
| `seatAvailability[].status` | string | `available`, `held`, or `sold` |

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No inventory record found for the given `flightId` |

---

### POST /v1/flights/{flightId}/seat-reservations

Reserve specific seats against a basket or check-in flow. Records the seat reservation in the Offer MS so that `GET /v1/flights/{flightId}/seat-availability` reflects the updated status.

**When to use:** Called by the Retail API when a passenger selects seats during the bookflow or OLCI. This is a soft reservation — it marks seats as `held` for availability display purposes. The formal inventory hold (`POST /v1/inventory/hold`) manages the aggregate `SeatsHeld` count at the cabin level.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `flightId` | string (UUID) | The `InventoryId` identifying the specific flight and cabin |

#### Request

```json
{
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "seatNumbers": ["1A", "1K"]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `basketId` | string (UUID) | Yes | The basket or check-in session identifier. Used for idempotency and to release reservations on basket expiry |
| `seatNumbers` | array | Yes | List of seat numbers to reserve, e.g. `["1A", "1K"]` |

#### Response — `200 OK`

```json
{
  "flightId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "reserved": ["1A", "1K"]
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or empty `seatNumbers` array |
| `404 Not Found` | No inventory record found for the given `flightId` |
| `409 Conflict` | One or more requested seats are already held or sold by another basket |

---

### PATCH /v1/flights/{flightId}/seat-availability

Update seat status on a flight. Used to mark seats as checked-in following OLCI completion, or to apply other administrative seat status changes.

**When to use:** Called by the Retail API after the passenger completes online check-in, to update the seat status for gate management and departure control purposes.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `flightId` | string (UUID) | The `InventoryId` identifying the specific flight and cabin |

#### Request

```json
{
  "updates": [
    {
      "seatNumber": "1A",
      "status": "checked-in"
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `updates` | array | Yes | List of seat status updates |
| `updates[].seatNumber` | string | Yes | Seat number to update |
| `updates[].status` | string | Yes | New status. Valid values: `checked-in` |

#### Response — `200 OK`

```json
{
  "updated": 1
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid status value |
| `404 Not Found` | No inventory record found for the given `flightId`, or seat number not found |

---

### POST /v1/inventory/hold

Hold seats against a new or replacement booking. Increments `SeatsHeld` and decrements `SeatsAvailable` for the specified inventory record and pax count. Called after validating the stored offer at basket creation.

**When to use:** Called by the Retail API after `GET /v1/offers/{offerId}` confirms the offer is valid, as part of basket creation. Also called by the Disruption API when rebooking passengers onto a replacement flight. If the hold call fails, previously held inventory on other segments must be released via `POST /v1/inventory/release`.

> **Idempotency:** Calls with the same `basketId` and `inventoryId` combination return success without double-incrementing `SeatsHeld`.

> **Inventory integrity check:** Before applying the hold, the Offer MS must verify `SeatsAvailable >= paxCount`. If insufficient seats are available, `422 Unprocessable Entity` is returned and the basket creation is aborted.

#### Request

```json
{
  "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cabinCode": "J",
  "paxCount": 2,
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `inventoryId` | string (UUID) | Yes | The inventory record to hold seats against |
| `cabinCode` | string | Yes | Cabin class — used to validate the hold is against the correct cabin |
| `paxCount` | integer | Yes | Number of seats to hold. Must be ≥ 1 |
| `basketId` | string (UUID) | Yes | The basket identifier. Used for idempotency and to associate the hold with a specific booking attempt |

#### Response — `200 OK`

```json
{
  "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "seatsHeld": 2,
  "seatsAvailable": 26,
  "seatsSold": 2
}
```

| Field | Type | Description |
|-------|------|-------------|
| `inventoryId` | string (UUID) | Echoed back |
| `seatsHeld` | integer | Updated `SeatsHeld` count after this hold |
| `seatsAvailable` | integer | Updated `SeatsAvailable` count after this hold |
| `seatsSold` | integer | Unchanged `SeatsSold` count |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, `paxCount` < 1, or `cabinCode` mismatch with the inventory record |
| `404 Not Found` | No inventory record found for the given `inventoryId` |
| `422 Unprocessable Entity` | Insufficient seats available — `SeatsAvailable < paxCount` |

---

### POST /v1/inventory/sell

Convert held seats to sold at order confirmation. Decrements `SeatsHeld` and increments `SeatsSold`. `SeatsAvailable` is unchanged — it was already decremented when the hold was placed.

**When to use:** Called by the Retail API after `POST /v1/tickets` (e-ticket issuance) succeeds and before `POST /v1/orders` (order creation) on the Order MS. If sell fails after up to 3 retries, the Retail API must void the payment authorisation and return an error — the order must not be confirmed.

> **`SeatsAvailable` is unchanged** — available capacity was decremented at hold time. Selling converts held capacity to sold capacity only.

#### Request

```json
{
  "inventoryIds": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "7cb87a21-1234-4abc-9def-1a2b3c4d5e6f"
  ],
  "paxCount": 2,
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `inventoryIds` | array | Yes | List of inventory record identifiers to convert from held to sold. All supplied IDs are processed atomically |
| `paxCount` | integer | Yes | Number of seats to convert. Must match the `paxCount` used in the original hold |
| `basketId` | string (UUID) | Yes | The basket identifier used for the original hold. Used for idempotency and validation |

> **Atomicity:** All `inventoryIds` in the array are updated in a single database transaction. If any update fails, all are rolled back.

#### Response — `200 OK`

```json
{
  "sold": [
    {
      "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "seatsSold": 4,
      "seatsHeld": 0,
      "seatsAvailable": 26
    }
  ]
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or `paxCount` < 1 |
| `404 Not Found` | One or more `inventoryIds` not found |
| `422 Unprocessable Entity` | `SeatsHeld < paxCount` for one or more inventory records — the hold does not cover the requested sell quantity |

---

### POST /v1/inventory/release

Release held or sold seats back to available inventory. Increments `SeatsAvailable` and decrements either `SeatsHeld` or `SeatsSold` depending on the release type. Used on voluntary cancellation, flight change rollback, and basket expiry.

**When to use:**
- **Basket expiry:** Background job releases held seats for expired baskets.
- **Basket creation failure:** If `POST /v1/inventory/hold` succeeds for one leg but fails for another in a connecting itinerary, the Retail API must release the successful hold.
- **Voluntary cancellation:** Retail API releases sold seats after voiding e-tickets and confirming the cancellation.
- **Flight change:** Retail API releases sold seats on the original flight after the replacement flight has been held.
- **IROPS rollback:** Disruption API releases held seats if rebooking fails.

#### Request

```json
{
  "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "paxCount": 2,
  "releaseType": "Held",
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `inventoryId` | string (UUID) | Yes | The inventory record to release seats from |
| `paxCount` | integer | Yes | Number of seats to release. Must be ≥ 1 |
| `releaseType` | string | Yes | `Held` — decrements `SeatsHeld`, increments `SeatsAvailable`. `Sold` — decrements `SeatsSold`, increments `SeatsAvailable` |
| `basketId` | string (UUID) | No | Optional basket or booking identifier for audit purposes |

#### Response — `200 OK`

```json
{
  "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "seatsAvailable": 28,
  "seatsHeld": 0,
  "seatsSold": 2
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid `releaseType`, or `paxCount` < 1 |
| `404 Not Found` | No inventory record found for the given `inventoryId` |
| `422 Unprocessable Entity` | `SeatsHeld < paxCount` (for `releaseType=Held`) or `SeatsSold < paxCount` (for `releaseType=Sold`) |

---

### PATCH /v1/inventory/cancel

Close a cancelled flight's inventory. Sets `SeatsAvailable = 0` and `Status = Cancelled` for all inventory records matching the specified flight and date. Used exclusively by the Disruption API on flight cancellation.

**When to use:** Called by the Disruption API as the **first synchronous action** after receiving a flight cancellation event from the Flight Operations System — before returning `202 Accepted` to the FOS. This prevents new bookings from being accepted on the cancelled flight while passenger rebooking is processed asynchronously.

> **All cabins:** This endpoint cancels **all** inventory records for the given flight and departure date across all cabin classes in a single call, not per cabin.

#### Request

```json
{
  "flightNumber": "AX205",
  "departureDate": "2026-03-22"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `flightNumber` | string | Yes | The flight number to cancel |
| `departureDate` | string (date) | Yes | ISO 8601 departure date of the flight to cancel |

#### Response — `200 OK`

```json
{
  "flightNumber": "AX205",
  "departureDate": "2026-03-22",
  "inventoriesCancelled": 3,
  "status": "Cancelled"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `flightNumber` | string | Echoed back |
| `departureDate` | string (date) | Echoed back |
| `inventoriesCancelled` | integer | Number of inventory records updated (one per cabin class on this flight) |
| `status` | string | Always `Cancelled` on success |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid date format |
| `404 Not Found` | No active inventory records found for the given flight number and departure date |
| `422 Unprocessable Entity` | All inventory records for this flight are already cancelled |

---

## Orchestration API Call Sequences

### Search and Basket Creation (Retail API)

**Direct flight search:**
1. **Retail API → Offer MS:** `POST /v1/search` — returns stored offers with `OfferId` values.
2. Channel presents offers; customer selects.
3. **Retail API → Offer MS:** `GET /v1/offers/{offerId}` — validates offer is unconsumed and unexpired.
4. **Retail API → Offer MS:** `POST /v1/inventory/hold` — holds seats against the basket.

**Connecting flight search:** Retail API calls `POST /v1/search` twice (once per leg), pairs results with 60-minute MCT filter, then calls `GET /v1/offers/{offerId}` and `POST /v1/inventory/hold` once per leg.

### Booking Confirmation (Retail API)

5. Payment authorised.
6. Tickets issued (`POST /v1/tickets` on Delivery MS).
7. **Retail API → Offer MS:** `POST /v1/inventory/sell` — converts held to sold for all inventory IDs in the basket.
8. Order confirmed (`POST /v1/orders` on Order MS).
9. Manifest written (`POST /v1/manifest` on Delivery MS).

### Seatmap Assembly (Retail API)

For every seatmap display to the channel:
1. **Retail API → Seat MS:** `GET /v1/seatmap/{aircraftType}` — layout.
2. **Retail API → Seat MS:** `GET /v1/seat-offers?flightId={flightId}` — pricing and `SeatOfferId`.
3. **Retail API → Offer MS:** `GET /v1/flights/{flightId}/seat-availability` — per-seat status.
4. Retail API merges and returns to channel.

When passenger selects seats:
5. **Retail API → Offer MS:** `POST /v1/flights/{flightId}/seat-reservations` — soft-reserve selected seats.

### Voluntary Cancellation (Retail API)

1. E-tickets voided (Delivery MS).
2. Manifest deleted (Delivery MS).
3. **Retail API → Offer MS:** `POST /v1/inventory/release` (`releaseType=Sold`) — return seats to available.

### Voluntary Flight Change (Retail API)

1. **Retail API → Offer MS:** `POST /v1/inventory/hold` — hold seats on replacement flight.
2. E-tickets voided and reissued (Delivery MS).
3. Old manifest deleted; new manifest written (Delivery MS).
4. **Retail API → Offer MS:** `POST /v1/inventory/release` (`releaseType=Sold`) — release seats on original flight.

### Flight Cancellation (Disruption API)

1. **Disruption API → Offer MS:** `PATCH /v1/inventory/cancel` — synchronous; takes all cabins off sale before `202 Accepted` is returned to FOS.
2. (Async) For each passenger being rebooked: **Disruption API → Offer MS:** `POST /v1/search` — find replacement flight. `POST /v1/inventory/hold` — hold seats on replacement.

### Schedule Generation (Operations API via Schedule MS)

For each operating date × cabin:
1. **Operations API → Offer MS:** `POST /v1/flights` — create inventory record.
2. For each fare in that cabin: **Operations API → Offer MS:** `POST /v1/flights/{inventoryId}/fares` — create fare record.

---

## Data Conventions

| Convention | Format | Example |
|------------|--------|---------|
| Timestamps | ISO 8601 UTC | `"2026-08-15T11:00:00Z"` |
| Dates | ISO 8601 | `"2026-08-15"` |
| Times | HH:mm (24-hour local) | `"09:30"` |
| Airport codes | IATA 3-letter, uppercase | `"LHR"`, `"JFK"` |
| Currency codes | ISO 4217 | `"GBP"` |
| Currency amounts | Decimal, 2 places | `2950.00` |
| JSON field names | camelCase | `flightNumber`, `departurDate` |
| UUIDs | RFC 4122 lowercase | `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"` |
| Cabin codes | Single character | `"F"`, `"J"`, `"W"`, `"Y"` |
| Aircraft type codes | 4-character IATA-style | `"A351"`, `"B789"`, `"A339"` |

---

## Invocation Examples

### Create flight inventory (Operations API → Offer MS)

```bash
curl -X POST https://{offer-ms-host}/v1/flights \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "flightNumber": "AX001",
    "departureDate": "2026-04-01",
    "departureTime": "09:30",
    "arrivalTime": "13:45",
    "arrivalDayOffset": 0,
    "origin": "LHR",
    "destination": "JFK",
    "aircraftType": "A351",
    "cabinCode": "J",
    "totalSeats": 30
  }'
```

### Add a fare to an inventory record (Operations API → Offer MS)

```bash
curl -X POST https://{offer-ms-host}/v1/flights/3fa85f64-5717-4562-b3fc-2c963f66afa6/fares \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "fareBasisCode": "JFLEXGB",
    "fareFamily": "Business Flex",
    "bookingClass": "J",
    "currencyCode": "GBP",
    "baseFareAmount": 2500.00,
    "taxAmount": 450.00,
    "isRefundable": true,
    "isChangeable": true,
    "changeFeeAmount": 0.00,
    "cancellationFeeAmount": 0.00,
    "pointsPrice": 75000,
    "pointsTaxes": 450.00,
    "validFrom": "2026-01-01T00:00:00Z",
    "validTo": "2026-10-31T23:59:59Z"
  }'
```

### Search for offers (Retail API → Offer MS)

```bash
curl -X POST https://{offer-ms-host}/v1/search \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "origin": "LHR",
    "destination": "JFK",
    "departureDate": "2026-08-15",
    "cabinCode": "J",
    "paxCount": 2,
    "bookingType": "Revenue"
  }'
```

### Retrieve and validate a stored offer (Retail API → Offer MS)

```bash
curl -X GET https://{offer-ms-host}/v1/offers/9ab12345-6789-0abc-def0-123456789abc \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

### Hold seats for a basket (Retail API → Offer MS)

```bash
curl -X POST https://{offer-ms-host}/v1/inventory/hold \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "cabinCode": "J",
    "paxCount": 2,
    "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  }'
```

### Sell seats at order confirmation (Retail API → Offer MS)

```bash
curl -X POST https://{offer-ms-host}/v1/inventory/sell \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "inventoryIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"],
    "paxCount": 2,
    "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
  }'
```

### Release seats on cancellation (Retail API → Offer MS)

```bash
curl -X POST https://{offer-ms-host}/v1/inventory/release \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "paxCount": 2,
    "releaseType": "Sold"
  }'
```

### Cancel a flight inventory (Disruption API → Offer MS)

```bash
curl -X PATCH https://{offer-ms-host}/v1/inventory/cancel \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 661f9500-f30c-52e5-b827-557766551111" \
  -d '{
    "flightNumber": "AX205",
    "departureDate": "2026-03-22"
  }'
```

### Retrieve seat availability (Retail API → Offer MS, step 3 of seatmap assembly)

```bash
curl -X GET https://{offer-ms-host}/v1/flights/3fa85f64-5717-4562-b3fc-2c963f66afa6/seat-availability \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

> **Note:** All calls to the Offer microservice are authenticated using the `x-functions-key` header. The Offer MS never receives or validates end-user JWTs. See [`api.md` — Microservice Authentication](../api.md#microservice-authentication--host-keys) for the full host key mechanism.

---

## Related Documentation

- [API Endpoint Reference](../api-reference.md) — Summary of all microservice endpoints
- [System Design](../design.md) — Full domain design including search and stored offer sequence diagrams, bookflow inventory management, and IROPS inventory cancellation flows
