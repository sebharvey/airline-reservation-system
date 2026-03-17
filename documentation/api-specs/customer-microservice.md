# Customer Microservice — API Specification

> **Service owner:** Customer domain
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Customer microservice is the system of record for loyalty programme membership. It manages customer profiles, tier status, points balances, and transaction history. Accounts are identified by a unique loyalty number (e.g. `AX9876543`) issued at registration.

> **Important:** The Customer microservice is an internal service. It is not called directly by channels (Web, App, NDC). All channel-facing requests are routed through the **Loyalty API** orchestration layer, which handles JWT validation before forwarding calls to this service. See the [Security](#security) section for details on how calls are authenticated.

---

## Security

### Authentication

The Customer microservice sits behind the Loyalty API orchestration layer. Channels authenticate via the Loyalty API using OAuth 2.0 / OIDC:

1. The channel obtains a JWT access token by calling `POST /v1/auth/login` on the Loyalty API.
2. The channel sends the access token as a `Bearer` token in the `Authorization` header on all subsequent Loyalty API requests.
3. The Loyalty API validates the JWT signature using the Identity microservice's public signing key (RS256 or ES256) — no database round-trip required.
4. Once validated, the Loyalty API forwards the request to the Customer microservice.

Internal service-to-service calls (Loyalty API → Customer MS) use **managed identities** or **scoped API keys** — no static credentials. The Customer microservice itself does not validate JWTs; that responsibility belongs to the Loyalty API.

### Required Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes (for request bodies) | Must be `application/json` |
| `Authorization` | Yes (at Loyalty API layer) | `Bearer {accessToken}` — JWT with 15-minute TTL, validated by the Loyalty API before the request reaches the Customer MS |
| `X-Correlation-ID` | Yes | UUID generated at the channel boundary; propagated on every downstream call for distributed tracing and log correlation |

### Data Protection

- PII (names, dates of birth, phone numbers, nationality) must never appear in logs, telemetry, or error messages. Use anonymised identifiers (`LoyaltyNumber`, `CustomerId`) in log entries.
- The Customer microservice does **not** store email addresses or passwords — those are owned exclusively by the Identity microservice. The two domains are linked only by an opaque `IdentityReference` UUID.

---

## Endpoints

### POST /v1/customers

Create a new loyalty account. Called by the Loyalty API during the registration flow after the Identity microservice has created the login account.

**When to use:** During new member registration only. The Loyalty API orchestrates the two-step creation (Identity account first, then Customer account).

#### Request

```json
{
  "givenName": "Amara",
  "surname": "Okafor",
  "dateOfBirth": "1988-03-22",
  "preferredLanguage": "en-GB",
  "identityReference": "c4f2e8a1-7b3d-4e9f-a6c1-2d8b5e0f3a7c"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `givenName` | string | Yes | Customer's given (first) name. Max 100 characters |
| `surname` | string | Yes | Customer's surname. Max 100 characters |
| `dateOfBirth` | string (date) | No | ISO 8601 date, e.g. `"1988-03-22"` |
| `preferredLanguage` | string | No | BCP 47 language tag, e.g. `"en-GB"`. Defaults to `en-GB` |
| `identityReference` | string (UUID) | No | Opaque reference linking to the Identity microservice account. Null for legacy accounts without a login |

#### Response — `201 Created`

```json
{
  "customerId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "loyaltyNumber": "AX9876543",
  "tierCode": "Blue"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `customerId` | string (UUID) | Unique identifier for the customer record |
| `loyaltyNumber` | string | Unique loyalty programme number issued at creation (e.g. `AX9876543`) |
| `tierCode` | string | Initial tier assignment — always `Blue` for new accounts |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid field format |
| `409 Conflict` | Duplicate `identityReference` — account already exists for this identity |

---

### GET /v1/customers/{loyaltyNumber}

Retrieve a customer's profile, tier status, and points balance. Used by the Loyalty API to serve the loyalty dashboard and to provide loyalty context during booking flows.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `loyaltyNumber` | string | The customer's unique loyalty number, e.g. `AX9876543` |

#### Request

No request body. No query parameters.

```
GET /v1/customers/AX9876543
```

#### Response — `200 OK`

```json
{
  "customerId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "loyaltyNumber": "AX9876543",
  "identityReference": "c4f2e8a1-7b3d-4e9f-a6c1-2d8b5e0f3a7c",
  "givenName": "Amara",
  "surname": "Okafor",
  "dateOfBirth": "1988-03-22",
  "nationality": "GBR",
  "preferredLanguage": "en-GB",
  "phoneNumber": "+447700900123",
  "tierCode": "Silver",
  "pointsBalance": 48250,
  "tierProgressPoints": 62100,
  "isActive": true,
  "createdAt": "2024-06-15T09:30:00Z",
  "updatedAt": "2025-11-02T14:22:00Z"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `customerId` | string (UUID) | Unique customer identifier |
| `loyaltyNumber` | string | Loyalty programme number |
| `identityReference` | string (UUID) | Opaque link to the Identity microservice; `null` for legacy accounts |
| `givenName` | string | Customer's given name |
| `surname` | string | Customer's surname |
| `dateOfBirth` | string (date) | ISO 8601 date |
| `nationality` | string | ISO 3166-1 alpha-3 country code (e.g. `GBR`) |
| `preferredLanguage` | string | BCP 47 language tag |
| `phoneNumber` | string | Phone number including country code |
| `tierCode` | string | Current tier: `Blue`, `Silver`, `Gold`, or `Platinum` |
| `pointsBalance` | integer | Current redeemable points balance |
| `tierProgressPoints` | integer | Qualifying points for tier evaluation — not decremented on redemption |
| `isActive` | boolean | Whether the account is active |
| `createdAt` | string (datetime) | ISO 8601 UTC timestamp of account creation |
| `updatedAt` | string (datetime) | ISO 8601 UTC timestamp of last profile update |

> **`pointsBalance` vs `tierProgressPoints`:** These are separate values. `pointsBalance` is the redeemable currency a member can spend on award bookings. `tierProgressPoints` accumulates qualifying activity for tier evaluation (Blue → Silver → Gold → Platinum) and is not decremented when points are redeemed.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No customer found for the given loyalty number |

---

### PATCH /v1/customers/{loyaltyNumber}

Update profile fields on an existing customer record. Only the fields included in the request body are updated; omitted fields are left unchanged.

**When to use:** When a member updates their profile via the loyalty portal. The Loyalty API validates the JWT before forwarding to this endpoint.

> **Note:** Updating a loyalty profile name does **not** amend any confirmed booking or issued e-ticket. Booking name corrections must go through the manage-booking flow on the Retail API.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `loyaltyNumber` | string | The customer's unique loyalty number |

#### Request

Send only the fields being changed:

```json
{
  "phoneNumber": "+447700900456",
  "preferredLanguage": "fr-FR"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `givenName` | string | No | Max 100 characters |
| `surname` | string | No | Max 100 characters |
| `dateOfBirth` | string (date) | No | ISO 8601 date |
| `nationality` | string | No | ISO 3166-1 alpha-3 country code |
| `phoneNumber` | string | No | Phone number with country code. Max 30 characters |
| `preferredLanguage` | string | No | BCP 47 language tag |

#### Response — `200 OK`

Returns the full updated customer record (same schema as `GET /v1/customers/{loyaltyNumber}`):

```json
{
  "customerId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "loyaltyNumber": "AX9876543",
  "identityReference": "c4f2e8a1-7b3d-4e9f-a6c1-2d8b5e0f3a7c",
  "givenName": "Amara",
  "surname": "Okafor",
  "dateOfBirth": "1988-03-22",
  "nationality": "GBR",
  "preferredLanguage": "fr-FR",
  "phoneNumber": "+447700900456",
  "tierCode": "Silver",
  "pointsBalance": 48250,
  "tierProgressPoints": 62100,
  "isActive": true,
  "createdAt": "2024-06-15T09:30:00Z",
  "updatedAt": "2026-03-17T10:45:00Z"
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Invalid field format (e.g. malformed date, invalid country code) |
| `404 Not Found` | No customer found for the given loyalty number |

---

### GET /v1/customers/{loyaltyNumber}/transactions

Retrieve paginated points transaction history for a customer. Transactions are returned in reverse-chronological order (most recent first).

**When to use:** To display the points statement on the loyalty portal — the immutable audit trail of every points movement on a member's account.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `loyaltyNumber` | string | The customer's unique loyalty number |

#### Query Parameters

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| `page` | integer | No | `1` | Page number (1-based) |
| `pageSize` | integer | No | `20` | Number of transactions per page |

#### Request

```
GET /v1/customers/AX9876543/transactions?page=1&pageSize=20
```

#### Response — `200 OK`

```json
{
  "loyaltyNumber": "AX9876543",
  "page": 1,
  "pageSize": 20,
  "totalCount": 47,
  "transactions": [
    {
      "transactionId": "f7a1b2c3-d4e5-6789-0abc-def123456789",
      "transactionType": "Earn",
      "pointsDelta": 8750,
      "balanceAfter": 48250,
      "bookingReference": "XK7T2P",
      "flightNumber": "AX003",
      "description": "Points earned — AX003 LHR-JFK, Business Flex",
      "transactionDate": "2025-11-02T14:22:00Z"
    },
    {
      "transactionId": "e6b0a1d2-c3f4-5678-9abc-def012345678",
      "transactionType": "Redeem",
      "pointsDelta": -5000,
      "balanceAfter": 39500,
      "bookingReference": "ML9R4J",
      "flightNumber": null,
      "description": "Upgrade to Business Class",
      "transactionDate": "2025-09-18T08:15:00Z"
    },
    {
      "transactionId": "d5c9f0e1-b2a3-4567-89ab-cde901234567",
      "transactionType": "Adjustment",
      "pointsDelta": 2500,
      "balanceAfter": 44500,
      "bookingReference": null,
      "flightNumber": null,
      "description": "Goodwill gesture — disruption on AX301",
      "transactionDate": "2025-08-30T16:40:00Z"
    }
  ]
}
```

#### Transaction Object

| Field | Type | Description |
|-------|------|-------------|
| `transactionId` | string (UUID) | Unique transaction identifier |
| `transactionType` | string | One of: `Earn`, `Redeem`, `Adjustment`, `Expiry`, `Reinstate` |
| `pointsDelta` | integer | Points added (positive) or removed (negative) |
| `balanceAfter` | integer | Running points balance snapshot after this transaction was applied |
| `bookingReference` | string | Associated booking reference, if applicable. `null` for adjustments and expiry |
| `flightNumber` | string | Associated flight number, if applicable (primarily for `Earn` transactions). `null` otherwise |
| `description` | string | Human-readable description of the transaction |
| `transactionDate` | string (datetime) | ISO 8601 UTC timestamp of when the transaction occurred |

#### Transaction Types

| Type | Points Direction | Description |
|------|-----------------|-------------|
| `Earn` | Positive | Points accrued from a completed flight, calculated from fare paid, cabin class, and tier at time of travel. Also used for welcome bonuses |
| `Redeem` | Negative | Points spent on an award booking or cabin upgrade |
| `Adjustment` | Positive or Negative | Manual correction by a customer service agent, always accompanied by a reason |
| `Expiry` | Negative | Points removed due to account inactivity or programme rules |
| `Reinstate` | Positive | Reversal of a previous `Expiry` or erroneous `Redeem` transaction |

> **Sign convention:** `pointsDelta` is positive for credits (`Earn`, `Reinstate`, positive `Adjustment`) and negative for debits (`Redeem`, `Expiry`, negative `Adjustment`). The `balanceAfter` column always reflects the running total after the transaction is applied.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No customer found for the given loyalty number |

---

## Data Conventions

| Convention | Format | Example |
|------------|--------|---------|
| Timestamps | ISO 8601 UTC | `"2025-08-15T11:00:00Z"` |
| Dates | ISO 8601 | `"1988-03-22"` |
| Country codes | ISO 3166-1 alpha-3 | `"GBR"` |
| Language tags | BCP 47 | `"en-GB"` |
| JSON field names | camelCase | `pointsBalance` |
| UUIDs | RFC 4122 lowercase | `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"` |

---

## Invocation Examples

### Creating a customer (via Loyalty API registration flow)

```bash
curl -X POST https://{customer-ms-host}/v1/customers \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "givenName": "Amara",
    "surname": "Okafor",
    "dateOfBirth": "1988-03-22",
    "preferredLanguage": "en-GB",
    "identityReference": "c4f2e8a1-7b3d-4e9f-a6c1-2d8b5e0f3a7c"
  }'
```

### Retrieving a customer profile (via Loyalty API)

```bash
curl -X GET https://{loyalty-api-host}/v1/customers/AX9876543 \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIs..." \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

### Updating profile fields (via Loyalty API)

```bash
curl -X PATCH https://{loyalty-api-host}/v1/customers/AX9876543/profile \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIs..." \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "phoneNumber": "+447700900456",
    "preferredLanguage": "fr-FR"
  }'
```

### Retrieving transaction history (via Loyalty API)

```bash
curl -X GET "https://{loyalty-api-host}/v1/customers/AX9876543/transactions?page=1&pageSize=20" \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIs..." \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

> **Note:** The `Authorization: Bearer` header is required when calling via the Loyalty API (the channel-facing route). Internal service-to-service calls from the Loyalty API to the Customer microservice use managed identities or scoped API keys instead, and do not carry the end-user JWT.

---

## Related Documentation

- [API Endpoint Reference](../api-reference.md) — Summary of all microservice endpoints
- [System Design](../design.md) — Full domain design including sequence diagrams for registration, profile update, and points accrual flows
