# Coding Standards
**Apex Air — Reservation Platform**

---

## Language and Runtime

All backend services are written in C# targeting .NET 8 with nullable reference types enabled.

- C# 12 language features are available; use modern idioms (primary constructors, collection expressions, raw string literals) where they improve clarity.
- Nullable reference types (`<Nullable>enable</Nullable>`) are enabled on all projects; the compiler's nullability warnings must not be suppressed.
- Implicit usings are enabled; explicit `using` statements added only for namespaces not covered by the global set.
- `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` is set on all projects; build warnings are not permitted to accumulate.

---

## Naming Conventions

Names must be unambiguous, domain-consistent, and follow the casing rules below without exception.

| Artefact | Convention | Example |
|----------|-----------|---------|
| Namespaces | `ReservationSystem.<ApiType>.<ApiName>.<Layer>` | `ReservationSystem.Microservices.Offer.Domain.Entities` |
| Classes, records, structs | PascalCase | `CreateOfferHandler`, `OfferMetadata` |
| Interfaces | PascalCase prefixed with `I` | `IOfferRepository`, `IHealthCheckService` |
| Methods | PascalCase | `HandleAsync`, `GetByIdAsync` |
| Properties | PascalCase | `FlightNumber`, `DepartureAt` |
| Private fields | `_camelCase` with leading underscore | `_repository`, `_logger` |
| Local variables and parameters | camelCase | `offerId`, `cancellationToken` |
| Constants and static readonly | PascalCase | `SectionName`, `Available` |
| JSON properties | camelCase (via `[JsonPropertyName]`) | `flightNumber`, `departureAt` |
| SQL column names | PascalCase | `FlightNumber`, `DepartureAt` |
| URL path segments | kebab-case | `stored-offers`, `booking-reference` |
| Route parameters | camelCase | `{offerId:guid}`, `{bookingRef}` |
| Query parameters | camelCase | `?cabinCode=Y&flightNumber=AX101` |

Domain names — Offer, Order, Payment, Delivery, Customer, Identity, Accounting, Seat — are used verbatim in all namespaces, class names, and identifiers. Do not abbreviate or rename them.

---

## Class Design

Classes are designed for minimal surface area with clear, scoped responsibilities.

- All concrete classes are `sealed` by default; remove `sealed` only when inheritance is explicitly required and justified.
- One type per file; file name matches the type name exactly (e.g. `OfferHandler.cs` contains only `OfferHandler`).
- Classes must not exceed a single responsibility; split when a class begins handling concerns from more than one layer or domain.
- Domain entities use a private parameterless constructor and expose only static factory methods (`Create`, `Reconstitute`) for construction — direct property assignment from outside the class is not permitted.
- Value objects are immutable records or structs; no setters on their properties.
- Commands and queries are `record` types: `public sealed record CreateOfferCommand(string FlightNumber, ...)`.
- Mappers are static classes with no state; all methods are static to eliminate DI overhead and maximise testability.

---

## Dependency Injection and Lifetimes

Services are registered and consumed via constructor injection only.

- Handlers are registered as `Scoped`; they depend on repositories and are tied to a request lifecycle.
- Repositories are registered as `Scoped`; they wrap a scoped SQL connection.
- `SqlConnectionFactory` is registered as `Singleton`; it holds no per-request state.
- `IOptions<T>` values are resolved at registration time via `options.Value` stored in the constructor — not accessed repeatedly from the options object in methods.
- Service locator (`IServiceProvider` injected into application code) is prohibited; always inject concrete dependencies.
- `Program.cs` is the sole registration point; extension methods on `IServiceCollection` are acceptable for grouping related registrations.

---

## Async Programming

All I/O is asynchronous; synchronous blocking on async code is prohibited.

- Every I/O method is async and returns `Task` or `Task<T>`; method names end with `Async` (e.g. `GetByIdAsync`, `HandleAsync`).
- `CancellationToken cancellationToken = default` is the final parameter on all async public methods; it is propagated to every downstream async call.
- `async void` is never used; event handlers that require it are wrapped to return `Task`.
- `.Result`, `.Wait()`, and `GetAwaiter().GetResult()` are prohibited; they cause deadlocks in Azure Functions hosting contexts.
- `ConfigureAwait(false)` is not used in application code; the Azure Functions isolated worker model does not use a synchronisation context that requires it.

---

## Null Handling

Null is treated explicitly; implicit null references are not permitted.

- `?` nullable annotations are applied to all reference types that can legitimately be null; non-nullable annotations assert the value is always present.
- `ArgumentNullException.ThrowIfNull` and `ArgumentException.ThrowIfNullOrWhiteSpace` are used for guard clauses at public method entry points in domain and application layers.
- Null-coalescing (`??`) and null-conditional (`?.`) operators are preferred over explicit null checks for simple fallback and propagation patterns.
- Returning `null` from a repository method (e.g. `GetByIdAsync` returning `null` when not found) is the standard not-found signal; callers must handle it explicitly.
- `null!` (null-forgiving) is used only for genuine post-construction initialisation (e.g. EF Core / Dapper model binding); it must never suppress a real nullable warning.

