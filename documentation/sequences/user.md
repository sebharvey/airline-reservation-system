# User — sequence diagrams

Covers employee user account management via the Admin API. All user management endpoints require a valid staff JWT token (validated by `TerminalAuthenticationMiddleware`). All operations delegate to the User microservice.

---

## Staff login

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant AdminAPI as Admin API
    participant UserMS as User MS

    Terminal->>AdminAPI: POST /v1/auth/login
    Note over Terminal,AdminAPI: {username, password}
    AdminAPI->>UserMS: POST /api/v1/auth/login
    Note over AdminAPI,UserMS: Validate credentials;<br/>check account not locked/inactive;<br/>issue signed JWT
    UserMS-->>AdminAPI: {accessToken, userId, expiresAt}
    AdminAPI-->>Terminal: LoginResponse
    Note over AdminAPI,Terminal: {accessToken, userId,<br/>expiresAt, tokenType=Bearer}
```

---

## List all users

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant AdminAPI as Admin API
    participant UserMS as User MS

    Terminal->>AdminAPI: GET /v1/admin/users
    Note over Terminal,AdminAPI: Bearer token (staff JWT) required
    AdminAPI->>UserMS: GET /api/v1/users
    UserMS-->>AdminAPI: UserList
    AdminAPI-->>Terminal: [UserResponse]
    Note over AdminAPI,Terminal: [{userId, username, email,<br/>firstName, lastName, isActive,<br/>isLocked, lastLoginAt, createdAt}]
```

---

## Get single user

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant AdminAPI as Admin API
    participant UserMS as User MS

    Terminal->>AdminAPI: GET /v1/admin/users/{userId}
    AdminAPI->>UserMS: GET /api/v1/users/{userId}
    UserMS-->>AdminAPI: UserRecord (or 404)
    AdminAPI-->>Terminal: UserResponse (or 404 Not Found)
```

---

## Create user

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant AdminAPI as Admin API
    participant UserMS as User MS

    Terminal->>AdminAPI: POST /v1/admin/users
    Note over Terminal,AdminAPI: {username, email, password,<br/>firstName, lastName}
    AdminAPI->>UserMS: POST /api/v1/users
    Note over AdminAPI,UserMS: username and email uniqueness<br/>enforced by User MS
    UserMS-->>AdminAPI: {userId} (or 409 Conflict)
    AdminAPI-->>Terminal: 201 Created {userId} (or 409 Conflict)
```

---

## Update user

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant AdminAPI as Admin API
    participant UserMS as User MS

    Terminal->>AdminAPI: PATCH /v1/admin/users/{userId}
    Note over Terminal,AdminAPI: Partial update: {firstName?,<br/>lastName?, email?}
    AdminAPI->>UserMS: PATCH /api/v1/users/{userId}
    UserMS-->>AdminAPI: 204 No Content (or 404/409)
    AdminAPI-->>Terminal: 204 No Content
```

---

## Set user active/inactive status

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant AdminAPI as Admin API
    participant UserMS as User MS

    Terminal->>AdminAPI: PATCH /v1/admin/users/{userId}/status
    Note over Terminal,AdminAPI: {isActive: true|false}
    Note over AdminAPI: Guard: cannot deactivate own account
    AdminAPI->>UserMS: PATCH /api/v1/users/{userId}/status
    UserMS-->>AdminAPI: 204 No Content (or 404/403)
    AdminAPI-->>Terminal: 204 No Content
```

---

## Unlock locked account

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant AdminAPI as Admin API
    participant UserMS as User MS

    Terminal->>AdminAPI: POST /v1/admin/users/{userId}/unlock
    AdminAPI->>UserMS: POST /api/v1/users/{userId}/unlock
    Note over AdminAPI,UserMS: Clears lockout; resets failed<br/>login attempt counter
    UserMS-->>AdminAPI: 204 No Content (or 404)
    AdminAPI-->>Terminal: 204 No Content
```

---

## Reset user password (admin)

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant AdminAPI as Admin API
    participant UserMS as User MS

    Terminal->>AdminAPI: POST /v1/admin/users/{userId}/reset-password
    Note over Terminal,AdminAPI: {newPassword}
    AdminAPI->>UserMS: POST /api/v1/users/{userId}/reset-password
    Note over AdminAPI,UserMS: Password complexity enforced by User MS
    UserMS-->>AdminAPI: 204 No Content (or 400/404)
    AdminAPI-->>Terminal: 204 No Content
```

---

## Delete user

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant AdminAPI as Admin API
    participant UserMS as User MS

    Terminal->>AdminAPI: DELETE /v1/admin/users/{userId}
    Note over AdminAPI: Guard: cannot delete own account
    AdminAPI->>UserMS: DELETE /api/v1/users/{userId}
    UserMS-->>AdminAPI: 204 No Content (or 404/403)
    AdminAPI-->>Terminal: 204 No Content
```
