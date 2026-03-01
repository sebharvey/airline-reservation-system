# Integration Principles
**Apex Air — Reservation Platform**

---

## API Style

- **All internal and external APIs must be RESTful, using HTTP as the transport protocol.**
  REST is the default integration style for all synchronous communication between channels, orchestration APIs, and microservices. RESTful design provides a well-understood, toolable, and interoperable contract that aligns with airline industry standards and the broader technology ecosystem.

- **JSON must be the default data format for all API request and response bodies.**
  JSON is lightweight, human-readable, and universally supported across client frameworks, testing tools, and API gateways. All services must accept and emit `Content-Type: application/json`. XML is only used at the NDC channel boundary where the IATA NDC standard requires it; internal services remain JSON-native.

- **API contracts must be defined using OpenAPI Specification (OAS) 3.x.**
  Every orchestration API and microservice API must have a machine-readable OpenAPI specification. Specifications must be version-controlled alongside the service code, kept up to date with the implementation, and used to generate client SDKs and documentation. The spec is the contract; implementation must conform to it, not the other way around.

- **GraphQL is not used for service-to-service communication in this platform.**
  RESTful JSON APIs with well-defined, purpose-built endpoints are preferred for the bounded context communication patterns in this system. GraphQL may be considered for future customer-facing query aggregation use cases but is not a default integration pattern.

---

## Resource Design

- **APIs must be resource-oriented, using nouns to identify entities and HTTP verbs to express operations.**
  URL paths must identify resources, not actions. For example: `GET /orders/{bookingRef}` to retrieve an order; `PATCH /orders/{bookingRef}/passengers` to update passenger details; `POST /basket/{basketId}/confirm` to confirm a basket. Verbs in URL paths (e.g. `/confirmBasket`) are not permitted.

- **HTTP methods must be used semantically and consistently.**
  `GET` for read operations (must be safe and idempotent); `POST` for creating resources or triggering actions where idempotency is not guaranteed; `PUT` for full resource replacement; `PATCH` for partial updates; `DELETE` for resource removal. Method choice must reflect the actual semantics of the operation.

- **URL paths must use kebab-case for multi-word path segments.**
  For example: `/stored-offers/{offerId}`, `/booking-reference/{ref}`. Query parameter names must use camelCase consistent with JSON field naming conventions.

- **Collections must support pagination using consistent parameters.**
  All collection endpoints that may return more than a bounded number of results must support pagination. Cursor-based pagination is preferred for large or frequently updated collections (e.g. loyalty transaction history). Page-based pagination (`page`, `pageSize`) is acceptable for smaller, stable collections. Pagination metadata must be included in the response.

---

## HTTP Standards

- **HTTP status codes must be used accurately and consistently.**
  `200 OK` for successful reads; `201 Created` for successful resource creation (with a `Location` header); `204 No Content` for successful operations with no response body; `400 Bad Request` for client validation errors; `401 Unauthorized` for missing or invalid authentication; `403 Forbidden` for authenticated but unauthorised requests; `404 Not Found` when a resource does not exist; `409 Conflict` for state conflicts (e.g. offer already consumed); `422 Unprocessable Entity` for semantically invalid requests; `429 Too Many Requests` for rate limit breaches; `500 Internal Server Error` for unexpected server-side failures.

- **Error responses must follow a consistent, structured format.**
  All error responses must include at minimum: a machine-readable error code, a human-readable message, and (where applicable) field-level validation details. A correlation ID must always be included to enable support tracing. Example structure:
  ```json
  {
    "errorCode": "OFFER_EXPIRED",
    "message": "The selected offer has expired. Please perform a new search.",
    "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "details": []
  }
  ```

- **`Location` headers must be returned on `201 Created` responses.**
  When a new resource is created (e.g. a basket, an order, a payment), the response must include a `Location` header with the URI of the newly created resource, enabling the client to retrieve it without constructing the URL.

- **`ETag` and conditional request headers (`If-Match`, `If-None-Match`) must be used for optimistic concurrency on mutable resources.**
  Order and basket updates must use ETags to detect concurrent modifications. A `409 Conflict` must be returned when an update is attempted against a stale ETag, preventing lost updates in concurrent booking scenarios.

---

## Versioning

- **APIs must be versioned from the first release.**
  URI path versioning is the mandatory approach: `/v1/`, `/v2/`, etc. Header-based or query-parameter versioning must not be used as they are less discoverable and harder to route at the API Gateway. Version numbers must be integers; no minor version in the URL.

- **Breaking changes must always result in a new major version.**
  A breaking change is any change that could cause a correctly implemented consumer to fail: removing a field, changing a field's type, changing required/optional semantics, removing an endpoint, or changing HTTP status code semantics. Non-breaking additions (new optional fields, new endpoints) do not require a version increment.

- **Previous API versions must be supported for a defined deprecation period.**
  A minimum of 6 months' notice must be given before a version is retired. Deprecated versions must return a `Deprecation` response header and a `Sunset` header indicating the planned retirement date, allowing clients to plan migration.

- **API specifications must document the version, change history, and deprecation status.**
  The OpenAPI spec for each version must include a `version` field, an info description noting the version's status (active, deprecated, sunset date), and a changelog section documenting changes from the previous version.

---

## Idempotency

- **All state-changing API operations that may be retried must support idempotency.**
  Callers must be able to safely retry a request on network failure without risk of duplicate side effects. Payment authorisation, e-ticket issuance, and inventory updates are examples where duplicate execution must be detected and handled.

- **Idempotency keys must be used for `POST` operations on critical resources.**
  Clients must generate a unique `Idempotency-Key` UUID per logical operation and include it in the request header. Servers must store the key and response for a defined period (e.g. 24 hours) and return the stored response on duplicate submission rather than re-executing the operation.

