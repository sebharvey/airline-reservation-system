# Loyalty API — API Specification

> **Service owner:** Loyalty domain (orchestration layer)
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Loyalty API is the channel-facing orchestration layer for all loyalty programme interactions. It coordinates the Identity microservice (credential management, authentication, session tokens) and the Customer microservice (profile, tier status, points balances, transaction history) to provide a unified loyalty interface to channels. The Loyalty API does not own any database tables — all persistence is delegated to the microservices it orchestrates.

Channels (Web, App, Contact Centre) authenticate with the Loyalty API using OAuth 2.0 / OIDC. The Loyalty API validates inbound JWTs using the Identity microservice's public signing key (RS256 or ES256) — no database round-trip is required. It forwards authenticated requests downstream using Azure Function Host Keys in the `x-functions-key` header.

> **Important:** Channels must never call the Identity or Customer microservices directly. All loyalty-related channel interactions route through this API.

---

## Security

### Authentication

The Loyalty API is the single entry point for all loyalty-related channel requests.

1. Unauthenticated channels call `POST /v1/auth/login` to obtain a JWT access token (15-minute TTL, RS256/ES256) and a refresh token.
2. The channel sends the access token as `Bearer {accessToken}` in the `Authorization` header on all subsequent requests.
3. The Loyalty API validates the JWT signature using the Identity microservice's public signing key — no round-trip to the Identity MS is required per request.
4. On forwarding to downstream microservices (Identity MS, Customer MS), the Loyalty API uses the Azure Function Host Key in the `x-functions-key` header. End-user JWTs are **never** forwarded to microservices.

### Required Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes (for request bodies) | Must be `application/json` |
| `Authorization` | Yes (on all authenticated endpoints) | `Bearer {accessToken}` — JWT with 15-minute TTL issued by Identity MS via this API |
| `X-Correlation-ID` | Yes | UUID generated at the channel boundary; propagated on every downstream call for distributed tracing and log correlation |

### Data Protection

- PII (names, dates of birth, phone numbers, nationality) must never appear in logs or telemetry. Log entries use anonymised identifiers (`LoyaltyNumber`, `CustomerId`).
- Email addresses and passwords are owned exclusively by the Identity microservice. The Loyalty API never persists credentials.
- The Loyalty API must not reveal whether an email address is registered when handling unauthenticated requests (enumeration protection). All password reset requests return `202 Accepted` regardless of whether the address is known.

---

## Downstream Service Dependencies

| Service | Endpoints Called | Purpose |
|---------|-----------------|---------|
| **Identity MS** | `POST /v1/auth/login`, `POST /v1/auth/refresh`, `POST /v1/auth/logout`, `POST /v1/auth/password/reset-request`, `POST /v1/auth/password/reset`, `POST /v1/accounts`, `DELETE /v1/accounts/{userAccountId}`, `POST /v1/accounts/{userAccountId}/verify-email`, `POST /v1/accounts/{identityReference}/email/change-request`, `POST /v1/email/verify` | All credential management, session management, and email verification operations |
| **Customer MS** | `POST /v1/customers`, `GET /v1/customers/{loyaltyNumber}`, `GET /v1/customers/by-identity/{identityId}`, `PATCH /v1/customers/{loyaltyNumber}`, `DELETE /v1/customers/{loyaltyNumber}`, `GET /v1/customers/{loyaltyNumber}/transactions` | All profile, tier, points balance, and transaction history operations |

---

## Registration Flow — Orchestration Detail

Registration is a three-step orchestration with defined rollback responsibilities. The Customer record is created first (without an `identityReference`), then the Identity account, then the Customer record is patched to link the returned `identityReference`.

**Step 1:** `POST /v1/customers` on Customer MS — creates the loyalty account (`identityReference = null`). Returns `customerId` and `loyaltyNumber`.

**Step 2:** `POST /v1/accounts` on Identity MS — creates the login account linked to `customerId`. Returns `identityReference` and `userAccountId`.

**Step 3:** `PATCH /v1/customers/{loyaltyNumber}` on Customer MS — links the `identityReference` to the customer record.

