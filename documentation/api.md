# API Development Guide

This guide describes how to build backend APIs for the Apex Air Reservation System. Read `../CLAUDE.md` and `system-overview.md` first to understand the domain model before writing any code.

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 8, Azure Functions v4 (isolated worker) |
| Architecture | Clean architecture — Domain, Application, Infrastructure, Models, Functions layers |
| Database | Azure SQL (Dapper, no ORM) |
| Serialisation | `System.Text.Json` with camelCase naming |
| HTTP | Azure Functions HTTP trigger (no ASP.NET routing middleware) |
| DI | Built-in `Microsoft.Extensions.DependencyInjection` |
| Shared infrastructure | `ReservationSystem.Shared.Common` — generic utility code (HTTP helpers, JSON options, SQL connection, pagination, guard clauses) |
| Shared business logic | `ReservationSystem.Shared.Business` — domain-aware code shared across services (field validation, JWT, password hashing, staff auth middleware) |

---

## Project Types

There are two categories of API project, placed in different directories under `src/API/`:

| Type | Directory | Purpose |
|------|-----------|---------|
| `Microservices` | `src/API/Microservices/` | Standalone domain API that owns a single bounded context and its data store |
| `Orchestration` | `src/API/Orchestration/` | Cross-domain API that coordinates calls across multiple microservices within a single request flow |

Microservices must never call other microservices. All synchronous cross-domain coordination is the exclusive responsibility of orchestration APIs. See `principles/architecture-principals.md`.

---

## Shared Libraries

All APIs (microservices and orchestration) live under `src/API/Shared/` and reference two class libraries:

### ReservationSystem.Shared.Common

Generic, technology-level utilities with **no knowledge of the airline domain**. Use these for cross-cutting infrastructure concerns that are the same in every project.

| Namespace | Contents | When to use |
|-----------|----------|-------------|
| `Shared.Common.Http` | `HttpRequestExtensions`, `HttpResponseExtensions`, `HttpResponseMessageExtensions`, `ApiError`, `CorrelationId`, `IdempotencyKey` | Reading requests, writing standardised responses, propagating correlation IDs and idempotency keys |
| `Shared.Common.Health` | `HealthCheckFunction`, `HealthCheckService`, `HealthCheckExtensions`, `IHealthCheckProvider` | Registering and exposing the `/v1/health` endpoint |
| `Shared.Common.Infrastructure.Configuration` | `DatabaseOptions` | Binding the `Database` config section for SQL connections |
| `Shared.Common.Infrastructure.Persistence` | `SqlConnectionFactory` | Creating open `SqlConnection` instances for Dapper queries |
| `Shared.Common.Json` | `SharedJsonOptions` | Accessing the standard `CamelCase` and `CamelCaseIgnoreNull` serialiser options |
| `Shared.Common.Models` | `PagedResult<T>`, `PagedRequest` | Returning paginated results from list endpoints |
| `Shared.Common.Validation` | `Guard` | Lightweight guard-clause helpers that throw `ArgumentException` on invalid inputs |

**Do not add airline-domain knowledge here.** If the code would only make sense in an airline context, it belongs in `Shared.Business`.

### ReservationSystem.Shared.Business

Domain-aware code that encodes **airline system business rules** shared across multiple services. Reference this library wherever services need common validation, security, or authentication behaviour.

| Namespace | Contents | When to use |
|-----------|----------|-------------|
| `Shared.Business.Validation` | `CommonFieldValidator` | Validating email addresses, passwords, names, phone numbers, BCP 47 language tags, and ISO 3166-1 country codes in service-specific validators |
| `Shared.Business.Security` | `PasswordHasher`, `IJwtService`, `JwtService` | Hashing passwords and tokens (SHA-256), generating JWT access tokens; inject `IJwtService` in handlers that issue tokens |
| `Shared.Business.Infrastructure.Configuration` | `JwtOptions` | Binding the `Jwt` config section in services that issue or validate JWT tokens; register with `services.AddOptions<JwtOptions>().Bind(...).ValidateDataAnnotations()` |
| `Shared.Business.Middleware` | `TerminalAuthenticationMiddleware` | Validating staff JWT tokens on `Admin`-prefixed Azure Functions; register with `worker.UseMiddleware<TerminalAuthenticationMiddleware>()` in orchestration API `Program.cs` |

**Dependency direction:** `Shared.Business` references `Shared.Common`. Never reference `Shared.Business` from `Shared.Common`.

### Usage pattern

