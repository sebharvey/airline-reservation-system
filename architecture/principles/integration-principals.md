# Integration Principles
**Apex Air — Reservation Platform**

---

## API Style

All APIs are RESTful, JSON-native, and defined using OpenAPI Specification 3.x.

- REST over HTTP is the default for all synchronous communication between channels, orchestration APIs, and microservices.
- JSON (`Content-Type: application/json`) is the default for all request and response bodies; XML is used only at the NDC channel boundary.
- All APIs must have a machine-readable OpenAPI 3.x spec, version-controlled alongside the service code and kept in sync with the implementation — the spec is the contract.
- GraphQL is not used for service-to-service communication; RESTful JSON APIs with purpose-built endpoints are preferred.

---

## Resource Design

APIs are resource-oriented using nouns for entities and HTTP verbs for operations.

- URL paths identify resources, not actions; verbs in paths (e.g. `/confirmBasket`) are not permitted (e.g. use `POST /basket/{basketId}/confirm`).
- HTTP methods used semantically: `GET` (safe, idempotent reads), `POST` (create or trigger actions), `PUT` (full replacement), `PATCH` (partial update), `DELETE` (removal).
- URL paths use kebab-case for multi-word segments (e.g. `/stored-offers/{offerId}`); query parameters use camelCase.
- Collection endpoints support pagination; cursor-based preferred for large/frequently updated collections, page-based acceptable for smaller stable ones; pagination metadata included in responses.

---

## HTTP Standards

HTTP status codes and error responses must be used accurately and consistently.

- Standard codes: `200` reads, `201` creates (with `Location` header), `204` no content, `400` validation errors, `401` unauthenticated, `403` unauthorised, `404` not found, `409` state conflict, `422` semantically invalid, `429` rate limited, `500` server error.
- All error responses include a machine-readable `errorCode`, human-readable `message`, `correlationId`, and optional field-level `details`.
- `Location` header returned on all `201 Created` responses with the URI of the newly created resource.
- ETags and conditional headers (`If-Match`, `If-None-Match`) used for optimistic concurrency on mutable resources (orders, baskets); `409` returned on stale ETag updates.

---

## Versioning

APIs are versioned via URI path from first release, with breaking changes requiring a new major version.

- URI path versioning only: `/v1/`, `/v2/` etc.; header- or query-parameter versioning is prohibited; version numbers are integers only.
- Breaking changes (removed fields, type changes, removed endpoints, changed status code semantics) always require a new major version; non-breaking additions do not.
- Previous versions supported for a minimum 6-month deprecation period; deprecated versions return `Deprecation` and `Sunset` headers.
- OpenAPI specs document version, status (active/deprecated/sunset date), and a changelog from the previous version.

---

## Idempotency

All state-changing operations that may be retried must be safe to replay.

- Callers must be able to safely retry requests on network failure without risk of duplicate side effects; critical for payment authorisation, e-ticket issuance, and inventory updates.
- `POST` operations on critical resources require a client-generated `Idempotency-Key` UUID; servers store key and response for 24 hours and return the stored response on duplicate submission.
- `GET`, `PUT`, and `DELETE` are idempotent by design; repeated `DELETE` on a non-existent resource returns `204` or `404` consistently, not an error.

---

## Asynchronous Integration

Event-driven integration via Azure Service Bus is used for all async, decoupled communication.

- Events used for downstream reactions to order state changes (accounting, points accrual); publisher has no dependency on or awareness of consumers.
- Events describe domain facts, not commands: `OrderConfirmed`, `OrderChanged`, `OrderCancelled`; consumers decide independently how to react.
- Event payloads are self-contained with all data consumers are likely to need — no synchronous callback to the publisher required (e.g. `OrderConfirmed` includes booking reference, loyalty number, flights, fare, cabin, passenger type).
- Events include a schema version, correlation ID, and timestamp for versioned parsing, tracing, and ordering.

---

## External and Partner Integration

External integrations are isolated at the boundary and abstracted behind anti-corruption layers.

- NDC follows the IATA NDC standard; XML-to-JSON transformation occurs at the APIM layer or NDC adapter — internal microservices remain NDC-agnostic.
- Each NDC partner / OTA has a unique scoped API key for attribution, rate limiting, and independent revocation; onboarding follows a documented process.
- Payment processor integration encapsulated entirely within Payment MS; no other service calls the processor directly — switching processors requires changes only within Payment MS.
- All third-party integrations wrapped in an anti-corruption layer; external models must not leak into internal domain models.

---

## Service-to-Service Communication

Orchestration APIs are the only layer permitted to call multiple microservices within a single request flow.

- **No direct microservice-to-microservice communication:** Microservices are isolated units of capability. They expose APIs to be called by orchestration layers and publish events to the event bus for async consumption. A microservice must never call another microservice directly. All synchronous interactions between two or more microservices are the exclusive responsibility of the orchestration API layer. This applies without exception — including internal platform calls, validation lookups, and data enrichment.
- Microservices must not call other microservices synchronously; all inter-service synchronous calls route through the orchestration layer.
- Correlation IDs generated at the channel boundary are propagated as a header (e.g. `X-Correlation-ID`) on every downstream call and event payload; all log entries include it.
- Timeouts configured on all outbound service calls; values fail fast enough to allow the orchestration layer to execute compensation before its own timeout.
- Consumer-driven contract tests maintained by each orchestration API run in CI to catch microservice breaking changes before deployment.

---

## Data Formats and Conventions

Data formats are standardised across all services and APIs.

- Timestamps: ISO 8601 UTC (e.g. `"2025-08-15T11:00:00Z"`); time zone conversion is the channel's responsibility.
- Monetary amounts: `DECIMAL` with two decimal places plus explicit currency code (e.g. `{ "amount": 437.25, "currency": "GBP" }`); floating-point types prohibited for money.
- Airport codes: IATA 3-letter (`CHAR(3)`, uppercase); ICAO codes not used in API contracts unless specifically required.
- Passenger types: IATA standard codes — `ADT`, `CHD`, `INF`, `YTH` — used uniformly across all services, APIs, and events.
- JSON field names: camelCase throughout; database column names use PascalCase with mapping in the serialisation layer.

---

## API Documentation and Discoverability

OpenAPI specifications must be published, complete, and proactively communicated to consumers.

- Developer portal (e.g. Azure APIM built-in portal) hosts all orchestration API specs; internal microservice specs accessible within the engineering network.
- Every endpoint, parameter, and response schema must have meaningful descriptions — an empty-description spec is not sufficient.
- Breaking changes communicated to all known consumers before deployment, with change detail, migration path, and sunset date for the old version.
