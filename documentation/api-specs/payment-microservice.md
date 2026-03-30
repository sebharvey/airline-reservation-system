# Payment Microservice — API Specification

> **Service owner:** Payment domain
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Payment microservice is the financial orchestration layer for all Apex Air transactions. It manages payment initialisation, card authorisation, settlement, void, and refund operations. A single booking generates multiple independent payment transactions (fare, seat ancillary, bag ancillary) — each initialised, authorised, and settled separately with its own `PaymentId` (GUID).

> **Important:** The Payment microservice is an internal service. It is not called directly by channels (Web, App, NDC). All channel-facing requests are routed through the **Retail API** orchestration layer, which handles JWT validation before forwarding calls to this service. See the [Security](#security) section for details on how calls are authenticated.

> **Payment gateway integration (future):** In a production environment the Payment microservice will call an external payment gateway (e.g. Adyen, Stripe, Worldpay) to perform card authorisation, settlement, void, and refund operations against the acquiring bank. This integration is not yet implemented. The current codebase contains placeholder comments in the authorise, settle, void, and refund handlers at the points where the gateway call should be made. When the gateway adapter is built it will sit behind an `IPaymentGateway` interface so the provider can be swapped without changing business logic.

---

## Security

### Authentication

The Payment microservice sits behind the Retail API orchestration layer. Channels authenticate via the Retail API using OAuth 2.0 / OIDC:

1. The channel obtains a JWT access token by calling the Identity microservice via the Retail API.
2. The channel sends the access token as a `Bearer` token in the `Authorization` header on all subsequent Retail API requests.
3. The Retail API validates the JWT signature using the Identity microservice's public signing key (RS256 or ES256) — no database round-trip required.
4. Once validated, the Retail API forwards the request to the Payment microservice.

Calls from the Retail API to the Payment microservice are authenticated using an **Azure Function Host Key**, passed in the `x-functions-key` HTTP header. The Payment microservice itself does not validate JWTs; that responsibility belongs to the Retail API. See [Microservice Authentication — Host Keys](../api.md#microservice-authentication--host-keys) in `api.md` for the full mechanism, key storage, and retrieval details.

### Required Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes (for request bodies) | Must be `application/json` |
| `Authorization` | Yes (at Retail API layer) | `Bearer {accessToken}` — JWT with 15-minute TTL, validated by the Retail API before the request reaches the Payment MS |
| `x-functions-key` | Yes (on all Retail API → Payment MS calls) | Azure Function Host Key authenticating the Retail API as an authorised caller. See [`api.md` — Microservice Authentication](../api.md#microservice-authentication--host-keys) for key storage and retrieval details |
| `X-Correlation-ID` | Yes | UUID generated at the channel boundary; propagated on every downstream call for distributed tracing and log correlation |

### Data Protection

- **PCI DSS compliance:** Full card numbers (PAN) and CVV values are held in memory only during authorisation processing and are never persisted to the database or written to logs. Only `CardLast4` and `CardType` are stored.
- Card data is handled and discarded within the Payment microservice boundary — no downstream service ever receives full card details.
- `PaymentId` UUIDs are the only payment identifiers that should appear in log entries and telemetry.

---

## Data Schema

### payment.Payment

| Column | Type | Notes |
|--------|------|-------|
| `PaymentId` | UNIQUEIDENTIFIER PK | Generated at initialisation; returned to Retail API |
| `BookingReference` | CHAR(6), nullable | Set once order confirmed |
| `PaymentType` | VARCHAR(30) | `Fare` / `SeatAncillary` / `BagAncillary` / `FareChange` / `Cancellation` / `Refund` |
| `Method` | VARCHAR(20) | `CreditCard` / `DebitCard` / `PayPal` / `ApplePay` |
| `CardType` | VARCHAR(20), nullable | `Visa` / `Mastercard` / `Amex`; null until authorisation |
| `CardLast4` | CHAR(4), nullable | Last 4 digits only — full PAN never stored; null until authorisation |
| `CurrencyCode` | CHAR(3), default `GBP` | |
| `Amount` | DECIMAL(10,2) | Intended payment amount, set at initialisation |
| `AuthorisedAmount` | DECIMAL(10,2), nullable | Set at authorisation; may equal `Amount` |
| `SettledAmount` | DECIMAL(10,2), nullable | May differ from `AuthorisedAmount` |
| `Status` | VARCHAR(20) | `Initialised` / `Authorised` / `Settled` / `PartiallySettled` / `Refunded` / `Declined` / `Voided` |
| `AuthorisedAt` | DATETIME2, nullable | Null until authorisation |
| `SettledAt` | DATETIME2, nullable | Null until settlement |
| `Description` | VARCHAR(255), nullable | |
| `CreatedAt` | DATETIME2 | **Read-only — generated by a SQL trigger on insert.** |
| `UpdatedAt` | DATETIME2 | **Read-only — updated automatically by a SQL trigger on every row modification.** |

### payment.PaymentEvent

| Column | Type | Notes |
|--------|------|-------|
| `PaymentEventId` | UNIQUEIDENTIFIER PK | |
| `PaymentId` | UNIQUEIDENTIFIER FK | References `payment.Payment` |
| `EventType` | VARCHAR(20) | `Authorised` / `Settled` / `PartialSettlement` / `Refunded` / `Declined` / `Voided` |
| `Amount` | DECIMAL(10,2) | |
| `CurrencyCode` | CHAR(3), default `GBP` | |
| `Notes` | VARCHAR(255), nullable | |
| `CreatedAt` | DATETIME2 | **Read-only — generated by a SQL trigger on insert.** |
| `UpdatedAt` | DATETIME2 | **Read-only — updated automatically by a SQL trigger on every row modification.** |

> **Lifecycle:** A `PaymentEvent` row is created when the payment is authorised and updated when the payment is subsequently settled or voided. Refund operations create a new `PaymentEvent` row.

---

## Endpoints

### POST /v1/payment/initialise

Initialise a payment with order details. Creates a `Payment` record with `Status = Initialised` and returns a `PaymentId` (GUID) for use in subsequent authorise, settle, void, or refund calls.

**When to use:** As the first step in the payment flow. The Retail API calls this endpoint once per payment line (fare, seat ancillary, bag ancillary) to create the payment record before card authorisation.

#### Request

```json
{
  "bookingReference": "AB1234",
  "paymentType": "Fare",
  "method": "CreditCard",
  "currencyCode": "GBP",
  "amount": 459.99,
  "description": "Fare payment — AX003 LHR-JFK, Economy Flex"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bookingReference` | string | No | Booking reference if known at this point; may be null during initial booking flow |
| `paymentType` | string | Yes | One of: `Fare`, `SeatAncillary`, `BagAncillary`, `FareChange`, `Cancellation`, `Refund` |
| `method` | string | Yes | Payment method: `CreditCard`, `DebitCard`, `PayPal`, `ApplePay` |
| `currencyCode` | string | Yes | ISO 4217 currency code, e.g. `"GBP"` |
| `amount` | number | Yes | Amount to be paid, e.g. `459.99` |
| `description` | string | No | Human-readable description of the payment. Max 255 characters |

#### Response — `201 Created`

```json
{
  "paymentId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "amount": 459.99,
  "status": "Initialised"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `paymentId` | string (uuid) | Unique payment identifier for all subsequent operations |
| `amount` | number | Intended payment amount |
| `status` | string | Payment status — `Initialised` on success |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid field format |

---

### POST /v1/payment/{paymentId}/authorise

Authorise a card payment against the payment provider. Updates the `Payment` record with card details and sets `Status = Authorised`. Creates a `PaymentEvent` row with `EventType = Authorised`.

Supports **partial authorisation**: when `amount` is provided, only that portion is authorised and `AuthorisedAmount` accumulates across multiple calls. This enables independent auth+settle cycles (e.g. fare and seat) against a single initialised payment. May be called from `Initialised` or `PartiallySettled` status.

**When to use:** During booking confirmation, after initialisation. The Retail API calls this endpoint with the `paymentId` and card details.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `paymentId` | string (uuid) | The payment ID returned from the prior initialise call |

#### Request

```json
{
  "amount": 400.00,
  "cardDetails": {
    "cardNumber": "4111111111111234",
    "expiryDate": "12/28",
    "cvv": "737",
    "cardholderName": "Amara Okafor"
  }
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `amount` | number | No | Amount to authorise. When omitted, the full remaining uninitialised balance is authorised. Must be greater than zero when provided |
| `cardDetails` | object | Yes | Card details object (see below) |
| `cardDetails.cardNumber` | string | Yes | Full card number (PAN). Held in memory only — never persisted |
| `cardDetails.expiryDate` | string | Yes | Card expiry in `MM/YY` format |
| `cardDetails.cvv` | string | Yes | Card verification value. Held in memory only — never persisted |
| `cardDetails.cardholderName` | string | Yes | Name as printed on the card |

> **PCI DSS:** The full `cardNumber` and `cvv` are held in memory only for the duration of the authorisation call. Only `CardLast4` (last 4 digits of the PAN) and `CardType` (derived from BIN range) are persisted to the database.

#### Response — `200 OK`

```json
{
  "paymentId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "authorisedAmount": 400.00,
  "status": "Authorised"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `paymentId` | string (uuid) | The payment identifier |
| `authorisedAmount` | number | Cumulative authorised amount after this call |
| `status` | string | Payment status — `Authorised` on success |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid field format, `amount` ≤ 0, or card number fails Luhn validation |
| `404 Not Found` | No payment found for the given `paymentId` |
| `409 Conflict` | Payment is not in `Initialised` or `PartiallySettled` status — cannot be authorised in its current state |
| `422 Unprocessable Entity` | Card declined by payment provider — insufficient funds, expired card, or fraud check failure |

---

### POST /v1/payment/{paymentId}/settle

Settle a previously authorised payment, capturing the funds. Updates the `Payment` record status to `Settled` and updates the existing `PaymentEvent` row with `EventType = Settled`. The `settledAmount` may differ from the `authorisedAmount` (e.g. partial settlement).

**When to use:** After booking confirmation and e-ticket issuance. The Retail API calls this endpoint to capture funds once the order is fully confirmed.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `paymentId` | string (uuid) | The payment ID returned from the prior initialise call |

#### Request

```json
{
  "settledAmount": 459.99
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `settledAmount` | number | Yes | Amount to settle. May be less than or equal to the authorised amount |

#### Response — `200 OK`

```json
{
  "paymentId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "settledAmount": 459.99,
  "settledAt": "2026-03-17T14:23:15Z"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `paymentId` | string (uuid) | The payment identifier |
| `settledAmount` | number | Amount captured |
| `settledAt` | string (datetime) | ISO 8601 UTC timestamp when settlement occurred |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid format, or `settledAmount` exceeds `authorisedAmount` |
| `404 Not Found` | No payment found for the given `paymentId` |
| `409 Conflict` | Payment has already been settled, refunded, or voided |

---

### POST /v1/payment/{paymentId}/void

Void a previously authorised payment, releasing the held funds. Updates the `Payment` record status to `Voided` and updates the existing `PaymentEvent` row with `EventType = Voided`. A void can only be performed on a payment in `Authorised` status — once settled, a refund must be used instead.

**When to use:** When an authorised payment needs to be cancelled before settlement — for example, when a downstream step in the booking confirmation flow fails (ticketing failure, inventory failure) and the card hold must be released.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `paymentId` | string (uuid) | The payment ID returned from the prior initialise call |

#### Request

```json
{
  "reason": "TicketingFailure"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `reason` | string | No | Optional reason for the void, e.g. `TicketingFailure`, `InventoryFailure`, `BookingFailure`, `CustomerRequest` |

#### Response — `200 OK`

```json
{
  "paymentId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "status": "Voided"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `paymentId` | string (uuid) | The payment identifier |
| `status` | string | Updated payment status — `Voided` |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Invalid `paymentId` format |
| `404 Not Found` | No payment found for the given `paymentId` |
| `409 Conflict` | Payment is not in `Authorised` status — cannot void a payment that has already been settled, refunded, or voided |

---

### POST /v1/payment/{paymentId}/refund

Refund a settled payment in full or in part. Updates the `Payment` record status to `Refunded` (or `PartiallySettled` for partial refunds) and creates a new `PaymentEvent` row with `EventType = Refunded`.

**When to use:** Used for automated reversals during booking flow failures — for example, when ticketing or inventory settlement fails after card authorisation and settlement. Voluntary cancellation refunds initiated by the customer are handled by the Accounting system, not this endpoint.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `paymentId` | string (uuid) | The payment ID of the settled payment to refund |

#### Request

```json
{
  "refundAmount": 459.99,
  "reason": "TicketingFailure"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `refundAmount` | number | Yes | Amount to refund. May be less than or equal to the settled amount |
| `reason` | string | Yes | Reason for the refund, e.g. `TicketingFailure`, `InventoryFailure`, `BookingFailure` |

#### Response — `200 OK`

```json
{
  "paymentId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "refundedAmount": 459.99,
  "status": "Refunded"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `paymentId` | string (uuid) | The payment identifier |
| `refundedAmount` | number | Amount refunded |
| `status` | string | Updated payment status — `Refunded` for full refunds |

> **Scope:** This endpoint handles automated reversals during booking flow failures only. Voluntary cancellation refunds (initiated by the customer via the manage-booking flow) are processed by the Accounting system, which manages its own refund lifecycle.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid format, or `refundAmount` exceeds `settledAmount` |
| `404 Not Found` | No payment found for the given `paymentId` |
| `409 Conflict` | Payment has already been fully refunded or voided |

---

## Data Conventions

| Convention | Format | Example |
|------------|--------|---------|
| Timestamps | ISO 8601 UTC | `"2025-08-15T11:00:00Z"` |
| Currency codes | ISO 4217 | `"GBP"` |
| Currency amounts | Decimal, 2 places | `459.99` |
| JSON field names | camelCase | `paymentId` |
| UUIDs | RFC 4122 lowercase | `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"` |

---

## Invocation Examples

### Initialising a fare payment (via Retail API booking flow)

```bash
curl -X POST https://{payment-ms-host}/v1/payment/initialise \
  -H "Content-Type: application/json" \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "bookingReference": "AB1234",
    "paymentType": "Fare",
    "method": "CreditCard",
    "currencyCode": "GBP",
    "amount": 459.99,
    "description": "Fare payment — AX003 LHR-JFK, Economy Flex"
  }'
```

### Authorising a payment (with card details)

```bash
curl -X POST https://{payment-ms-host}/v1/payment/a1b2c3d4-e5f6-7890-abcd-ef1234567890/authorise \
  -H "Content-Type: application/json" \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "cardDetails": {
      "cardNumber": "4111111111111234",
      "expiryDate": "12/28",
      "cvv": "737",
      "cardholderName": "Amara Okafor"
    }
  }'
```

### Settling a payment (after booking confirmation)

```bash
curl -X POST https://{payment-ms-host}/v1/payment/a1b2c3d4-e5f6-7890-abcd-ef1234567890/settle \
  -H "Content-Type: application/json" \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "settledAmount": 459.99
  }'
```

### Voiding a payment (on downstream failure before settlement)

```bash
curl -X POST https://{payment-ms-host}/v1/payment/a1b2c3d4-e5f6-7890-abcd-ef1234567890/void \
  -H "Content-Type: application/json" \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "reason": "TicketingFailure"
  }'
```

### Refunding a payment (on downstream failure after settlement)

```bash
curl -X POST https://{payment-ms-host}/v1/payment/a1b2c3d4-e5f6-7890-abcd-ef1234567890/refund \
  -H "Content-Type: application/json" \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "refundAmount": 459.99,
    "reason": "TicketingFailure"
  }'
```

> **Note:** The `Authorization: Bearer` header is required when calling via the Retail API (the channel-facing route). Calls from the Retail API directly to the Payment microservice are authenticated using the `x-functions-key` header — the end-user JWT is not forwarded. See [`api.md` — Microservice Authentication](../api.md#microservice-authentication--host-keys) for the full host key mechanism.

---

## Related Documentation

- [API Endpoint Reference](../api-reference.md) — Summary of all microservice endpoints
- [System Design](../system-overview.md) — Full domain design including sequence diagrams for booking and payment flows