```csharp
// Service-specific validator delegates field rules to CommonFieldValidator:
using ReservationSystem.Shared.Business.Validation;

public static class CustomerValidator
{
    public static List<string> ValidateCreate(string? givenName, string? email)
    {
        var errors = new List<string>();
        CommonFieldValidator.ValidateRequiredName(givenName, errors, "givenName");
        CommonFieldValidator.ValidateEmail(email, errors);
        return errors;
    }
}

// Handler that issues tokens injects IJwtService:
using ReservationSystem.Shared.Business.Security;

public sealed class LoginHandler(IJwtService jwtService, ...)
{
    private (string Token, DateTime ExpiresAt) GenerateJwt(UserAccount account)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.UserAccountId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, account.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        return jwtService.GenerateToken(claims);
    }
}

// Program.cs registration:
services.AddOptions<JwtOptions>()
    .Bind(context.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
services.AddScoped<IJwtService, JwtService>();
```

---

## Scaffolding a New API

Use the `/new-api` slash command to scaffold a new project from the reference template:

```
/new-api <ApiName> <EntityName> <ApiType>
```

**Examples:**
```
/new-api Offer Offer Microservices
/new-api Booking BookingRecord Orchestration
```

The command creates the full clean-architecture directory structure, registers the project in the solution file, and creates a GitHub Actions build workflow. See `.claude/commands/new-api.md` for the complete step-by-step specification.

**Never modify `src/API/Template/`** — it is the reference scaffold and must remain pristine.

---

## Clean Architecture Layer Responsibilities

Every API project follows the same four-layer structure:

### Domain layer (`Domain/`)

The innermost layer. No dependencies on any other layer or on infrastructure.

- `Entities/` — core domain objects with business identity (e.g. `Offer`, `Order`)
- `ValueObjects/` — immutable descriptors without identity (e.g. `OfferMetadata`, `ExchangeRate`)
- `Repositories/` — repository interfaces only; no implementation
- `ExternalServices/` — external service interfaces only (e.g. `ICurrencyExchangeClient`)

Domain objects use meaningful, domain-specific fields. Do not use generic `Name`/`Status` placeholders — choose fields that reflect the actual airline domain concept (cross-reference `system-overview.md` and `design/<domain>.md` for the correct terminology).

### Application layer (`Application/`)

Contains use-case handlers. Each capability lives in its own subdirectory directly under `Application/` — there is no intermediate `UseCases/` folder:

```
Application/
├── CreateOffer/
│   ├── CreateOfferCommand.cs
│   └── CreateOfferHandler.cs
├── GetOffer/
│   ├── GetOfferQuery.cs
│   └── GetOfferHandler.cs
...
```

- Commands and queries are plain C# records with no behaviour.
- Handlers hold all business logic and call repository interfaces — never infrastructure directly.
- No MediatR — handlers are registered directly in DI as scoped services.
- Handlers must not call other microservices. Orchestration logic belongs in an orchestration API.

### Infrastructure layer (`Infrastructure/`)

Implements interfaces defined in the Domain layer.

- `Persistence/Sql<EntityName>Repository.cs` — Dapper-based SQL repository implementation
- `Persistence/Scripts/schema.sql` — SQL schema definition for this service's data store
- `ExternalServices/` — HTTP client implementations for external dependencies

Schema conventions:
- Schema name: `[<entity-lower>]` (e.g. `[offer]`, `[order]`)
- Table name: `[<entity-lower>].[<EntityPlural>]` (e.g. `[offer].[Offers]`)
- Column names: PascalCase — Dapper maps directly without attribute decorators
- JSON columns: must include an `ISJSON` check constraint
- Indexes: add for common query patterns (status, date ranges, flight number)

### Models layer (`Models/`)

HTTP-boundary data transfer objects — never used in domain or application logic.

- `Requests/` — inbound HTTP request bodies
- `Responses/` — outbound HTTP response bodies
- `Database/` — Dapper record types (flat, SQL-shaped)
- `Database/JsonFields/` — strongly typed objects deserialised from JSON columns
- `Mappers/` — static mapper classes; no AutoMapper

JSON property names use camelCase (`[JsonPropertyName("camelCaseName")]`). SQL column names use PascalCase.

### Functions layer (`Functions/`)

Azure Functions HTTP triggers. Each function class handles one resource.

- `HealthCheckFunction.cs` — always present; exposes `/health`
- `HelloWorldFunction.cs` — always present; smoke-test endpoint
- `<EntityName>Function.cs` — CRUD endpoints for the domain entity

Route conventions (follow these exactly):

