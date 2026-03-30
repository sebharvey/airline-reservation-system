# Retail API — API Specification

> **Service owner:** Retailing domain (orchestration layer)
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Retail API is the primary channel-facing orchestration layer for the Apex Air reservation system. It coordinates the complete passenger journey — from flight search and offer management through basket creation, payment, ticketing, order confirmation, manage-booking, ancillary selection, check-in, and boarding card generation.

The Retail API owns no database tables other than `retail.SsrCatalogue`. All reservation persistence is delegated to the microservices it orchestrates. Its role is to sequence multi-step flows, enforce business rules, execute compensation on failure, and assemble composite responses from multiple microservice data sources before returning to channels.

> **Important:** Channels (Web, App, NDC, Kiosk, Contact Centre, Airport) must never call microservices directly. All reservation system interactions route through this API or the Loyalty API.

---

## Security

### Authentication

Channels authenticate with the Retail API using OAuth 2.0 / OIDC:

1. The channel obtains a JWT access token from the Identity MS via the Loyalty API (`POST /v1/auth/login`).
2. The channel sends the access token as `Bearer {accessToken}` in the `Authorization` header on all authenticated requests.
3. The Retail API validates the JWT signature using the Identity MS public signing key (RS256 or ES256) — no database round-trip required per request.
4. On forwarding to downstream microservices, the Retail API uses the Azure Function Host Key in the `x-functions-key` header. End-user JWTs are **never** forwarded to microservices.

**Guest booking flows** (manage-booking retrieval, check-in retrieval) require `bookingReference` + `givenName` + `surname` validated server-side — no JWT is required for these endpoints.

### Required Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes (for request bodies) | Must be `application/json` |
| `Authorization` | Yes (on all authenticated endpoints) | `Bearer {accessToken}` — JWT with 15-minute TTL |
| `X-Correlation-ID` | Yes | UUID generated at the channel boundary; propagated on every downstream call. Must be included in all log entries |

### Data Protection

- PII (names, passport numbers, dates of birth, contact details) must never appear in logs or telemetry. Log entries use `BookingReference`, `BasketId`, `PassengerId`, and `OfferId`.
- Full card numbers (PAN) and CVV values are passed to the Payment MS only. They must never be logged by the Retail API or stored anywhere outside the Payment MS boundary.
- The Retail API is PCI DSS scoped only at the transport boundary — card data is passed through immediately to the Payment MS and not retained.

---

## Downstream Service Dependencies

| Service | Key Operations |
|---------|---------------|
| **Offer MS** | Search, stored offer retrieval, inventory hold/sell/release/cancel, seat availability, seat reservations |
| **Order MS** | Basket creation and management, order confirmation, post-sale mutations, check-in recording |
| **Payment MS** | Card payment authorisation, settlement, refund |
| **Delivery MS** | E-ticket issuance and reissuance, manifest write/update/delete/retrieve, ancillary documents, boarding cards |
| **Seat MS** | Seatmap layout, priced seat offers and `SeatOfferId` generation, seat offer validation |
| **Bag MS** | Bag policy and priced bag offers, bag offer validation |
| **Customer MS** | Points balance verification, points authorisation/settle/reverse/reinstate for reward bookings |

---

## Core Business Rules

### Price Integrity — Stored Offer Pattern

Prices are locked at search time, not at payment time. The Retail API passes `OfferId` values into baskets and retrieves stored offer snapshots from the Offer MS at basket creation — the fare charged at confirmation is exactly the fare presented at search. The Offer MS validates `IsConsumed = 0` and `ExpiresAt > now` before returning an offer. If either check fails, the customer must re-search.

### Basket Expiry and Ticketing Time Limit

Baskets expire after **60 minutes** from creation, matching the stored offer expiry window. The Retail API must validate that a basket has not expired before attempting payment authorisation. If `now >= ExpiresAt`, the basket must be rejected, held inventory released via `POST /v1/inventory/release` on the Offer MS, and the customer directed to re-search.

The `TicketingTimeLimit` is stored on the `order.Order` record and must also be evaluated before authorising payment at confirmation.

### Inventory Hold and Release

The Retail API is solely responsible for all inventory hold and release operations. Microservices never call each other.

- **Hold:** Called on the Offer MS after `GET /v1/offers/{offerId}` confirms the offer is valid, during basket creation.
- **Connecting itinerary holds:** Two `POST /v1/inventory/hold` calls — one per leg. If either fails, both must be released.
- **Release on failure:** Any failure during basket confirmation that follows a successful hold must trigger `POST /v1/inventory/release` for all held inventory before returning the error.
- **Sell:** Called on the Offer MS after `POST /v1/tickets` succeeds and before `POST /v1/orders` on the Order MS.

### Payment Authorise-and-Settle

Payment authorisation and settlement are separate steps. Multiple independent authorisations are made during basket confirmation — one per payment line (fare, seat ancillary, bag ancillary). Each has its own `PaymentId`.

- Fare and reward-tax authorisations are settled after `POST /v1/orders` succeeds.
- Ancillary authorisations (seat, bag) are settled after order confirmation — failure to settle an ancillary does not roll back the confirmed order but must be flagged for manual reconciliation.

### Reward Booking Flow

Reward bookings follow the same basket and order flow as revenue bookings, with three additional steps:

1. **Before basket creation:** `GET /v1/customers/{loyaltyNumber}` on the Customer MS to verify sufficient points balance.
2. **At confirmation, before payment:** `POST /v1/customers/{loyaltyNumber}/points/authorise` on the Customer MS to place a hold on the required points. Returns a `RedemptionReference`.
3. **After inventory sell and before order confirmation:** `POST /v1/customers/{loyaltyNumber}/points/settle` on the Customer MS to deduct points and append a `Redeem` ledger transaction.

**Reward failure hierarchy:**
- Points authorisation fails → abort immediately; no card payment attempted.
- Card payment (taxes) fails → reverse points hold via `POST /v1/customers/{loyaltyNumber}/points/reverse`; release inventory; return error.
- Ticketing fails → reverse points hold; void card authorisation; release inventory; return error.
- Points settlement fails after order confirmation → flag for manual reconciliation; order remains confirmed.

### Seatmap Assembly

The full seatmap response is assembled from three separate microservice calls — never from a single source:

1. **Seat MS:** `GET /v1/seatmap/{aircraftType}` — physical cabin layout, seat positions, attributes.
2. **Seat MS:** `GET /v1/seat-offers?flightId={flightId}` — `SeatOfferId`, price, `isChargeable` per selectable seat.
3. **Offer MS:** `GET /v1/flights/{flightId}/seat-availability` — per-seat status (available, held, sold).

The Retail API merges all three datasets before returning to the channel. Channels must never call these endpoints directly.

### Seat Validation Before Manifest Write

Before calling `POST /v1/manifest` or `PUT /v1/manifest` on the Delivery MS, the Retail API **must** validate all seat numbers against the active seatmap via `GET /v1/seatmap/{aircraftType}` on the Seat MS. Any seat number not present on the active seatmap must be rejected. The Delivery MS trusts seat numbers provided to it — validation is exclusively the Retail API's responsibility.

