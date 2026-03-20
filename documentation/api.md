# API Development Guide

This guide describes how to build backend APIs for the Apex Air Reservation System. Read `../agents.md` and `design.md` first to understand the domain model before writing any code.

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
| Shared infrastructure | `ReservationSystem.Shared.Common` (connection factory, DB options, JSON options, HTTP extensions) |

---

## Project Types

There are two categories of API project, placed in different directories under `src/API/`:

| Type | Directory | Purpose |
|------|-----------|---------|
| `Microservices` | `src/API/Microservices/` | Standalone domain API that owns a single bounded context and its data store |
| `Orchestration` | `src/API/Orchestration/` | Cross-domain API that coordinates calls across multiple microservices within a single request flow |

Microservices must never call other microservices. All synchronous cross-domain coordination is the exclusive responsibility of orchestration APIs. See `principles/architecture-principals.md`.

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

Domain objects use meaningful, domain-specific fields. Do not use generic `Name`/`Status` placeholders — choose fields that reflect the actual airline domain concept (cross-reference `design.md` for the correct terminology).

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

1. Read `design.md` — confirm the capability belongs to this service's bounded context.
2. Read `api-reference.md` — confirm no conflicting endpoint already exists.
3. Add the command/query and handler in `Application/UseCases/`.
4. Add or extend the repository interface in `Domain/Repositories/`.
5. Implement the repository method in `Infrastructure/Persistence/`.
6. Add request/response models and mapper in `Models/`.
7. Add the HTTP trigger function or method in `Functions/`.
8. Update `api-reference.md` with the new endpoint row.
9. Update `design.md` if the endpoint represents new domain capability.

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

## Cross-References

- **Domain model** — `design.md`: the authoritative source for what each domain owns and what capabilities it provides.
- **Endpoint catalogue** — `api-reference.md`: every existing endpoint; verify before adding new ones.
- **Architecture rules** — `principles/architecture-principals.md`: DDD, microservices boundaries, no direct MS-to-MS calls.
- **API conventions** — `principles/integration-principals.md`: REST style, HTTP codes, versioning, idempotency.
- **Scaffolding** — `.claude/commands/new-api.md`: the full specification for the `/new-api` command.
- **Web integration** — `web.md`: how the Angular front-end consumes these APIs.
