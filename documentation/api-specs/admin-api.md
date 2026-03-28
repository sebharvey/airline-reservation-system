# Admin API — API Specification

> **Service owner:** Admin domain (orchestration layer)
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Admin API is the orchestration entry point for internal staff applications (Contact Centre App, Airport App, Accounting System App, Ops Admin App). It coordinates the User microservice (employee credentials, account state, JWT issuance) to provide a unified authentication interface and user management capabilities for staff-facing channels. The Admin API owns no database tables — all persistence is delegated to the User microservice it orchestrates.

> **Important:** Staff-facing applications must never call the User microservice directly. All staff authentication and user management routes through this API.

---

## Security

### Authentication

1. Unauthenticated staff applications call `POST /v1/auth/login` to obtain a JWT access token (15-minute TTL).
2. The application sends the access token as `Bearer {accessToken}` in the `Authorization` header on all subsequent protected requests.
3. On forwarding to the User microservice, the Admin API uses the Azure Function Host Key in the `x-functions-key` header. End-user JWTs are **never** forwarded to microservices.

### Required headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes (for request bodies) | Must be `application/json` |
| `Authorization` | Yes (on all authenticated endpoints) | `Bearer {accessToken}` — JWT with 15-minute TTL issued by User MS via this API |
| `X-Correlation-ID` | Yes | UUID generated at the channel boundary; propagated on every downstream call for distributed tracing and log correlation |

### Data protection

- Credentials (username, password) are owned exclusively by the User microservice. The Admin API never persists them.
- `passwordHash` is never returned in any response.
- Account lockout state is enforced by the User MS — the Admin API surfaces `403 Forbidden` when the User MS returns one.

---

## Downstream service dependencies

| Service | Endpoints called | Purpose |
|---------|-----------------|---------|
| **User MS** | `POST /v1/users/login` | Credential validation, account state checks, and JWT issuance for staff login |
| **User MS** | `GET /v1/users` | List all employee user accounts |
| **User MS** | `GET /v1/users/{userId}` | Get a single employee user account |
| **User MS** | `POST /v1/users` | Create a new employee user account |
| **User MS** | `PATCH /v1/users/{userId}` | Update user profile fields |
| **User MS** | `PATCH /v1/users/{userId}/status` | Activate or deactivate a user account |
| **User MS** | `POST /v1/users/{userId}/unlock` | Unlock a locked user account |
| **User MS** | `POST /v1/users/{userId}/reset-password` | Reset a user's password |

---

## Token behaviour

The Admin API issues no tokens of its own. It proxies the JWT returned by the User MS directly to the calling application.

| Concern | Value |
|---------|-------|
| Token type | JWT (HMAC-SHA256) |
| Lifetime | 15 minutes |
| Stored server-side | No |
| Refresh tokens | Not supported — staff applications must re-authenticate on expiry |
| Account lockout threshold | 5 consecutive failed login attempts |

---

## Endpoints

### POST /v1/auth/login

Authenticate a staff member with their username and password. Delegates to the User MS. Returns a JWT access token on success.

**Route:** `POST /api/v1/auth/login`
**Auth:** Public — no Bearer token required

#### Request body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `username` | `string` | Yes | Employee login username |
| `password` | `string` | Yes | Plain-text password (transmitted over TLS; never persisted) |

```json
{
  "username": "jsmith",
  "password": "s3cr3tP@ss"
}
```

#### Response body — 200 OK

