# clean-architecture-review

Audit and remediate a single API or microservice project for clean architecture compliance.

## Usage

```
/clean-architecture-review <ProjectPath>
```

- `<ProjectPath>` — path to the API project directory relative to the repo root, e.g. `src/API/Microservices/ReservationSystem.Microservices.Offer`

---

## What to do

Work through every section below in order. For each violation found, fix it immediately before moving to the next section. Do not batch violations — fix as you go.

Before starting, read and hold in mind:
- `documentation/api.md` — layer responsibilities and naming conventions
- `documentation/principles/architecture-principals.md` — DDD and microservice rules
- `documentation/principles/coding-standards.md` — C# standards, naming, async, logging, error handling
- `documentation/principles/integration-principals.md` — HTTP codes, idempotency, versioning

---

## Section 1 — Directory structure

**Check:** The project must contain exactly these top-level directories:

```
Domain/
  Entities/
  ValueObjects/
  Repositories/
  ExternalServices/       ← only if external service clients exist
Application/
  <Capability>/           ← one folder per use case, no UseCases/ wrapper
Infrastructure/
  Persistence/
  ExternalServices/       ← only if external service clients exist
Models/
  Requests/
  Responses/
  Database/
  Database/JsonFields/    ← only if JSON columns exist
  Mappers/
Functions/
Program.cs
host.json
local.settings.json
```

**Fix:** Create any missing directories. Move misplaced files to the correct location. Remove any empty directories left behind. If a `UseCases/` folder exists between `Application/` and the capability folders, flatten it — move all contents up one level.

---

## Section 2 — Functions layer (HTTP trigger functions)

The Functions layer is the HTTP boundary only. It must do exactly these things and nothing else:

1. Parse the incoming `HttpRequestData` (route parameters, query string, request body)
2. Call exactly one Application handler with a command or query
3. Map the result to a response model via a static mapper
4. Return an `HttpResponseData` with the correct status code

**Check each `Functions/*.cs` file for these violations:**

### 2a — Business logic in functions
Flag any logic that is not one of the four steps above. Examples of forbidden logic:
- Conditional branching on entity state (e.g. `if (item.Status == "active")`)
- Calculations, data transformations beyond simple request-to-command mapping
- Direct database or repository calls (repositories must never be injected into a Function class)
- Multiple handler calls within a single function method (orchestrate in Application layer instead)
- Calls to `HttpClient` or any external service client

**Fix:** Extract the logic into the appropriate Application handler. The function should only call the handler and translate the result.

### 2b — Incorrect HTTP status codes
Every endpoint must follow this mapping:

| Operation | Success code | Notes |
|-----------|-------------|-------|
| GET (found) | `200 OK` | |
| GET (not found) | `404 Not Found` | Return immediately on null |
| POST (create) | `201 Created` | Must include `Location` header |
| PUT/PATCH (updated) | `200 OK` or `204 No Content` | |
| DELETE (deleted) | `204 No Content` | |
| DELETE (not found) | `404 Not Found` | |
| Validation failure | `400 Bad Request` | |
| State conflict | `409 Conflict` | |
| Semantic error | `422 Unprocessable Entity` | |

**Fix:** Correct any mismatched status codes.

### 2c — Missing `Location` header on 201 responses
Every `201 Created` response must set a `Location` header pointing to the newly created resource URI (e.g. `$"/v1/offers/{created.Id}"`).

**Fix:** Add the `Location` header where missing.

### 2d — JSON deserialisation not wrapped in try/catch
Request body deserialisation must be wrapped in a `try/catch (JsonException)` that returns `400 Bad Request` with a descriptive message. The `_logger.LogWarning(ex, "...")` call must be present inside the catch block.

**Fix:** Wrap any unwrapped deserialisations.

### 2e — Null request guard missing
After deserialisation, the function must guard against a null request object and return `400 Bad Request`. Required fields must also be validated (e.g. `string.IsNullOrWhiteSpace`).

**Fix:** Add the null/required field guard immediately after the try/catch block.

### 2f — Route conventions
Routes must follow this pattern:

| Verb | Route pattern |
|------|--------------|
| GET list | `v1/<entity-kebab-plural>` |
| GET by ID | `v1/<entity-kebab-plural>/{id:guid}` |
| POST create | `v1/<entity-kebab-plural>` |
| DELETE | `v1/<entity-kebab-plural>/{id:guid}` |

Routes must use kebab-case. Route parameter names must be camelCase (`{offerId:guid}`, not `{OfferID:guid}`).

**Fix:** Rename any non-conforming routes.

### 2g — Injected dependencies
Function classes must inject only Application handler types and `ILogger<T>`. They must not inject repositories, `DbContext`, `HttpClient`, or any infrastructure type.