---

## Error Handling

Errors are handled at the layer best equipped to act on them; exceptions are not used for control flow.

- Functions layer catches `JsonException` on deserialisation and returns `400 Bad Request`; all other unhandled exceptions propagate to the Functions host, which returns `500`.
- Handlers do not catch exceptions unless a specific, recoverable domain condition exists; unexpected infrastructure exceptions bubble up.
- Domain logic does not throw HTTP exceptions (`HttpRequestException`, `BadHttpRequestException`); it throws domain exceptions or argument exceptions.
- `try/catch` blocks that catch and immediately swallow an exception without logging or rethrowing are prohibited.
- All `catch` blocks that suppress an exception must include a `_logger.LogWarning` or `_logger.LogError` call with the exception as the first argument.

---

## Logging

Structured logging via `ILogger<T>` is used throughout; plain string concatenation in log messages is prohibited.

- Use the correct severity: `LogDebug` for diagnostic detail (SQL statements, row counts), `LogInformation` for significant business events (entity created, status changed), `LogWarning` for recoverable anomalies (update with zero rows affected), `LogError` for unexpected failures.
- Message templates use named placeholders — `_logger.LogInformation("Created Offer {Id} for flight {FlightNumber}", offer.Id, offer.FlightNumber)` — never string interpolation.
- PII (names, emails, passport numbers, payment references) must never appear in log messages; use opaque identifiers (`OfferId`, `BookingReference`, `PassengerId`).
- `CorrelationId` must be included in log entries for any operation that crosses a service boundary; propagated via `X-Correlation-ID` header and included in structured log scope.
- `ILogger<T>` is injected; static log helpers and `LoggerFactory` accessed directly in application classes are prohibited.

---

## SQL and Data Access

All data access uses Dapper with raw parameterised SQL; dynamic SQL built from user input is prohibited.

- SQL strings are defined as `const string sql = """ ... """;` (raw string literals) at the top of each method, never inline in the method call.
- All SQL parameters use named Dapper parameters (`@ParameterName`), never positional parameters.
- Queries use `CommandDefinition` with `commandTimeout: _options.CommandTimeoutSeconds` on every call.
- `using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken)` opens and disposes the connection per method; connections are never shared across methods or stored as fields.
- `SELECT *` is never used; column lists are explicit and match the target `Record` type exactly.
- Schema-qualified table names are always used — `[offer].[Offers]`, never just `Offers`.

---

## XML Documentation and Comments

XML documentation is required on public types and their public members in the Domain, Application, and Infrastructure layers; inline comments are used only to explain non-obvious logic.

- All public classes have a `<summary>` XML comment describing their responsibility.
- Factory methods (`Create`, `Reconstitute`) have `<summary>` comments distinguishing their purpose.
- Mapper classes document all mapping directions in their `<summary>` (e.g. `HTTP request → Application command`).
- Section dividers (`// ----`) separate logical groups within a class (e.g. private helpers from public methods, or separate HTTP verbs in a Functions class).
- Inline comments explain *why*, not *what*; a comment that restates the code (`// increment counter`) is not permitted.
- `TODO` comments must include the author and a tracking issue reference; unresolved TODOs must not be committed to the main branch.

---

## Code Organisation Within Files

File layout is consistent across all projects to allow rapid navigation.

For classes with injected dependencies, the canonical order is:

1. Private fields (injected dependencies)
2. Constructor
3. Public methods (grouped by HTTP verb or use case, with section dividers)
4. Private helpers

For static mapper classes:

1. One section per mapping direction, marked with a section divider comment identifying source and target types.

For `Program.cs`:

1. `HostBuilder` configuration in a single expression, with inline comments grouping registrations by concern (Telemetry, Configuration, Infrastructure, Health, Application handlers).

---

## Testing

Tests are co-located with the service they test and follow the same naming structure.

- Unit tests use the `Arrange / Act / Assert` structure with a blank line between each section.
- Test class names match the class under test with a `Tests` suffix: `CreateOfferHandlerTests`.
- Test method names follow the pattern `<MethodName>_<Scenario>_<ExpectedBehaviour>`: `HandleAsync_ValidCommand_ReturnsCreatedOffer`.
- Repositories and external services are mocked via interfaces; no test should require a live database or network.
- Handler tests verify the command/query is passed to the repository and that the return value is correctly mapped; they do not test repository SQL.
- Each test asserts a single behaviour; a test with multiple unrelated assertions must be split.
