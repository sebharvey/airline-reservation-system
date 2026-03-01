# Architecture Principles
**Apex Air — Reservation Platform**

---

## Domain-Driven Design

- **The system is structured around bounded contexts aligned to airline capability domains.**
  The eight core domains — Offer, Order, Payment, Delivery, Customer, Identity, Accounting, and Seat — each represent a distinct business capability with clear ownership, a ubiquitous language, and explicit boundaries. These domain names must be used uniformly in code, database schemas, API paths, and documentation.

- **Domain models must be owned entirely by the service responsible for that domain.**
  No other service may directly read from or write to another domain's data store. Shared data is exposed through APIs (synchronous) or events (asynchronous), never through shared schema access.

- **Anti-corruption layers must be used when integrating with external systems that use different domain models.**
  Integration with NDC partners, GDS systems, or third-party payment processors must translate between the external model and Apex Air's internal domain model at the boundary. Internal services must not be contaminated by external schemas or naming conventions.

---

## Microservices

- **Each microservice owns a single bounded context, its own data store, and its own deployment lifecycle.**
  Services must be independently deployable without requiring coordinated releases with other services. A change within one service's internal implementation must not require changes to another service.

- **Services must be loosely coupled and highly cohesive.**
  Coupling between services must be limited to well-defined API contracts and event schemas. Changes to a service's internal data model or implementation must not break consumers as long as the contract is honoured.

- **Services must be designed for failure in their dependencies.**
  A service must degrade gracefully or return a meaningful error when a downstream dependency is unavailable, rather than cascading failure. Timeouts, circuit breakers, and retry policies must be applied to all service-to-service calls.

- **Services must be stateless where possible.**
  State that must persist across requests must be externalised to the service's database. No in-memory session state should be required for a request to be handled by any instance of the service. This enables horizontal scaling without session affinity.

---

## Orchestration vs Choreography

- **Synchronous, complex, multi-step flows must be orchestrated by the orchestration API layer (Retail API, Loyalty API, Airport API, Accounting API).**
  The booking confirmation flow — involving Offer, Payment, Delivery, Order, and Seat microservices — is orchestrated by the Retail API, which owns the sequence, compensating actions, and failure handling. Microservices must not call each other directly in synchronous flows.

- **Asynchronous, decoupled downstream reactions must use choreography via the event bus.**
  The Accounting and Customer microservices react to `OrderConfirmed` and `OrderChanged` events published by the Order microservice. These consumers are not known to the publisher, and the Order microservice has no dependency on their availability or behaviour.

- **Orchestration APIs must own all compensating transaction logic for multi-step flows.**
  If a step fails mid-sequence (e.g. inventory removal fails after payment authorisation), the Retail API must execute the defined compensation (e.g. void the payment authorisation). Microservices must expose compensation endpoints (e.g. void, reinstate) to support this.

---

## IATA Standards Alignment

- **The Order domain follows the IATA ONE Order model.**
  The `OrderData` JSON structure is aligned to ONE Order concepts: passengers, flight segments, order items, payments, and e-tickets. This positions the platform for external ONE Order interoperability and ensures the data model reflects industry-standard retailing semantics.

- **NDC (New Distribution Capability) is a first-class channel.**
  The NDC channel connects through the Retail API and must be supported with parity to the Web and App channels for offer search, booking, and post-booking management. NDC-specific XML transformation must be handled at the channel boundary; internal services use Apex Air's own JSON-based models.

- **E-ticket numbers must follow the IATA format: 3-digit airline code prefix followed by a 10-digit serial number (e.g. `932-1234567890`).**
  The Delivery microservice is the system of record for all issued e-tickets. E-ticket numbers are immutable after issuance; post-booking changes requiring a new ticket trigger reissuance with a new number, not amendment of the existing one.

- **Boarding pass barcode strings must comply with IATA Resolution 792 (BCBP).**
  The Delivery microservice is responsible for assembling the BCBP string. Channels render it using their preferred library (PDF417 for print, QR for mobile). The string must be constructed and validated against the BCBP specification before being returned to the channel.

- **Aircraft type codes must follow the Apex Air 4-character convention (manufacturer prefix + 3-digit variant), aligned to IATA SSIM standards.**
  This code must be used uniformly across all services, databases, API contracts, and documentation. Examples: `A351` (Airbus A350-1000), `B789` (Boeing 787-9), `A339` (Airbus A330-900).

---

## Price Integrity

- **Offers must be persisted at the point of presentation to the customer and retrieved by ID — never re-priced at order creation.**
  The `StoredOffer` pattern ensures the fare shown to the customer is the fare charged, regardless of elapsed time or intervening price changes. The Order API must retrieve the stored offer snapshot by `OfferId` and must reject expired or consumed offers.

- **Offer IDs must be treated as single-use; consumption must be marked atomically.**
  Once an `OfferId` is retrieved by the Order API, `IsConsumed` must be set to `1` in the same operation to prevent the same offer being used across multiple concurrent orders. Concurrent order attempts using the same `OfferId` must be handled with optimistic concurrency control.