| Operation | Verb | Path |
|-----------|------|------|
| List all | `GET` | `v1/<entity-kebab-plural>` |
| Get by ID | `GET` | `v1/<entity-kebab-plural>/{id:guid}` |
| Create | `POST` | `v1/<entity-kebab-plural>` |
| Delete | `DELETE` | `v1/<entity-kebab-plural>/{id:guid}` |

For orchestration APIs, paths are resource-action oriented (e.g. `POST v1/basket/{basketId}/confirm`). See `api-reference.md` for the full list of existing orchestration endpoints.

---

## Naming Conventions

| Artefact | Convention | Example |
|----------|-----------|---------|
| Namespace | `ReservationSystem.<ApiType>.<ApiName>.<Layer>` | `ReservationSystem.Microservices.Offer.Domain.Entities` |
| Class names | PascalCase | `OfferHandler`, `SqlOfferRepository` |
| C# properties | PascalCase | `FlightNumber`, `DepartureAt` |
| JSON fields | camelCase | `flightNumber`, `departureAt` |
| SQL columns | PascalCase | `FlightNumber`, `DepartureAt` |
| URL paths | kebab-case | `v1/stored-offers/{offerId}` |
| Query params | camelCase | `?cabinCode=Y&flightNumber=AX101` |
| Route IDs | camelCase | `{offerId:guid}`, `{bookingRef}` |

---

## Microservice Authentication — Host Keys

All orchestration APIs authenticate to microservices using **Azure Function Host Keys**, passed in the `x-functions-key` HTTP header.

> **Shared key (current):** All microservices share the same host key for now. A single key is stored in Azure Key Vault and retrieved by orchestration services at runtime via managed identity. Individual per-service keys will be introduced in a future release.

### How it works

| Step | Actor | Action |
|------|-------|--------|
| 1 | Azure (deployment) | Generates the Function Host Key when the Azure Function app is first deployed |
| 2 | Operator | Stores the key in **Azure Key Vault** |
| 3 | Orchestration API (runtime) | Retrieves the key from Key Vault via **managed identity** — no secrets in config or environment variables |
| 4 | Orchestration API (request) | Passes the key in the `x-functions-key` HTTP header on every call to a microservice |
| 5 | Azure Functions runtime | Validates the header and rejects requests with a missing or invalid key with `401 Unauthorized` |

### Required headers for Orchestration → Microservice calls

| Header | Required | Description |
|--------|----------|-------------|
| `x-functions-key` | Yes | Azure Function Host Key. Shared across all microservices (for now); stored in Azure Key Vault and retrieved at runtime via managed identity |
| `X-Correlation-ID` | Yes | UUID generated at the channel boundary; propagated on every downstream call for distributed tracing and log correlation |
| `Content-Type` | Yes (for request bodies) | Must be `application/json` |

> **JWTs are not forwarded to microservices.** The orchestration layer validates the end-user JWT before forwarding the request. Microservices do not receive or validate Bearer tokens — their only authentication mechanism is the `x-functions-key` header.

See individual API spec files under `documentation/api-specs/` for service-specific security details.

---

## HTTP and API Conventions

Follow `principles/integration-principals.md` for the full set of rules. Key points:

- **Status codes:** `200` reads, `201` creates (with `Location` header), `204` no content, `400` validation, `404` not found, `409` conflict, `422` semantic error, `500` server error.
- **Error bodies:** always include `errorCode`, `message`, `correlationId`, and optional `details`.
- **Idempotency:** `POST` operations on critical resources require a client-provided `Idempotency-Key` UUID header.
- **Correlation:** propagate `X-Correlation-ID` on all outbound calls and include it in every log entry.
- **Versioning:** URI path only — `/v1/`, `/v2/`. No header or query-param versioning.
- **Money:** `decimal` in C#, `DECIMAL(18,2)` in SQL, always with an explicit currency code field.
- **Timestamps:** `DateTimeOffset` in C#, `DATETIMEOFFSET` in SQL, ISO 8601 UTC in JSON.

---

## Adding a New Endpoint to an Existing API

1. Read `system-overview.md` and `design/<domain>.md` — confirm the capability belongs to this service's bounded context.
2. Read `api-reference.md` — confirm no conflicting endpoint already exists.
3. Add the command/query and handler in `Application/UseCases/`.
4. Add or extend the repository interface in `Domain/Repositories/`.
5. Implement the repository method in `Infrastructure/Persistence/`.
6. Add request/response models and mapper in `Models/`.
7. Add the HTTP trigger function or method in `Functions/`.
8. Update `api-reference.md` with the new endpoint row.
9. Update `design/<domain>.md` if the endpoint represents new domain capability.

