# Operations API — API Specification

> **Service owner:** Operations domain
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Operations API is the orchestration layer used by airline operations staff to manage flight schedules and schedule groups, and is the integration point for the Flight Operations System (FOS) to notify the reservation system of disruption events. It receives schedule definitions from the Ops Admin App, coordinates persistence via the Schedule MS, generates bulk `FlightInventory` and `Fare` records in the Offer domain via the Offer MS, and updates the schedule record with the count of flights created. The Operations API does not own any database tables itself — it orchestrates the Schedule MS and Offer MS exclusively.

Schedules are organised into **schedule groups** — named collections (e.g. "Summer 2026", "Annual 2026") that allow multiple schedule versions to coexist. The Ops Admin App provides a group dropdown to select which group to view and import into.

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

## Orchestration flows

The Operations API coordinates two distinct orchestration flows.

### SSIM import flow (`POST /v1/schedules/ssim?scheduleGroupId=`)

Parses an IATA SSIM Chapter 7 file and persists the schedule definitions to a specific schedule group in the Schedule MS. Requires a `scheduleGroupId` query parameter. Does not generate inventory. To generate inventory after a SSIM import, call `POST /v1/schedules/import-inventory`.

### Schedule-to-inventory import flow (`POST /v1/schedules/import-inventory`)

Takes schedules stored in the Schedule MS (optionally filtered by `scheduleGroupId` in the request body) and generates `FlightInventory` and `Fare` records in the Offer MS. This is the second step after a SSIM import. The full sequence is:

1. **Fetch schedules** — call Schedule MS `GET /v1/schedules?scheduleGroupId=` to retrieve persisted schedule records (for a specific group, or all groups if omitted).
2. **Fetch aircraft configs** — call Seat MS `GET /v1/aircraft-types` to resolve cabin seat counts for each aircraft type referenced by the schedules.
3. **Generate inventory** — enumerate operating dates for each schedule (using `daysOfWeek`, `validFrom`, `validTo`). For each operating date × cabin, call Offer MS `POST /v1/flights/batch`. Records that already exist are skipped.
4. **Create fares** — for each newly created inventory record, call Offer MS `POST /v1/flights/{inventoryId}/fares` for each matching stored fare rule.
5. **Return** a summary of schedules processed, inventories created/skipped, and fares created.

### Legacy schedule creation flow (`POST /v1/schedules`)

> **Note:** The `POST /v1/schedules` endpoint described in earlier documentation (accepting cabin/fare definitions directly) is superseded by the two-step SSIM + import-inventory flow above. The direct schedule-creation flow may be added in a future release.

---

## Downstream service dependencies

| Service | Endpoints Called | Purpose |
|---------|-----------------|---------|
| **Schedule MS** | `POST /v1/schedules`, `GET /v1/schedules`, `GET /v1/schedule-groups`, `POST /v1/schedule-groups`, `PUT /v1/schedule-groups/{id}`, `DELETE /v1/schedule-groups/{id}` | Persist schedule definitions (SSIM import), retrieve schedules (inventory import), manage schedule groups |
| **Offer MS** | `POST /v1/flights/batch`, `POST /v1/flights/{inventoryId}/fares`, `GET /v1/flights/{flightNumber}/inventory` | Batch-create `FlightInventory` records, create `Fare` records per inventory, retrieve flight inventory for status derivation |

---

## Pre-existing orchestration flow documentation

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

### GET /v1/schedule-groups

Retrieve all schedule groups from the Schedule MS. Returns a summary of each group including name, season dates, active status, and the count of schedules in the group.

**When to use:** Called by the Ops Admin App to populate the schedule group dropdown selector.

#### Request

No request body. No query parameters.

#### Response — `200 OK`

```json
{
  "count": 2,
  "groups": [
    {
      "scheduleGroupId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "name": "Annual 2026",
      "seasonStart": "2026-01-01",
      "seasonEnd": "2026-12-31",
      "isActive": true,
      "scheduleCount": 48,
      "createdBy": "ops-admin@apexair.com",
      "createdAt": "2026-03-26T10:00:00.0000000Z"
    }
  ]
}
```

---

### POST /v1/schedule-groups

Create a new schedule group.

---

### PUT /v1/schedule-groups/{scheduleGroupId}

Update an existing schedule group's name, season dates, and active status.

---

### DELETE /v1/schedule-groups/{scheduleGroupId}

Delete a schedule group and all its associated flight schedules.

