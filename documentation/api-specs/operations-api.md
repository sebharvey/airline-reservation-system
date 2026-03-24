# Operations API — API Specification

> **Service owner:** Operations domain
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Operations API is the orchestration layer used by airline operations staff to manage flight schedules. It receives schedule definitions from the Ops Admin App, coordinates persistence via the Schedule MS, generates bulk `FlightInventory` and `Fare` records in the Offer domain via the Offer MS, and updates the schedule record with the count of flights created. The Operations API does not own any database tables itself — it orchestrates the Schedule MS and Offer MS exclusively.

> **Important:** The Operations API is not channel-facing. It is called exclusively by the Ops Admin App used by airline operations staff. It uses `x-functions-key` authentication for inbound calls from the Ops Admin App. It does not validate JWTs. See the [Security](#security) section for details on how calls are authenticated.

---

## Security

### Authentication

The Operations API is an internal service called only by the Ops Admin App. There is no channel-facing route or JWT-based authentication.

1. The Ops Admin App authenticates to the Operations API using an **Azure Function Host Key** passed in the `x-functions-key` HTTP header.
2. The Operations API authenticates to downstream microservices (Schedule MS, Offer MS) using their respective host keys. See [Microservice Authentication — Host Keys](../api.md#microservice-authentication--host-keys) in `api.md` for the full mechanism, key storage, and retrieval details.

### Required Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes | Must be `application/json` |
| `x-functions-key` | Yes | Azure Function Host Key authenticating the Ops Admin App as an authorised caller. See [`api.md` — Microservice Authentication](../api.md#microservice-authentication--host-keys) for key storage and retrieval details |
| `X-Correlation-ID` | Yes | UUID generated at the Ops Admin App boundary; propagated on every downstream call for distributed tracing and log correlation |

### Data Protection

- Schedule data is not customer-facing PII. Standard logging practices apply.
- The Operations API does not store any data directly — all persistence is delegated to the Schedule MS and Offer MS.

---

## Orchestration Flow

The Operations API coordinates a multi-step schedule creation flow across the Schedule MS and Offer MS. The full sequence is:

1. **Receive schedule definition** from the Ops Admin App.
2. **Persist schedule** — call Schedule MS `POST /v1/schedules` to persist the schedule record and receive the list of enumerated operating dates and parsed cabin/fare definitions.
3. **Generate inventory and fares** — for each operating date and each cabin:
   - Call Offer MS `POST /v1/flights` to create a `FlightInventory` record. Receives `inventoryId`.
   - For each fare defined for that cabin:
     - Call Offer MS `POST /v1/flights/{inventoryId}/fares` to create a `Fare` record linked to the inventory.
4. **Update flights created count** — call Schedule MS `PATCH /v1/schedules/{scheduleId}` with the total number of `FlightInventory` records created.
5. **Return** `scheduleId` and `flightsCreated` count to the Ops Admin App.

### Downstream Service Dependencies

| Service | Endpoints Called | Purpose |
|---------|-----------------|---------|
| **Schedule MS** | `POST /v1/schedules`, `PATCH /v1/schedules/{scheduleId}` | Persist schedule definition, enumerate operating dates, update `FlightsCreated` count |
| **Offer MS** | `POST /v1/flights`, `POST /v1/flights/{inventoryId}/fares` | Create `FlightInventory` records per operating date per cabin, create `Fare` records per fare per inventory |

---

## Endpoints

### POST /v1/schedules

Create a flight schedule. Orchestrates schedule persistence, bulk flight inventory and fare generation, and schedule record update in a single synchronous request.

**When to use:** Called by the Ops Admin App when an operations user defines a new flight schedule. The Operations API orchestrates the full creation flow: (1) persists the schedule via Schedule MS, (2) generates `FlightInventory` and `Fare` records in the Offer domain via Offer MS for every operating date within the `ValidFrom`–`ValidTo` window matching the `daysOfWeek` bitmask, (3) updates the `FlightsCreated` count on the schedule record via Schedule MS.

#### Request

```json
{
  "flightNumber": "AX001",
  "origin": "LHR",
  "destination": "JFK",
  "departureTime": "09:30",
  "arrivalTime": "13:45",
  "arrivalDayOffset": 0,
  "daysOfWeek": 127,
  "aircraftType": "A351",
  "validFrom": "2026-04-01",
  "validTo": "2026-10-25",
  "cabins": [
    {
      "cabinCode": "J",
      "totalSeats": 30,
      "fares": [
        {
          "fareBasisCode": "JFLEX",
          "fareFamily": "Business Flex",
          "currencyCode": "GBP",
          "baseFareAmount": 2500.00,
          "taxAmount": 450.00,
          "isRefundable": true,
          "isChangeable": true,
          "changeFeeAmount": 0.00,
          "cancellationFeeAmount": 0.00,
          "pointsPrice": 75000,
          "pointsTaxes": 450.00
        },
        {
          "fareBasisCode": "JSAVER",
          "fareFamily": "Business Saver",
          "currencyCode": "GBP",
          "baseFareAmount": 1800.00,
          "taxAmount": 450.00,
          "isRefundable": false,
          "isChangeable": true,
          "changeFeeAmount": 150.00,
          "cancellationFeeAmount": 300.00,
          "pointsPrice": 55000,
          "pointsTaxes": 450.00
        }
      ]
    },
    {
      "cabinCode": "Y",
      "totalSeats": 220,
      "fares": [
        {
          "fareBasisCode": "YFLEX",
          "fareFamily": "Economy Flex",
          "currencyCode": "GBP",
          "baseFareAmount": 650.00,
          "taxAmount": 180.00,
          "isRefundable": true,
          "isChangeable": true,
          "changeFeeAmount": 0.00,
          "cancellationFeeAmount": 0.00,
          "pointsPrice": 25000,
          "pointsTaxes": 180.00
        },
        {
          "fareBasisCode": "YSAVER",
          "fareFamily": "Economy Saver",
          "currencyCode": "GBP",
          "baseFareAmount": 350.00,
          "taxAmount": 180.00,
          "isRefundable": false,
          "isChangeable": false,
          "changeFeeAmount": 0.00,
          "cancellationFeeAmount": 0.00,
          "pointsPrice": 15000,
          "pointsTaxes": 180.00
        }
      ]
    }
  ]
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `flightNumber` | string | Yes | Flight number, e.g. `"AX001"`. Max 10 characters |
| `origin` | string | Yes | IATA airport code for departure, e.g. `"LHR"`. Exactly 3 characters |
| `destination` | string | Yes | IATA airport code for arrival, e.g. `"JFK"`. Exactly 3 characters |
| `departureTime` | string (time) | Yes | Local departure time at origin, e.g. `"09:30"` |
| `arrivalTime` | string (time) | Yes | Local arrival time at destination, e.g. `"13:45"` |
| `arrivalDayOffset` | integer | No | `0` = same day (default), `1` = next day arrival |
| `daysOfWeek` | integer | Yes | Bitmask: Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64; `127` = daily |
| `aircraftType` | string | Yes | Aircraft type code, e.g. `"A351"`. Max 4 characters |
| `validFrom` | string (date) | Yes | First operating date (inclusive), ISO 8601 |
| `validTo` | string (date) | Yes | Last operating date (inclusive), ISO 8601 |
| `cabins` | array | Yes | Array of cabin definitions, each containing cabin configuration and fare definitions |
| `cabins[].cabinCode` | string | Yes | Cabin class code: `F` (First), `J` (Business), `W` (Premium Economy), `Y` (Economy) |
| `cabins[].totalSeats` | integer | Yes | Total seats available in this cabin |
| `cabins[].fares` | array | Yes | Array of fare definitions for this cabin |
| `cabins[].fares[].fareBasisCode` | string | Yes | Fare basis code, e.g. `"JFLEX"`, `"YSAVER"` |
| `cabins[].fares[].fareFamily` | string | Yes | Human-readable fare family name, e.g. `"Business Flex"` |
| `cabins[].fares[].currencyCode` | string | Yes | ISO 4217 currency code, e.g. `"GBP"` |
| `cabins[].fares[].baseFareAmount` | number | Yes | Base fare amount before tax. Decimal, 2 places |
| `cabins[].fares[].taxAmount` | number | Yes | Tax amount. Decimal, 2 places |
| `cabins[].fares[].isRefundable` | boolean | Yes | Whether the fare is refundable |
| `cabins[].fares[].isChangeable` | boolean | Yes | Whether the fare allows date/time changes |
| `cabins[].fares[].changeFeeAmount` | number | Yes | Fee charged for changes (`0.00` if changes are free or not permitted) |
| `cabins[].fares[].cancellationFeeAmount` | number | Yes | Fee charged for cancellation (`0.00` if cancellation is free or not permitted) |
| `cabins[].fares[].pointsPrice` | integer | Yes | Points price for award bookings |
| `cabins[].fares[].pointsTaxes` | number | Yes | Tax amount payable on award bookings (cash component). Decimal, 2 places |

#### Response — `201 Created`

```json
{
  "scheduleId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
  "flightsCreated": 208
}
```

| Field | Type | Description |
|-------|------|-------------|
| `scheduleId` | string (UUID) | Unique identifier for the created schedule, returned by the Schedule MS |
| `flightsCreated` | integer | Total number of `FlightInventory` records created across all operating dates and cabins |

> **Inventory is immediately live:** Generated `FlightInventory` and `Fare` records are immediately available for offer search via the Offer MS. There is no separate activation step.

> **Pricing at creation time:** Fare pricing (base fare, tax, points price, points taxes) is supplied in the schedule definition and written directly to `offer.Fare` records. Pricing is not dynamically calculated — it is fixed at schedule creation time.

> **Immutability:** Each schedule record is immutable after creation. Modifications to a schedule (e.g. changing fares, adjusting operating dates, or updating flight times) require creating a new schedule definition. The original schedule and its generated inventory remain unchanged.

> **No owned database tables:** The Operations API does not own any database tables. It orchestrates the Schedule MS (which owns `schedule.FlightSchedule`) and the Offer MS (which owns `offer.FlightInventory` and `offer.Fare`).

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid field format (e.g. malformed IATA code, invalid bitmask value, `validFrom` after `validTo`, invalid cabin code), or invalid cabin/fare structure |
| `500 Internal Server Error` | Downstream microservice call failed during orchestration (Schedule MS or Offer MS returned an error) |

---

## Data Conventions

| Convention | Format | Example |
|------------|--------|---------|
| Timestamps | ISO 8601 UTC | `"2026-03-20T14:05:00Z"` |
| Dates | ISO 8601 | `"2026-04-01"` |
| Times | HH:mm (24-hour, local) | `"09:30"` |
| Airport codes | IATA 3-letter | `"LHR"` |
| Currency codes | ISO 4217 | `"GBP"` |
| Currency amounts | Decimal, 2 places | `2500.00` |
| JSON field names | camelCase | `flightNumber` |
| UUIDs | RFC 4122 lowercase | `"b2c3d4e5-f6a7-8901-bcde-f12345678901"` |

---

## Invocation Examples

### Creating a flight schedule (Ops Admin App → Operations API)

```bash
curl -X POST https://{operations-api-host}/v1/schedules \
  -H "Content-Type: application/json" \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "flightNumber": "AX001",
    "origin": "LHR",
    "destination": "JFK",
    "departureTime": "09:30",
    "arrivalTime": "13:45",
    "arrivalDayOffset": 0,
    "daysOfWeek": 127,
    "aircraftType": "A351",
    "validFrom": "2026-04-01",
    "validTo": "2026-10-25",
    "cabins": [
      {
        "cabinCode": "J",
        "totalSeats": 30,
        "fares": [
          {
            "fareBasisCode": "JFLEX",
            "fareFamily": "Business Flex",
            "currencyCode": "GBP",
            "baseFareAmount": 2500.00,
            "taxAmount": 450.00,
            "isRefundable": true,
            "isChangeable": true,
            "changeFeeAmount": 0.00,
            "cancellationFeeAmount": 0.00,
            "pointsPrice": 75000,
            "pointsTaxes": 450.00
          }
        ]
      },
      {
        "cabinCode": "Y",
        "totalSeats": 220,
        "fares": [
          {
            "fareBasisCode": "YFLEX",
            "fareFamily": "Economy Flex",
            "currencyCode": "GBP",
            "baseFareAmount": 650.00,
            "taxAmount": 180.00,
            "isRefundable": true,
            "isChangeable": true,
            "changeFeeAmount": 0.00,
            "cancellationFeeAmount": 0.00,
            "pointsPrice": 25000,
            "pointsTaxes": 180.00
          },
          {
            "fareBasisCode": "YSAVER",
            "fareFamily": "Economy Saver",
            "currencyCode": "GBP",
            "baseFareAmount": 350.00,
            "taxAmount": 180.00,
            "isRefundable": false,
            "isChangeable": false,
            "changeFeeAmount": 0.00,
            "cancellationFeeAmount": 0.00,
            "pointsPrice": 15000,
            "pointsTaxes": 180.00
          }
        ]
      }
    ]
  }'
```

### Creating a schedule for a single cabin with minimal fares

```bash
curl -X POST https://{operations-api-host}/v1/schedules \
  -H "Content-Type: application/json" \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 661f9500-f30c-52e5-b827-557766551111" \
  -d '{
    "flightNumber": "AX205",
    "origin": "LHR",
    "destination": "CDG",
    "departureTime": "07:15",
    "arrivalTime": "09:30",
    "arrivalDayOffset": 0,
    "daysOfWeek": 31,
    "aircraftType": "A339",
    "validFrom": "2026-06-01",
    "validTo": "2026-08-31",
    "cabins": [
      {
        "cabinCode": "Y",
        "totalSeats": 280,
        "fares": [
          {
            "fareBasisCode": "YFLEX",
            "fareFamily": "Economy Flex",
            "currencyCode": "GBP",
            "baseFareAmount": 180.00,
            "taxAmount": 45.00,
            "isRefundable": true,
            "isChangeable": true,
            "changeFeeAmount": 0.00,
            "cancellationFeeAmount": 0.00,
            "pointsPrice": 8000,
            "pointsTaxes": 45.00
          }
        ]
      }
    ]
  }'
```

> **Note:** The Operations API is not channel-facing. All calls originate from the Ops Admin App and are authenticated using the `x-functions-key` header. There is no `Authorization: Bearer` header. See [`api.md` — Microservice Authentication](../api.md#microservice-authentication--host-keys) for the full host key mechanism.

---

## Related Documentation

- [API Endpoint Reference](../api-reference.md) — Summary of all microservice endpoints
- [System Design](../system-overview.md) — Full domain design including sequence diagrams for schedule creation and inventory generation flows
- [Schedule Microservice](schedule-microservice.md) — API specification for the Schedule MS (downstream persistence layer)