---

---

## Optimistic Concurrency Control

Booking (`order.Order`, `order.Basket`) and ticket (`delivery.Ticket`) records implement **Optimistic Concurrency Control (OCC)** using an integer `Version` column. This prevents lost updates when concurrent requests (e.g. a passenger updating seat selection while an agent modifies SSRs) attempt to modify the same record simultaneously.

### Mechanism

- Each record is created with `Version = 1`.
- Every mutating operation (UPDATE) must supply the caller's known version and include it in the `WHERE` clause:
  ```sql
  UPDATE order.[Order]
  SET    OrderStatus = @newStatus,
         OrderData   = @newData,
         UpdatedAt   = SYSUTCDATETIME(),
         Version     = Version + 1
  WHERE  OrderId = @orderId
  AND    Version  = @expectedVersion;
  ```
- If the `UPDATE` affects **0 rows**, the record has been modified by another writer since it was read — the operation is rejected with a `409 Conflict` response.
- The same pattern applies to `order.Basket` and `delivery.Ticket`.

### API Behaviour

All mutating endpoints that operate on booking or ticket records accept a `version` field in the request body (or as a header `If-Match: <version>`). The current version is returned in every read response.

| Scenario | HTTP Response |
|---|---|
| `version` matches the stored value — update succeeds | `200 OK` (or `204 No Content`) |
| `version` is absent from the request | `400 Bad Request` — `version` is required |
| `version` does not match the stored value | `409 Conflict` — `{"error": "version_conflict", "message": "The record has been modified by another request. Re-fetch and retry."}` |

### Caller Responsibilities

1. **Read before write:** Always `GET` the current record to obtain the latest `version` before issuing a mutation.
2. **Include version on write:** Pass the retrieved `version` in every mutation request.
3. **Handle 409 Conflict:** On receipt of `409 Conflict`, re-fetch the record and re-apply the intended change, then retry. Do not blindly overwrite — re-evaluate whether the change is still valid against the updated state.

> **No distributed locks:** OCC avoids the need for pessimistic locking or distributed lock managers. Contention is expected to be low (most orders are modified by one actor at a time); the retry-on-conflict path is an exception, not the common path.

---

## Entity Framework Core DbContext configuration

Some microservices use EF Core instead of Dapper for persistence. When configuring a `DbContext` for a table that has database triggers, EF Core **must** be told about those triggers explicitly.

### Why this is required

EF Core uses SQL Server's `OUTPUT` clause to retrieve generated values after `INSERT` and `UPDATE`. SQL Server forbids a bare `OUTPUT` clause (without `INTO`) on tables that have enabled triggers. Without the declaration below, `SaveChangesAsync` will throw:

> *Could not save changes because the target table has database triggers … The target table cannot have any enabled triggers if the statement contains an OUTPUT clause without INTO clause.*

### How to declare a trigger

In `OnModelCreating`, pass a table-builder action as the third argument to `ToTable` and call `HasTrigger` for every trigger defined on the table:

```csharp
entity.ToTable("Customer", "customer", t => t.HasTrigger("TR_Customer_UpdatedAt"));
```

Every table in this project has an `AFTER UPDATE` trigger named `TR_<TableName>_UpdatedAt` that maintains the `UpdatedAt` timestamp (see `src/Database/Script.sql`). **Every `ToTable` call for a table that has a trigger in `Script.sql` must include the corresponding `HasTrigger` declaration.**

### Checklist for new EF Core DbContexts

When adding a new `DbContext` or a new entity mapping:

1. Search `src/Database/Script.sql` for `CREATE TRIGGER` entries matching the target table.
2. For each trigger found, add `.HasTrigger("<trigger-name>")` inside the `ToTable` builder action.
3. If the table has no trigger, the two-argument form `ToTable("Table", "schema")` is correct — do not add an empty builder.

---

## Cross-References

- **Domain model** — `system-overview.md`: architecture and domain capability model. `design/<domain>.md`: per-domain design, schemas, and flows.
- **Endpoint catalogue** — `api-reference.md`: every existing endpoint; verify before adding new ones.
- **Architecture rules** — `principles/architecture-principals.md`: DDD, microservices boundaries, no direct MS-to-MS calls.
- **API conventions** — `principles/integration-principals.md`: REST style, HTTP codes, versioning, idempotency.
- **Scaffolding** — `.claude/commands/new-api.md`: the full specification for the `/new-api` command.
- **Web integration** — `web.md`: how the Angular front-end consumes these APIs.
