# Authentication and token flow

This document describes how access tokens and refresh tokens are issued, verified, and rotated across the Loyalty orchestration layer and the Identity microservice.

---

## Overview

Authentication is a two-layer concern:

| Layer | Responsibility |
|-------|---------------|
| **Loyalty API** (orchestration) | Public-facing entry point. Issues tokens to clients, enforces Bearer auth on protected routes via middleware, delegates all token logic to Identity MS. |
| **Identity MS** (microservice) | Issues JWTs, verifies them, rotates refresh tokens, manages account state. Never called directly by clients. |

Loyalty only communicates with the Identity and Customer microservices. It performs no direct SQL and calls no other microservice.

---

## Token types

### Access token (JWT)

- Format: signed JWT (HMAC-SHA256).
- Claims: `sub` (UserAccountId), `email`, `jti`, `iat`, `exp`.
- Lifetime: 15 minutes (configurable via `Jwt:AccessTokenExpiryMinutes`).
- Not stored server-side — verified by validating the signature and expiry, then checking account status in the Identity DB.
- Sent as `Authorization: Bearer <token>` on every protected request.

### Refresh token

- Format: 32 cryptographically random bytes, Base64-encoded.
- Stored as SHA-256 hash in the Identity DB (`identity.RefreshToken`).
- Lifetime: 30 days (`ExpiresAt` column).
- Single-use rotation: each use revokes the presented token and issues a new one.
- Only sent to `POST /v1/auth/refresh`.

---

## Token flow

```
Client
  │
  ▼
Loyalty API  ──────────────────────────────────────────────────────────
  │                          (only talks to Identity MS or Customer MS)
  ├── POST /v1/auth/login ──────────► Identity MS: POST /v1/auth/login
  │                                        └─ validates credentials
  │                                        └─ issues accessToken + refreshToken
  │                                        └─ returns {accessToken, refreshToken, userAccountId}
  │◄──────────────────────────────────────────────────────────────────
  │   returns tokens to client
  │
  ├── GET /v1/customers/{loyaltyNumber}/profile
  │   Authorization: Bearer {accessToken}
  │   │
  │   ├── [Loyalty middleware intercepts Bearer token]
  │   ├── POST /v1/auth/verify ────► Identity MS: POST /v1/auth/verify
  │   │                                  └─ decodes Base64 payload
  │   │                                  └─ checks UserAccountId exists & not locked
  │   │                                  └─ checks token not expired (ticks TTL check)
  │   │                                  └─ returns {valid: true, userAccountId, email}
  │   │◄──────────────────────────────────────────────────────────────
  │   ├── [inject userAccountId into request context]
  │   └── CustomerServiceClient.GetProfile(loyaltyNumber)
  │
  ├── POST /v1/auth/refresh ─────────► Identity MS: POST /v1/auth/refresh
  │   {refreshToken}                       └─ looks up hash in DB
  │                                        └─ checks not revoked, not expired
  │                                        └─ revokes old token (single-use rotation)
  │                                        └─ issues NEW accessToken + refreshToken
  │◄──────────────────────────────────────────────────────────────────
  │   returns new token pair to client
```

---

## Access token vs refresh token

| Concern | Access token | Refresh token |
|---------|-------------|---------------|
| Lifetime | 15 min (JWT `exp` claim) | 30 days (`ExpiresAt` in DB) |
| Stored server-side | No | Yes (hash in Identity DB) |
| Verified by | Identity MS `POST /v1/auth/verify` | Identity MS `POST /v1/auth/refresh` (DB lookup) |
| Revocable mid-flight | Only via account lock (`IsLocked = true`) | Yes, immediately via `IsRevoked = true` |
| Rotation | New one issued on each refresh | Single-use — new one per call to `/v1/auth/refresh` |
| Sent on every request | Yes (`Authorization: Bearer`) | No — only on `/v1/auth/refresh` |

---

## Endpoints

### Loyalty API (public-facing)

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/auth/login` | Authenticate with email and password. Returns `accessToken`, `refreshToken`, `expiresAt`, `tokenType`. |
| `POST` | `/v1/auth/refresh` | Exchange a valid refresh token for a new token pair. Single-use rotation. |
| `POST` | `/v1/auth/logout` | Revoke a refresh token. |

### Identity MS (internal — Loyalty to Identity only)

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/auth/login` | Validate credentials, issue JWT access token and refresh token. |
| `POST` | `/v1/auth/verify` | Validate a JWT access token and return its claims. Called by Loyalty middleware on every protected request. |
| `POST` | `/v1/auth/refresh` | Rotate a refresh token and issue a new JWT access token. |
| `POST` | `/v1/auth/logout` | Revoke a refresh token by hash. |

---

## Protected vs public routes

The `TokenVerificationMiddleware` in the Loyalty API intercepts every HTTP request. Routes are classified by their Azure Functions function name:

| Function name | Public? |
|---------------|---------|
| `Login` | Yes |
| `RefreshToken` | Yes |
| `Logout` | Yes |
| `PasswordResetRequest` | Yes |
| `PasswordReset` | Yes |
| `RegisterMember` | Yes |
| All others | **No — requires valid Bearer token** |

When a protected route receives a request without a valid token, the middleware short-circuits with `401 Unauthorized` before the function executes.

---

## Middleware

`TokenVerificationMiddleware` (`Loyalty/Middleware/TokenVerificationMiddleware.cs`) implements `IFunctionsWorkerMiddleware`:

1. Check if the function name is in the public list. If so, pass through.
2. Extract `Authorization: Bearer <token>` from the request headers.
3. Call `IdentityServiceClient.VerifyTokenAsync(token)` → `POST /v1/auth/verify` on Identity MS.
4. If the response is not `200 OK` or `valid = false`, return `401 Unauthorized` and short-circuit.
5. Store `UserAccountId` and `Email` in `FunctionContext.Items` for downstream handlers.
6. Call `next(context)` to continue execution.

---

## Service-to-service authentication

Loyalty calls Identity MS using a named `HttpClient` (`"IdentityMs"`) configured in `Program.cs`. The `x-functions-key` host key is added as a default request header:

```
IdentityMs:BaseUrl   — base URL of the Identity Azure Function
IdentityMs:HostKey   — Azure Functions host key for service-to-service auth
```

---

## JWT configuration (Identity MS)

Configured via the `Jwt` section in `appsettings.json` or environment variables:

| Key | Description | Default |
|-----|-------------|---------|
| `Jwt:Secret` | Base64-encoded 256-bit HMAC-SHA256 signing secret. **Required.** | — |
| `Jwt:Issuer` | JWT `iss` claim value. | `apex-air-identity` |
| `Jwt:Audience` | JWT `aud` claim value. | `apex-air-loyalty` |
| `Jwt:AccessTokenExpiryMinutes` | Access token lifetime in minutes. | `15` |

The same `Secret`, `Issuer`, and `Audience` values must be consistent across all environments. Rotate the secret to invalidate all outstanding access tokens immediately.

---

## Account lockout and token invalidation

- After **5 consecutive failed logins**, `IsLocked = true` is set on the `UserAccount`.
- `VerifyTokenHandler` checks `IsLocked` on every token verification — a locked account causes immediate `401` even if the JWT itself is still within its expiry window.
- Logout revokes the refresh token. The access token remains valid until it expires (maximum 15 minutes). To force immediate invalidation, lock the account.