### Seat Selection — Charge Rules

| Cabin | Bookflow | Post-Sale | OLCI |
|-------|----------|-----------|------|
| First (F) | No charge | No charge | No charge |
| Business (J) | No charge | No charge | No charge |
| Premium Economy (W) | Charged | Charged | No charge |
| Economy (Y) | Charged | Charged | No charge |

At OLCI, seat selection is **always free** regardless of cabin. No payment is authorised. No `delivery.Document` is issued.

### Ancillary Document Creation

For every **paid** seat selection or bag purchase, the Retail API must call `POST /v1/documents` on the Delivery MS after payment settlement to issue a `delivery.Document` record (type `SeatAncillary` or `BagAncillary`). This triggers the `DocumentIssued` accounting event. Business and First Class seats carry no charge — no document is issued for those.

### SSR Amendment Cut-Off

SSR changes via `PATCH /v1/orders/{bookingRef}/ssrs` are rejected with `422 Unprocessable Entity` if the request falls within the amendment cut-off window — typically **24 hours before departure**. The Retail API evaluates this cut-off before forwarding to the Order MS.

SSR changes do **not** trigger e-ticket reissuance — SSR codes are not encoded in the BCBP barcode string.

### Voluntary Cancellation — Refund Boundary

Refund execution is external to the reservation system. The Retail API voids e-tickets, releases inventory, raises the `OrderCancelled` event (via the Order MS with `refundableAmount`), and returns. The Accounting system consumes this event and initiates the refund with the payment provider. `POST /v1/payment/{paymentId}/refund` on Payment MS is **not** called for voluntary cancellations — it exists only for automated reversals during booking flow failures (e.g. ticketing failure after payment authorisation).

**`refundableAmount` calculation (revenue):** `isRefundable ? (totalPaid − cancellationFeeAmount) : 0`.

**Reward cancellation:** `totalPointsAmount` is always fully reinstated via `POST /v1/customers/{loyaltyNumber}/points/reinstate` regardless of fare conditions. Tax refund: `isRefundable ? (totalTaxesPaid − cancellationFeeAmount) : 0`.

### Connecting Itineraries

For connecting itinerary searches, the Retail API calls `POST /v1/search` on the Offer MS **twice** — once for `origin → LHR` and once for `LHR → destination`. It pairs the results and applies a **60-minute minimum connect time (MCT)** filter at LHR before returning composite itinerary options to the channel. Each leg has its own `OfferId`; both are placed in the basket together. Holding, selling, and releasing inventory for a connecting itinerary requires two calls — one per leg. The Offer MS has no concept of multi-leg itineraries.

---

## Endpoints

---

### POST /v1/search/slice

Search for available direct flights for a single directional slice (outbound or inbound). Returns one offer per available fare per matching flight, each with a unique `OfferId`. Delegates to `POST /v1/search` on the Offer MS.

**When to use:** Called by the channel for each direction of a direct flight search. Outbound and inbound are searched independently. The Offer MS creates a stored offer snapshot per result, locking prices for 60 minutes.

#### Request