**Fix:** Remove forbidden injected dependencies. Move the logic that required them into the Application layer.

### 2h — OpenAPI attributes
Every public function method must have `[OpenApiOperation]`, `[OpenApiResponseWithBody]` or `[OpenApiResponseWithoutBody]` attributes for all documented response codes. Request bodies must have `[OpenApiRequestBody]`. Path parameters must have `[OpenApiParameter]`.

**Fix:** Add any missing OpenAPI attributes.

---

## Section 3 — Application layer (handlers)

The Application layer owns all business logic. Handlers receive a command or query, execute domain operations, and return a domain entity or value. They never deal with HTTP.

**Check each `Application/<Capability>/*.cs` file:**

### 3a — Commands and queries are plain records
Commands and queries must be `public sealed record` types with no methods, no logic, and no domain references beyond primitive types and value types.

**Fix:** Strip any methods or logic from command/query types. Convert classes to records if needed.

### 3b — Handlers call repository interfaces only
Handlers must inject and call `I<Entity>Repository` interfaces (from `Domain/Repositories/`). They must never:
- Reference a concrete repository class (e.g. `SqlOfferRepository`, `EfOfferRepository`)
- Reference a `DbContext` directly
- Call `HttpClient` or any HTTP client (use `Domain/ExternalServices/` interfaces instead)
- Call another handler directly

**Fix:** Replace any concrete references with the appropriate interface. Introduce an `ExternalServices` interface in `Domain/ExternalServices/` if an HTTP call is needed.

### 3c — Business logic belongs in handlers, not in the domain entity's persistence mapping
Handler logic must not call mapper methods to transform to DB records, serialise JSON, or interact with SQL. Those concerns belong in the Infrastructure layer.

**Fix:** Move any persistence-format transformations into the repository implementation.

### 3d — Handlers do not catch exceptions for control flow
Handlers must not wrap repository calls in `try/catch` to convert exceptions into return values. Not-found is signalled by the repository returning `null`. Domain violations throw domain or argument exceptions, which propagate upward.

**Fix:** Remove try/catch blocks used for control flow. Use null returns from the repository for not-found cases.

### 3e — Logging in handlers
Handlers must log significant business events at `LogInformation` level (e.g. `"Created Offer {Id} for flight {FlightNumber}"`). Message templates must use named placeholders, never string interpolation. PII must never appear in log messages.

**Fix:** Add missing `ILogger<T>` injection and log calls where absent.

### 3f — CancellationToken propagation
Every handler's `HandleAsync` method must accept `CancellationToken cancellationToken = default` as its final parameter and pass it to every repository call and async operation.

**Fix:** Add `cancellationToken` parameter where missing and propagate it.

### 3g — No MediatR or other mediator
Handlers are registered in DI and called directly — do not introduce MediatR, Wolverine, or any other mediator library. Handlers are injected directly into Function classes.

**Fix:** Remove any mediator library references. Refactor to direct DI injection.

---

## Section 4 — Domain layer

The Domain layer has zero dependencies on any other layer. It must reference no NuGet package except the .NET runtime itself.

**Check `Domain/**/*.cs`:**

### 4a — Entity construction via factory methods only
Domain entities must have a `private` parameterless constructor. The only public construction paths are static factory methods named `Create` (for new instances) and `Reconstitute` (for loading from persistence). Direct public property setters are forbidden on entities.

**Fix:** Make the parameterless constructor `private`. Convert any public setters to `private set`. Add `Create`/`Reconstitute` factory methods.

### 4b — Domain entities do not reference infrastructure or model types
Entities must not reference `DbContext`, Dapper record types, request/response models, or any infrastructure namespace.

**Fix:** Remove forbidden references.

### 4c — Repository interfaces define async methods with CancellationToken
Every method on an `I<Entity>Repository` interface must be async (`Task` or `Task<T>` return type) and accept `CancellationToken cancellationToken = default` as its final parameter.

**Fix:** Add missing async return types and `CancellationToken` parameters.

### 4d — Value objects are immutable
Value objects (in `Domain/ValueObjects/`) must be `record` or `readonly struct` types. They must have no mutable properties (`init` or constructor-only). They must have no side effects.

**Fix:** Convert mutable value objects to `record` types.

### 4e — External service interfaces live in `Domain/ExternalServices/`
If the service calls an external HTTP API (exchange rates, a partner airline, etc.), the interface (e.g. `ICurrencyExchangeClient`) must be defined in `Domain/ExternalServices/`. The concrete HTTP client belongs in `Infrastructure/ExternalServices/`.

**Fix:** Move any misplaced external service interfaces into `Domain/ExternalServices/`.

---

## Section 5 — Infrastructure layer