**Failure handling:**
- If Step 1 fails: return error immediately. No cleanup required.
- If Step 2 fails: call `DELETE /v1/customers/{loyaltyNumber}` on Customer MS to remove the orphaned customer record, then return error.
- If Step 3 fails: call `DELETE /v1/accounts/{userAccountId}` on Identity MS and `DELETE /v1/customers/{loyaltyNumber}` on Customer MS, then return error.

Partial registration states must never be left in the system.

After all three steps succeed, the Loyalty API triggers a confirmation email to the registered address and returns `201 Created` to the channel.

**Duplicate email handling:** The Identity MS enforces uniqueness on email addresses and returns `409 Conflict` if the address is already registered. The Loyalty API surfaces this as a validation error. It must not reveal that the email belongs to an existing account.

---

## Endpoints

---

### POST /v1/register

Register a new loyalty programme member. Orchestrates the three-step creation of linked Identity and Customer records (see Registration Flow above). On success, the member receives a unique loyalty number, is assigned to the `Blue` tier, and receives a confirmation email.

**When to use:** Called by channels when a new user completes the sign-up form.

#### Request

```json
{
  "givenName": "Amara",
  "surname": "Okafor",
  "dateOfBirth": "1988-03-22",
  "email": "amara.okafor@example.com",
  "password": "correct-horse-battery-staple",
  "preferredLanguage": "en-GB"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `givenName` | string | Yes | Customer's given (first) name. Max 100 characters |
| `surname` | string | Yes | Customer's surname. Max 100 characters |
| `dateOfBirth` | string (date) | No | ISO 8601 date, e.g. `"1988-03-22"` |
| `email` | string | Yes | Email address for login. Must be unique. Max 254 characters (RFC 5321) |
| `password` | string | Yes | Plaintext password; hashed with Argon2id by Identity MS before storage |
| `preferredLanguage` | string | No | BCP 47 language tag, e.g. `"en-GB"`. Defaults to `en-GB` |

#### Response — `201 Created`

```json
{
  "loyaltyNumber": "AX9876543",
  "givenName": "Amara",
  "tierCode": "Blue",
  "message": "Registration successful. Please check your email to verify your account."
}
```

| Field | Type | Description |
|-------|------|-------------|
| `loyaltyNumber` | string | Unique loyalty programme number issued at registration |
| `givenName` | string | Customer's given name, for display purposes |
| `tierCode` | string | Initial tier — always `Blue` for new registrations |
| `message` | string | Human-readable confirmation message |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid field format (e.g. malformed date, invalid language tag) |
| `409 Conflict` | Email address is already registered. Response must not confirm the email belongs to an existing account — return a generic conflict message only |
| `500 Internal Server Error` | Downstream microservice call failed; any partial records created are rolled back per the failure-handling rules above |

---

### POST /v1/auth/login

Authenticate with email and password. Delegates credential validation to the Identity MS. On success, returns a short-lived JWT access token, a single-use refresh token, and the customer's loyalty number looked up from the Customer MS.

**When to use:** Called by channels when a user submits login credentials.

**Side effects:**
- Identity MS resets `FailedLoginAttempts` to 0 and updates `LastLoginAt`.
- Identity MS creates a new `RefreshToken` record.
- After receiving the `userAccountId` from the Identity MS, the Loyalty API calls `GET /v1/customers/by-identity/{identityId}` on the Customer MS to resolve the loyalty number and include it in the response.

**Account lockout:** The Identity MS locks the account (`IsLocked = 1`) after 5 consecutive failed login attempts. Further attempts are rejected with `403 Forbidden` until the account is unlocked via password reset.

#### Request

```json
{
  "email": "amara.okafor@example.com",
  "password": "correct-horse-battery-staple"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `email` | string | Yes | The user's registered email address |
| `password` | string | Yes | The user's plaintext password |

#### Response — `200 OK`

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4...",
  "expiresAt": "2026-03-23T14:30:00Z",
  "tokenType": "Bearer",
  "loyaltyNumber": "AX9876543"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `accessToken` | string | JWT access token with 15-minute TTL, signed with RS256 or ES256 |
| `refreshToken` | string | Opaque single-use refresh token; must be exchanged before expiry |
| `expiresAt` | string (ISO 8601) | UTC expiry time of the access token |
| `tokenType` | string | Always `Bearer` |
| `loyaltyNumber` | string | The customer's loyalty programme number (e.g. `AX9876543`); use this for all subsequent Customer API calls |

#### Error Responses

| Status | Reason |
|--------|--------|
| `401 Unauthorized` | Invalid credentials — email not found or password does not match |
| `403 Forbidden` | Account is locked due to repeated failed login attempts. User must reset password to unlock |

---

### POST /v1/auth/refresh

Exchange a valid refresh token for a new access token and rotated refresh token. Uses single-use rotation semantics — the presented token is revoked and a replacement is issued. Delegates to Identity MS.

**When to use:** Called by channels when the access token has expired and a valid refresh token is available, to re-authenticate without prompting the user.

> **Security:** If a revoked token is presented (possible indicator of token theft), the Identity MS rejects the request and the channel must redirect the user to log in again.

#### Request

```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4..."
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `refreshToken` | string | Yes | The refresh token issued during login or a previous refresh |

#### Response — `200 OK`

```json
{
  "accessToken": "eyJhbGciOiJSUzI1NiIs...",
  "refreshToken": "bmV3IHJlZnJlc2ggdG9rZW4..."
}
```

| Field | Type | Description |
|-------|------|-------------|
| `accessToken` | string | New JWT access token with 15-minute TTL |
| `refreshToken` | string | New opaque refresh token replacing the consumed one |

#### Error Responses

| Status | Reason |
|--------|--------|
| `401 Unauthorized` | Refresh token not found, already revoked, or expired |

---

### POST /v1/auth/logout

Revoke the current refresh token, invalidating the session. Delegates to Identity MS. The JWT access token will continue to be valid until its 15-minute TTL expires — channels should discard it immediately on logout.

**When to use:** Called by channels when a user explicitly logs out.

#### Request

```json
{
  "refreshToken": "dGhpcyBpcyBhIHJlZnJlc2ggdG9rZW4..."
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `refreshToken` | string | Yes | The refresh token to revoke |

#### Response — `200 OK`

No response body. The refresh token is revoked; the session is ended.

#### Error Responses

| Status | Reason |
|--------|--------|
| `401 Unauthorized` | Refresh token not found or already revoked |

---

### POST /v1/auth/password/reset-request

Request a password reset link. Dispatched to the registered email address if found. Response is always `202 Accepted` regardless of whether the email exists, to prevent account enumeration. Delegates to Identity MS, which generates a time-limited single-use reset token (1-hour TTL) and sends the link.

**When to use:** Called by channels when a user requests a password reset from the login screen.

**Enumeration protection:** This endpoint always returns `202 Accepted` — the channel and user receive no indication of whether the address is registered.

#### Request

```json
{
  "email": "amara.okafor@example.com"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `email` | string | Yes | The email address to send the reset link to |

#### Response — `202 Accepted`

No response body. If the address is registered, a reset link has been sent.

#### Error Responses

None — this endpoint always returns `202 Accepted`.

---

### POST /v1/auth/password/reset

Submit a new password using a valid single-use reset token. The Identity MS validates the token, updates the password hash (Argon2id), unlocks the account (`IsLocked = 0`, `FailedLoginAttempts = 0`), and invalidates all active refresh tokens for the account. The user must log in again after a successful reset.

**When to use:** Called by channels when a user follows the password reset link from their email and submits a new password.

**Account unlock:** Setting `IsLocked = 0` on successful password reset is intentional — a legitimate account owner who recovers access via password reset is unblocked automatically.

**Session invalidation:** All active refresh tokens for the account are revoked on success, forcing re-authentication across all sessions and devices.

#### Request

```json
{
  "token": "a1b2c3d4e5f6...",
  "newPassword": "new-correct-horse-battery-staple"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `token` | string | Yes | The single-use reset token received via email |
| `newPassword` | string | Yes | The new plaintext password (will be hashed with Argon2id by Identity MS) |

#### Response — `200 OK`

No response body. Password has been updated; the account is unlocked; all sessions are invalidated.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Reset token is invalid, expired, or has already been used |

---

### GET /v1/customers/{loyaltyNumber}

Retrieve a customer's profile, tier status, and points balance. Delegates to Customer MS. Used by channels to serve the loyalty dashboard and to provide loyalty context during booking flows.

**When to use:** Called after login to populate the loyalty dashboard, or during the booking flow when the loyalty member's context is needed.

**Authorisation:** The JWT must be valid and the `loyaltyNumber` in the path must correspond to the authenticated user's account. Channels must not query profiles for other loyalty numbers.

> **`pointsBalance` vs `tierProgressPoints`:** These are separate values. `pointsBalance` is the redeemable currency a member can spend on award bookings. `tierProgressPoints` accumulates qualifying activity for tier evaluation and is not decremented when points are redeemed.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `loyaltyNumber` | string | The customer's unique loyalty number, e.g. `AX9876543` |

#### Response — `200 OK`

```json
{
  "customerId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "loyaltyNumber": "AX9876543",
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
| `createdAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated on insert |
| `updatedAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated on every update |

#### Error Responses

| Status | Reason |
|--------|--------|
| `401 Unauthorized` | Missing or invalid JWT |
| `404 Not Found` | No customer found for the given loyalty number |

---

### GET /v1/customers/{loyaltyNumber}/transactions

Retrieve paginated points transaction history for a customer. Returns in reverse-chronological order (most recent first). Delegates to Customer MS.

**When to use:** Called by channels to display the points statement on the loyalty portal — the immutable audit trail of every points movement on a member's account.

**Authorisation:** The JWT must be valid and must correspond to the loyalty number in the path.

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
| `balanceAfter` | integer | Running points balance snapshot after this transaction |
| `bookingReference` | string | Associated booking reference, if applicable. `null` for adjustments and expiry |
| `flightNumber` | string | Associated flight number (primarily `Earn` transactions). `null` otherwise |
| `description` | string | Human-readable description |
| `transactionDate` | string (datetime) | ISO 8601 UTC timestamp |

#### Transaction Types

| Type | Points Direction | Description |
|------|-----------------|-------------|
| `Earn` | Positive | Points accrued from a completed flight. Calculated from route miles × cabin multiplier × tier multiplier. Also used for welcome bonuses |
| `Redeem` | Negative | Points spent on an award booking or cabin upgrade |
| `Adjustment` | Positive or Negative | Manual correction by a customer service agent |
| `Expiry` | Negative | Points removed due to account inactivity or programme rules |
| `Reinstate` | Positive | Reversal of a previous `Expiry` or erroneous `Redeem` transaction |

> **Sign convention:** `pointsDelta` is positive for credits (`Earn`, `Reinstate`, positive `Adjustment`) and negative for debits (`Redeem`, `Expiry`, negative `Adjustment`).

#### Error Responses

| Status | Reason |
|--------|--------|
| `401 Unauthorized` | Missing or invalid JWT |
| `404 Not Found` | No customer found for the given loyalty number |

---

### PATCH /v1/customers/{loyaltyNumber}/profile

Update profile fields on an existing customer record. Only fields included in the request body are updated; omitted fields are unchanged. Delegates to Customer MS (`PATCH /v1/customers/{loyaltyNumber}`).

**When to use:** Called when a member updates their profile via the loyalty portal.

**Important:** Updating a loyalty profile name does **not** amend any confirmed booking or issued e-ticket. Those records are independent. Booking name corrections require the manage-booking flow via the Retail API.

**Authorisation:** JWT must be valid. The Loyalty API validates the JWT before forwarding.

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

Returns the full updated customer record in the same schema as `GET /v1/customers/{loyaltyNumber}`.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Invalid field format (e.g. malformed date, invalid country code) |
| `401 Unauthorized` | Missing or invalid JWT |
| `404 Not Found` | No customer found for the given loyalty number |

---

### POST /v1/customers/{loyaltyNumber}/points/authorise

Authorise a points redemption hold against the customer's balance for a reward booking. Places a hold on the required points without deducting them, enabling rollback if downstream steps fail. Returns a `RedemptionReference` used in subsequent settle or reverse calls. Delegates to Customer MS.

**When to use:** Called by the Retail API (not directly by channels) during reward booking confirmation, before authorising the tax-only card payment. If the customer has insufficient points, the entire booking flow is aborted.

> **This endpoint is called from the Retail API's booking orchestration flow — not from channel-facing UI directly.**

**Hold semantics:** Points are marked as held but `PointsBalance` is not decremented. The balance is only decremented when the hold is settled via `/points/settle`.

> **Idempotency:** Repeated calls with the same `basketId` return the same `RedemptionReference` and do not double-hold points.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `loyaltyNumber` | string | The customer's unique loyalty number |

#### Request

```json
{
  "points": 50000,
  "basketId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `points` | integer | Yes | Number of points to authorise (hold against balance) |
| `basketId` | string (UUID) | Yes | The basket identifier for tracking the redemption |

#### Response — `200 OK`

```json
{
  "redemptionReference": "f7a1b2c3-d4e5-6789-0abc-def123456789",
  "pointsAuthorised": 50000,
  "pointsHeld": 50000,
  "authorisedAt": "2026-03-17T14:22:00Z"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `redemptionReference` | string (UUID) | The `TransactionId` of the loyalty transaction record created for this hold. Used as the identifier in subsequent settle/reverse calls |
| `pointsAuthorised` | integer | Number of points placed on hold |
| `pointsHeld` | integer | Running total of points currently held (after this hold) |
| `authorisedAt` | string (datetime) | ISO 8601 UTC timestamp when the hold was placed |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid format |
| `404 Not Found` | No customer found for the given loyalty number |
| `409 Conflict` | Points hold already exists for this basket |
| `422 Unprocessable Entity` | Customer's `PointsBalance` is less than the requested points amount — abort the booking flow |

---

### POST /v1/customers/{loyaltyNumber}/points/settle

Settle a previously authorised points redemption. Deducts the held points from the customer's balance and appends a `Redeem` transaction to the loyalty ledger. Delegates to Customer MS. Called after e-ticket issuance and inventory settlement succeed.

**When to use:** Called by the Retail API during reward booking confirmation, after ticketing and inventory settlement have completed successfully.

> **Idempotency:** Repeated calls with the same `redemptionReference` return the same response without double-deducting points.

> **Failure handling:** If settlement fails after order confirmation, the order remains confirmed but the failure must be flagged for manual reconciliation and retried asynchronously.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `loyaltyNumber` | string | The customer's unique loyalty number |

#### Request

```json
{
  "redemptionReference": "f7a1b2c3-d4e5-6789-0abc-def123456789"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `redemptionReference` | string (UUID) | Yes | The `TransactionId` GUID returned from the prior authorise call |

#### Response — `200 OK`

```json
{
  "redemptionReference": "f7a1b2c3-d4e5-6789-0abc-def123456789",
  "pointsDeducted": 50000,
  "newPointsBalance": 48250,
  "transactionId": "f7a1b2c3-d4e5-6789-0abc-def123456789",
  "settledAt": "2026-03-17T14:23:15Z"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `redemptionReference` | string (UUID) | The `TransactionId` of the loyalty transaction being settled |
| `pointsDeducted` | integer | Number of points deducted from the balance |
| `newPointsBalance` | integer | Updated `PointsBalance` after deduction |
| `transactionId` | string (UUID) | Unique identifier of the `LoyaltyTransaction` record appended to the ledger |
| `settledAt` | string (datetime) | ISO 8601 UTC timestamp when settlement occurred |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid format |
| `404 Not Found` | No customer found or `redemptionReference` does not exist |
| `409 Conflict` | The redemption has already been settled or reversed |

---

### POST /v1/reward/{redemptionReference}/reverse

Reverse a points authorisation hold, returning held points to the customer's available balance. Used when a downstream step fails during reward booking confirmation (e.g. ticketing failure, card payment failure, inventory settlement failure). Delegates to Customer MS (`POST /v1/customers/{loyaltyNumber}/points/reverse`).

**When to use:** Called by the Retail API orchestration layer whenever a step after points authorisation fails during reward booking confirmation, to release the hold and restore the customer's available balance.

**Failure hierarchy:**
- If points authorisation fails → abort booking; no downstream calls needed.
- If card payment authorisation fails → reverse points hold, release inventory, return error.
- If ticketing fails → reverse points hold, void card authorisation, release inventory, return error.
- If points settlement fails after order confirmation → flag for manual reconciliation; order remains confirmed.

> **No ledger entry:** Since the hold was never settled, no `LoyaltyTransaction` record is appended. The hold is simply marked as reversed.

> **Idempotency:** Repeated calls with the same `redemptionReference` return the same response without double-releasing points.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `redemptionReference` | string (UUID) | The `TransactionId` GUID returned from the prior authorise call |

#### Request

```json
{
  "loyaltyNumber": "AX9876543",
  "reason": "TicketingFailure"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `loyaltyNumber` | string | Yes | The customer's unique loyalty number |
| `reason` | string | No | Reason for reversal, e.g. `TicketingFailure`, `PaymentFailure`, `BookingFailure` |

#### Response — `200 OK`

```json
{
  "redemptionReference": "f7a1b2c3-d4e5-6789-0abc-def123456789",
  "pointsReleased": 50000,
  "newPointsBalance": 98250,
  "reversedAt": "2026-03-17T14:23:45Z"
}
```

| Field | Type | Description |
|-------|------|-------------|
| `redemptionReference` | string (UUID) | The `TransactionId` of the loyalty transaction being reversed |
| `pointsReleased` | integer | Number of points returned to available balance |
| `newPointsBalance` | integer | Updated `PointsBalance` after reversal |
| `reversedAt` | string (datetime) | ISO 8601 UTC timestamp when reversal occurred |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid format |
| `404 Not Found` | No customer found or `redemptionReference` does not exist |
| `409 Conflict` | The redemption has already been settled or reversed |

---

### POST /v1/customers/{loyaltyNumber}/email/change-request

Initiate an email address change (step 1 of 2). Validates the new email is not already registered and sends a verification link to the new address. Delegates to Identity MS (`POST /v1/accounts/{identityReference}/email/change-request`). The email is not updated immediately — the user must confirm via the verification link.

**When to use:** Called by channels when a logged-in user requests to change their email address from the profile settings.

**Authorisation:** JWT must be valid. The Loyalty API validates the JWT before forwarding.

**Duplicate address protection:** The Identity MS enforces uniqueness on email addresses and returns `409 Conflict` if the new address belongs to another account.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `loyaltyNumber` | string | The customer's unique loyalty number |

#### Request

```json
{
  "newEmail": "amara.new@example.com"
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `newEmail` | string | Yes | The new email address. Max 254 characters (RFC 5321) |

#### Response — `202 Accepted`

No response body. A verification link has been sent to the new email address.

#### Error Responses

| Status | Reason |
|--------|--------|
| `401 Unauthorized` | Missing or invalid JWT |
| `404 Not Found` | No customer found for the given loyalty number |
| `409 Conflict` | The new email address is already registered to another account |

---

### POST /v1/email/verify

Verify a new email address using a time-limited token (step 2 of 2). Validates the token and completes the email change. On success, all active refresh tokens for the account are invalidated, forcing re-authentication. Delegates to Identity MS (`POST /v1/email/verify`).

**When to use:** Called when the user clicks the verification link in the email change confirmation message.

**Session invalidation:** All active refresh tokens for the account are revoked on success. The user must log in again with the new email address.

#### Request

```json
{
  "token": "f8e7d6c5b4a3..."
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `token` | string | Yes | The single-use verification token received via email |

#### Response — `200 OK`

No response body. The email address has been updated and all active sessions have been invalidated.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Token is invalid, expired, or has already been used |

---

## Points Accrual on Booking Confirmation (Event-Driven)

The Loyalty API does not participate in points accrual directly. When an order is confirmed, the Order MS publishes an `OrderConfirmed` event to the Azure Service Bus. The Customer MS consumes this event and calculates accrual.

**Rules:**
- Points accrue on **revenue bookings only**. Reward bookings (`bookingType=Reward`) do not earn points.
- Points are calculated per flight segment: `pointsEarned = routeMiles × cabinMultiplier × tierMultiplier`.

**Route miles (great-circle distances):**

| Route | Miles |
|-------|-------|
| LHR ↔ JFK | 3,459 |
| LHR ↔ LAX | 5,456 |
| LHR ↔ MIA | 4,432 |
| LHR ↔ SFO | 5,367 |
| LHR ↔ ORD | 3,941 |
| LHR ↔ BOS | 3,269 |
| LHR ↔ BGI | 4,237 |
| LHR ↔ KIN | 4,694 |
| LHR ↔ NAS | 4,341 |
| LHR ↔ HKG | 5,994 |
| LHR ↔ NRT | 5,974 |
| LHR ↔ PVG | 5,741 |
| LHR ↔ PEK | 5,063 |
| LHR ↔ SIN | 6,764 |
| LHR ↔ BOM | 4,479 |
| LHR ↔ DEL | 4,180 |
| LHR ↔ BLR | 5,127 |

Route miles are symmetric — LHR→JFK = JFK→LHR. For connecting itineraries, each segment earns independently.

**Cabin class multipliers:**

| Cabin | Multiplier |
|-------|-----------|
| Economy (Y) | ×1.0 |
| Premium Economy (W) | ×1.5 |
| Business (J) | ×2.0 |
| First (F) | ×3.0 |

**Tier bonus multipliers (applied on top of cabin-adjusted accrual):**

| Tier | Bonus Multiplier |
|------|-----------------|
| Blue | ×1.0 (no bonus) |
| Silver | ×1.25 |
| Gold | ×1.5 |
| Platinum | ×2.0 |

**Full example:** A Platinum-tier member flying LHR→JFK in Business earns `3,459 × 2.0 × 2.0 = 13,836 points`.

The Customer MS appends a `LoyaltyTransaction` (type `Earn`) and atomically updates both `PointsBalance` and `TierProgressPoints`.

> **`PointsBalance` vs `TierProgressPoints`:** `PointsBalance` is the redeemable currency available to spend. `TierProgressPoints` accumulates qualifying activity for tier evaluation and is never decremented when points are redeemed.

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

### Register a new member

```bash
curl -X POST https://{loyalty-api-host}/v1/register \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "givenName": "Amara",
    "surname": "Okafor",
    "dateOfBirth": "1988-03-22",
    "email": "amara.okafor@example.com",
    "password": "correct-horse-battery-staple",
    "preferredLanguage": "en-GB"
  }'
```

### Log in

```bash
curl -X POST https://{loyalty-api-host}/v1/auth/login \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "email": "amara.okafor@example.com",
    "password": "correct-horse-battery-staple"
  }'
```

### Retrieve profile

```bash
curl -X GET https://{loyalty-api-host}/v1/customers/AX9876543 \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIs..." \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

### Update profile

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

### Retrieve transaction history

```bash
curl -X GET "https://{loyalty-api-host}/v1/customers/AX9876543/transactions?page=1&pageSize=20" \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIs..." \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

### Request a password reset

```bash
curl -X POST https://{loyalty-api-host}/v1/auth/password/reset-request \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "email": "amara.okafor@example.com"
  }'
```

### Complete a password reset

```bash
curl -X POST https://{loyalty-api-host}/v1/auth/password/reset \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "token": "a1b2c3d4e5f6...",
    "newPassword": "new-correct-horse-battery-staple"
  }'
```

### Request an email change

```bash
curl -X POST https://{loyalty-api-host}/v1/customers/AX9876543/email/change-request \
  -H "Authorization: Bearer eyJhbGciOiJSUzI1NiIs..." \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "newEmail": "amara.new@example.com"
  }'
```

### Confirm an email change

```bash
curl -X POST https://{loyalty-api-host}/v1/email/verify \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "token": "f8e7d6c5b4a3..."
  }'
```

### Reverse a points authorisation (called by Retail API on booking failure)

```bash
curl -X POST https://{loyalty-api-host}/v1/reward/f7a1b2c3-d4e5-6789-0abc-def123456789/reverse \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "loyaltyNumber": "AX9876543",
    "reason": "TicketingFailure"
  }'
```

---

## Related Documentation

- [API Endpoint Reference](../api-reference.md) — Summary of all orchestration API and microservice endpoints
- [System Design](../system-overview.md) — Full domain design including sequence diagrams for registration, login, password reset, email change, and points accrual flows
- [Customer Microservice Specification](customer-microservice.md) — Full spec for the Customer MS (downstream from this API)
- [Identity Microservice Specification](identity-microservice.md) — Full spec for the Identity MS (downstream from this API)
