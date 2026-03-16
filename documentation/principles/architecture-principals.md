# Architecture Principles
**Apex Air — Reservation Platform**

---

## Domain-Driven Design

The system is structured around eight bounded contexts aligned to airline capability domains, with strict data ownership and explicit boundaries enforced throughout.

- Eight core domains — Offer, Order, Payment, Delivery, Customer, Identity, Accounting, Seat — each with clear ownership and a ubiquitous language; domain names used uniformly in code, schemas, API paths, and docs.
- Domain models are owned exclusively by their service; cross-domain data access occurs only via APIs (synchronous) or events (asynchronous), never through shared schemas.
- Anti-corruption layers required at all external integration boundaries (NDC, GDS, payment processors) to prevent external schemas contaminating internal domain models.

---

## Microservices

Each microservice owns a single bounded context, its own data store, and an independent deployment lifecycle.

- Independently deployable; internal changes must not require coordinated releases with other services.
- Loosely coupled via well-defined API contracts and event schemas; internal data model changes must not break consumers.
- Designed for dependency failure: timeouts, circuit breakers, and retry policies applied to all service-to-service calls.
- Stateless where possible; persistent state externalised to the service's own database to enable horizontal scaling without session affinity.

---

## Orchestration vs Choreography

Complex synchronous flows are orchestrated by the API layer; decoupled downstream reactions use event-driven choreography.

- Multi-step synchronous flows (e.g. booking confirmation) owned by the Retail API, which controls the sequence, compensating actions, and failure handling — microservices must not call each other synchronously.
- Async downstream reactions (Accounting, Customer points accrual) use choreography via the event bus; consumers are unknown to the publisher.
- Orchestration APIs own all compensating transaction logic; microservices must expose compensation endpoints (void, reinstate) to support rollback.

---

## No Direct Microservice Communication

Microservices must not call other microservices. This constraint applies without exception.

Direct service-to-service calls create tight coupling, make independent deployment harder, obscure system behaviour in distributed traces, and undermine the orchestration layer as the single point of control for cross-domain flows. All cross-microservice coordination must pass through an orchestration API.

See the Integration Principles for the authoritative statement of this rule and its application to service-to-service communication patterns.

---

## IATA Standards Alignment

The platform aligns to IATA ONE Order and NDC standards for interoperability and industry-standard retailing semantics.

- Order domain follows the IATA ONE Order model; `OrderData` maps to passengers, segments, order items, payments, and e-tickets.
- NDC is a first-class channel with parity to Web and App; XML transformation handled at the channel boundary — internal services remain JSON-native.
- E-ticket numbers follow IATA format: 3-digit airline code + 10-digit serial (e.g. `932-1234567890`); immutable after issuance — changes trigger reissuance, not amendment.
- Boarding pass barcodes comply with IATA Resolution 792 (BCBP); the Delivery MS assembles the BCBP string, channels handle rendering.
- Aircraft type codes use the Apex Air 4-character convention aligned to IATA SSIM (e.g. `A351`, `B789`, `A339`); applied uniformly across all services, databases, and API contracts.

---

## Price Integrity

Offers are persisted at the point of presentation and retrieved by ID at order creation — the price shown is always the price charged.

- `StoredOffer` pattern: fare snapshot locked at search time; Order API retrieves by `OfferId` and rejects expired or already-consumed offers.
- Offer IDs are single-use; `IsConsumed` set atomically on retrieval to prevent concurrent orders using the same offer.
- `SeatOfferIds` are session-scoped and must not be cached or reused across booking sessions.

---

## Separation of Concerns

Each service has a precisely scoped responsibility, and cross-domain data flows are explicit and deliberately limited.

- Seat MS owns seatmap layout and pricing rules; Offer MS owns seat inventory (availability per flight) — changes to either can be made independently.
- Delivery MS is the authoritative source for who is on each flight; data is populated explicitly by the Retail API, not by reading from `order.Order`.
- Identity MS holds credentials only; loyalty and profile data lives in Customer MS, linked via an opaque `IdentityReference`.
- Accounting MS is entirely event-driven and must never be called synchronously during the booking path.

---

## Resilience and Reliability

All services must tolerate dependency failures and support recovery without data loss.

- Retry with exponential backoff and jitter (max 3 attempts); idempotency keys required on payment authorisation, e-ticket issuance, and inventory updates.
- Circuit breakers on all service-to-service calls; open state returns a fast failure and circuit breaker status must be observable via health metrics.
- All state-changing operations log sufficient detail for manual reconciliation; compensation actions logged with correlation IDs linking back to the originating transaction.
- Every service exposes a `/health` endpoint reporting dependency health (database, event bus), integrated with platform monitoring and alerting.

---

## Scalability

Services are horizontally scalable by design, with high-volume async processing decoupled from the synchronous booking path.

- Stateless services backed by externalised state scale by adding instances; Azure Functions scale automatically, with concurrency limits tuned per function via load testing.
- Points accrual, accounting, and all downstream order-event reactions must be event-driven — not in the synchronous booking path.
- Database access patterns (indexing, connection pooling, read replicas) designed per domain read/write ratio: Offer is read-heavy, Order more balanced.

---

## Testability

Services must be independently testable and API compatibility validated at build time.

- Each microservice has unit and integration tests runnable without dependent services; external dependencies injected and mockable.
- Consumer-driven contract tests (e.g. Pact) between orchestration APIs and microservices run in CI to catch breaking changes before deployment; API versioning introduced before any breaking change.
- End-to-end tests cover the full booking path including payment failure, inventory rollback, and e-ticket void scenarios, running against a staging environment.

---

## Observability

Distributed tracing, structured logging, and business metrics must be implemented across all services.

- `CorrelationId` (or W3C `traceparent`) generated at the channel boundary and propagated through all calls and event payloads; every log entry includes it.
- Structured JSON logs emitted to a centralised sink (e.g. Azure Monitor / Application Insights); verbose/debug logging disabled in production by default.
- Business metrics emitted alongside technical metrics — baskets created, orders confirmed, payments settled, e-tickets issued, boarding passes generated — dashboarded and alerted on.