The Infrastructure layer implements repository interfaces. It knows about Dapper, EF Core, SQL, and HTTP clients. It must not contain business logic.

**Check `Infrastructure/**/*.cs`:**

### 5a — SQL queries are const strings with named parameters
Every SQL string must be defined as `const string sql = """ ... """;` at the top of the method. Named parameters (`@ParameterName`) must be used throughout. Positional parameters and dynamic SQL built from user input are prohibited.

**Fix:** Extract inline SQL strings to const. Replace positional parameters with named ones.

### 5b — No `SELECT *`
All SQL queries must list columns explicitly matching the target Dapper record type.

**Fix:** Replace every `SELECT *` with an explicit column list.

### 5c — Schema-qualified table names
All table references must use the two-part name `[schema].[Table]` (e.g. `[offer].[Offers]`).

**Fix:** Add schema prefixes where missing.

### 5d — Connection per method, not per class
Repositories must open a connection inside each method with `using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)`. Connections must never be stored as class fields.

**Fix:** Move any field-stored connections into individual methods.

### 5e — `CommandDefinition` with timeout
Every Dapper query must use `CommandDefinition` with `commandTimeout: _options.CommandTimeoutSeconds` (from `DatabaseOptions` via `IOptions<DatabaseOptions>`).

**Fix:** Wrap bare Dapper calls in `CommandDefinition`.

### 5f — EF Core: triggers declared on `HasTrigger`
For any table that has an `AFTER UPDATE` trigger in `src/Database/Script.sql`, the `ToTable` call in the `DbContext.OnModelCreating` must include `t.HasTrigger("<trigger-name>")`. The standard trigger name pattern is `TR_<TableName>_UpdatedAt`.

**Fix:** Add any missing `HasTrigger` declarations. Verify against `src/Database/Script.sql`.

### 5g — No business logic in repositories
Repository methods must perform only data access: SQL queries, mapping from DB records to domain entities, and back. No branching on business state, no calculations, no multi-step business workflows.

**Fix:** Move any business logic to the Application handler.

### 5h — `AsNoTracking` on EF Core reads
All EF Core read queries must use `.AsNoTracking()`.

**Fix:** Add `.AsNoTracking()` to read queries that are missing it.

---

## Section 6 — Models layer

Models are HTTP boundary DTOs and database record types. They must not contain business logic.

**Check `Models/**/*.cs`:**

### 6a — Request/response models not used in domain or application
Request models (`Models/Requests/`) and response models (`Models/Responses/`) must never be imported into `Domain/` or `Application/` namespaces.

**Fix:** Remove forbidden cross-layer imports.

### 6b — JSON property names use camelCase
All request and response model properties must have `[JsonPropertyName("camelCaseName")]` attributes. Do not rely on runtime serialiser casing conventions.

**Fix:** Add missing `[JsonPropertyName]` attributes.

### 6c — Database record types reflect SQL column names in PascalCase
Dapper record types in `Models/Database/` must have PascalCase property names matching the SQL column names exactly. No `[Column]` attribute remapping.

**Fix:** Rename any non-PascalCase properties.

### 6d — Mappers are static classes
All mapper classes in `Models/Mappers/` must be `public static class` with only `public static` methods. No instance state, no DI injection, no AutoMapper.

**Fix:** Convert any instance mapper classes to static. Remove AutoMapper references.

### 6e — Mapper XML documentation direction
Each mapper method must have an XML `<summary>` comment identifying source and target types (e.g. `/// <summary>Maps HTTP request → Application command.</summary>`).

**Fix:** Add missing summary comments.

---

## Section 7 — Program.cs (DI registration)

### 7a — Registration order and grouping
`Program.cs` must register services in this order with inline comments grouping by concern:
1. Telemetry / Application Insights
2. Configuration (options bindings)
3. Infrastructure (connection factory, repository implementations)
4. Health checks
5. Application handlers

**Fix:** Reorder and add comments where missing.

### 7b — Repository registrations
Repositories and handlers must be registered as `Scoped`. `SqlConnectionFactory` (if used) must be `Singleton`.

**Fix:** Correct any mismatched lifetimes.

### 7c — No service locator
`IServiceProvider` must never be injected into application code. All dependencies must be resolved at composition root.

**Fix:** Refactor any service locator usage to direct constructor injection.

---

## Section 8 — Naming conventions

Do a final pass across all files for naming violations:

