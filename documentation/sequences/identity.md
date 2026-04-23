# Identity — sequence diagrams

Covers authentication flows for loyalty members (via the Loyalty API) and staff/agents (via the Admin API). All identity operations are delegated to the Identity microservice.

---

## Loyalty member login

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant IdentityMS as Identity MS
    participant CustomerMS as Customer MS

    Web->>LoyaltyAPI: POST /v1/auth/login
    Note over Web,LoyaltyAPI: {email, password}
    LoyaltyAPI->>IdentityMS: POST /api/v1/auth/login
    Note over LoyaltyAPI,IdentityMS: Validates email + password;<br/>issues JWT + refresh token
    IdentityMS-->>LoyaltyAPI: {accessToken, refreshToken,<br/>expiresAt, userAccountId}

    LoyaltyAPI->>CustomerMS: GET /api/v1/customers/by-identity/{userAccountId}
    CustomerMS-->>LoyaltyAPI: CustomerProfile (loyaltyNumber, tier, pointsBalance)

    LoyaltyAPI-->>Web: LoginResponse
    Note over LoyaltyAPI,Web: {accessToken, refreshToken,<br/>expiresAt, tokenType=Bearer,<br/>loyaltyNumber}

    Note over Web: Store tokens in LoyaltyStateService<br/>(localStorage/sessionStorage)
```

---

## Loyalty member logout

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant IdentityMS as Identity MS

    Web->>LoyaltyAPI: POST /v1/auth/logout
    Note over Web,LoyaltyAPI: Bearer token in Authorization header
    LoyaltyAPI->>IdentityMS: POST /api/v1/auth/logout
    Note over LoyaltyAPI,IdentityMS: Revoke/invalidate tokens
    IdentityMS-->>LoyaltyAPI: 204 No Content
    LoyaltyAPI-->>Web: 204 No Content
    Note over Web: Clear LoyaltyStateService (tokens, profile)
```

---

## Password reset request

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant IdentityMS as Identity MS

    Web->>LoyaltyAPI: POST /v1/auth/password/reset-request
    Note over Web,LoyaltyAPI: {email}
    LoyaltyAPI->>IdentityMS: POST /api/v1/auth/password/reset-request
    Note over LoyaltyAPI,IdentityMS: Sends one-time reset code to email
    IdentityMS-->>LoyaltyAPI: 204 No Content
    LoyaltyAPI-->>Web: 204 No Content
```

---

## Password reset

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant IdentityMS as Identity MS

    Web->>LoyaltyAPI: POST /v1/auth/password/reset
    Note over Web,LoyaltyAPI: {email, resetToken, newPassword}
    LoyaltyAPI->>IdentityMS: POST /api/v1/auth/password/reset
    Note over LoyaltyAPI,IdentityMS: Validates token and updates password
    IdentityMS-->>LoyaltyAPI: 204 No Content
    LoyaltyAPI-->>Web: 204 No Content
```

---

## Email change

The email change flow involves a request step (sends verification to the new address) and a confirmation step (verifies the token and applies the change in both Identity MS and Customer MS).

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant IdentityMS as Identity MS
    participant CustomerMS as Customer MS

    Web->>LoyaltyAPI: POST /v1/customers/{loyaltyNumber}/email/change-request
    Note over Web,LoyaltyAPI: {newEmail}
    LoyaltyAPI->>IdentityMS: POST /api/v1/auth/email/change-request
    Note over LoyaltyAPI,IdentityMS: Sends verification token to new email
    IdentityMS-->>LoyaltyAPI: 204 No Content
    LoyaltyAPI-->>Web: 204 No Content

    Web->>LoyaltyAPI: POST /v1/auth/email/confirm
    Note over Web,LoyaltyAPI: {token, newEmail}
    LoyaltyAPI->>IdentityMS: POST /api/v1/auth/email/confirm
    Note over LoyaltyAPI,IdentityMS: Validate token; update email in Identity MS
    IdentityMS-->>LoyaltyAPI: {identityId, newEmail}
    LoyaltyAPI->>CustomerMS: PATCH /api/v1/customers/{loyaltyNumber}/email
    Note over LoyaltyAPI,CustomerMS: Sync new email to Customer MS
    CustomerMS-->>LoyaltyAPI: Updated
    LoyaltyAPI-->>Web: 204 No Content
```

---

## Loyalty member registration

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS
    participant IdentityMS as Identity MS

    Web->>LoyaltyAPI: POST /v1/register
    Note over Web,LoyaltyAPI: {givenName, surname, dateOfBirth,<br/>email, password, phoneNumber,<br/>nationality, preferredLanguage?}

    LoyaltyAPI->>CustomerMS: POST /api/v1/customers
    Note over LoyaltyAPI,CustomerMS: Create customer record<br/>(no identity link yet)
    CustomerMS-->>LoyaltyAPI: {loyaltyNumber, tierCode}

    LoyaltyAPI->>IdentityMS: POST /api/v1/accounts
    Note over LoyaltyAPI,IdentityMS: Create identity account (email + password)
    IdentityMS-->>LoyaltyAPI: {userAccountId}

    LoyaltyAPI->>CustomerMS: PATCH /api/v1/customers/{loyaltyNumber}/identity
    Note over LoyaltyAPI,CustomerMS: Link identityId to customer record
    CustomerMS-->>LoyaltyAPI: Linked

    LoyaltyAPI->>CustomerMS: POST /api/v1/customers/{loyaltyNumber}/points
    Note over LoyaltyAPI,CustomerMS: Award 1,500 sign-up bonus points<br/>(transactionType=Earn)
    CustomerMS-->>LoyaltyAPI: Points awarded

    LoyaltyAPI->>CustomerMS: GET /api/v1/customers/{loyaltyNumber}
    CustomerMS-->>LoyaltyAPI: FullCustomerProfile

    LoyaltyAPI-->>Web: ProfileResponse
    Note over LoyaltyAPI,Web: {loyaltyNumber, givenName, surname,<br/>tier, pointsBalance, memberSince}
```

---

## Staff login (Admin API)

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant AdminAPI as Admin API
    participant UserMS as User MS

    Terminal->>AdminAPI: POST /v1/auth/login
    Note over Terminal,AdminAPI: {username, password}
    AdminAPI->>UserMS: POST /api/v1/auth/login
    Note over AdminAPI,UserMS: Validate credentials, check account<br/>not locked/inactive, issue JWT
    UserMS-->>AdminAPI: {accessToken, userId, expiresAt}
    AdminAPI-->>Terminal: LoginResponse
    Note over AdminAPI,Terminal: {accessToken, userId,<br/>expiresAt, tokenType=Bearer}
```
