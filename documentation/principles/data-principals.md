# Data Storage Principles
**Apex Air — Reservation Platform**

---

## Data Ownership

Each microservice is the sole owner of its data; no direct cross-domain data access is permitted.

- Schema-level isolation enforced: `offer.*`, `order.*`, `payment.*`, etc.; cross-schema foreign keys not declared; cross-domain data access via API or event consumption only.
- Cross-schema references (e.g. `InventoryId` in `delivery.FlightManifest`) stored as plain columns with integrity maintained at the application/orchestration layer.
- Denormalisation within a service's own schema is permitted (e.g. `FlightNumber` on `delivery.FlightManifest`) but must be explicitly documented with the canonical source of truth identified.

---

## Schema Design

Database schemas use domain-prefixed naming with consistent conventions across all services.

- Schema names match capability domains: `offer.*`, `order.*`, `payment.*`, `delivery.*`, `customer.*`, `identity.*`, `seat.*`; used uniformly in code, migrations, and docs.
- Primary keys use `UNIQUEIDENTIFIER` (UUID v4, `NEWID()`) to prevent sequential enumeration, reduce collision risk, and support future physical database separation.
- All tables include `CreatedAt` and `UpdatedAt` as `DATETIME2` UTC (`SYSUTCDATETIME()` default); `UpdatedAt` maintained by application or trigger on every write.
- Immutable tables (e.g. `payment.PaymentEvent`, `customer.LoyaltyTransaction`) are append-only; no update or delete paths exposed through any service API.
- `NVARCHAR(MAX)` JSON columns must carry an `ISJSON` check constraint; structural validation beyond this occurs at the application layer before write.
- Nullable columns used deliberately and documented; transiently nullable columns should be evaluated for a state machine or status column approach instead.

---

## JSON Document Storage

JSON columns are used for complex, variable-structure documents read and written as a unit.

- Suitable for `OrderData`, `BasketData`, `CabinLayout` — consumed holistically with internally variable structure; not a substitute for relational modelling on regularly queried or joined fields.
- Scalar fields used for indexed lookups, lifecycle management, routing, or eventing must be typed columns (e.g. `OrderStatus`, `BookingReference`, `TotalAmount`); these must not be duplicated inside the JSON document.
- Frequently queried JSON properties must use SQL Server persisted computed columns with JSON path expressions to enable indexing, rather than full-document scans.
- JSON documents must include a `schemaVersion` field; migration strategies for documents stored under previous versions must be defined before deploying breaking changes.

---

## Indexing Strategy

Indexes must be designed around documented read access patterns and reviewed regularly.

- Every table has at minimum its primary key index; additional indexes justified by a documented query pattern and dropped if unused.
- Partial (filtered) indexes used where queries consistently target a row subset (e.g. basket expiry job indexes only `BasketStatus = 'Active'`).
- Unique indexes enforce business-level uniqueness constraints (e.g. `BookingReference`, `LoyaltyNumber`, `PaymentId`, compound seat/passenger constraints on manifests); not left to application-layer enforcement alone.

---

## Transient vs Persistent Data

Transient pre-sale data is hard-deleted after confirmation; session-scoped identifiers are never persisted long-term.

- Basket data hard-deleted on successful order confirmation; expired and abandoned baskets retained briefly (e.g. 7 days) for diagnostics before automated purge.
- `offer.StoredOffer` rows carry an `ExpiresAt` timestamp; a background job purges expired unconsumed offers; Order API validates offer expiry before consumption.
- Refresh tokens are the only auth artefact persisted to Identity DB, stored as a hash only; access tokens are never stored.
- Session-scoped identifiers (e.g. `SeatOfferId`) must not be persisted in client-side storage beyond the booking session.

---

## Data Lifecycle and Retention

Retention periods are defined per domain, regulatory-compliant, and enforced via automated purge jobs.

- Financial and manifest records retained for a minimum of 7 years; personal data purged or anonymised once the retention period lapses.
- Purge jobs are idempotent, observable (metrics and alerts on failure), and log each action with counts and record types; tested in staging before production deployment.
- A data inventory documents all personal and financial data held, where, and for how long; updated whenever a new service, table, or PII/financial JSON field is introduced.

---

## Data Quality and Integrity

Data integrity is maintained atomically, at the application layer, and as close to the point of creation as possible.

- Running balances (e.g. `PointsBalance`) updated atomically with their transaction row; `BalanceAfter` snapshot on each `LoyaltyTransaction` row is the source of truth on discrepancy.
- Cross-domain referential integrity maintained by the orchestration layer, not database foreign keys — a deliberate consequence of domain isolation.
- Database migrations must be backwards-compatible and applied without downtime; destructive changes follow an expand-contract pattern (add, migrate, update consumers, drop in a separate release).
- Data validated as close to creation as possible (e.g. `SeatNumber` validated against the active seatmap at Delivery MS write time, not only at the API boundary).

---

## Database Technology

Microsoft SQL Server is the standard RDBMS, managed as code with connection pooling monitored per service.

- Single SQL Server instance with logical schema separation for this project; architecture treats each schema as an independent database so physical separation is achievable via connection string change alone.
- All DDL changes applied through a reviewed, automated migration pipeline (e.g. Flyway, EF Core); manual schema changes to production are prohibited; migrations idempotent where possible.
- Connection pool settings tuned per function to concurrency profile; pool exhaustion must generate an alert.
- Reporting, analytics, and non-operational queries routed to read replicas or read-only endpoints; analytical workloads must not compete with transactional workloads on the primary.