| Artefact | Required convention | Example |
|----------|-------------------|---------|
| Namespace segments | `ReservationSystem.<ApiType>.<ApiName>.<Layer>` | `ReservationSystem.Microservices.Offer.Application.CreateOffer` |
| Classes, records, structs | PascalCase | `CreateOfferHandler` |
| Interfaces | `I` prefix + PascalCase | `IOfferRepository` |
| Methods | PascalCase, async methods end in `Async` | `HandleAsync`, `GetByIdAsync` |
| Properties | PascalCase | `FlightNumber` |
| Private fields | `_camelCase` | `_repository`, `_logger` |
| Local variables and parameters | camelCase | `offerId`, `cancellationToken` |
| URL path segments | kebab-case | `v1/stored-offers` |
| Route parameters | camelCase | `{offerId:guid}` |

**Fix:** Rename any non-conforming identifiers. Update all usages.

---

## Section 9 — Async and null safety

### 9a — No synchronous blocking
`.Result`, `.Wait()`, and `GetAwaiter().GetResult()` are prohibited everywhere.

**Fix:** Replace with `await`.

### 9b — `async void` prohibited
No `async void` methods anywhere except Azure Functions entry points (which are handled by the runtime). Convert any `async void` helpers to `async Task`.

**Fix:** Change return type to `Task` and update callers.

### 9c — All async method names end in `Async`
Any method returning `Task` or `Task<T>` must have an `Async` suffix.

**Fix:** Rename methods missing the suffix. Update all call sites.

### 9d — Nullable reference types honoured
Null-forgiving (`!`) must not be used to suppress genuine nullability warnings. Repository `GetByIdAsync` returns `T?`; callers must handle the null case explicitly.

**Fix:** Remove unjustified null-forgiving operators. Add null checks or null-conditional operators.

---

## Section 10 — Cross-cutting concerns

### 10a — No direct microservice-to-microservice calls
If this is a Microservice project (`src/API/Microservices/`), it must contain no `HttpClient` injections or calls to URLs of other microservices. Cross-domain calls belong in an Orchestration API.

**Fix:** Remove any MS-to-MS calls. If the capability requires cross-domain data, document it as a requirement for the Orchestration layer and leave a `// TODO` with the tracking issue.

### 10b — Monetary amounts use `decimal`
No `double` or `float` for monetary values anywhere. SQL must use `DECIMAL(18,2)`.

**Fix:** Change any `double`/`float` monetary fields to `decimal`.

### 10c — Timestamps use `DateTimeOffset`
All timestamp properties in C# use `DateTimeOffset`, not `DateTime`. JSON serialises to ISO 8601 UTC.

**Fix:** Replace `DateTime` with `DateTimeOffset` for all timestamps.

### 10d — `createdAt`/`updatedAt` not written by application code
The `CreatedAt` and `UpdatedAt` fields must never be set in request bodies or written by application code — they are set by database DEFAULT constraints and triggers. The entity's `Create` and `Reconstitute` factory methods may set provisional in-memory values, but these must not be passed in INSERT statements.

**Fix:** Remove any INSERT or UPDATE SQL that sets `CreatedAt`/`UpdatedAt` explicitly.

### 10e — IATA code types
Airport codes must be `string` with a length hint comment `// CHAR(3) IATA airport code`. Passenger types (`ADT`, `CHD`, `INF`, `YTH`) must be used as constants, not magic strings.

**Fix:** Add constants or type aliases for passenger types if bare strings are used.

### 10f — CorrelationId propagation
If the service makes outbound HTTP calls, the `X-Correlation-ID` header must be read from the inbound `HttpRequestData` and forwarded on every outbound call.

**Fix:** Add correlation ID propagation where missing.

---

## Section 11 — Final verification

After all fixes have been applied:

1. Print a summary table of every violation found and whether it was fixed or left with a justification.
2. Do a final scan of each layer to confirm no cross-layer dependency violations remain:
   - `Domain/` imports: must reference only .NET runtime namespaces
   - `Application/` imports: may reference `Domain/` only
   - `Infrastructure/` imports: may reference `Domain/`, `Models/`, and infrastructure NuGet packages
   - `Functions/` imports: may reference `Application/`, `Models/`, `Shared.Common`, and Azure Functions packages
3. Confirm all file names match the single type they contain.

---

## Commit

Stage only files within the target project path. Commit with this format:

```
Clean architecture review: <ApiName>

- <bullet per category of fix, e.g. "Moved business logic from OfferFunction into CreateOfferHandler">
- <bullet>
- <bullet>
```

Push to the current branch.

---

## Rules and constraints

- **Never modify `src/API/Template/`** — it is the reference scaffold
- **Do not introduce new NuGet packages** beyond what the project already uses
- **Do not add AutoMapper, MediatR, or any mediator library**
- **Do not create new files unless a required file is genuinely absent** — prefer moving and fixing existing code
- **Do not add docstrings or comments to code you did not change** — only touch what is being fixed
- **Do not refactor working code that is not in violation** — the goal is compliance, not style preferences