- **Seat offer IDs are session-scoped and must not be reused across sessions.**
  `SeatOfferIds` are valid for the duration of the booking session in which they were generated. Channels must not cache or reuse them across sessions, and the Order microservice must validate that a `SeatOfferId` is consistent with the current seat pricing version.

---

## Separation of Concerns

- **The Seat microservice defines what seats exist and what they cost; it does not manage availability.**
  Seat inventory — which seats are sold, held, or available on a specific flight — is owned by the Offer microservice. The Seat microservice provides the seatmap layout and fleet-wide pricing rules. This separation allows seatmap definitions to be updated without touching inventory, and vice versa.

- **The Delivery microservice is the system of record for who is on each flight and where they are sitting.**
  It does not read from `order.Order` directly. All data required for manifest population is passed explicitly by the Retail API at the point of booking confirmation and subsequent changes. The Delivery DB is the authoritative source for check-in status, boarding passes, and regulatory APIS submissions.

- **The Identity microservice holds credentials only; it never holds loyalty or profile data.**
  The Customer microservice holds loyalty account data and references the Identity domain via an opaque `IdentityReference`. This separation allows credential management (password reset, MFA, lockout) to evolve independently of the loyalty programme.

- **The Accounting microservice is event-driven and must not be in the synchronous booking path.**
  Accounting consumes `OrderConfirmed` and `OrderChanged` events asynchronously. It must never be called synchronously during order confirmation, as its availability must not be a dependency for completing a booking.

---

## Resilience and Reliability

- **All services must implement retry with exponential backoff and jitter for transient failures.**
  Retries must be bounded (e.g. maximum 3 attempts) and must not be applied to non-idempotent operations without idempotency keys. Idempotency keys must be used on payment authorisation, e-ticket issuance, and inventory updates to ensure safe retry.

- **Circuit breakers must be applied to all service-to-service calls.**
  When a downstream service is consistently failing, the circuit breaker must open and return a fast failure response rather than queuing requests. Circuit breaker state must be observable via health metrics.

- **All state-changing operations must log sufficient detail to support manual reconciliation in the event of partial failure.**
  The ticketing flow involves multiple sequential steps across services; partial failures must be detectable and recoverable. Compensation actions and their outcomes must be logged with correlation identifiers linking them to the originating transaction.

- **Health endpoints must be exposed by every service.**
  A `/health` or equivalent endpoint must return the service's operational status, including dependency health (database connectivity, event bus connectivity). Health checks must be integrated into the platform's monitoring and alerting infrastructure.

---

## Scalability

- **Services must be horizontally scalable without architectural changes.**
  Stateless services backed by externalised state (database, event bus) can be scaled by adding instances. Azure Functions scale automatically on consumption plans; concurrency limits and scale-out settings must be tuned per function based on load testing results.

- **The event bus must be the mechanism for decoupling high-volume downstream processing from the synchronous booking path.**
  Points accrual, accounting entries, and any future downstream reactions to order events must be event-driven to prevent them becoming latency bottlenecks in the booking flow.

- **Database access patterns must be designed to support the expected read/write ratio per domain.**
  The Offer domain is read-heavy (search is far more frequent than inventory updates); the Order domain has a more balanced read/write pattern. Indexing strategies, connection pooling, and future read replica configurations must reflect these patterns.

---

## Testability

- **Services must be independently testable through their API contracts.**
  Each microservice must have a comprehensive suite of unit and integration tests that can run without standing up dependent services. External dependencies must be mockable via interfaces and dependency injection.

- **Contract testing must be used to validate service API compatibility at build time.**
  Consumer-driven contract tests (e.g. using Pact) must be maintained between orchestration APIs and microservices to catch breaking changes before deployment. API versioning must be introduced before any breaking change is merged.

- **End-to-end tests must cover the critical booking path, including failure and compensation scenarios.**
  The booking confirmation flow, including payment failure, inventory rollback, and e-ticket void scenarios, must be covered by automated end-to-end tests running against a staging environment.

---

## Observability

- **Distributed tracing must be implemented across all services using a correlation ID propagated through every request.**
  A `CorrelationId` (or W3C `traceparent`) must be generated at the channel boundary and passed through all downstream service calls and event payloads. Every log entry must include this identifier to enable end-to-end trace reconstruction.

- **Structured logging must be used across all services.**
  Logs must be emitted as structured JSON to a centralised log sink (e.g. Azure Monitor, Application Insights). Log levels must be used consistently; verbose/debug logging must not be enabled in production by default.

- **Business-level metrics must be emitted alongside technical metrics.**
  In addition to latency, error rate, and throughput, services must emit metrics relevant to business operations: baskets created, orders confirmed, payments settled, e-tickets issued, boarding passes generated. These metrics must be dashboarded and alerted on.