```json
{
  "origin": "LHR",
  "destination": "JFK",
  "departureDate": "2026-08-15",
  "paxCount": 2,
  "bookingType": "Revenue"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `origin` | string | Yes | IATA 3-letter airport code |
| `destination` | string | Yes | IATA 3-letter airport code |
| `departureDate` | string (date) | Yes | ISO 8601 departure date |
| `paxCount` | integer | Yes | Number of passengers. Must be ≥ 1 |
| `bookingType` | string | No | `Revenue` (default) or `Reward`. If `Reward`, only fares with a `pointsPrice` are returned |

#### Response — `200 OK`

```json
{
  "origin": "LHR",
  "destination": "JFK",
  "departureDate": "2026-08-15",
  "offers": [
    {
      "offerId": "9ab12345-6789-0abc-def0-123456789abc",
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
| `offers` | array | One entry per stored offer created. Empty array if no matching flights or fares |
| `offers[].offerId` | string (UUID) | The `OfferId` to pass to the basket. Single-use, 60-minute TTL |
| `offers[].flightNumber` | string | e.g. `AX001` |
| `offers[].departureTime` | string (time) | Local departure time at origin |
| `offers[].arrivalTime` | string (time) | Local arrival time at destination |
| `offers[].arrivalDayOffset` | integer | `0` = same day; `1` = next day at destination |
| `offers[].aircraftType` | string | 4-character aircraft type code |
| `offers[].fareBasisCode` | string | Revenue management fare basis code |
| `offers[].fareFamily` | string | Commercial fare family name |
| `offers[].totalAmount` | number | Total price locked at search time |
| `offers[].isRefundable` | boolean | Whether the fare permits refund on cancellation |
| `offers[].isChangeable` | boolean | Whether the fare permits voluntary change |
| `offers[].changeFeeAmount` | number | Fee charged on voluntary change. `0.00` for fully flexible or non-changeable fares |
| `offers[].cancellationFeeAmount` | number | Fee deducted from refund on cancellation. `0.00` for fully refundable or non-refundable fares |
| `offers[].pointsPrice` | integer | Points price for award bookings. `null` for revenue-only fares |
| `offers[].pointsTaxes` | number | Cash taxes payable on award booking. `null` if `pointsPrice` is `null` |
| `offers[].expiresAt` | string (datetime) | ISO 8601 UTC offer expiry — `now + 60 minutes` |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid IATA codes, or `paxCount` < 1 |

---

### POST /v1/search/connecting

Search for connecting itinerary options via the LHR hub. Assembles pairs of per-segment offers from the Offer MS, applies a 60-minute MCT at LHR, and returns combined itinerary options each carrying two `OfferId` values — one per leg.

**Orchestration:** Calls `POST /v1/search` on the Offer MS twice — `origin → LHR` and `LHR → destination`. Pairs results where the LHR → destination departure is ≥ 60 minutes after the origin → LHR arrival. Returns only valid pairs.

#### Request

```json
{
  "origin": "DEL",
  "destination": "JFK",
  "departureDate": "2026-08-15",
  "paxCount": 1,
  "bookingType": "Revenue"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `origin` | string | Yes | IATA 3-letter departure airport code. Must not be `LHR` |
| `destination` | string | Yes | IATA 3-letter destination airport code. Must not be `LHR` |
| `departureDate` | string (date) | Yes | ISO 8601 departure date for the first leg |
| `paxCount` | integer | Yes | Number of passengers. Must be ≥ 1 |
| `bookingType` | string | No | `Revenue` (default) or `Reward` |

#### Response — `200 OK`

```json
{
  "origin": "DEL",
  "destination": "JFK",
  "itineraries": [
    {
      "totalAmount": 1200.00,
      "currencyCode": "GBP",
      "totalPointsPrice": null,
      "legs": [
        {
          "offerId": "leg1-offer-uuid",
          "flightNumber": "AX412",
          "departureDate": "2026-08-15",
          "departureTime": "03:30",
          "arrivalTime": "08:00",
          "arrivalDayOffset": 0,
          "origin": "DEL",
          "destination": "LHR",
          "aircraftType": "B789",
          "cabinCode": "Y",
          "fareBasisCode": "YFLEX",
          "fareFamily": "Economy Flex",
          "totalAmount": 600.00,
          "isRefundable": true,
          "isChangeable": true,
          "expiresAt": "2026-03-21T15:30:00Z"
        },
        {
          "offerId": "leg2-offer-uuid",
          "flightNumber": "AX001",
          "departureDate": "2026-08-15",
          "departureTime": "09:30",
          "arrivalTime": "13:45",
          "arrivalDayOffset": 0,
          "origin": "LHR",
          "destination": "JFK",
          "aircraftType": "A351",
          "cabinCode": "Y",
          "fareBasisCode": "YFLEX",
          "fareFamily": "Economy Flex",
          "totalAmount": 600.00,
          "isRefundable": true,
          "isChangeable": true,
          "expiresAt": "2026-03-21T15:30:00Z"
        }
      ]
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `itineraries` | array | One entry per valid connecting pair satisfying the 60-minute MCT |
| `itineraries[].totalAmount` | number | Combined total price of both legs |
| `itineraries[].totalPointsPrice` | integer | Combined points price. `null` for revenue-only |
| `itineraries[].legs` | array | Always exactly 2 entries: leg 1 (origin → LHR) and leg 2 (LHR → destination) |
| `itineraries[].legs[].offerId` | string (UUID) | `OfferId` for this leg — pass both to `POST /v1/basket` |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, `origin` or `destination` is LHR, invalid codes, or `paxCount` < 1 |

---

### POST /v1/basket

Create a new basket with one or more flight offer IDs. Initiates the bookflow. For reward bookings, verifies the customer's points balance via Customer MS before creating the basket.

**Orchestration sequence:**
1. (Reward only) `GET /v1/customers/{loyaltyNumber}` on Customer MS — verify `pointsBalance >= totalPointsRequired`. Abort if insufficient.
2. For each `offerId`: `GET /v1/offers/{offerId}` on Offer MS — validate `IsConsumed = 0` and `ExpiresAt > now`.
3. For each `offerId`: `POST /v1/inventory/hold` on Offer MS — hold `paxCount` seats. If any hold fails, release all previously held inventory and return error.
4. `POST /v1/basket` on Order MS — create basket record. Returns `basketId`.
5. For each `offerId`: `POST /v1/basket/{basketId}/offers` on Order MS — add validated offer snapshot.

> **Connecting itinerary:** Pass both `OfferId` values in `offerIds`. Steps 2–3 run for each independently. If either hold fails, both must be released.

#### Request

```json
{
  "offerIds": ["9ab12345-6789-0abc-def0-123456789abc"],
  "channelCode": "WEB",
  "currencyCode": "GBP",
  "bookingType": "Revenue",
  "loyaltyNumber": null
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `offerIds` | array | Yes | One or two `OfferId` values. Two for a connecting itinerary, one for a direct or single-direction journey |
| `channelCode` | string | Yes | `WEB`, `APP`, `NDC`, `KIOSK`, `CC`, `AIRPORT` |
| `currencyCode` | string | No | ISO 4217. Defaults to `GBP` |
| `bookingType` | string | No | `Revenue` (default) or `Reward` |
| `loyaltyNumber` | string | Conditional | Required if `bookingType=Reward` |

#### Response — `201 Created`

```json
{
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "bookingType": "Revenue",
  "totalFareAmount": 2950.00,
  "totalSeatAmount": 0.00,
  "totalBagAmount": 0.00,
  "totalAmount": 2950.00,
  "totalPointsAmount": null,
  "currencyCode": "GBP",
  "expiresAt": "2026-03-21T15:30:00Z",
  "flights": [
    {
      "offerId": "9ab12345-6789-0abc-def0-123456789abc",
      "flightNumber": "AX001",
      "origin": "LHR",
      "destination": "JFK",
      "departureDateTime": "2026-08-15T09:30:00Z",
      "arrivalDateTime": "2026-08-15T13:45:00Z",
      "cabinCode": "J",
      "fareFamily": "Business Flex",
      "totalAmount": 2950.00
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `basketId` | string (UUID) | Basket identifier used in all subsequent basket operations |
| `totalFareAmount` | number | Sum of flight offer prices |
| `totalSeatAmount` | number | Sum of seat offer prices (initially `0.00`) |
| `totalBagAmount` | number | Sum of bag offer prices (initially `0.00`) |
| `totalAmount` | number | `totalFareAmount + totalSeatAmount + totalBagAmount` |
| `totalPointsAmount` | integer | Total points to redeem for reward bookings. `null` for revenue |
| `expiresAt` | string (datetime) | Basket expiry — `now + 60 minutes` |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or `loyaltyNumber` required but not supplied for reward booking |
| `404 Not Found` | One or more `offerIds` not found |
| `410 Gone` | One or more offers are expired or already consumed. Customer must re-search |
| `422 Unprocessable Entity` | Insufficient points balance (reward), or insufficient seats available on one or more flights |

---

### PUT /v1/basket/{basketId}/passengers

Add or update passenger details on a basket. Delegates to `PUT /v1/basket/{basketId}/passengers` on Order MS.

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
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `passengers[].passengerId` | string | Yes | Caller-assigned PAX reference, unique within this basket, e.g. `PAX-1` |
| `passengers[].type` | string | Yes | IATA passenger type: `ADT`, `CHD`, `INF`, `YTH` |
| `passengers[].givenName` | string | Yes | Max 100 characters |
| `passengers[].surname` | string | Yes | Max 100 characters |
| `passengers[].dateOfBirth` | string (date) | No | ISO 8601 date |
| `passengers[].gender` | string | No | `Male`, `Female`, `Unspecified` |
| `passengers[].loyaltyNumber` | string | No | Apex Air loyalty number if the passenger is a member |
| `passengers[].contacts` | object | No | At least one contact (email or phone) required on the lead passenger |
| `passengers[].travelDocument` | object | No | Passport or other travel document details |

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
| `400 Bad Request` | Missing required fields or invalid passenger data |
| `404 Not Found` | Basket not found |
| `410 Gone` | Basket has expired |

---

### PUT /v1/basket/{basketId}/seats

Add or update seat selections on a basket during the bookflow.

**Orchestration sequence:**
1. For each `seatOfferId`: `GET /v1/seat-offers/{seatOfferId}` on Seat MS — validate offer is still active and lock current price.
2. `POST /v1/flights/{flightId}/seat-reservations` on Offer MS — soft-reserve selected seats to update availability display.
3. `PUT /v1/basket/{basketId}/seats` on Order MS — write seat offer IDs, PAX assignments, and updated totals.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `basketId` | string (UUID) | The basket identifier |

#### Request

```json
{
  "seatSelections": [
    {
      "seatOfferId": "so-3fa85f64-1A-v1",
      "passengerRef": "PAX-1",
      "flightOfferId": "9ab12345-6789-0abc-def0-123456789abc"
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `seatSelections[].seatOfferId` | string | Yes | `SeatOfferId` from the Seat MS offer response |
| `seatSelections[].passengerRef` | string | Yes | PAX reference matching the basket passenger list |
| `seatSelections[].flightOfferId` | string (UUID) | Yes | `OfferId` of the flight segment this seat applies to |

#### Response — `200 OK`

```json
{
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "totalSeatAmount": 140.00,
  "totalAmount": 3090.00
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or mismatched passenger/flight references |
| `404 Not Found` | Basket not found or `seatOfferId` not found on Seat MS |
| `409 Conflict` | One or more selected seats are already held or sold |
| `410 Gone` | Basket has expired or the seat offer is no longer valid |

---

### PUT /v1/basket/{basketId}/bags

Add or update bag selections on a basket during the bookflow. Updates `TotalBagAmount` on the basket.

**Orchestration sequence:**
1. For each `bagOfferId`: `GET /v1/bags/offers/{bagOfferId}` on Bag MS — validate offer is active and lock price.
2. `PUT /v1/basket/{basketId}/bags` on Order MS — write bag offer IDs, PAX/segment assignments, and updated totals.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `basketId` | string (UUID) | The basket identifier |

#### Request

```json
{
  "bagSelections": [
    {
      "bagOfferId": "bo-3fa85f64-Y-1-v1",
      "passengerRef": "PAX-1",
      "flightOfferId": "9ab12345-6789-0abc-def0-123456789abc"
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bagSelections[].bagOfferId` | string | Yes | `BagOfferId` from the Bag MS offer response |
| `bagSelections[].passengerRef` | string | Yes | PAX reference |
| `bagSelections[].flightOfferId` | string (UUID) | Yes | `OfferId` of the flight segment this bag applies to |

#### Response — `200 OK`

```json
{
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "totalBagAmount": 60.00,
  "totalAmount": 3010.00
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Basket not found or `bagOfferId` not found/no longer active on Bag MS |
| `410 Gone` | Basket has expired |

---

### PUT /v1/basket/{basketId}/ssrs

Add or update Special Service Request selections on a basket. No charge — basket total is unchanged.

**SSR rules:** SSRs are segment-specific — a connecting passenger requires independent entries per leg. All SSR codes must exist and be active in `retail.SsrCatalogue`. The Retail API reads the catalogue directly — no downstream microservice call is required.

**Delegates to:** `PUT /v1/basket/{basketId}/ssrs` on Order MS.

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
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ssrSelections[].ssrCode` | string | Yes | IATA 4-character SSR code. Must be active in `retail.SsrCatalogue` |
| `ssrSelections[].passengerRef` | string | Yes | PAX reference |
| `ssrSelections[].segmentRef` | string | Yes | Segment reference identifying which flight leg the SSR applies to |

#### Response — `200 OK`

```json
{
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "ssrCount": 1
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or unknown/inactive SSR code |
| `404 Not Found` | Basket not found |
| `410 Gone` | Basket has expired |

---

### POST /v1/basket/{basketId}/confirm

Confirm a basket, triggering payment (fare + any seat/bag ancillaries as separate transactions), ticketing, inventory settlement, and order creation.

**Full orchestration sequence:**

1. **Validate basket** — verify `BasketStatus = Active` and `now < ExpiresAt / TicketingTimeLimit`. If expired, release inventory and return `410 Gone`.
2. **(Reward only) Authorise points** — `POST /v1/customers/{loyaltyNumber}/points/authorise` on Customer MS. Returns `RedemptionReference`. If insufficient balance, abort.
3. **Authorise fare/tax payment** — `POST /v1/payment/authorise` on Payment MS. Revenue: `amount=totalFareAmount`, `description=Fare`. Reward: `amount=totalTaxesAmount`, `description=RewardTaxes`.
4. **(If seats selected) Authorise seat payment** — `POST /v1/payment/authorise` on Payment MS. `amount=totalSeatAmount`, `description=SeatAncillary`. Business/First Class: skip — no charge.
5. **(If bags selected) Authorise bag payment** — `POST /v1/payment/authorise` on Payment MS. `amount=totalBagAmount`, `description=BagAncillary`.
6. **Issue e-tickets** — `POST /v1/tickets` on Delivery MS. If this fails: reverse points hold (reward), void all payment authorisations, release inventory, return error.
7. **Sell inventory** — `POST /v1/inventory/sell` on Offer MS for all inventory IDs in the basket. If this fails after 3 retries: void payments, return error. Do not confirm order.
8. **(Reward only) Settle points** — `POST /v1/customers/{loyaltyNumber}/points/settle` on Customer MS.
9. **Settle fare/tax payment** — `POST /v1/payment/{paymentId}/settle` on Payment MS.
10. **Confirm order** — `POST /v1/orders` on Order MS. Returns `bookingReference`. Basket is hard-deleted by Order MS.
11. **Validate seat numbers and write manifest** — `GET /v1/seatmap/{aircraftType}` on Seat MS to validate each seat, then `POST /v1/manifest` on Delivery MS.
12. **Settle ancillary payments** (if applicable) — `POST /v1/payment/{paymentId}/settle` per ancillary. Failure does not roll back the confirmed booking — flag for manual reconciliation.
13. **(If paid seats selected) Issue seat documents** — `POST /v1/documents` on Delivery MS per charged seat, `documentType=SeatAncillary`.
14. **(If bags selected) Issue bag documents** — `POST /v1/documents` on Delivery MS per bag, `documentType=BagAncillary`.

**Failure compensation table:**

| Failure point | Compensation |
|---------------|-------------|
| Points authorisation fails | Abort. No card payment attempted |
| Card payment authorisation fails | Reverse points hold (reward). Release inventory. Return error |
| Ticketing fails | Reverse points hold (reward). Void all card authorisations. Release inventory. Return error |
| Inventory sell fails (after 3 retries) | Void payments. Return error. Do not confirm order |
| Points settlement fails after order confirmed | Flag for manual reconciliation. Order confirmed |
| Ancillary payment settlement fails | Flag for manual reconciliation. Order confirmed |

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `basketId` | string (UUID) | The basket to confirm |

#### Request

```json
{
  "payment": {
    "method": "CreditCard",
    "cardNumber": "4111111111111234",
    "expiryDate": "12/28",
    "cvv": "737",
    "cardholderName": "Alex Taylor"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `payment` | object | Yes | Payment details. Card data is passed directly to Payment MS and never stored by the Retail API |
| `payment.method` | string | Yes | `CreditCard`, `DebitCard`, `PayPal`, `ApplePay` |
| `payment.cardNumber` | string | Conditional | Required for card methods. Never logged or stored |
| `payment.expiryDate` | string | Conditional | `MM/YY` format. Required for card methods |
| `payment.cvv` | string | Conditional | Required for card methods. Never logged or stored |
| `payment.cardholderName` | string | Conditional | Required for card methods |

#### Response — `201 Created`

```json
{
  "bookingReference": "AB1234",
  "orderStatus": "Confirmed",
  "bookingType": "Revenue",
  "totalAmount": 3010.00,
  "currencyCode": "GBP",
  "eTickets": [
    {
      "eTicketNumber": "932-1234567890",
      "passengerId": "PAX-1",
      "flightNumber": "AX001",
      "departureDate": "2026-08-15"
    }
  ],
  "paymentIds": ["a1b2c3d4-e5f6-7890-abcd-ef1234567890"],
  "redemptionReference": null
}
```

| Field | Type | Description |
|-------|------|-------------|
| `bookingReference` | string | 6-character booking reference |
| `orderStatus` | string | `Confirmed` |
| `bookingType` | string | `Revenue` or `Reward` |
| `eTickets` | array | All issued e-ticket numbers per passenger per segment |
| `paymentIds` | array | All `PaymentId` values issued during this confirmation |
| `redemptionReference` | string (UUID) | The `TransactionId` GUID of the points authorisation loyalty transaction, for reward bookings. `null` for revenue bookings |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing payment details or malformed request |
| `404 Not Found` | Basket not found |
| `410 Gone` | Basket has expired |
| `422 Unprocessable Entity` | Invalid card details (card number fails Luhn validation, expired card, or invalid CVV format), insufficient points balance (reward), card declined by payment provider, or ticketing failed |

---

### POST /v1/orders/retrieve

Retrieve a confirmed order by booking reference and passenger name. Used for manage-booking and guest-authenticated order access. No JWT required — guest validation via booking reference + name.

**Delegates to:** `POST /v1/orders/retrieve` on Order MS.

#### Request

```json
{
  "bookingReference": "AB1234",
  "givenName": "Alex",
  "surname": "Taylor"
}
```

#### Response — `200 OK`

Returns the full order detail from the Order MS `OrderData` JSON document, including: passengers, flight segments, order items (flights, seats, bags, SSRs), e-ticket numbers, seat assignments, payment references, booking type, points redemption details (reward bookings), and order history.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | No confirmed order matching the booking reference and passenger name combination |

---

### PATCH /v1/orders/{bookingRef}/passengers

Correct or update passenger details on a confirmed order. E-ticket reissuance is triggered **only** if the given name or surname changes (name is encoded in the BCBP barcode string). Passport, contact, and travel document updates do not trigger reissuance.

**Orchestration sequence:**
1. `PATCH /v1/orders/{bookingRef}/passengers` on Order MS.
2. (If name changed) `POST /v1/tickets/reissue` on Delivery MS, `reason=NameCorrection`.

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
      "givenName": "Alexander",
      "surname": "Taylor",
      "travelDocument": {
        "type": "PASSPORT",
        "number": "PA9999999",
        "issuingCountry": "GBR",
        "expiryDate": "2031-06-30",
        "nationality": "GBR"
      }
    }
  ]
}
```

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "ticketsReissued": true,
  "newETicketNumbers": ["932-1234567900"]
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | No confirmed order found |

---

### PATCH /v1/orders/{bookingRef}/seats

Add or change seat selection on a confirmed order. Full ancillary charge applies. Reissues e-tickets and updates the manifest. Issues a `SeatAncillary` document.

**Orchestration sequence:**
1. `GET /v1/seatmap/{aircraftType}` on Seat MS — validate seat numbers.
2. For each `seatOfferId`: `GET /v1/seat-offers/{seatOfferId}` on Seat MS — validate and price.
3. `POST /v1/flights/{flightId}/seat-reservations` on Offer MS — soft-reserve seats.
4. `POST /v1/payment/authorise` on Payment MS — `description=SeatAncillary`.
5. `POST /v1/payment/{paymentId}/settle`.
6. `PATCH /v1/orders/{bookingRef}/seats` on Order MS.
7. `POST /v1/tickets/reissue` on Delivery MS, `reason=SeatChange`.
8. `PUT /v1/manifest` on Delivery MS — update seat numbers and e-ticket numbers.
9. `POST /v1/documents` on Delivery MS per paid seat, `documentType=SeatAncillary`.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "seatSelections": [
    {
      "seatOfferId": "so-3fa85f64-3A-v1",
      "passengerRef": "PAX-1",
      "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "payment": {
    "method": "CreditCard",
    "cardNumber": "4111111111111234",
    "expiryDate": "12/28",
    "cvv": "737",
    "cardholderName": "Alex Taylor"
  }
}
```

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "totalSeatAmount": 70.00,
  "paymentId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "newETicketNumbers": ["932-1234567901"]
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Order or seat offer not found |
| `409 Conflict` | Requested seat is already held or sold |
| `422 Unprocessable Entity` | Card declined |

---

### POST /v1/orders/{bookingRef}/change

Change a confirmed flight to a new itinerary. Governed by the fare conditions of the original ticket. Reshops the Offer MS for the replacement flight at the same cabin class.

**Pre-condition:** `isChangeable = true` on the original fare. If `false`, return `422 Unprocessable Entity`.

**Add-collect calculation (revenue):** `addCollect = max(0, newBaseFare − originalBaseFare)`. `totalDue = changeFee + addCollect`.

**Points recalculation (reward):** `pointsDifference = newPointsPrice − originalPointsAmount`. If positive, additional points are authorised and settled. If negative, surplus points are reinstated via `POST /v1/customers/{loyaltyNumber}/points/reinstate` with `reason=FlightChange`.

**IROPS fare override:** When `reason=FlightCancellation` is present (Disruption API context), the Order MS overrides all fare conditions and allows free rebooking regardless of fare type.

**Orchestration sequence:**
1. Retrieve order — confirm `isChangeable = true`, collect `changeFee`, `originalBaseFare`, `originalPaymentId`.
2. `POST /v1/search` on Offer MS — reshop for replacement flight.
3. (Revenue, `totalDue > 0`) `POST /v1/payment/authorise` on Payment MS, `description=FareChange`.
4. (Reward, `pointsDifference > 0`) `POST /v1/customers/{loyaltyNumber}/points/authorise` on Customer MS.
5. (Reward, `taxDifference > 0`) `POST /v1/payment/authorise` on Payment MS, `description=RewardChangeTaxes`.
6. `POST /v1/inventory/hold` on Offer MS — hold seats on replacement flight.
7. `PATCH /v1/tickets/{eTicketNumber}/void` on Delivery MS per changed-segment ticket.
8. `DELETE /v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}` on Delivery MS.
9. `POST /v1/inventory/release` on Offer MS (`releaseType=Sold`) — release original flight.
10. `PATCH /v1/orders/{bookingRef}/change` on Order MS.
11. `POST /v1/tickets/reissue` on Delivery MS, `reason=VoluntaryChange`.
12. `POST /v1/manifest` on Delivery MS — write manifest for replacement flight.
13. (Reward, `pointsDifference > 0`) `POST /v1/customers/{loyaltyNumber}/points/settle`.
14. (Reward, `pointsDifference < 0`) `POST /v1/customers/{loyaltyNumber}/points/reinstate`.
15. (Revenue, `totalDue > 0`) `POST /v1/payment/{paymentId}/settle`.
16. (Reward, `taxDifference > 0`) `POST /v1/payment/{paymentId}/settle`.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "newOfferId": "new-offer-uuid",
  "payment": {
    "method": "CreditCard",
    "cardNumber": "4111111111111234",
    "expiryDate": "12/28",
    "cvv": "737",
    "cardholderName": "Alex Taylor"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `newOfferId` | string (UUID) | Yes | `OfferId` from the reshop search for the replacement flight |
| `payment` | object | Conditional | Required if `totalDue > 0` (revenue) or `taxDifference > 0` (reward) |

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "newFlightNumber": "AX003",
  "newDepartureDate": "2026-08-16",
  "totalDue": 150.00,
  "paymentId": "c3d4e5f6-a7b8-9012-cdef-123456789012",
  "newETicketNumbers": ["932-1234567902"]
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Order or new offer not found |
| `422 Unprocessable Entity` | Fare does not permit voluntary change, card declined, or insufficient points |

---

### POST /v1/orders/{bookingRef}/cancel

Cancel a confirmed booking. Voids e-tickets, releases inventory, reinstates points (reward), and raises the `OrderCancelled` event. The Accounting system handles refund execution externally.

**`refundableAmount` calculation (revenue):** `isRefundable ? (totalPaid − cancellationFeeAmount) : 0`.

**Reward cancellation:** Always reinstates `totalPointsAmount` in full via Customer MS `points/reinstate`, regardless of fare conditions. Tax refundable amount follows the same formula as revenue.

**Orchestration sequence:**
1. Retrieve order — collect fare conditions, payment references, e-ticket numbers, booking type.
2. For each e-ticket: `PATCH /v1/tickets/{eTicketNumber}/void` on Delivery MS.
3. `DELETE /v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}` on Delivery MS per segment.
4. `POST /v1/inventory/release` on Offer MS (`releaseType=Sold`) per inventory ID.
5. (Reward) `POST /v1/customers/{loyaltyNumber}/points/reinstate` on Customer MS, `reason=VoluntaryCancellation`.
6. `PATCH /v1/orders/{bookingRef}/cancel` on Order MS. Publishes `OrderCancelled` event with `refundableAmount` and `originalPaymentId`.
7. (If ancillary documents exist and refund is due) `PATCH /v1/documents/{documentNumber}/void` on Delivery MS per ancillary document.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "orderStatus": "Cancelled",
  "refundableAmount": 2950.00,
  "cancellationFeeAmount": 0.00,
  "refundInitiated": true,
  "pointsReinstated": null
}
```

| Field | Type | Description |
|-------|------|-------------|
| `refundableAmount` | number | Amount to be refunded (may be `0` for non-refundable fares) |
| `refundInitiated` | boolean | `true` if `OrderCancelled` event was published with a non-zero `refundableAmount` |
| `pointsReinstated` | integer | Points restored for reward bookings. `null` for revenue bookings |

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No confirmed order found |
| `422 Unprocessable Entity` | Order is already cancelled |

---

### POST /v1/orders/{bookingRef}/bags

Add or update checked bag selection on a confirmed order. Issues a `BagAncillary` document.

**Orchestration sequence:**
1. For each `bagOfferId`: `GET /v1/bags/offers/{bagOfferId}` on Bag MS — validate and price.
2. `POST /v1/payment/authorise` on Payment MS — `description=BagAncillary`.
3. `POST /v1/payment/{paymentId}/settle`.
4. `PATCH /v1/orders/{bookingRef}/bags` on Order MS. Publishes `OrderChanged` event.
5. `POST /v1/documents` on Delivery MS per bag, `documentType=BagAncillary`.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "bagSelections": [
    {
      "bagOfferId": "bo-3fa85f64-Y-1-v1",
      "passengerRef": "PAX-1",
      "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "payment": {
    "method": "CreditCard",
    "cardNumber": "4111111111111234",
    "expiryDate": "12/28",
    "cvv": "737",
    "cardholderName": "Alex Taylor"
  }
}
```

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "totalBagAmount": 60.00,
  "paymentId": "d4e5f6a7-b8c9-0123-defa-234567890123"
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Order or bag offer not found |
| `422 Unprocessable Entity` | Card declined |

---

### PATCH /v1/orders/{bookingRef}/ssrs

Add, update, or remove SSRs on a confirmed order. Rejected if within the 24-hour amendment cut-off. SSR changes do **not** trigger e-ticket reissuance.

**Orchestration sequence:**
1. Evaluate amendment cut-off — if `now >= departureDateTime − 24 hours`, return `422`.
2. `PATCH /v1/orders/{bookingRef}/ssrs` on Order MS. Publishes `OrderChanged` event.
3. `PATCH /v1/manifest/{bookingRef}` on Delivery MS — update `SsrCodes` JSON array.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "ssrSelections": [
    { "action": "add", "ssrCode": "VGML", "passengerRef": "PAX-1", "segmentRef": "SEG-1" },
    { "action": "remove", "ssrCode": "WCHR", "passengerRef": "PAX-2", "segmentRef": "SEG-1" }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ssrSelections[].action` | string | Yes | `add` or `remove` |
| `ssrSelections[].ssrCode` | string | Yes | IATA 4-character SSR code. Must be active in `retail.SsrCatalogue` |
| `ssrSelections[].passengerRef` | string | Yes | PAX reference |
| `ssrSelections[].segmentRef` | string | Yes | Segment reference |

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "updated": true
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or unknown SSR code |
| `404 Not Found` | Order not found |
| `422 Unprocessable Entity` | Within the 24-hour SSR amendment cut-off window |

---

### POST /v1/reward/{redemptionReference}/reverse

Reverse a points authorisation hold if a downstream step fails during reward booking confirmation. Delegates to `POST /v1/customers/{loyaltyNumber}/points/reverse` on Customer MS.

**When to use:** Exposed to allow Contact Centre agents or retry mechanisms to reverse a points hold from a failed partial booking attempt where the Retail API's internal compensation did not complete.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `redemptionReference` | string (UUID) | The `TransactionId` GUID returned from the points authorisation call |

#### Request

```json
{
  "loyaltyNumber": "AX9876543",
  "reason": "TicketingFailure"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `loyaltyNumber` | string | Yes | The customer's loyalty number |
| `reason` | string | No | `TicketingFailure`, `PaymentFailure`, `BookingFailure` |

#### Response — `200 OK`

```json
{
  "redemptionReference": "f7a1b2c3-d4e5-6789-0abc-def123456789",
  "pointsReleased": 50000,
  "newPointsBalance": 98250
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Redemption reference not found |
| `409 Conflict` | Redemption already settled or reversed |

---

### GET /v1/flights/{flightId}/seatmap

Retrieve seatmap with pricing and availability for a flight. Assembles the full channel-facing seatmap by merging three microservice data sources.

**Orchestration sequence:**
1. `GET /v1/seatmap/{aircraftType}` on Seat MS — physical cabin layout, seat positions, attributes, `isSelectable`.
2. `GET /v1/seat-offers?flightId={flightId}` on Seat MS — `SeatOfferId`, price, `isChargeable` per selectable seat.
3. `GET /v1/flights/{flightId}/seat-availability` on Offer MS — per-seat status (available, held, sold).
4. Merge on `seatNumber` and return to channel.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `flightId` | string (UUID) | The `InventoryId` from `offer.FlightInventory` identifying the specific flight and cabin |

#### Response — `200 OK`

```json
{
  "flightId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "flightNumber": "AX001",
  "aircraftType": "A351",
  "cabins": [
    {
      "cabinCode": "J",
      "cabinName": "Business Class",
      "layout": "1-2-1",
      "rows": [
        {
          "rowNumber": 1,
          "seats": [
            {
              "seatNumber": "1A",
              "column": "A",
              "position": "Window",
              "type": "Suite",
              "attributes": ["ExtraLegroom", "BulkheadForward"],
              "isSelectable": true,
              "isChargeable": false,
              "price": 0.00,
              "currencyCode": "GBP",
              "seatOfferId": "so-3fa85f64-1A-v1",
              "availability": "available"
            }
          ]
        }
      ]
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `seats[].seatOfferId` | string | `SeatOfferId` from Seat MS. `null` for non-selectable seats |
| `seats[].isChargeable` | boolean | `false` for Business and First Class |
| `seats[].price` | number | Current price. `0.00` for non-chargeable seats |
| `seats[].availability` | string | `available`, `held`, or `sold` from Offer MS |

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No active seatmap or inventory found for the given flight ID |

---

### GET /v1/flights/{flightId}/seat-availability

Retrieve real-time seat availability overlay for a flight without full pricing. Delegates directly to `GET /v1/flights/{flightId}/seat-availability` on the Offer MS.

**When to use:** When a channel needs to refresh availability without rebuilding the full seatmap.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `flightId` | string (UUID) | The `InventoryId` identifying the specific flight and cabin |

#### Response — `200 OK`

Passes through the Offer MS response. See Offer Microservice spec for full schema.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No inventory found for the given flight ID |

---

### GET /v1/ssr/options

Retrieve all active SSR codes, labels, and categories from `retail.SsrCatalogue`. The Retail API reads directly from its own table — no downstream microservice call is required.

#### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `cabinCode` | string | No | Filter SSRs applicable to a specific cabin |
| `flightNumbers` | string | No | Comma-separated flight numbers to filter applicable SSRs |

#### Response — `200 OK`

```json
{
  "ssrOptions": [
    { "ssrCode": "VGML", "label": "Vegetarian meal (lacto-ovo)", "category": "Meal" },
    { "ssrCode": "HNML", "label": "Hindu meal", "category": "Meal" },
    { "ssrCode": "MOML", "label": "Muslim / halal meal", "category": "Meal" },
    { "ssrCode": "KSML", "label": "Kosher meal", "category": "Meal" },
    { "ssrCode": "DBML", "label": "Diabetic meal", "category": "Meal" },
    { "ssrCode": "GFML", "label": "Gluten-free meal", "category": "Meal" },
    { "ssrCode": "CHML", "label": "Child meal", "category": "Meal" },
    { "ssrCode": "BBML", "label": "Baby / infant meal", "category": "Meal" },
    { "ssrCode": "WCHR", "label": "Wheelchair — can walk, needs distance assistance", "category": "Mobility" },
    { "ssrCode": "WCHS", "label": "Wheelchair — cannot manage steps", "category": "Mobility" },
    { "ssrCode": "WCHC", "label": "Wheelchair — fully immobile", "category": "Mobility" },
    { "ssrCode": "BLND", "label": "Blind or severely visually impaired", "category": "Accessibility" },
    { "ssrCode": "DEAF", "label": "Deaf or severely hearing impaired", "category": "Accessibility" },
    { "ssrCode": "DPNA", "label": "Disabled passenger needing assistance", "category": "Accessibility" }
  ]
}
```

---

### POST /v1/ssr/options

Create a new SSR catalogue entry in `retail.SsrCatalogue`. Admin endpoint — not channel-facing.

#### Request

```json
{
  "ssrCode": "UMNR",
  "label": "Unaccompanied minor",
  "category": "Accessibility"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `ssrCode` | string | Yes | IATA 4-character uppercase SSR code |
| `label` | string | Yes | Human-readable label. Max 100 characters |
| `category` | string | Yes | `Meal`, `Mobility`, or `Accessibility` |

#### Response — `201 Created`

```json
{
  "ssrCode": "UMNR",
  "label": "Unaccompanied minor",
  "category": "Accessibility",
  "isActive": true
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Invalid `ssrCode` format or invalid `category` |
| `409 Conflict` | SSR code already exists |

---

### PUT /v1/ssr/options/{ssrCode}

Update an existing SSR catalogue entry — label or category only. `ssrCode` is immutable. Admin endpoint — not channel-facing.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `ssrCode` | string | The IATA SSR code to update |

#### Request

```json
{
  "label": "Updated label",
  "category": "Accessibility"
}
```

#### Response — `200 OK`

Returns the updated SSR catalogue entry.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Invalid category or missing fields |
| `404 Not Found` | SSR code not found |

---

### DELETE /v1/ssr/options/{ssrCode}

Deactivate an SSR code (`IsActive = 0`). Existing order items referencing the code are unaffected. Admin endpoint — not channel-facing.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `ssrCode` | string | The IATA SSR code to deactivate |

#### Response — `200 OK`

```json
{
  "ssrCode": "UMNR",
  "isActive": false
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | SSR code not found |

---

### POST /v1/checkin/retrieve

Retrieve booking details to begin the online check-in flow. Validates the booking is within the OLCI window (≤ 24 hours before departure, > 0 hours). Delegates to `POST /v1/orders/retrieve` on Order MS.

#### Request

```json
{
  "bookingReference": "AB1234",
  "givenName": "Alex",
  "surname": "Taylor"
}
```

#### Response — `200 OK`

Returns full order detail as per `POST /v1/orders/retrieve`, plus:

```json
{
  "checkInEligible": true,
  "passengers": [
    {
      "passengerId": "PAX-1",
      "checkInStatus": "NotCheckedIn"
    }
  ]
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No confirmed order found |
| `422 Unprocessable Entity` | Outside the 24-hour OLCI window, or all passengers are already checked in |

---

### PATCH /v1/checkin/{bookingRef}/seats

Update seat assignment during check-in. **No charge at OLCI** regardless of cabin. No payment is authorised. No `delivery.Document` is issued.

**Orchestration sequence:**
1. `GET /v1/seatmap/{aircraftType}` on Seat MS — validate seat numbers.
2. `POST /v1/flights/{flightId}/seat-reservations` on Offer MS — soft-reserve.
3. `PATCH /v1/orders/{bookingRef}/seats` on Order MS.

> E-ticket reissuance is **not** triggered by OLCI seat changes — boarding cards have not yet been generated at this point.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bookingRef` | string | The 6-character booking reference |

#### Request

```json
{
  "seatSelections": [
    {
      "seatOfferId": "so-3fa85f64-5A-v1",
      "passengerRef": "PAX-1",
      "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ]
}
```

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "updated": true
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields |
| `404 Not Found` | Order not found |
| `409 Conflict` | Requested seat already held or sold |
| `422 Unprocessable Entity` | Outside the OLCI window |

---

### POST /v1/checkin/{bookingRef}

Submit check-in for all passengers, recording APIS data and generating boarding cards.

**Orchestration sequence:**
1. `POST /v1/orders/{bookingRef}/checkin` on Order MS — record check-in status and APIS data.
2. `PATCH /v1/flights/{flightId}/seat-availability` on Offer MS — mark seats as `checked-in`.
3. `PATCH /v1/manifest/{bookingRef}` on Delivery MS — set `checkedIn = true`, stamp `checkedInAt`, update `SsrCodes`.
4. `POST /v1/boarding-cards` on Delivery MS — generate BCBP boarding cards for all checked-in passengers.

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
      "travelDocument": {
        "type": "PASSPORT",
        "number": "PA1234567",
        "issuingCountry": "GBR",
        "expiryDate": "2030-01-01",
        "nationality": "GBR",
        "dateOfBirth": "1985-03-12",
        "gender": "Male",
        "residenceCountry": "GBR"
      }
    }
  ]
}
```

#### Response — `200 OK`

```json
{
  "bookingReference": "AB1234",
  "boardingCards": [
    {
      "passengerId": "PAX-1",
      "flightNumber": "AX001",
      "departureDate": "2026-08-15",
      "seatNumber": "1A",
      "cabinCode": "J",
      "sequenceNumber": "0001",
      "bcbpString": "M1TAYLOR/ALEX        EAB1234 LHRJFKAX 0001 228J001A0001 156>518 W6042 AX 2A00000012345678 JAX7KLP2NZR901A",
      "origin": "LHR",
      "destination": "JFK",
      "eTicketNumber": "932-1234567890"
    }
  ]
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing APIS data or invalid travel document |
| `404 Not Found` | Order not found |
| `422 Unprocessable Entity` | Outside the OLCI window or all passengers already checked in |

---

## SSR Catalogue — `retail.SsrCatalogue`

The only database table owned by the Retail API. Read directly for `GET /v1/ssr/options` — no downstream microservice call required.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `SsrCatalogueId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `SsrCode` | CHAR(4) | No | | UK | IATA 4-character SSR code, e.g. `VGML`, `WCHR` |
| `Label` | VARCHAR(100) | No | | | Human-readable name displayed on channel |
| `Category` | VARCHAR(20) | No | | | `Meal`, `Mobility`, or `Accessibility` |
| `IsActive` | BIT | No | `1` | | Inactive codes excluded from `GET /v1/ssr/options` but retained for historical order display |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — SQL trigger-generated on every update** |

> **Indexes:** `IX_SsrCatalogue_Code` on `(SsrCode)` WHERE `IsActive = 1`.

**Seed data (14 SSR codes):** `VGML`, `HNML`, `MOML`, `KSML`, `DBML`, `GFML`, `CHML`, `BBML` (Meal category) · `WCHR`, `WCHS`, `WCHC` (Mobility) · `BLND`, `DEAF`, `DPNA` (Accessibility).

---

## Data Conventions

| Convention | Format | Example |
|------------|--------|---------|
| Timestamps | ISO 8601 UTC | `"2026-08-15T09:30:00Z"` |
| Dates | ISO 8601 | `"2026-08-15"` |
| Times | HH:mm (24-hour local) | `"09:30"` |
| Airport codes | IATA 3-letter, uppercase | `"LHR"`, `"JFK"` |
| Currency codes | ISO 4217 | `"GBP"` |
| Currency amounts | Decimal, 2 places | `2950.00` |
| Booking reference | 6 characters, alphanumeric | `"AB1234"` |
| E-ticket numbers | IATA format | `"932-1234567890"` |
| Passenger types | IATA codes | `ADT`, `CHD`, `INF`, `YTH` |
| JSON field names | camelCase | `bookingReference`, `totalAmount` |
| UUIDs | RFC 4122 lowercase | `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"` |

---

## Invocation Examples

### Search for direct flights (channel → Retail API)

```bash
curl -X POST https://{retail-api-host}/v1/search/slice \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{ "origin": "LHR", "destination": "JFK", "departureDate": "2026-08-15", "paxCount": 2 }'
```

### Create a basket (channel → Retail API)

```bash
curl -X POST https://{retail-api-host}/v1/basket \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIs..." \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{ "offerIds": ["9ab12345-6789-0abc-def0-123456789abc"], "channelCode": "WEB" }'
```

### Confirm a basket (channel → Retail API)

```bash
curl -X POST https://{retail-api-host}/v1/basket/a1b2c3d4-e5f6-7890-abcd-ef1234567890/confirm \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIs..." \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{ "payment": { "method": "CreditCard", "cardNumber": "4111111111111234", "expiryDate": "12/28", "cvv": "737", "cardholderName": "Alex Taylor" } }'
```

### Retrieve an order (channel → Retail API, guest auth)

```bash
curl -X POST https://{retail-api-host}/v1/orders/retrieve \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{ "bookingReference": "AB1234", "givenName": "Alex", "surname": "Taylor" }'
```

### Cancel a booking (channel → Retail API)

```bash
curl -X POST https://{retail-api-host}/v1/orders/AB1234/cancel \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIs..." \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

### Submit check-in (channel → Retail API)

```bash
curl -X POST https://{retail-api-host}/v1/checkin/AB1234 \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIs..." \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{ "passengers": [{ "passengerId": "PAX-1", "travelDocument": { "type": "PASSPORT", "number": "PA1234567", "issuingCountry": "GBR", "expiryDate": "2030-01-01", "nationality": "GBR", "dateOfBirth": "1985-03-12", "gender": "Male", "residenceCountry": "GBR" } }] }'
```

---

## Related Documentation

- [API Endpoint Reference](../api-reference.md) — Summary of all orchestration and microservice endpoints
- [System Design](../system-overview.md) — Bookflow, reward booking, manage-booking, OLCI, and IROPS sequence diagrams
- [Offer Microservice Specification](offer-microservice.md)
- [Order Microservice Specification](order-microservice.md)
- [Payment Microservice Specification](payment-microservice.md)
- [Delivery Microservice Specification](delivery-microservice.md)
- [Seat Microservice Specification](seat-microservice.md)
- [Bag Microservice Specification](bag-microservice.md)