| Field | Type | Description |
|-------|------|-------------|
| `accessToken` | `string` | Signed JWT access token; 15-minute TTL |
| `userId` | `guid` | The authenticated employee's `UserId` |
| `expiresAt` | `datetime` | UTC expiry timestamp of the access token (ISO 8601) |
| `tokenType` | `string` | Always `"Bearer"` |

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "userId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "expiresAt": "2026-03-28T12:15:00Z",
  "tokenType": "Bearer"
}
```

#### Error responses

| Status | Condition |
|--------|-----------|
| `400 Bad Request` | `username` or `password` field missing or empty |
| `401 Unauthorized` | Credentials invalid (username not found or password incorrect) |
| `403 Forbidden` | Account is locked (5+ consecutive failed attempts) or inactive |
| `500 Internal Server Error` | Unexpected downstream failure |

---

### GET /v1/admin/users

Retrieve all employee user accounts. Passwords are never included.

**Route:** `GET /api/v1/admin/users`
**Auth:** Bearer token required (staff JWT with `role: User`)

#### Response body — 200 OK

Array of user objects:

| Field | Type | Description |
|-------|------|-------------|
| `userId` | `guid` | Unique identifier |
| `username` | `string` | Employee login username |
| `email` | `string` | Employee email address |
| `firstName` | `string` | Employee first name |
| `lastName` | `string` | Employee last name |
| `isActive` | `boolean` | Whether the account is active |
| `isLocked` | `boolean` | Whether the account is locked |
| `lastLoginAt` | `datetime?` | UTC timestamp of last successful login; `null` if never logged in |
| `createdAt` | `datetime` | UTC timestamp when account was created |

---

### GET /v1/admin/users/{userId}

Retrieve a single employee user account by ID.

**Route:** `GET /api/v1/admin/users/{userId:guid}`
**Auth:** Bearer token required (staff JWT with `role: User`)

#### Response body — 200 OK

Same shape as a single element from the list endpoint above.

#### Error responses

| Status | Condition |
|--------|-----------|
| `404 Not Found` | User does not exist |

---

### POST /v1/admin/users

Create a new employee user account.

**Route:** `POST /api/v1/admin/users`
**Auth:** Bearer token required (staff JWT with `role: User`)

#### Request body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `username` | `string` | Yes | Employee login username (max 100 chars) |
| `email` | `string` | Yes | Employee email (max 254 chars, RFC 5321) |
| `password` | `string` | Yes | Initial password |
| `firstName` | `string` | Yes | First name (max 100 chars) |
| `lastName` | `string` | Yes | Last name (max 100 chars) |

#### Response body — 201 Created

| Field | Type | Description |
|-------|------|-------------|
| `userId` | `guid` | The new user's unique identifier |

#### Error responses

| Status | Condition |
|--------|-----------|
| `400 Bad Request` | Missing or invalid fields |
| `409 Conflict` | Username or email already exists |

---

### PATCH /v1/admin/users/{userId}

Update an employee user account's profile fields. All fields are optional; only supplied fields are updated.

**Route:** `PATCH /api/v1/admin/users/{userId:guid}`
**Auth:** Bearer token required (staff JWT with `role: User`)

#### Request body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `firstName` | `string` | No | Updated first name |
| `lastName` | `string` | No | Updated last name |
| `email` | `string` | No | Updated email |

#### Response — 204 No Content

#### Error responses

| Status | Condition |
|--------|-----------|
| `400 Bad Request` | No fields supplied or validation error |
| `404 Not Found` | User does not exist |
| `409 Conflict` | Email already in use by another user |

---

### PATCH /v1/admin/users/{userId}/status

Activate or deactivate an employee user account.

**Route:** `PATCH /api/v1/admin/users/{userId:guid}/status`
**Auth:** Bearer token required (staff JWT with `role: User`)

#### Request body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `isActive` | `boolean` | Yes | `true` to activate, `false` to deactivate |

#### Response — 204 No Content

#### Error responses

| Status | Condition |
|--------|-----------|
| `404 Not Found` | User does not exist |

---

### POST /v1/admin/users/{userId}/unlock

Unlock a locked employee user account and reset failed login attempts to zero.

**Route:** `POST /api/v1/admin/users/{userId:guid}/unlock`
**Auth:** Bearer token required (staff JWT with `role: User`)

#### Response — 204 No Content

#### Error responses

| Status | Condition |
|--------|-----------|
| `404 Not Found` | User does not exist |

---

### POST /v1/admin/users/{userId}/reset-password

Reset an employee user's password. Also unlocks the account and clears failed login attempts.

**Route:** `POST /api/v1/admin/users/{userId:guid}/reset-password`
**Auth:** Bearer token required (staff JWT with `role: User`)

#### Request body

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `newPassword` | `string` | Yes | The new password |

#### Response — 204 No Content

#### Error responses

| Status | Condition |
|--------|-----------|
| `400 Bad Request` | Password field missing or invalid |
| `404 Not Found` | User does not exist |

---

## Login flow — orchestration detail

```
Staff App
  │
  ▼
Admin API ─────────────────────────────────────────────────
  │
  ├── POST /v1/auth/login ──────────► User MS: POST /v1/users/login
  │   { username, password }              └─ validates username exists and is active
  │                                        └─ checks account not locked
  │                                        └─ verifies SHA-256 password hash
  │                                        └─ resets FailedLoginAttempts on success
  │                                        └─ increments FailedLoginAttempts on failure
  │                                        └─ locks account if threshold reached
  │                                        └─ returns { accessToken, userId, expiresAt }
  │◄──────────────────────────────────────────────────────────────────────────────────
  │   adds tokenType: "Bearer"
  │   returns { accessToken, userId, expiresAt, tokenType } to staff app
```

---

## Configuration

The Admin API reads the following settings at startup:

| Key | Description |
|-----|-------------|
| `UserMs:BaseUrl` | Base URL of the User microservice Azure Function |
| `UserMs:HostKey` | Azure Function Host Key for service-to-service authentication; retrieved from Azure Key Vault via managed identity at runtime |
| `UserMs:JwtSecret` | Base64-encoded HMAC-SHA256 key for staff JWT validation (used by TerminalAuthenticationMiddleware on admin endpoints) |
| `UserMs:JwtIssuer` | Expected JWT issuer (default: `apex-air-user`) |
| `UserMs:JwtAudience` | Expected JWT audience (default: `apex-air-reservation`) |
