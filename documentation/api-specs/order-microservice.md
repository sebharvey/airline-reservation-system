# Order Microservice — API Specification

> **Service owner:** Order domain
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Order microservice manages the complete booking lifecycle — from basket creation through confirmation, post-sale changes, and cancellation — built on the **IATA ONE Order** standard. It is the sole owner of order state; all changes — passenger updates, seat changes, flight changes, ancillary additions, cancellations, and IROPS rebooking — are orchestrated through the Retail API or Disruption API. No other microservice writes to the Order domain.

The Order MS owns two primary structures: the **Basket** (`order.Basket`) — transient pre-sale state accumulating flight offers, seat and bag selections, and passenger details; and the **Order** (`order.Order`) — the confirmed post-sale record following the IATA ONE Order model, identified by a six-character **booking reference** (the functional equivalent of a PNR in legacy systems).

The Order MS publishes three event types to the Azure Service Bus on state changes:
- `OrderConfirmed` — consumed by Accounting MS (revenue recording) and Customer MS (points accrual).
- `OrderChanged` — consumed by Accounting MS (revenue adjustment).
- `OrderCancelled` — consumed by Accounting MS (refund processing).

> **Important:** The Order microservice is an internal service. It is not called directly by channels (Web, App, NDC). All requests are routed through the **Retail API** or **Disruption API** orchestration layers. See the [Security](#security) section for authentication details.

---

## Security

### Authentication

The Order microservice is called exclusively by orchestration APIs. It does not validate JWTs; that responsibility belongs to the calling layer.

Calls from orchestration APIs to the Order microservice are authenticated using an **Azure Function Host Key**, passed in the `x-functions-key` HTTP header. See [Microservice Authentication — Host Keys](../api.md#microservice-authentication--host-keys) in `api.md` for the full mechanism, key storage, and retrieval details.

### Required Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes (for request bodies) | Must be `application/json` |
| `x-functions-key` | Yes | Azure Function Host Key authenticating the orchestration API as an authorised caller |
| `X-Correlation-ID` | Yes | UUID generated at the channel boundary; propagated on every downstream call for distributed tracing and log correlation |

### Data Protection

- PII (passenger names, passport numbers, dates of birth, contact details) must never appear in logs or telemetry. Log entries reference passengers by `BookingReference`, `OrderId`, or `PassengerId` only.
- The Order MS does not store email addresses or credentials — those are owned by the Identity MS.
- `OrderData` JSON includes APIS data and travel document details; these must never be logged.

---

## Business Rules

### Basket Lifecycle

- A basket is created at the start of checkout and accumulates flight offers, seat and bag offer IDs, SSR selections, and passenger details.
- Basket expiry is fixed at **60 minutes** from creation, matching the `StoredOffer` lifetime in the Offer MS. This ensures all offer IDs referenced by a basket remain valid throughout the basket lifetime.
- A basket is **hard-deleted** on successful order confirmation. The confirmed `OrderData` JSON is the authoritative post-sale record; the basket is no longer needed.
- If abandoned or expired, a background job releases held inventory via the Offer MS and marks the basket `Expired`. Expired and abandoned baskets are retained for 7 days for diagnostics before purge.
- The `TicketingTimeLimit` is set at basket creation and stored on the `order.Order` record itself. The Retail API must validate `now < TicketingTimeLimit` before attempting payment authorisation. If elapsed, the basket must be marked `Expired` and inventory released.

### Order Data Model — IATA ONE Order

The confirmed order is a single evolving `OrderData` JSON document. Scalar fields used for indexed lookups, lifecycle management, routing, or event publishing are stored as typed columns on `order.Order`. These scalar columns are **excluded from `OrderData`** to avoid duplication — they are the single source of truth for those values.

`OrderData` carries the relational detail: passengers, flight segments, order items (flight, seat, bag), e-tickets, seat assignments, payments, and audit history.

### Order Status Transitions

| Status | Description |
|--------|-------------|
| `Draft` | Default status at order row creation (rare — typically transition directly to `Confirmed`) |
| `OrderInit` | Contact Centre incremental build (future scope; scaffolded for schema compatibility — no endpoints implement this flow in the current release) |
| `Confirmed` | Active booking post-payment and ticketing |
| `Changed` | Booking modified — flight change or significant ancillary change applied |
| `Cancelled` | Booking cancelled voluntarily or via IROPS |

### OrderInit (Contact Centre) — Future Scope

The `OrderInit` status is included in the schema to support future Contact Centre incremental booking flows but is **not implemented in the current release**. No endpoints or orchestration flows for `OrderInit` are built now. The `OrderStatus` field must include `OrderInit` as a valid value to avoid a breaking schema change when implemented later.

### Booking Reference

The booking reference (`BookingReference`) is a 6-character alphanumeric identifier generated at order confirmation, equivalent to a PNR in legacy systems (e.g. `AB1234`). It is `NULL` until the order is confirmed. A filtered unique index on `(BookingReference)` WHERE `BookingReference IS NOT NULL` enforces uniqueness while permitting multiple unconfirmed rows with `NULL`.

### Optimistic Concurrency Control

`order.Basket` and `order.Order` records implement OCC using an integer `Version` column. Every mutation must supply the caller's known version. If the UPDATE affects 0 rows (version mismatch), the operation is rejected with `409 Conflict`. The caller must re-fetch and retry. See [Optimistic Concurrency Control](../api.md#optimistic-concurrency-control) in `api.md`.

### Event Publishing

| Event | Trigger | Consumers |
|-------|---------|-----------|
| `OrderConfirmed` | `POST /v1/orders` succeeds | Accounting MS (revenue recording); Customer MS (points accrual if `loyaltyNumber` present) |
| `OrderChanged` | `PATCH /v1/orders/{bookingRef}/seats`, `/bags`, `/ssrs`, `/change`, `/rebook` | Accounting MS (revenue adjustment) |
| `OrderCancelled` | `PATCH /v1/orders/{bookingRef}/cancel` | Accounting MS (refund processing) |

**Reward booking event fields:** For reward bookings (`bookingType=Reward`), the `OrderConfirmed` event includes `totalPointsAmount` and `redemptionReference` so Accounting can record points liability separately from cash revenue. `OrderCancelled` includes `bookingType=Reward`, `pointsReinstated`, and `redemptionReference` for points liability reversal. `OrderChanged` includes `pointsAdjustment` and updated `totalPointsAmount`.

### IROPS Fare Override

The Order MS must recognise a `reason=FlightCancellation` flag on `PATCH /v1/orders/{bookingRef}/rebook` requests and waive all fare change restrictions regardless of the original fare conditions. This override must be logged in the order's `changeHistory` for audit. This waiver applies only when `reason=FlightCancellation` — voluntary changes (`reason=VoluntaryChange`) remain subject to fare conditions.

---

## Data Schema

### `order.Basket`

Transient pre-sale state. Hard-deleted on successful order confirmation.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `BasketId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `ChannelCode` | VARCHAR(20) | No | | | `WEB` · `APP` · `NDC` · `KIOSK` · `CC` · `AIRPORT` |
| `CurrencyCode` | CHAR(3) | No | `'GBP'` | | ISO 4217 |
| `BasketStatus` | VARCHAR(20) | No | `'Active'` | | `Active` · `Expired` · `Abandoned` · `Confirmed` |
| `TotalFareAmount` | DECIMAL(10,2) | Yes | | | Sum of flight offer prices |
| `TotalSeatAmount` | DECIMAL(10,2) | Yes | `0.00` | | Sum of seat offer prices |
| `TotalBagAmount` | DECIMAL(10,2) | Yes | `0.00` | | Sum of bag offer prices |
| `TotalAmount` | DECIMAL(10,2) | Yes | | | `TotalFareAmount + TotalSeatAmount + TotalBagAmount` |
| `ExpiresAt` | DATETIME2 | No | | | `CreatedAt + 60 minutes` |
| `ConfirmedOrderId` | UNIQUEIDENTIFIER | Yes | | FK → `order.Order(OrderId)` | Set on confirmation; null until then |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on every update** |
| `Version` | INT | No | `1` | | OCC version counter; incremented on every write |
| `BasketData` | NVARCHAR(MAX) | No | | | JSON document — see BasketData JSON Structure |

> **Indexes:** `IX_Basket_Status_Expiry` on `(BasketStatus, ExpiresAt)` WHERE `BasketStatus = 'Active'` — used by background expiry job.
> **Constraints:** `CHK_BasketData` — `ISJSON(BasketData) = 1`.
> **Basket deletion:** Hard-deleted by the Order MS as part of the `POST /v1/orders` transaction. The Retail API does not issue a separate delete call.
> **Retention:** Expired and abandoned baskets retained for 7 days for diagnostics before automated purge.

#### BasketData JSON Structure

The JSON captures the full in-progress state — passengers, flight offer snapshots, seat and bag selections, SSR items, and payment intent. Scalar identifiers already present as typed columns (`BasketId`, `ChannelCode`, `CurrencyCode`, `TotalFareAmount`, `TotalSeatAmount`, `TotalBagAmount`, `TotalAmount`) are excluded from the JSON.

Key top-level fields: `ticketingTimeLimit`, `passengers` (array), `flightOffers` (array of offer snapshots with `offerId`, `inventoryId`, flight details, fare details, `passengerRefs`), `seatOffers` (array with `seatOfferId`, `basketItemRef`, `passengerRef`, seat details), `bagOffers` (array with `bagOfferId`, `basketItemRef`, `passengerRef`, bag details), `ssrSelections` (array with `ssrCode`, `passengerRef`, `segmentRef`), `paymentIntent` (method, card details, totals, status), `history` (append-only event log of basket mutations).

For reward bookings, add: `bookingType: "Reward"`, `loyaltyNumber`, `totalPointsAmount`, `totalTaxesAmount`.

---

### `order.Order`

Confirmed post-sale record. Created once the basket is confirmed — payment taken, inventory settled, e-tickets issued.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `OrderId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `BookingReference` | CHAR(6) | Yes | | UK (filtered) | Populated on confirmation, e.g. `AB1234`. Null before confirmation. Filtered unique index permits multiple NULL rows |
| `OrderStatus` | VARCHAR(20) | No | `'Draft'` | | `OrderInit` · `Draft` · `Confirmed` · `Changed` · `Cancelled` |
| `ChannelCode` | VARCHAR(20) | No | | | `WEB` · `APP` · `NDC` · `KIOSK` · `CC` · `AIRPORT` |
| `CurrencyCode` | CHAR(3) | No | `'GBP'` | | ISO 4217 |
| `TicketingTimeLimit` | DATETIME2 | Yes | | | Latest time at which payment must complete. Set at order creation |
| `TotalAmount` | DECIMAL(10,2) | Yes | | | Total order value including all items. Null until confirmed |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on every update** |
| `Version` | INT | No | `1` | | OCC version counter; incremented on every write |
| `OrderData` | NVARCHAR(MAX) | No | | | JSON document — see OrderData JSON Structure |

> **Indexes:** `IX_Order_BookingReference` (unique, filtered) on `(BookingReference)` WHERE `BookingReference IS NOT NULL`.
> **Constraints:** `CHK_OrderData` — `ISJSON(OrderData) = 1`.
> **Column exclusion:** Fields present as typed columns are **not** duplicated inside `OrderData`. The columns are the single source of truth for `OrderId`, `BookingReference`, `OrderStatus`, `ChannelCode`, `CurrencyCode`, `TotalAmount`, `CreatedAt`.

#### OrderData JSON Structure

Aligned to IATA ONE Order concepts. Contains: `dataLists` (passengers array, flightSegments array), `orderItems` (array of flight, seat, and bag order items with `offerId`, fare details, `eTickets`, `seatAssignments`, `paymentReference`), `payments` (array of payment records), `history` (append-only event log).

For reward bookings, add to `OrderData`: `bookingType: "Reward"`, `totalPointsAmount`, `pointsRedemption` object (`redemptionReference`, `loyaltyNumber`, `pointsRedeemed`, `status`). The standard `payments` array covers cash transactions (taxes and ancillaries only for reward bookings).

Each `flightSegment` includes: `segmentId`, `flightNumber`, `origin`, `destination`, `departureDateTime`, `arrivalDateTime`, `aircraftType`, `operatingCarrier`, `marketingCarrier`, `cabinCode`, `bookingClass`.

Each `orderItem` of type `Flight` includes: `orderItemId`, `type: "Flight"`, `segmentRef`, `passengerRefs`, `offerId`, `fareBasisCode`, `fareFamily`, `unitPrice`, `taxes`, `totalPrice`, `isRefundable`, `isChangeable`, `paymentReference`, `eTickets` (array of `{passengerId, eTicketNumber}`), `seatAssignments` (array of `{passengerId, seatNumber}`).

---

## Endpoints

---

### POST /v1/basket

Create a new basket. Expiry is fixed at 60 minutes from creation, matching the `StoredOffer` lifetime. Returns the `basketId` for all subsequent basket operations.

**When to use:** Called by the Retail API at the start of the booking flow, immediately after the customer selects their flight offer(s). The basket is created before passenger details are captured.

> **Reward bookings:** When `bookingType=Reward`, the Retail API has already verified the customer's points balance via the Customer MS before calling this endpoint. The basket records `loyaltyNumber` and `totalPointsAmount` for use during confirmation.

#### Request

```json
{
  "channelCode": "WEB",
  "currencyCode": "GBP",
  "bookingType": "Revenue"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `channelCode` | string | Yes | `WEB`, `APP`, `NDC`, `KIOSK`, `CC`, or `AIRPORT` |
| `currencyCode` | string | No | ISO 4217 currency code. Defaults to `GBP` |
| `bookingType` | string | No | `Revenue` (default) or `Reward` |
| `loyaltyNumber` | string | No | Required when `bookingType=Reward`. The customer's loyalty programme number |
| `totalPointsAmount` | integer | No | Required when `bookingType=Reward`. Total points to be redeemed for the fare |

#### Response — `201 Created`

```json
{
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "basketStatus": "Active",
  "expiresAt": "2026-03-21T15:30:00Z",
  "totalAmount": 0.00,
  "currencyCode": "GBP"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `basketId` | string (UUID) | Unique basket identifier; used in all subsequent basket calls |
| `basketStatus` | string | Always `Active` on creation |
| `expiresAt` | string (datetime) | ISO 8601 UTC expiry — `CreatedAt + 60 minutes` |
| `totalAmount` | number | `0.00` on creation; updated as items are added |
| `currencyCode` | string | ISO 4217 currency code |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Invalid `channelCode`, invalid `currencyCode`, or `bookingType=Reward` without `loyaltyNumber` or `totalPointsAmount` |

---

### POST /v1/basket/{basketId}/offers

Add a validated stored offer to a basket. Called by the Retail API after fetching and validating the offer from the Offer MS. Validates `IsConsumed = 0` and `ExpiresAt > now` before adding to the basket. Updates `TotalFareAmount` and `TotalAmount`.

**When to use:** Called once per flight offer (once per leg for connecting itineraries) after the Retail API has successfully called `GET /v1/offers/{offerId}` on the Offer MS and `POST /v1/inventory/hold` to hold seats. The full offer snapshot is passed so the basket is self-contained.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `basketId` | string (UUID) | The basket identifier |

#### Request

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
  "bookingClass": "J",
  "fareBasisCode": "JFLEXGB",
  "fareFamily": "Business Flex",
  "passengerRefs": ["PAX-1", "PAX-2"],
  "unitPrice": 2500.00,
  "taxes": 450.00,
  "totalPrice": 2950.00,
  "isRefundable": true,
  "isChangeable": true,
  "changeFeeAmount": 0.00,
  "cancellationFeeAmount": 0.00,
  "pointsPrice": 75000,
  "pointsTaxes": 450.00,
  "offerExpiresAt": "2026-03-21T15:30:00Z"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `offerId` | string (UUID) | Yes | The `OfferId` from `offer.StoredOffer` |
| `inventoryId` | string (UUID) | Yes | The `InventoryId` from `offer.FlightInventory` |
| `flightNumber` | string | Yes | e.g. `AX001` |
| `departureDate` | string (date) | Yes | ISO 8601 date |
| `departureTime` | string (time) | Yes | Local departure time, `HH:mm` |
| `arrivalTime` | string (time) | Yes | Local arrival time, `HH:mm` |
| `arrivalDayOffset` | integer | No | `0` = same day (default); `1` = next day at destination |
| `origin` | string | Yes | IATA 3-letter airport code |
| `destination` | string | Yes | IATA 3-letter airport code |
| `aircraftType` | string | Yes | 4-character aircraft type code |
| `cabinCode` | string | Yes | `F`, `J`, `W`, or `Y` |
| `bookingClass` | string | Yes | Single character booking class |
| `fareBasisCode` | string | Yes | e.g. `JFLEXGB` |
| `fareFamily` | string | No | e.g. `Business Flex` |
| `passengerRefs` | array | Yes | List of PAX references this offer covers, e.g. `["PAX-1", "PAX-2"]` |
| `unitPrice` | number | Yes | Per-passenger base fare. Decimal, 2 places |
| `taxes` | number | Yes | Per-passenger taxes. Decimal, 2 places |
| `totalPrice` | number | Yes | Total price for all passengers on this segment. Decimal, 2 places |
| `isRefundable` | boolean | Yes | From the stored offer snapshot |
| `isChangeable` | boolean | Yes | From the stored offer snapshot |
| `changeFeeAmount` | number | Yes | From the stored offer snapshot |
| `cancellationFeeAmount` | number | Yes | From the stored offer snapshot |
| `pointsPrice` | integer | No | Points price snapshot. `null` for revenue-only offers |
| `pointsTaxes` | number | No | Points taxes snapshot. `null` if `pointsPrice` is `null` |
| `offerExpiresAt` | string (datetime) | Yes | `ExpiresAt` from the stored offer. The Order MS validates `offerExpiresAt > now` |

#### Response — `200 OK`

```json
{
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "basketItemId": "BI-1",
  "totalFareAmount": 5900.00,
  "totalAmount": 5900.00
}
```

| Field | Type | Description |
|-------|------|-------------|
| `basketId` | string (UUID) | Echoed back |
| `basketItemId` | string | Basket-scoped identifier for this offer item, e.g. `BI-1` |
| `totalFareAmount` | number | Updated basket fare total |
| `totalAmount` | number | Updated basket grand total |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid format |
| `404 Not Found` | Basket not found for the given `basketId` |
| `410 Gone` | Offer has expired (`offerExpiresAt <= now`) or is already consumed |
| `422 Unprocessable Entity` | Basket is not in `Active` status |

---

### PUT /v1/basket/{basketId}/passengers

Add or update passenger details on a basket. Replaces the full passengers array in `BasketData`. Called after basket creation when the customer enters passenger details.

**When to use:** Called by the Retail API when the customer submits passenger information. For reward bookings, the lead passenger details are pre-populated from the loyalty profile — the channel submits the pre-filled form data via this endpoint.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `basketId` | string (UUID) | The basket identifier |

#### Request

```json
{
  "passengers": [
    {
      "passengerId": "PAX-1",
      "type": "ADT",
      "givenName": "Alex",
      "surname": "Taylor",
      "dateOfBirth": "1985-03-12",
      "gender": "Male",
      "loyaltyNumber": "AX9876543",
      "contacts": {
        "email": "alex.taylor@example.com",
        "phone": "+447700900100"
      },
      "travelDocument": {
        "type": "PASSPORT",
        "number": "PA1234567",
        "issuingCountry": "GBR",
        "expiryDate": "2030-01-01",
        "nationality": "GBR"
      }
    }
  ],
  "version": 1
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `passengers` | array | Yes | Full replacement passenger list |
| `passengers[].passengerId` | string | Yes | PAX reference, e.g. `PAX-1`. Must be stable across all basket updates |
| `passengers[].type` | string | Yes | IATA passenger type: `ADT`, `CHD`, `INF`, or `YTH` |
| `passengers[].givenName` | string | Yes | Given (first) name |
| `passengers[].surname` | string | Yes | Surname |
| `passengers[].dateOfBirth` | string (date) | No | ISO 8601 date |
| `passengers[].gender` | string | No | `Male` or `Female` |
| `passengers[].loyaltyNumber` | string | No | Loyalty number if the passenger is a member |
| `passengers[].contacts` | object | No | Contact information — `email` and/or `phone`. At least one contact required for the lead passenger |
| `passengers[].travelDocument` | object | No | Travel document details — `type`, `number`, `issuingCountry`, `expiryDate`, `nationality` |
| `version` | integer | Yes | Current `Version` for OCC |

#### Response — `200 OK`

```json
{
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "passengerCount": 1
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid passenger type, or no contact on lead passenger |
| `404 Not Found` | Basket not found |
| `409 Conflict` | Version mismatch — re-fetch and retry |
| `422 Unprocessable Entity` | Basket is not in `Active` status |

---

### PUT /v1/basket/{basketId}/seats

Add or update seat selections on a basket during the bookflow. Replaces the full seat selections in `BasketData`. Updates `TotalSeatAmount` and `TotalAmount`.

**When to use:** Called by the Retail API when the customer selects seats during the bookflow. The Retail API has already validated the `SeatOfferId` values via the Seat MS before calling this endpoint.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `basketId` | string (UUID) | The basket identifier |

#### Request

```json
{
  "seatOffers": [
    {
      "seatOfferId": "so-3fa85f64-1A-v1",
      "basketItemRef": "BI-1",
      "passengerRef": "PAX-1",
      "seatNumber": "1A",
      "seatPosition": "Window",
      "cabinCode": "J",
      "price": 0.00,
      "currencyCode": "GBP"
    }
  ],
  "version": 2
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `seatOffers` | array | Yes | Full replacement seat selection list |
| `seatOffers[].seatOfferId` | string | Yes | Deterministic `SeatOfferId` from the Seat MS |
| `seatOffers[].basketItemRef` | string | Yes | References the flight offer basket item this seat applies to, e.g. `BI-1` |
| `seatOffers[].passengerRef` | string | Yes | PAX reference |
| `seatOffers[].seatNumber` | string | Yes | e.g. `1A` |
| `seatOffers[].seatPosition` | string | Yes | `Window`, `Aisle`, or `Middle` |
| `seatOffers[].cabinCode` | string | Yes | Cabin class |
| `seatOffers[].price` | number | Yes | `0.00` for Business/First Class. Decimal, 2 places |
| `seatOffers[].currencyCode` | string | Yes | ISO 4217 |
| `version` | integer | Yes | Current `Version` for OCC |

#### Response — `200 OK`

```json
{
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "totalSeatAmount": 0.00,
  "totalAmount": 5900.00
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Basket not found |
| `409 Conflict` | Version mismatch |
| `422 Unprocessable Entity` | Basket is not in `Active` status |

---

### PUT /v1/basket/{basketId}/bags

Add or update bag selections on a basket during the bookflow. Replaces the full bag selections in `BasketData`. Updates `TotalBagAmount` and `TotalAmount`.

**When to use:** Called by the Retail API when the customer selects additional bags during the bookflow. The Retail API has already validated the `BagOfferId` values via the Bag MS before calling this endpoint.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `basketId` | string (UUID) | The basket identifier |

#### Request

```json
{
  "bagOffers": [
    {
      "bagOfferId": "bo-3fa85f64-Y-1-v1",
      "basketItemRef": "BI-1",
      "passengerRef": "PAX-1",
      "bagSequence": 1,
      "freeBagsIncluded": 1,
      "additionalBags": 1,
      "price": 60.00,
      "currencyCode": "GBP"
    }
  ],
  "version": 3
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bagOffers` | array | Yes | Full replacement bag selection list |
| `bagOffers[].bagOfferId` | string | Yes | Deterministic `BagOfferId` from the Bag MS |
| `bagOffers[].basketItemRef` | string | Yes | References the flight offer basket item this bag applies to |
| `bagOffers[].passengerRef` | string | Yes | PAX reference |
| `bagOffers[].bagSequence` | integer | Yes | Sequence number of this additional bag, e.g. `1` |
| `bagOffers[].freeBagsIncluded` | integer | Yes | Free bag allowance for this cabin (informational) |
| `bagOffers[].additionalBags` | integer | Yes | Number of additional bags purchased |
| `bagOffers[].price` | number | Yes | Price for this additional bag. Decimal, 2 places |
| `bagOffers[].currencyCode` | string | Yes | ISO 4217 |
| `version` | integer | Yes | Current `Version` for OCC |

#### Response — `200 OK`

```json
{
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "totalBagAmount": 60.00,
  "totalAmount": 5960.00
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Basket not found |
| `409 Conflict` | Version mismatch |
| `422 Unprocessable Entity` | Basket is not in `Active` status |

---

### PUT /v1/basket/{basketId}/ssrs

Add or update SSR (Special Service Request) selections on a basket during the bookflow. Replaces the full SSR selection list in `BasketData`. No charge — basket total is unchanged.

**When to use:** Called by the Retail API when the customer selects SSRs during the bookflow. SSRs carry no cost. The SSR catalogue is served by the Retail API from its own `retail.SsrCatalogue` table — the Order MS stores the selections only.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `basketId` | string (UUID) | The basket identifier |

#### Request

```json
{
  "ssrSelections": [
    {
      "ssrCode": "VGML",
      "passengerRef": "PAX-1",
      "segmentRef": "SEG-1"
    },
    {
      "ssrCode": "WCHR",
      "passengerRef": "PAX-2",
      "segmentRef": "SEG-1"
    }
  ],
  "version": 4
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ssrSelections` | array | Yes | Full replacement SSR selection list. Empty array removes all SSRs |
| `ssrSelections[].ssrCode` | string | Yes | IATA 4-character SSR code, e.g. `VGML`, `WCHR` |
| `ssrSelections[].passengerRef` | string | Yes | PAX reference |
| `ssrSelections[].segmentRef` | string | Yes | Segment reference the SSR applies to, e.g. `SEG-1`. SSRs are segment-specific |
| `version` | integer | Yes | Current `Version` for OCC |

#### Response — `200 OK`

```json
{
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "ssrCount": 2,
  "totalAmount": 5960.00
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Basket not found |
| `409 Conflict` | Version mismatch |
| `422 Unprocessable Entity` | Basket is not in `Active` status |

---

### POST /v1/orders

Confirm a basket and create a permanent order record. Sets `OrderStatus = Confirmed`, generates a 6-character `BookingReference`, and hard-deletes the basket row. Publishes `OrderConfirmed` event to the event bus.

**When to use:** Called by the Retail API after all of the following have succeeded: payment authorised, e-tickets issued (Delivery MS), inventory sold (Offer MS), and (for reward bookings) points settled (Customer MS). This is the final step of the confirmation sequence — do not call until all upstream steps are complete.

**Basket deletion:** The basket row is hard-deleted atomically as part of this transaction. If order creation fails, the basket is retained and the Retail API can retry.

**Booking reference generation:** The Order MS generates the 6-character `BookingReference` on confirmation. It must be globally unique across all confirmed orders.

#### Request

```json
{
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "eTickets": [
    { "passengerId": "PAX-1", "segmentId": "SEG-1", "eTicketNumber": "932-1234567890" },
    { "passengerId": "PAX-2", "segmentId": "SEG-1", "eTicketNumber": "932-1234567891" }
  ],
  "paymentReferences": [
    { "type": "Fare", "paymentReference": "AXPAY-0001", "amount": 5900.00 },
    { "type": "BagAncillary", "paymentReference": "AXPAY-0002", "amount": 60.00 }
  ],
  "redemptionReference": null,
  "bookingType": "Revenue"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `basketId` | string (UUID) | Yes | The basket to confirm and delete |
| `eTickets` | array | Yes | E-ticket numbers issued by the Delivery MS. One entry per passenger per segment |
| `eTickets[].passengerId` | string | Yes | PAX reference |
| `eTickets[].segmentId` | string | Yes | Segment reference |
| `eTickets[].eTicketNumber` | string | Yes | IATA-format e-ticket number, e.g. `932-1234567890` |
| `paymentReferences` | array | Yes | Payment references from the Payment MS. One per payment type |
| `paymentReferences[].type` | string | Yes | `Fare`, `SeatAncillary`, `BagAncillary`, `RewardTaxes` |
| `paymentReferences[].paymentReference` | string | Yes | e.g. `AXPAY-0001` |
| `paymentReferences[].amount` | number | Yes | Amount for this payment line. Decimal, 2 places |
| `redemptionReference` | string | No | Required when `bookingType=Reward`. The `RedemptionReference` from the Customer MS points settle call |
| `bookingType` | string | No | `Revenue` (default) or `Reward` |

#### Response — `201 Created`

```json
{
  "orderId": "c1d2e3f4-a5b6-7890-cdef-012345678901",
  "bookingReference": "AB1234",
  "orderStatus": "Confirmed",
  "totalAmount": 5960.00,
  "currencyCode": "GBP"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `orderId` | string (UUID) | Unique order record identifier |
| `bookingReference` | string | 6-character booking reference, e.g. `AB1234` |
| `orderStatus` | string | `Confirmed` |
| `totalAmount` | number | Total order value |
| `currencyCode` | string | ISO 4217 |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or `bookingType=Reward` without `redemptionReference` |
| `404 Not Found` | Basket not found for the given `basketId` |
| `422 Unprocessable Entity` | Basket is expired, already confirmed, or `TicketingTimeLimit` has elapsed |

---

### POST /v1/orders/retrieve

Retrieve a confirmed order by booking reference and passenger name. Authentication is via name matching — the surname of the passenger must match a passenger on the booking.

**When to use:** Called by the Retail API for manage-booking flows (flight change, cancellation, seat change, bag addition, SSR management) and for the check-in flow when the passenger retrieves their booking. Also called by the Disruption API to retrieve affected orders by flight.

#### Request

```json
{
  "bookingReference": "AB1234",
  "surname": "Taylor"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bookingReference` | string | Yes | The 6-character booking reference |
| `surname` | string | Yes | Surname of any passenger on the booking. Case-insensitive match |

#### Response — `200 OK`

Returns the full order record including `OrderData` JSON:

```json
{
  "orderId": "c1d2e3f4-a5b6-7890-cdef-012345678901",
  "bookingReference": "AB1234",
  "orderStatus": "Confirmed",
  "channelCode": "WEB",
  "currencyCode": "GBP",
  "totalAmount": 5960.00,
  "version": 1,
  "createdAt": "2026-03-21T10:32:00Z",
  "updatedAt": "2026-03-21T10:32:00Z",
  "orderData": {
    "dataLists": {
      "passengers": [],
      "flightSegments": []
    },
    "orderItems": [],
    "payments": [],
    "history": []
  }
}
```

| Field | Type | Description |
|-------|------|-------------|
| `orderId` | string (UUID) | Order record identifier |
| `bookingReference` | string | 6-character booking reference |
| `orderStatus` | string | Current status |
| `channelCode` | string | Channel through which the booking was made |
| `currencyCode` | string | ISO 4217 |
| `totalAmount` | number | Total order value |
| `version` | integer | Current OCC version |
| `createdAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated |
| `updatedAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated |
| `orderData` | object | Full `OrderData` JSON document |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | No confirmed order found for the given `bookingReference` and `surname` combination |

---

### GET /v1/orders

Query orders by flight number and departure date. Returns all confirmed orders on the specified flight. Used by the Disruption API to retrieve all affected passengers when processing a delay or cancellation.

**When to use:** Called by the Disruption API as the first step of disruption handling after receiving a flight event from the Flight Operations System.

#### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `flightNumber` | string | Yes | The flight number to query, e.g. `AX101` |
| `departureDate` | string (date) | Yes | ISO 8601 departure date of the flight |
| `status` | string | No | Filter by order status. Typically `Confirmed` for disruption processing |

#### Request

```
GET /v1/orders?flightNumber=AX101&departureDate=2026-03-22&status=Confirmed
```

#### Response — `200 OK`

```json
{
  "flightNumber": "AX101",
  "departureDate": "2026-03-22",
  "totalOrders": 142,
  "orders": [
    {
      "orderId": "c1d2e3f4-a5b6-7890-cdef-012345678901",
      "bookingReference": "AB1234",
      "orderStatus": "Confirmed",
      "totalAmount": 5960.00,
      "version": 1,
      "orderData": {}
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `flightNumber` | string | Echoed back |
| `departureDate` | string (date) | Echoed back |
| `totalOrders` | integer | Total number of matching orders |
| `orders` | array | Full order records including `orderData` for each matching order |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required query parameters |
| `404 Not Found` | No orders found for the given flight and date |

---

### PATCH /v1/orders/{bookingRef}/passengers

Correct or update passenger details on a confirmed order. Updates the `passengers` array in `OrderData`. Publishes `OrderChanged` event.

**When to use:** Called by the Retail API during the manage-booking passenger update flow. Note: a name change detected here should trigger e-ticket reissuance via the Delivery MS — the Retail API is responsible for detecting the name change and calling `POST /v1/tickets/reissue` after this call succeeds.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "passengers": [
    {
      "passengerId": "PAX-1",
      "givenName": "Alex",
      "surname": "Taylor-Smith",
      "travelDocument": {
        "type": "PASSPORT",
        "number": "PA9999999",
        "issuingCountry": "GBR",
        "expiryDate": "2031-06-30",
        "nationality": "GBR"
      }
    }
  ],
  "version": 2
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `passengers` | array | Yes | Updated passenger details. Only include passengers being changed — unmodified passengers are unchanged |
| `passengers[].passengerId` | string | Yes | Must match an existing PAX reference on the order |
| `version` | integer | Yes | Current `Version` for OCC |

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "orderStatus": "Confirmed",
  "version": 3
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or `passengerId` not found on the order |
| `404 Not Found` | No order found for the given `bookingRef` |
| `409 Conflict` | Version mismatch |
| `422 Unprocessable Entity` | Order is not in a mutable status (`Confirmed` or `Changed`) |

---

### PATCH /v1/orders/{bookingRef}/seats

Update seat assignments on a confirmed order. Updates `seatAssignments` within `orderItems` in `OrderData`. Publishes `OrderChanged` event.

**When to use:** Called by the Retail API after post-sale seat change payment is settled, and by the Retail API during OLCI for no-charge seat assignment. The Retail API calls `POST /v1/tickets/reissue` on the Delivery MS before this call to obtain the new e-ticket numbers, then passes them here.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "seatUpdates": [
    {
      "passengerId": "PAX-1",
      "segmentId": "SEG-1",
      "seatOfferId": "so-3fa85f64-3A-v1",
      "seatNumber": "3A",
      "eTicketNumber": "932-1234567900",
      "paymentReference": "AXPAY-0003"
    }
  ],
  "version": 3
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `seatUpdates` | array | Yes | Seat changes to apply |
| `seatUpdates[].passengerId` | string | Yes | PAX reference |
| `seatUpdates[].segmentId` | string | Yes | Segment reference |
| `seatUpdates[].seatOfferId` | string | Yes | New `SeatOfferId` |
| `seatUpdates[].seatNumber` | string | Yes | New seat number |
| `seatUpdates[].eTicketNumber` | string | Yes | New e-ticket number (post-reissuance) |
| `seatUpdates[].paymentReference` | string | No | Payment reference for the seat ancillary charge. `null` for OLCI (no-charge) and Business/First Class seats |
| `version` | integer | Yes | Current `Version` for OCC |

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "orderStatus": "Changed",
  "version": 4
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Order not found |
| `409 Conflict` | Version mismatch |
| `422 Unprocessable Entity` | Order is not in a mutable status |

---

### PATCH /v1/orders/{bookingRef}/segments

Update segment departure and arrival times on a confirmed order. Used by the Disruption API for flight delay propagation. Publishes `OrderChanged` event.

**When to use:** Called by the Disruption API for every affected order during flight delay processing, after receiving the updated schedule from the Flight Operations System.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "segmentUpdates": [
    {
      "segmentId": "SEG-1",
      "flightNumber": "AX101",
      "newDepartureTime": "2026-03-22T16:30:00Z",
      "newArrivalTime": "2026-03-22T19:45:00Z"
    }
  ],
  "eTicketUpdates": [],
  "version": 2
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `segmentUpdates` | array | Yes | Segments to update |
| `segmentUpdates[].segmentId` | string | Yes | Segment reference to update |
| `segmentUpdates[].flightNumber` | string | Yes | Flight number for validation |
| `segmentUpdates[].newDepartureTime` | string (datetime) | Yes | Updated departure time in ISO 8601 UTC |
| `segmentUpdates[].newArrivalTime` | string (datetime) | Yes | Updated arrival time in ISO 8601 UTC |
| `eTicketUpdates` | array | No | If e-tickets were reissued (material schedule change > 60 min threshold), pass updated e-ticket numbers here: `[{ "passengerId", "segmentId", "eTicketNumber" }]` |
| `version` | integer | Yes | Current `Version` for OCC |

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "orderStatus": "Confirmed",
  "version": 3
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Order not found |
| `409 Conflict` | Version mismatch |

---

### PATCH /v1/orders/{bookingRef}/change

Apply a confirmed flight change, recording the new segment, add-collect amount, and payment reference. Sets `OrderStatus = Changed`. Publishes `OrderChanged` event.

**When to use:** Called by the Retail API after all change confirmation steps succeed: new inventory held (Offer MS), old e-tickets voided (Delivery MS), old manifest deleted (Delivery MS), new e-tickets issued (Delivery MS), new manifest written (Delivery MS), add-collect payment settled (Payment MS), original inventory released (Offer MS).

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "cancelledSegmentId": "SEG-1",
  "newOfferId": "d7e8f9a0-1234-5678-bcde-f01234567890",
  "newInventoryId": "5f6a7b8c-9d0e-1234-abcd-ef0123456789",
  "newFlightNumber": "AX003",
  "newDepartureDate": "2026-08-22",
  "newDepartureTime": "10:30",
  "newArrivalTime": "13:45",
  "newArrivalDayOffset": 0,
  "newETickets": [
    { "passengerId": "PAX-1", "eTicketNumber": "932-1234567900" }
  ],
  "changeFeeAmount": 0.00,
  "addCollectAmount": 200.00,
  "paymentReference": "AXPAY-0004",
  "bookingType": "Revenue",
  "pointsDifference": null,
  "redemptionReference": null,
  "version": 3
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `cancelledSegmentId` | string | Yes | The segment being replaced |
| `newOfferId` | string (UUID) | Yes | `OfferId` of the replacement flight stored offer |
| `newInventoryId` | string (UUID) | Yes | `InventoryId` of the replacement flight |
| `newFlightNumber` | string | Yes | Replacement flight number |
| `newDepartureDate` | string (date) | Yes | ISO 8601 date |
| `newDepartureTime` | string (time) | Yes | Local time `HH:mm` |
| `newArrivalTime` | string (time) | Yes | Local time `HH:mm` |
| `newArrivalDayOffset` | integer | No | `0` = same day; `1` = next day |
| `newETickets` | array | Yes | New e-ticket numbers issued by Delivery MS |
| `changeFeeAmount` | number | Yes | Change fee charged. `0.00` if none |
| `addCollectAmount` | number | Yes | Fare difference (add-collect). `0.00` if none |
| `paymentReference` | string | No | Payment reference for the add-collect + change fee. `null` if `totalDue = 0` |
| `bookingType` | string | No | `Revenue` (default) or `Reward` |
| `pointsDifference` | integer | No | For reward bookings: positive = additional points redeemed; negative = points to be reinstated |
| `redemptionReference` | string | No | For reward bookings where additional points were authorised |
| `version` | integer | Yes | Current `Version` for OCC |

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "orderStatus": "Changed",
  "version": 4
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Order not found |
| `409 Conflict` | Version mismatch |
| `422 Unprocessable Entity` | Order is not in a mutable status, or `cancelledSegmentId` not found on order |

---

### PATCH /v1/orders/{bookingRef}/cancel

Mark an order as cancelled with reason and any cancellation fee. Sets `OrderStatus = Cancelled`. Publishes `OrderCancelled` event containing `refundableAmount` and `originalPaymentReference` for the Accounting MS to initiate refund processing.

**When to use:** Called by the Retail API after all cancellation steps succeed: e-tickets voided (Delivery MS), manifest deleted (Delivery MS), inventory released (Offer MS), and for reward bookings, points reinstated (Customer MS).

> **Refund responsibility:** Refund execution is external to the reservation system. The `OrderCancelled` event carries `refundableAmount` and `paymentReference`; the Accounting MS consumes this and issues the refund to the payment provider. The Payment MS `POST /v1/payment/{paymentReference}/refund` is **not** called by the Retail API during voluntary cancellation — it is used only for automated reversals during booking flow failures.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "reason": "VoluntaryCancellation",
  "cancellationFeeAmount": 0.00,
  "refundableAmount": 5960.00,
  "originalPaymentReference": "AXPAY-0001",
  "bookingType": "Revenue",
  "pointsReinstated": null,
  "redemptionReference": null,
  "version": 3
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `reason` | string | Yes | `VoluntaryCancellation` |
| `cancellationFeeAmount` | number | Yes | Fee deducted from refund. `0.00` for fully refundable fares |
| `refundableAmount` | number | Yes | Amount to be refunded: `totalPaid − cancellationFee`. `0.00` for non-refundable fares |
| `originalPaymentReference` | string | Yes | The fare payment reference from the Payment MS |
| `bookingType` | string | No | `Revenue` (default) or `Reward` |
| `pointsReinstated` | integer | No | For reward bookings: total points restored to customer balance |
| `redemptionReference` | string | No | For reward bookings: the redemption reference for accounting reconciliation |
| `version` | integer | Yes | Current `Version` for OCC |

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "orderStatus": "Cancelled",
  "version": 4
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Order not found |
| `409 Conflict` | Version mismatch |
| `422 Unprocessable Entity` | Order is already cancelled |

---

### PATCH /v1/orders/{bookingRef}/rebook

Rebook a passenger onto a replacement flight. Used exclusively by the Disruption API for IROPS flight cancellation rebooking. Sets `OrderStatus = Changed`. Publishes `OrderChanged` event with `changeType=IROPSRebook`.

**When to use:** Called by the Disruption API for each affected order during asynchronous IROPS cancellation processing, after new inventory has been held (Offer MS), old manifest deleted (Delivery MS), new e-tickets issued (Delivery MS), and new manifest written (Delivery MS).

**IROPS fare override:** When `reason=FlightCancellation`, all fare change restrictions are waived regardless of the original fare conditions. The Order MS logs this override in the order's `changeHistory` for audit. The Retail API must never set `reason=FlightCancellation` — this flag is exclusively for the Disruption API.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "cancelledSegmentId": "SEG-1",
  "replacementOfferIds": ["e8f9a0b1-2345-6789-cdef-012345678901"],
  "newInventoryId": "5f6a7b8c-9d0e-1234-abcd-ef0123456789",
  "newFlightNumber": "AX005",
  "newDepartureDate": "2026-03-22",
  "newDepartureTime": "13:00",
  "newArrivalTime": "16:15",
  "newArrivalDayOffset": 0,
  "newETickets": [
    { "passengerId": "PAX-1", "eTicketNumber": "932-1234568000" }
  ],
  "reason": "FlightCancellation",
  "bookingType": "Revenue",
  "pointsAdjustment": null,
  "version": 2
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `cancelledSegmentId` | string | Yes | The cancelled segment being replaced |
| `replacementOfferIds` | array | Yes | `OfferId`(s) of the replacement flight stored offers |
| `newInventoryId` | string (UUID) | Yes | `InventoryId` of the replacement flight |
| `newFlightNumber` | string | Yes | Replacement flight number |
| `newDepartureDate` | string (date) | Yes | ISO 8601 date |
| `newDepartureTime` | string (time) | Yes | Local time `HH:mm` |
| `newArrivalTime` | string (time) | Yes | Local time `HH:mm` |
| `newArrivalDayOffset` | integer | No | `0` = same day; `1` = next day |
| `newETickets` | array | Yes | New e-ticket numbers from the Delivery MS |
| `reason` | string | Yes | Must be `FlightCancellation` for IROPS use. Triggers the fare override waiver |
| `bookingType` | string | No | `Revenue` (default) or `Reward` |
| `pointsAdjustment` | integer | No | For reward bookings: negative = surplus points reinstated (airline absorbs additional cost). `null` if no adjustment |
| `version` | integer | Yes | Current `Version` for OCC |

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "orderStatus": "Changed",
  "version": 3
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid `reason` value |
| `404 Not Found` | Order not found |
| `409 Conflict` | Version mismatch |
| `422 Unprocessable Entity` | Order is already cancelled, or `cancelledSegmentId` not found |

---

### PATCH /v1/orders/{bookingRef}/bags

Add or update bag order items on a confirmed order. Updates `orderItems` in `OrderData`. Publishes `OrderChanged` event.

**When to use:** Called by the Retail API after successful payment settlement for post-sale bag purchases. Also called during OLCI if the passenger adds bags with payment.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "bagItems": [
    {
      "bagOfferId": "bo-3fa85f64-Y-1-v1",
      "passengerId": "PAX-1",
      "segmentRef": "SEG-1",
      "bagSequence": 1,
      "price": 60.00,
      "currencyCode": "GBP",
      "paymentReference": "AXPAY-0005"
    }
  ],
  "version": 3
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bagItems` | array | Yes | Bag order items to add |
| `bagItems[].bagOfferId` | string | Yes | `BagOfferId` from the Bag MS |
| `bagItems[].passengerId` | string | Yes | PAX reference |
| `bagItems[].segmentRef` | string | Yes | Segment reference |
| `bagItems[].bagSequence` | integer | Yes | Additional bag sequence number |
| `bagItems[].price` | number | Yes | Price paid. Decimal, 2 places |
| `bagItems[].currencyCode` | string | Yes | ISO 4217 |
| `bagItems[].paymentReference` | string | Yes | Payment reference from Payment MS |
| `version` | integer | Yes | Current `Version` for OCC |

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "orderStatus": "Changed",
  "version": 4
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Order not found |
| `409 Conflict` | Version mismatch |
| `422 Unprocessable Entity` | Order is not in a mutable status |

---

### PATCH /v1/orders/{bookingRef}/ssrs

Add, update, or remove SSR items on a confirmed order. Updates the SSR items in `OrderData`. Publishes `OrderChanged` event.

**When to use:** Called by the Retail API during self-serve SSR management via the manage-booking flow. The Retail API must validate that the request is within the amendment cut-off window (typically 24 hours before departure) before calling this endpoint — it returns `422` if within the cut-off. The Retail API then calls `PATCH /v1/manifest/{bookingRef}` on the Delivery MS to propagate SSR changes to the flight manifest.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "ssrUpdates": [
    {
      "action": "add",
      "ssrCode": "VGML",
      "passengerRef": "PAX-1",
      "segmentRef": "SEG-1"
    },
    {
      "action": "remove",
      "ssrCode": "WCHR",
      "passengerRef": "PAX-2",
      "segmentRef": "SEG-1"
    }
  ],
  "version": 4
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ssrUpdates` | array | Yes | SSR changes to apply |
| `ssrUpdates[].action` | string | Yes | `add` or `remove` |
| `ssrUpdates[].ssrCode` | string | Yes | IATA 4-character SSR code |
| `ssrUpdates[].passengerRef` | string | Yes | PAX reference |
| `ssrUpdates[].segmentRef` | string | Yes | Segment reference |
| `version` | integer | Yes | Current `Version` for OCC |

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "orderStatus": "Confirmed",
  "version": 5
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Order not found |
| `409 Conflict` | Version mismatch |
| `422 Unprocessable Entity` | Order is not in a mutable status, or SSR not found on order when `action=remove` |

---

### POST /v1/orders/{bookingRef}/checkin

Record check-in status and APIS (Advance Passenger Information) data for passengers. Updates passenger APIS fields in `OrderData`. Does **not** publish an event — check-in status is an operational state tracked in the Delivery MS manifest, not an Order domain event.

**When to use:** Called by the Retail API after the passenger confirms their travel document details during OLCI. The Retail API calls `PATCH /v1/manifest/{bookingRef}` on the Delivery MS and `POST /v1/boarding-cards` in the same OLCI flow.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "checkins": [
    {
      "passengerId": "PAX-1",
      "travelDocument": {
        "type": "PASSPORT",
        "number": "PA1234567",
        "issuingCountry": "GBR",
        "expiryDate": "2030-01-01",
        "nationality": "GBR",
        "dateOfBirth": "1985-03-12",
        "gender": "Male",
        "residenceCountry": "GBR"
      },
      "segmentIds": ["SEG-1"]
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `checkins` | array | Yes | APIS data per passenger |
| `checkins[].passengerId` | string | Yes | PAX reference |
| `checkins[].travelDocument` | object | Yes | Full APIS document details — `type`, `number`, `issuingCountry`, `expiryDate`, `nationality`, `dateOfBirth`, `gender`, `residenceCountry` |
| `checkins[].segmentIds` | array | Yes | Segment references for which APIS is being submitted |

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "checkedInPassengers": 1
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or incomplete APIS data |
| `404 Not Found` | Order not found |
| `422 Unprocessable Entity` | Order is not in a mutable status, or `passengerId` not found on order |

---

## Orchestration API Call Sequences

### Booking Confirmation (Retail API)

1. `POST /v1/basket` — create basket.
2. `POST /v1/basket/{basketId}/offers` — add each flight offer (once per leg).
3. `PUT /v1/basket/{basketId}/passengers` — add passenger details.
4. (Optional) `PUT /v1/basket/{basketId}/seats` — add seat selections.
5. (Optional) `PUT /v1/basket/{basketId}/bags` — add bag selections.
6. (Optional) `PUT /v1/basket/{basketId}/ssrs` — add SSR selections.
7. Payment authorised (Payment MS).
8. E-tickets issued (Delivery MS).
9. Inventory sold (Offer MS — `POST /v1/inventory/sell`).
10. (Reward) Points settled (Customer MS).
11. **`POST /v1/orders`** — confirm basket, create order, delete basket, publish `OrderConfirmed`.
12. Manifest written (Delivery MS).
13. Ancillary payments settled (Payment MS).

### Voluntary Flight Change (Retail API)

1. `POST /v1/orders/retrieve` — retrieve current order state.
2. New inventory held (Offer MS).
3. Old e-tickets voided; new e-tickets issued (Delivery MS).
4. Old manifest deleted; new manifest written (Delivery MS).
5. Add-collect payment settled (Payment MS, if applicable).
6. **`PATCH /v1/orders/{bookingRef}/change`** — apply change, publish `OrderChanged`.
7. Original inventory released (Offer MS).

### Voluntary Cancellation (Retail API)

1. `POST /v1/orders/retrieve` — retrieve current order.
2. E-tickets voided (Delivery MS).
3. Manifest deleted (Delivery MS).
4. Inventory released (Offer MS).
5. (Reward) Points reinstated (Customer MS).
6. **`PATCH /v1/orders/{bookingRef}/cancel`** — mark cancelled, publish `OrderCancelled`.

### Post-Sale Seat Change (Retail API)

1. Seat payment settled (Payment MS).
2. E-tickets reissued (Delivery MS).
3. Manifest updated (Delivery MS — `PUT /v1/manifest`).
4. **`PATCH /v1/orders/{bookingRef}/seats`** — update seat assignments, publish `OrderChanged`.
5. Ancillary document issued (Delivery MS — `POST /v1/documents`).

### Post-Sale Bag Addition (Retail API)

1. Bag payment settled (Payment MS).
2. **`PATCH /v1/orders/{bookingRef}/bags`** — add bag items, publish `OrderChanged`.
3. Ancillary document issued (Delivery MS — `POST /v1/documents`).

### Self-Serve SSR Change (Retail API)

1. Retail API validates amendment cut-off window.
2. **`PATCH /v1/orders/{bookingRef}/ssrs`** — update SSR items, publish `OrderChanged`.
3. Manifest updated (Delivery MS — `PATCH /v1/manifest/{bookingRef}`).

### Online Check-In (Retail API)

1. `POST /v1/orders/retrieve` — retrieve order.
2. (Optional) Seat reserved (Offer MS), seat updated (Offer MS/Delivery MS).
3. (Optional) Bag payment settled; bags added via `PATCH /v1/orders/{bookingRef}/bags`.
4. **`POST /v1/orders/{bookingRef}/checkin`** — record APIS data.
5. Manifest check-in updated (Delivery MS — `PATCH /v1/manifest/{bookingRef}`).
6. Boarding cards generated (Delivery MS — `POST /v1/boarding-cards`).

### Flight Delay (Disruption API)

1. `GET /v1/orders?flightNumber&departureDate&status=Confirmed` — retrieve affected orders.
2. **`PATCH /v1/orders/{bookingRef}/segments`** — update departure/arrival times for each affected order.
3. (Conditional) E-tickets reissued if delay > 60 minutes (Delivery MS); new e-ticket numbers passed back via `PATCH /v1/orders/{bookingRef}/segments` with `eTicketUpdates`.

### IROPS Rebooking (Disruption API)

1. `GET /v1/orders?flightNumber&departureDate&status=Confirmed` — retrieve affected orders.
2. For each order: new inventory held (Offer MS); old manifest deleted; new e-tickets issued; new manifest written (Delivery MS).
3. **`PATCH /v1/orders/{bookingRef}/rebook`** — apply rebooking with `reason=FlightCancellation`, publish `OrderChanged`.

---

## Data Conventions

| Convention | Format | Example |
|------------|--------|---------|
| Timestamps | ISO 8601 UTC | `"2026-03-21T10:32:00Z"` |
| Dates | ISO 8601 | `"2026-08-15"` |
| Times | HH:mm (24-hour local) | `"09:30"` |
| Airport codes | IATA 3-letter | `"LHR"` |
| Currency codes | ISO 4217 | `"GBP"` |
| Currency amounts | Decimal, 2 places, stored as DECIMAL | `5960.00` |
| Monetary storage | DECIMAL — never floating-point | `DECIMAL(10,2)` |
| JSON field names | camelCase | `bookingReference`, `orderStatus` |
| UUIDs | RFC 4122 lowercase | `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"` |
| Booking references | 6-character alphanumeric | `"AB1234"` |
| E-ticket numbers | IATA format | `"932-1234567890"` |
| Passenger types | IATA codes | `"ADT"`, `"CHD"`, `"INF"`, `"YTH"` |

---

## Invocation Examples

### Create a basket (Retail API → Order MS)

```bash
curl -X POST https://{order-ms-host}/v1/basket \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{ "channelCode": "WEB", "currencyCode": "GBP", "bookingType": "Revenue" }'
```

### Confirm a basket (Retail API → Order MS)

```bash
curl -X POST https://{order-ms-host}/v1/orders \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "eTickets": [{ "passengerId": "PAX-1", "segmentId": "SEG-1", "eTicketNumber": "932-1234567890" }],
    "paymentReferences": [{ "type": "Fare", "paymentReference": "AXPAY-0001", "amount": 5960.00 }],
    "bookingType": "Revenue"
  }'
```

### Retrieve an order (Retail API → Order MS)

```bash
curl -X POST https://{order-ms-host}/v1/orders/retrieve \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{ "bookingReference": "AB1234", "surname": "Taylor" }'
```

### Query orders by flight (Disruption API → Order MS)

```bash
curl -X GET "https://{order-ms-host}/v1/orders?flightNumber=AX101&departureDate=2026-03-22&status=Confirmed" \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 661f9500-f30c-52e5-b827-557766551111"
```

### Cancel an order (Retail API → Order MS)

```bash
curl -X PATCH https://{order-ms-host}/v1/orders/AB1234/cancel \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "reason": "VoluntaryCancellation",
    "cancellationFeeAmount": 0.00,
    "refundableAmount": 5960.00,
    "originalPaymentReference": "AXPAY-0001",
    "bookingType": "Revenue",
    "version": 3
  }'
```

### IROPS rebook (Disruption API → Order MS)

```bash
curl -X PATCH https://{order-ms-host}/v1/orders/AB1234/rebook \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 661f9500-f30c-52e5-b827-557766551111" \
  -d '{
    "cancelledSegmentId": "SEG-1",
    "replacementOfferIds": ["e8f9a0b1-2345-6789-cdef-012345678901"],
    "newInventoryId": "5f6a7b8c-9d0e-1234-abcd-ef0123456789",
    "newFlightNumber": "AX005",
    "newDepartureDate": "2026-03-22",
    "newDepartureTime": "13:00",
    "newArrivalTime": "16:15",
    "newArrivalDayOffset": 0,
    "newETickets": [{ "passengerId": "PAX-1", "eTicketNumber": "932-1234568000" }],
    "reason": "FlightCancellation",
    "version": 2
  }'
```

> **Note:** All calls to the Order microservice are authenticated using the `x-functions-key` header. The Order MS never receives or validates end-user JWTs. See [`api.md` — Microservice Authentication](../api.md#microservice-authentication--host-keys) for the full host key mechanism.

---

## Related Documentation

- [API Endpoint Reference](../api-reference.md) — Summary of all microservice endpoints
- [System Design](../design.md) — Full domain design including bookflow, manage-booking, check-in, and IROPS sequence diagrams