---

### GET /v1/schedules

Retrieve stored flight schedules from the Schedule MS, optionally filtered by schedule group.

**When to use:** Called by the Ops Admin App (or Terminal Contact Centre app) to display flight schedules for a selected group. This is a read-only query — no data is modified.

#### Request

No request body.

| Query Parameter | Type | Required | Description |
|-----------------|------|----------|-------------|
| `scheduleGroupId` | string (UUID) | No | Filter to a specific schedule group. Omit to return all schedules |

#### Response — `200 OK`

```json
{
  "count": 12,
  "schedules": [
    {
      "scheduleId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
      "scheduleGroupId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "flightNumber": "AX001",
      "origin": "LHR",
      "destination": "JFK",
      "departureTime": "08:00",
      "arrivalTime": "11:10",
      "arrivalDayOffset": 0,
      "daysOfWeek": 127,
      "aircraftType": "A351",
      "validFrom": "2026-01-01",
      "validTo": "2026-12-31",
      "flightsCreated": 365,
      "operatingDateCount": 365
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `count` | integer | Total number of schedule records returned |
| `schedules` | array | Array of schedule summary objects |
| `schedules[].scheduleId` | string (UUID) | Unique schedule identifier |
| `schedules[].scheduleGroupId` | string (UUID) | Schedule group this schedule belongs to |
| `schedules[].flightNumber` | string | Flight number, e.g. `"AX001"` |
| `schedules[].origin` | string | IATA 3-letter departure airport code |
| `schedules[].destination` | string | IATA 3-letter arrival airport code |
| `schedules[].departureTime` | string (time) | Local departure time at origin, e.g. `"08:00"` |
| `schedules[].arrivalTime` | string (time) | Local arrival time at destination, e.g. `"11:10"` |
| `schedules[].arrivalDayOffset` | integer | `0` = same-day arrival, `1` = next-day arrival |
| `schedules[].daysOfWeek` | integer | Bitmask: Mon=1, Tue=2, Wed=4, Thu=8, Fri=16, Sat=32, Sun=64; `127` = daily |
| `schedules[].aircraftType` | string | Aircraft type code, e.g. `"A351"` |
| `schedules[].validFrom` | string (date) | First operating date (inclusive), ISO 8601 |
| `schedules[].validTo` | string (date) | Last operating date (inclusive), ISO 8601 |
| `schedules[].flightsCreated` | integer | Number of `FlightInventory` records generated from this schedule |
| `schedules[].operatingDateCount` | integer | Total number of operating dates within the validity window matching the `daysOfWeek` bitmask |

#### Error Responses

| Status | Reason |
|--------|--------|
| `500 Internal Server Error` | Downstream Schedule MS call failed |

---

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

---

### POST /v1/schedules/import-inventory

Import schedules stored in the Schedule MS into the Offer MS inventory tables. Optionally scoped to a specific schedule group via `scheduleGroupId` in the request body. Cabin seat counts are resolved automatically from the Seat MS aircraft type configuration — no cabin definitions are required in the request. For each schedule, operating dates are computed from the `ValidFrom`/`ValidTo` range and `DaysOfWeek` bitmask. For each operating date and each cabin in the aircraft type's configuration, a `FlightInventory` record is created. If a record already exists for that combination, it is skipped. Fares are then created for each newly created inventory record from stored fare rules.

**When to use:** Called after a SSIM import (via `POST /v1/schedules/ssim`) to activate the stored schedules for booking. Can be re-run at any time — existing inventory is never duplicated or overwritten.

#### Request

```json
{
  "scheduleGroupId": "f8c81b29-9e84-4842-b0f0-e0f5db756838"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `scheduleGroupId` | string (UUID) | No | Limit inventory import to schedules in a specific group. Omit to process all schedules |

> **Aircraft configuration:** Cabin codes and seat counts are resolved from the Seat MS `GET /v1/aircraft-types` endpoint using the `aircraftType` field on each schedule. Schedules whose aircraft type has no registered configuration in the Seat MS are skipped with a warning.

> **Fare rules:** Fares are created from stored fare rules in the Offer MS (`GET /v1/fare-rules`). Fare validity dates are taken from the corresponding schedule record's `ValidFrom`/`ValidTo` window.

#### Response — `200 OK`

```json
{
  "schedulesProcessed": 12,
  "inventoriesCreated": 245,
  "inventoriesSkipped": 0,
  "faresCreated": 490
}
```

| Field | Type | Description |
|-------|------|-------------|
| `schedulesProcessed` | integer | Number of schedule records retrieved from the Schedule MS |
| `inventoriesCreated` | integer | Number of new `FlightInventory` records created in the Offer MS |
| `inventoriesSkipped` | integer | Number of operating date/cabin combinations skipped because inventory already existed |
| `faresCreated` | integer | Number of `Fare` records created in the Offer MS |

> **Idempotency:** Calling this endpoint multiple times is safe. Existing inventory records are never overwritten or duplicated — only new records are created.

> **Inventory is immediately live:** Created `FlightInventory` and `Fare` records are immediately available for offer search via the Offer MS.

#### Error Responses

| Status | Reason |
|--------|--------|
| `500 Internal Server Error` | Downstream microservice call failed (Schedule MS, Seat MS, or Offer MS returned an error) |

---

### GET /v1/flights/{flightNumber}/status

Public endpoint — returns real-time flight status derived from the Offer microservice's flight inventory. No authentication required (function name does not start with `Admin`, so the `TerminalAuthenticationMiddleware` is bypassed).

**When to use:** Called by the Angular web app's flight status page to display departure/arrival times, gate, terminal, aircraft type, delay information, and operational status for a given flight on a given date.

#### Request

No request body.

| Path Parameter | Type | Required | Description |
|----------------|------|----------|-------------|
| `flightNumber` | string | Yes | Flight number, e.g. `"AX001"` |

| Query Parameter | Type | Required | Description |
|-----------------|------|----------|-------------|
| `date` | string (date) | No | Departure date in `yyyy-MM-dd` format. Defaults to today (UTC) |

#### Response — `200 OK`

```json
{
  "flightNumber": "AX001",
  "origin": "LHR",
  "destination": "JFK",
  "scheduledDepartureDateTime": "2026-05-15T08:00:00Z",
  "scheduledArrivalDateTime": "2026-05-15T11:10:00Z",
  "estimatedDepartureDateTime": "2026-05-15T08:00:00Z",
  "estimatedArrivalDateTime": "2026-05-15T11:10:00Z",
  "status": "OnTime",
  "gate": null,
  "terminal": null,
  "aircraftType": "A351",
  "delayMinutes": 0,
  "statusMessage": "Flight is on time — 42% booked"
}
```

| Field | Type | Nullable | Description |
|-------|------|----------|-------------|
| `flightNumber` | string | No | Flight number |
| `origin` | string | No | IATA 3-letter departure airport code |
| `destination` | string | No | IATA 3-letter arrival airport code |
| `scheduledDepartureDateTime` | string (ISO 8601) | No | Scheduled departure date-time UTC |
| `scheduledArrivalDateTime` | string (ISO 8601) | No | Scheduled arrival date-time UTC |
| `estimatedDepartureDateTime` | string (ISO 8601) | Yes | Estimated departure (same as scheduled unless delayed) |
| `estimatedArrivalDateTime` | string (ISO 8601) | Yes | Estimated arrival (same as scheduled unless delayed) |
| `status` | string | No | One of: `OnTime`, `Delayed`, `Boarding`, `Departed`, `Landed`, `Cancelled` |
| `gate` | string | Yes | Departure gate (null if not yet assigned) |
| `terminal` | string | Yes | Departure terminal (null if not yet assigned) |
| `aircraftType` | string | No | Aircraft type code, e.g. `"A351"` |
| `delayMinutes` | integer | No | Delay in minutes (`0` if on time) |
| `statusMessage` | string | No | Human-readable status message including load factor |

#### Orchestration flow

1. Parse and validate `flightNumber` (path) and `date` (query, default today).
2. Call Offer MS `GET /v1/flights/{flightNumber}/inventory?departureDate={date}`.
3. If the Offer MS returns `404 Not Found`, return `404` to the caller.
4. Map the inventory response to the `FlightStatus` response shape — derive `status` from inventory status (`Cancelled` → `Cancelled`, otherwise `OnTime`), compose ISO 8601 date-times from departure/arrival date and time fields, and include the load factor in the status message.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing or invalid `flightNumber`, or `date` not in `yyyy-MM-dd` format |
| `404 Not Found` | No inventory exists in the Offer MS for the given flight number and date |
| `500 Internal Server Error` | Downstream Offer MS call failed |

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

---

## Disruption handling

The Operations API receives IROPS events from the Flight Operations System (FOS) and orchestrates the reservation system's response across the Offer, Order, Delivery, and Customer microservices.

> **Important:** The disruption endpoints are not channel-facing. They are called exclusively by FOS via internal service-to-service communication authenticated with an Azure Function Host Key (`x-functions-key`).

### POST /v1/disruptions/delay

Notify the reservation system of a flight delay. Processing is **synchronous** — the response is returned after all affected passenger records have been updated.

#### Business logic

1. Validate payload and perform idempotency check on `disruptionEventId`.
2. Write a `DisruptionEvent` record with `Status = Received`.
3. Call Order MS `GET /v1/orders?flightNumber={flightNumber}&departureDate={departureDate}&status=Confirmed` to retrieve affected bookings.
4. For each affected order:
   - Call Order MS `PATCH /v1/orders/{bookingRef}/segments` with updated times.
   - Call Delivery MS `PATCH /v1/manifest/{bookingRef}/flight` with updated times.
   - If delay exceeds the material schedule change threshold (default: 60 minutes): call Delivery MS `POST /v1/tickets/reissue` and update Order MS with new e-ticket numbers.
   - Queue a passenger notification.
5. Update `DisruptionEvent` to `Status = Completed` and return `200 OK`.

#### Request

```json
{
  "disruptionEventId": "FOS-DLY-20260320-AX101",
  "flightNumber": "AX101",
  "departureDate": "2026-03-22",
  "newDepartureTime": "2026-03-22T16:30:00Z",
  "newArrivalTime": "2026-03-22T19:45:00Z",
  "delayMinutes": 90,
  "reason": "ATC restrictions"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `disruptionEventId` | string | Yes | FOS-supplied unique event identifier; used for idempotency |
| `flightNumber` | string | Yes | Carrier code + flight number, e.g. `AX101` |
| `departureDate` | string (date) | Yes | ISO 8601 date of the scheduled departure |
| `newDepartureTime` | string (datetime) | Yes | Updated departure time in ISO 8601 UTC |
| `newArrivalTime` | string (datetime) | Yes | Updated arrival time in ISO 8601 UTC |
| `delayMinutes` | integer | Yes | Delay duration in minutes relative to original scheduled departure |
| `reason` | string | Yes | Reason for the delay |

#### Response — `200 OK`

```json
{
  "disruptionEventId": "FOS-DLY-20260320-AX101",
  "status": "Completed",
  "affectedPassengerCount": 186,
  "processedPassengerCount": 186,
  "ticketsReissued": true,
  "receivedAt": "2026-03-20T14:05:00Z",
  "completedAt": "2026-03-20T14:05:47Z"
}
```

#### Error responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid format |
| `422 Unprocessable Entity` | Flight not found in the system |
| `500 Internal Server Error` | Downstream microservice call failed |

---

### POST /v1/disruptions/cancellation

Notify the reservation system of a flight cancellation. Inventory closure is **synchronous** (before `202 Accepted` is returned). Per-passenger rebooking is **asynchronous** via Service Bus.

#### Business logic — synchronous phase

1. Validate payload and perform idempotency check on `disruptionEventId`.
2. Call Offer MS `PATCH /v1/inventory/cancel` to close the flight immediately.
3. Write a `DisruptionEvent` record with `Status = Received`.
4. Publish a rebooking job to Service Bus.
5. Return `202 Accepted`.

#### Business logic — asynchronous phase

1. Set `DisruptionEvent.Status = Processing`.
2. Query affected orders (Order MS) and retrieve full manifest (Delivery MS).
3. Search for replacement flights: direct first, then connecting via LHR hub (same logic as Retail API `POST /v1/search/connecting`).
4. Process passengers in priority order: cabin class (F→J→W→Y) → loyalty tier (Platinum→Gold→Silver→Blue) → booking date (earliest first).
5. For each affected order, if a replacement is found: hold inventory, adjust reward points if applicable (Customer MS), rebook order, remove old manifest, reissue e-tickets, write new manifest, notify passenger.
6. If no replacement found within the 72-hour lookahead: cancel with full IROPS refund, void e-tickets, notify passenger.
7. Update `DisruptionEvent` to `Status = Completed` or `Failed`.

#### Request

```json
{
  "disruptionEventId": "FOS-CXL-20260320-AX205",
  "flightNumber": "AX205",
  "departureDate": "2026-03-22",
  "origin": "LHR",
  "destination": "CDG",
  "reason": "Aircraft technical failure"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `disruptionEventId` | string | Yes | FOS-supplied unique event identifier; used for idempotency |
| `flightNumber` | string | Yes | Carrier code + flight number |
| `departureDate` | string (date) | Yes | ISO 8601 date of the scheduled departure |
| `origin` | string | Yes | IATA 3-letter departure airport code |
| `destination` | string | Yes | IATA 3-letter arrival airport code |
| `reason` | string | Yes | Reason for the cancellation |

#### Response — `202 Accepted`

```json
{
  "disruptionEventId": "FOS-CXL-20260320-AX205",
  "status": "Received",
  "affectedPassengerCount": 142,
  "receivedAt": "2026-03-20T08:30:00Z",
  "message": "Flight taken off sale. Passenger rebooking queued for processing."
}
```

#### Error responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid format |
| `422 Unprocessable Entity` | Flight not found in inventory |
| `500 Internal Server Error` | Failed to close inventory or write event record |

---

---

## Admin disruption management

Staff-facing endpoints called by the Terminal app when operations staff act on a disrupted flight from the inventory screen. All routes require a valid staff JWT (`Authorization: Bearer <token>`) issued by the Admin API.

> **Note:** These endpoints are distinct from the FOS-facing `POST /v1/disruptions/*` endpoints. The admin disruption endpoints are called by human operators; FOS disruption endpoints are called by automated systems.

### POST /v1/admin/disruption/cancel

Cancel a flight and synchronously rebook all affected passengers in a single API call. The endpoint closes inventory, processes every affected booking in IROPS priority order, and returns a full per-passenger outcome summary.

#### Business logic

1. Call Offer MS `PATCH /v1/inventory/cancel` to close the flight immediately.
2. Retrieve flight details from Offer MS.
3. Query all confirmed orders on the flight from Order MS.
4. Retrieve the full passenger manifest from Delivery MS.
5. Sort passengers: cabin class (F=highest) → loyalty tier (Platinum=highest) → booking date (earliest first).
6. Search for replacement options across the 72-hour lookahead window: direct flights first, then connecting via LHR hub (60-min minimum connection time).
7. For each booking in priority order:
   - Find the best available replacement matching or upgrading cabin class.
   - Hold inventory on the replacement flight(s); decrement tracked in-memory availability to prevent over-allocation across bookings.
   - If the booking is a reward booking, reinstate any surplus points to the customer's loyalty account (Customer MS) if the replacement costs fewer points; absorb any additional points cost (IROPS policy — no charge to customer).
   - Rebook the order (Order MS `PATCH /v1/orders/{bookingRef}/rebook` with `reason=FlightCancellation`).
   - Delete old manifest entries (Delivery MS).
   - Reissue e-tickets (Delivery MS `POST /v1/tickets/reissue`).
   - Write new manifest entries per replacement leg (Delivery MS `POST /v1/manifest`).
   - If no replacement available within 72 hours: void e-tickets, release held inventory, cancel with full IROPS refund.
8. Return `200 OK` with aggregated counts and per-booking outcomes.

#### Request

```json
{
  "flightNumber": "AX205",
  "departureDate": "2026-04-25",
  "reason": "Aircraft technical failure"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `flightNumber` | string | Yes | Carrier code + flight number, e.g. `AX205` |
| `departureDate` | string (date) | Yes | Scheduled departure date in `yyyy-MM-dd` format |
| `reason` | string | No | Free-text reason for the cancellation; logged on each order |

#### Response — `200 OK`

```json
{
  "flightNumber": "AX205",
  "departureDate": "2026-04-25",
  "affectedPassengerCount": 142,
  "rebookedCount": 128,
  "cancelledWithRefundCount": 12,
  "failedCount": 2,
  "outcomes": [
    {
      "bookingReference": "AX12345",
      "outcome": "Rebooked",
      "replacementFlightNumber": "AX207",
      "replacementDepartureDate": "2026-04-25"
    },
    {
      "bookingReference": "AX12346",
      "outcome": "CancelledWithRefund"
    },
    {
      "bookingReference": "AX12347",
      "outcome": "Failed",
      "failureReason": "Order MS rebook call returned 500"
    }
  ],
  "processedAt": "2026-04-25T09:15:32.0000000Z"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `flightNumber` | string | Cancelled flight number |
| `departureDate` | string (date) | Cancelled flight departure date |
| `affectedPassengerCount` | integer | Total number of confirmed bookings on the flight |
| `rebookedCount` | integer | Bookings successfully rebooked onto a replacement flight |
| `cancelledWithRefundCount` | integer | Bookings cancelled with full IROPS refund (no replacement available) |
| `failedCount` | integer | Bookings that could not be processed due to a downstream error |
| `outcomes` | array | Per-booking processing outcome |
| `outcomes[].bookingReference` | string | PNR for this booking |
| `outcomes[].outcome` | string | `"Rebooked"`, `"CancelledWithRefund"`, or `"Failed"` |
| `outcomes[].replacementFlightNumber` | string \| null | Replacement flight number if rebooked; `null` otherwise |
| `outcomes[].replacementDepartureDate` | string \| null | Replacement departure date if rebooked; `null` otherwise |
| `outcomes[].failureReason` | string \| null | Downstream error detail if `outcome = "Failed"`; `null` otherwise |
| `processedAt` | string (datetime) | UTC timestamp when all bookings were processed |

#### Error responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid format |
| `404 Not Found` | Flight not found in inventory |
| `422 Unprocessable Entity` | Flight already cancelled |
| `500 Internal Server Error` | Unexpected failure |

---

### POST /v1/admin/disruption/change

> **Not yet implemented.** Returns `501 Not Implemented`. Stub endpoint reserved for future aircraft type change disruption handling.

When implemented, this endpoint will handle the operational rebooking flow when an aircraft type change results in cabin reconfiguration that affects passenger seat assignments.

#### Request

```json
{
  "flightNumber": "AX205",
  "departureDate": "2026-04-25",
  "newAircraftType": "A319",
  "reason": "Aircraft substitution"
}
```

---

### POST /v1/admin/disruption/time

> **Not yet implemented.** Returns `501 Not Implemented`. Stub endpoint reserved for future flight time change disruption handling.

When implemented, this endpoint will propagate a staff-initiated time change across all affected orders and manifests, including e-ticket reissuance where required by IATA ticketing rules.

#### Request

```json
{
  "flightNumber": "AX205",
  "departureDate": "2026-04-25",
  "newDepartureTime": "14:30",
  "newArrivalTime": "17:45",
  "reason": "ATC slot change"
}
```

---

### Disruption business rules

| Rule | Detail |
|------|--------|
| Idempotency | Events with a `disruptionEventId` already in the event log are acknowledged immediately without re-processing |
| E-ticket reissuance threshold | Default 60 minutes; configurable via application settings |
| Rebooking priority | Cabin class (F→J→W→Y) → loyalty tier (Platinum→Gold→Silver→Blue) → booking date (earliest first) |
| IROPS fare override | `reason=FlightCancellation` on Order MS rebook call waives all fare restrictions |
| IROPS refund policy | No suitable replacement within 72-hour lookahead = full fare refund regardless of fare conditions |
| Reward booking IROPS | Airline absorbs additional points cost if replacement costs more; reinstates difference if cheaper |
| Per-passenger Service Bus messages | Each passenger published as an individual message to prevent single-failure blocking the cohort |

---

### Disruption downstream service dependencies

| Service | Endpoints Called | Purpose |
|---------|-----------------|---------|
| **Offer MS** | `PATCH /v1/inventory/cancel`, `POST /v1/search`, `POST /v1/inventory/hold` | Close inventory, search replacements, hold seats |
| **Order MS** | `GET /v1/orders`, `PATCH /v1/orders/{bookingRef}/segments`, `PATCH /v1/orders/{bookingRef}/rebook` | Query affected orders, update times, rebook passengers |
| **Delivery MS** | `PATCH /v1/manifest/{bookingRef}/flight`, `GET /v1/manifest`, `DELETE /v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}`, `POST /v1/tickets/reissue`, `POST /v1/manifest` | Update/retrieve/remove manifests, reissue e-tickets |
| **Customer MS** | `POST /v1/customers/{loyaltyNumber}/points/reinstate` | Reinstate surplus points on reward bookings |

---

## Related Documentation

- [API Endpoint Reference](../api-reference.md) — Summary of all microservice endpoints
- [System Design](../system-overview.md) — Full domain design including sequence diagrams for schedule creation and inventory generation flows
- [Disruption Design](../design/disruption.md) — Detailed disruption domain design, sequence diagrams, and business rules
- [Schedule Microservice](schedule-microservice.md) — API specification for the Schedule MS (downstream persistence layer)