- **`GET`, `PUT`, and `DELETE` operations must be idempotent by design.**
  Multiple identical `GET` requests must return the same result (modulo state changes between requests). Multiple identical `PUT` requests must leave the resource in the same state. Multiple identical `DELETE` requests must not error on the second call if the resource no longer exists (return `204` or `404` consistently).

---

## Asynchronous Integration

- **Event-driven integration via Azure Service Bus must be used for all asynchronous, decoupled communication.**
  Downstream reactions to order state changes (accounting entries, points accrual) must be event-driven. The publisher must not depend on, wait for, or be aware of the consumers. Events must be durable and replayable from the dead-letter queue.

- **Events must be designed around domain facts, not commands.**
  Events describe something that has happened: `OrderConfirmed`, `OrderChanged`, `OrderCancelled`. They must not instruct consumers what to do. Consumers decide independently how to react. This prevents inadvertent coupling of publisher and consumer behaviour.

- **Event payloads must be self-contained and include all data consumers are likely to need.**
  Consumers must not need to call back to the publisher to retrieve additional data in order to process an event. The `OrderConfirmed` event must include booking reference, loyalty number, flights, fare amount, cabin code, and passenger type — all the fields needed by Customer and Accounting to process the event without a synchronous callback.

- **Events must include a schema version, correlation ID, and timestamp.**
  These fields enable consumers to apply appropriate parsing logic, trace events back to originating transactions, and reason about event ordering and latency.

---

## External and Partner Integration

- **NDC integration must follow the IATA NDC standard (latest supported level).**
  The NDC channel is a first-class integration point for GDS partners and OTAs. XML request/response transformation between the NDC wire format and Apex Air's internal JSON model must occur at the APIM layer or a dedicated NDC adapter service, keeping internal microservices NDC-agnostic.

- **Partner integrations must use scoped API keys issued per partner, with rate limits applied per key.**
  Each NDC partner or OTA must be issued a unique API key, allowing traffic to be attributed, rate-limited, and revoked per partner independently. Key issuance and rotation must follow a documented onboarding process.

- **Third-party payment processor integration must be encapsulated entirely within the Payment microservice.**
  No other service or orchestration layer may call the payment processor directly. The Payment microservice abstracts the processor-specific API; switching processors must require only changes within the Payment microservice boundary.

- **Third-party integrations must be wrapped in an anti-corruption layer.**
  External system models must not leak into internal domain models. Translations between external and internal representations must occur at the integration boundary. This protects the internal architecture from external schema changes.

---

## Service-to-Service Communication

- **Orchestration APIs are the only layer permitted to call multiple microservices within a single request flow.**
  Microservices must not call other microservices synchronously. All inter-service synchronous communication must be routed through the orchestration layer, which owns the sequence and compensation logic. This preserves microservice independence and prevents hidden coupling.

- **All service-to-service calls must include a correlation ID propagated from the originating request.**
  The correlation ID must be generated at the channel boundary (or API Gateway) and passed as a request header (e.g. `X-Correlation-ID`) on every downstream call and event payload. Every log entry must include this ID to enable end-to-end request tracing.

- **Timeouts must be configured on all outbound service calls.**
  No service call must be allowed to wait indefinitely for a response. Timeouts must be tuned to the expected response time of the downstream service, with values that fail fast enough to allow the orchestration layer to execute compensation before the caller's own timeout is reached.

- **Service contracts must be validated at build time using consumer-driven contract tests.**
  Each orchestration API must maintain a set of contract tests against the microservice APIs it calls. These tests must run in CI and must catch breaking changes before they reach a shared environment.

---

## Data Formats and Conventions

- **All timestamps must be in ISO 8601 format and in UTC.**
  Example: `"2025-08-15T11:00:00Z"`. Local time representations must not appear in API payloads. Display-layer time zone conversion is the responsibility of the channel, not the API.

- **All monetary amounts must be represented as `DECIMAL` with two decimal places, accompanied by an explicit currency code.**
  Floating-point types must not be used for monetary values. Currency must always be specified alongside the amount — never assumed. Example: `{ "amount": 437.25, "currency": "GBP" }`.

- **IATA airport codes (IATA Location Identifier, 3-letter) must be used for all airport references.**
  ICAO codes must not be used in API contracts unless specifically required for a partner integration. Airport codes must be `CHAR(3)` and uppercase throughout.

- **Passenger types must follow IATA standard codes: `ADT` (Adult), `CHD` (Child), `INF` (Infant), `YTH` (Youth).**
  These codes must be used consistently across all services, APIs, and event payloads.

- **Field naming must use camelCase in all JSON payloads.**
  Pascal case, snake_case, and kebab-case must not be used in JSON body fields. Database column names use PascalCase; API payload field names use camelCase. Mapping between the two occurs in the serialisation layer.

---

## API Documentation and Discoverability

- **OpenAPI specifications must be published and accessible to all internal consumers.**
  A developer portal (e.g. Azure API Management's built-in developer portal) must host the specifications for all orchestration APIs. Internal microservice specs must be accessible within the engineering network.

- **APIs must include meaningful descriptions on all endpoints, parameters, and response schemas.**
  An OpenAPI spec with empty descriptions is not sufficient. Every endpoint must document its purpose, required inputs, optional parameters, and all response codes with explanations. This reduces integration friction and support burden.

- **Breaking changes must be communicated to all known consumers before deployment.**
  A process must exist to identify all active consumers of an API version (via API Management analytics or consumer registration) and notify them in advance of a breaking change or version deprecation. Communication must include the change detail, the new version's migration path, and the sunset date of the old version.
