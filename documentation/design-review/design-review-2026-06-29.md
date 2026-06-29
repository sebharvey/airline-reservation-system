# Apex Air — Weekly Design Review

**Date:** 2026-06-29
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-06-01.md

---

## 1. Executive summary

The platform posture remains **Non-conforming** for a second consecutive cycle, held there by two CRITICAL AGEING findings that continue to age without action. H-04 (bag tag sequence number randomly generated, IATA Resolution 740 violation) now enters week 6 with no change; H-05 (Offer MS calling Schedule MS and Ancillary MS directly) enters week 5. N-11 (DatabaseMcp tool deployed to Azure with unrestricted PII read access and no audit logging) is now in its second review cycle without remediation. The single most important positive development this cycle is the substantial recovery of automated test execution: 73 commits added four new test projects (Offer, Order, Schedule, Retail orchestration) and wired the existing Delivery test project into its CI pipeline — 26 test methods now execute in 5 CI workflows, reversing the prior cycle's zero-test regression. A new test maintenance obligation (key rule 13) has been codified in CLAUDE.md. One new low-severity documentation finding is raised for the `isVip` passenger field, which was committed to OrderData and the Terminal app without any documentation update in the design or API reference. The single most important action this week remains closing H-04 and H-05, both of which have exceeded any reasonable tolerance for delay.

---

## 2. Critical findings (act this week)

### H-04 — Bag tag sequence number randomly generated; IATA Resolution 740 uniqueness violated — CRITICAL AGEING (Week 6)

**Severity:** CRITICAL AGEING (week 6 — unchanged for six consecutive reviews)
**Principle breached:** Architecture Principals — IATA Standards Alignment; IATA Resolution 740 requires bag tag licence plate numbers to be globally unique per airline per flight day.

**Evidence (unchanged for sixth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Application/OciCheckIn/OciCheckInHandler.cs:381–382` — `// TODO: In future, this 6-digit sequence number needs to be auto-incremented from a persistent counter rather than generated randomly. var sequence = Random.Shared.Next(0, 1_000_000).ToString("D6");`
- The same `Random.Shared.Next` generator is invoked for both OCI self check-in and agent check-in paths. No SQL `SEQUENCE` object, no persistent counter table, and no remediation action has taken place across six review cycles.

**Impact if unresolved:** Duplicate bag tags at any meaningful check-in volume (expected probability of a collision exceeds 50% once a single flight exceeds approximately 1,100 check-ins from the birthday paradox applied to a 1,000,000-space range). Bag misrouting and loss; IATA Resolution 740 non-compliance; operational and safety liability.

**Recommended remediation:** Replace `Random.Shared.Next` with `NEXT VALUE FOR delivery.BagTagSequence` (SQL Server `SEQUENCE` object in the `delivery` schema, scoped per airline numeric prefix). Pass the next sequence value into `GenerateBagTag()` from the repository. The mod-7 check-digit logic is correct and unchanged. Close the TODO comment once done.

**Age:** 6 weeks. No change recorded in 73 commits since the last review.

---

### H-05 — Offer MS timer trigger calls Schedule MS and Ancillary MS directly — CRITICAL AGEING (Week 5)

**Severity:** CRITICAL AGEING (week 5 — escalated from HIGH in the prior cycle; unchanged since)
**Principle breached:** Architecture Principals and Integration Principals — "No direct microservice-to-microservice communication. This constraint applies without exception."

**Evidence (unchanged for fifth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/ScheduleServiceClient.cs:11` — self-granted exception comment: `"Note: direct MS-to-MS calls are an accepted exception for timer triggers."` No governance document or ADR grants this exception.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/SeatServiceClient.cs:10` — acknowledged violation: `"TODO: remove this cross-domain call — cabin counts are already stored in offer.FlightInventory.Cabins"`.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Application/RollingInventoryImport/RollingInventoryImportHandler.cs:37,54` — both clients injected and called.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Program.cs:76–91` — `ScheduleMs` and `AncillaryMs` HTTP clients registered.

**Impact if unresolved:** Deployment coupling; obscured distributed traces; no circuit breaker — a Schedule MS or Ancillary MS outage at the 01:00 UTC nightly inventory import silently fails without alerting or compensation.

**Recommended remediation (in preference order):**
1. `SeatServiceClient` — implement the acknowledged TODO: derive cabin counts from existing `offer.FlightInventory.Cabins` rows for the same `AircraftType`. The data is already present locally; no cross-service call is required.
2. `ScheduleServiceClient` — raise an ADR to formally grant or deny the exception. If granted, add circuit breaker, retry, timeout, and dead-letter alerting. If denied, move the timer trigger to a dedicated `RollingInventoryOrchestration` function in the Operations API.
3. Regardless of path: delete the self-granted exception comment from `ScheduleServiceClient.cs:11`.

**Age:** 5 weeks.

---

## 3. Status of prior findings

| Finding | Prior Severity | Status | Evidence note |
|---------|---------------|--------|--------------|
| H-04 — Bag tag sequence randomly generated | CRITICAL AGEING | **UNCHANGED** | `OciCheckInHandler.cs:381–382` — `Random.Shared.Next(0, 1_000_000)` confirmed unchanged across 73 commits. Week 6. |
| H-05 — Offer MS calls Schedule MS and Ancillary MS directly | CRITICAL AGEING | **UNCHANGED** | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10` unchanged. Week 5. |
| N-11 — DatabaseMcp unrestricted PII read access, deployed to Azure | HIGH | **UNCHANGED** | `McpFunction.cs` — no audit logging, schema allowlist, or row cap added. `service-urls.md:57` still says "Local only". Week 2. |
| M-07 — No integration test execution in CI | MEDIUM | **PARTIALLY RESOLVED** | 5 CI workflows now execute specific test projects (Delivery, Offer, Order, Schedule, Retail) with 26 real test methods. 12 pipelines still run vacuous `dotnet test --no-build` without a test project path. |
| H-03r — dotnet test gate executes zero tests | MEDIUM | **PARTIALLY RESOLVED** | Same as M-07: 5 services now have real test execution; 12 remain vacuous. |
| C-02r — Debug infrastructure retained in production code | MEDIUM | **UNCHANGED** | `GetOrderDebugHandler.cs` and `GetOrderDebugQuery.cs` still present in `Order.Application.GetOrderDebug/`. |
| N-01 — Seatmap booking-window occupancy gap | MEDIUM | **UNCHANGED** | Pre-booking seatmap derives occupancy from manifest only; in-progress `offer.SeatReservation` holds not surfaced. |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `GetSeatOffersHandler.cs:20` TODO unchanged. |
| N-05 — Bag tag response field documented incorrectly | MEDIUM | **UNCHANGED** | `delivery.md:331` still attributes bag tag generation to the Airport API; Delivery MS OCI handler generates it. |
| N-10 — OrderData JSON lacks schemaVersion | MEDIUM | **UNCHANGED** | No `schemaVersion` field added. 73 commits continued the `paxId` migration without adding `schemaVersion`. |
| M-08 — TierConfig table not mapped with HasTrigger | LOW | **UNCHANGED** | `CustomerDbContext.cs` has HasTrigger for Customer, LoyaltyTransaction, Preferences, and CustomerNote — but not TierConfig. `TR_TierConfig_UpdatedAt` exists in `Script.sql`. |
| M-10 — TODO without issue reference committed to main | LOW | **UNCHANGED** | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs without author or issue reference remain. |
| N-08 — HandleDelayHandler throws NotImplementedException | LOW | **UNCHANGED** | `HandleDelayHandler.cs:35` still throws `NotImplementedException()`; `api-reference.md:321` documents the endpoint as returning `200 OK`. |
| N-09 — GET /v1/products basketId parameter undocumented | LOW | **UNCHANGED** | `api-reference.md:98` still does not mention the optional `basketId` query parameter or rule evaluation logic. |
| N-13 — Post-departure manage-booking lockout business rule undocumented | LOW | **UNCHANGED** | `manage-booking.md` has no mention of departure lockout enforcement on change, cancel, add-bags, or add-seats. `api-reference.md` endpoint descriptions do not reflect the 422 gate. |
| N-14 — DatabaseMcp tool absent from api-reference.md | LOW | **UNCHANGED** | No entry in `api-reference.md`; `service-urls.md:57` still describes the tool as "Local only" when it is deployed to Azure. |

---

## 4. High findings

### N-11 — DatabaseMcp tool: unrestricted PII read access, no audit logging, documented as local-only but deployed to Azure — HIGH (Week 2)

**Severity:** HIGH (week 2 — escalates to CRITICAL AGEING if still open in the 2026-07-06 review)
**Principle breached:** Security Principals — "PII must never appear in logs, telemetry, or error messages" (by extension, unrestricted PII reads with no trail breach the audit spirit); "All state-changing operations must produce a structured, immutable audit log entry" (the spirit of audit applies to bulk PII access paths); Infrastructure Principals — "All Azure resources authenticate via Managed Identities; no embedded credentials permitted" (connection string provenance unverifiable from code).

**Evidence (unchanged from prior review):**
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:85–101` — `run_select_query` MCP tool accepts arbitrary SQL `SELECT` or CTE from any holder of the function host key and executes it against the database. No schema allowlist, no table blocklist, no row cap, no query audit log.
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:144–163` — `RunSelectQuery` opens a `SqlConnection(ConnectionString)` and executes caller-supplied SQL verbatim. A holder of the key can issue `SELECT * FROM customer.Customer`, `SELECT * FROM [identity].RefreshToken`, `SELECT * FROM payment.Payment`, or `SELECT * FROM [order].[Order]` — any PII-bearing table in any schema.
- `documentation/service-urls.md:57` — still describes the tool as "Local only — Azure Functions host, listens on `http://localhost:7071`". The `.github/workflows/main_reservation-system-mcp-db.yml` workflow deploys it to Azure on every push to main.
- The only control added since initial discovery: `McpFunction.cs:231` rejects SQL containing semi-colons (one-statement enforcement). This is a minor hardening that does not address the core concerns.

**Impact if unresolved:** Any holder of the `reservation-system-mcp-db` function key has unrestricted bulk-read access to all PII across all schemas with no application-layer audit trail. Combined with the inaccurate documentation, operators cannot assess the real attack surface. If the function key is compromised, there is no way to determine what data was accessed.

**Recommended remediation:**
1. Correct `documentation/service-urls.md:57` to accurately reflect that the tool is deployed to Azure (immediate — documentation only).
2. Add query audit logging: every `run_select_query` call must emit a structured log entry with the query text, row count, and timestamp.
3. Enforce a schema allowlist: permit queries only against `offer.*` and `schedule.*`; block `customer.*`, `identity.*`, `payment.*`, and `order.*`. Document any approved exception explicitly.
4. Add a row cap (e.g. `TOP 500`) to `RunSelectQuery` to prevent bulk extraction in a single call.
5. Add an api-spec document (`documentation/api-specs/database-mcp.md`) and an entry in `api-reference.md` and `CLAUDE.md` documentation map.
6. Verify the database connection string used by the tool is a read-only, schema-restricted SQL login — not the full application credential.

**Age:** 2 weeks. Escalates to CRITICAL AGEING if unchanged in the next review.

---

## 5. Medium and low findings

### Test coverage gaps (M-07 residual)

Five CI workflows now execute real test methods (26 methods across Delivery, Offer, Order, Schedule, and Retail orchestration). However, 12 pipelines still run the vacuous `dotnet test --no-build --configuration Release` gate with no test project path and zero test methods executed:

| Pipeline | Test project available? | Test wired? |
|----------|------------------------|-------------|
| `main_reservation-system-db-api-admin.yml` | No | — |
| `main_reservation-system-db-api-loyalty.yml` | No | — |
| `main_reservation-system-db-api-operations.yml` | **Yes** — `ReservationSystem.Tests.Orchestration.Operations.csproj` exists | **Not wired** |
| `main_reservation-system-db-microservice-customer.yml` | No | — |
| `main_reservation-system-db-microservice-identity.yml` | No | — |
| `main_reservation-system-db-microservice-payment.yml` | No | — |
| `main_reservation-system-db-microservice-user.yml` | No | — |
| All other non-domain pipelines | No | — |

The Operations orchestration pipeline has a test project (`CheckInHelperTests.cs`) but the workflow still uses the vacuous gate — a straightforward one-line fix.

### Carried medium and low findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| C-02r | Debug infrastructure in production code | MEDIUM | C | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs`, three `AdminDebug*` Retail API functions, and debug client methods still present. All authenticated; dead code that should be deleted. | Retail API / Order MS owner |
| N-01 | Seatmap booking-window occupancy gap | MEDIUM | A / B | Pre-booking seatmap shows manifest-only occupancy; in-progress `offer.SeatReservation` holds not surfaced. DB constraint preserves integrity. | Retail API / Offer MS owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A | `GetSeatOffersHandler.cs:20` TODO unchanged. Business logic belongs in Application layer. | Ancillary MS owner |
| N-05 | Bag tag documentation incorrect | MEDIUM | G | `delivery.md:331` attributes bag tag generation to the Airport API; Delivery MS OCI handler generates it. | Delivery / Operations owner |
| N-10 | OrderData JSON lacks schemaVersion | MEDIUM | D | No `schemaVersion` field. paxId migration (73 commits this cycle) continues without adding one. Existing rows now cannot be distinguished by schema version. | Order MS owner |
| M-08 | TierConfig table not mapped with HasTrigger | LOW | D | `TR_TierConfig_UpdatedAt` in `Script.sql`; `CustomerDbContext.cs` has no TierConfig HasTrigger declaration. | Customer MS owner |
| M-10 | TODO without issue reference on main | LOW | Coding Standards | `OciCheckInHandler.cs:381`, `SeatServiceClient.cs:10` bare TODOs remain. Coding standard requires author and issue reference. | All owners |
| N-08 | HandleDelayHandler throws NotImplementedException | LOW | B | `api-reference.md:321` documents `/v1/disruptions/delay` as `200 OK`; implementation throws `NotImplementedException`. | Operations API owner |
| N-09 | GET /v1/products basketId undocumented | LOW | G | `api-reference.md:98` still does not mention the optional `basketId` parameter or rule evaluation logic. | Retail API / Docs owner |
| N-13 | Post-departure lockout rule undocumented | LOW | G | Departure-lockout enforcement (422 gate on change, cancel, add-bags, add-seats when all segments departed) enforced in code but absent from `manage-booking.md` and `api-reference.md`. | Retail API / Docs owner |
| N-14 | DatabaseMcp absent from api-reference.md | LOW | G | No entry in `api-reference.md`; `service-urls.md:57` still says "Local only" when the tool is deployed to Azure. | Platform Architect |
| N-15 | isVip passenger field not documented | LOW | G | Commit `f4a4197` added `isVip?: boolean` to Terminal passenger flows; `UpdateOrderPassengersHandler.cs:83–86` persists the field in OrderData. Neither `documentation/design/order.md` (passenger schema) nor `documentation/design/manage-booking.md` (PATCH passengers endpoint) nor `api-reference.md` (passenger update endpoint) document this field. | Order MS / Docs owner |

---

## 6. Observations and positive notes

- **M-07 SUBSTANTIALLY RECOVERED this cycle.** The prior cycle regressed to zero test execution by deleting the only two integration test workflows. This cycle added four new xUnit test projects (Offer, Order, Schedule, Retail orchestration) and wired the existing Delivery test project into its CI pipeline. Five workflows now execute specific test projects — 26 test methods run in CI on every push. This is the most significant quality improvement in several review cycles. The new CLAUDE.md rule 13 ("Tests must be validated and updated with every code change") codifies the obligation and gives governance reviewers a clear standard to enforce.

- **paxId migration executed coherently.** The systematic migration from string `passengerId` to integer `paxId` across all affected handlers (Order MS, Delivery MS, Operations API — 73 commits) was delivered consistently and accompanied by test updates. The scope is large and the execution has been disciplined. The residual concern (N-10, schemaVersion) is a documentation gap, not a correctness gap in the migration itself.

- **Operations orchestration test project exists.** `ReservationSystem.Tests.Orchestration.Operations.csproj` containing `CheckInHelperTests.cs` exists and merely needs its workflow wired. This is a one-line fix in `main_reservation-system-db-api-operations.yml`.

- **Semi-colon injection guard added to DatabaseMcp.** `McpFunction.cs:231` now rejects SQL strings containing semi-colons, preventing multi-statement injection. This is a positive hardening step, though the principal concerns about schema scope, audit logging, and documentation accuracy (N-11) remain unaddressed.

---

## 7. Conformance scorecard

| Dimension | Conformance | Trend vs 2026-06-01 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🔴 Red | → | H-04 CRITICAL AGEING week 6 unchanged; H-05 CRITICAL AGEING week 5 unchanged; N-01 seatmap gap unchanged |
| B — API Contract Conformance | 🟡 Amber | → | N-08 unchanged; N-09 unchanged; N-13 unchanged; no new contract regressions in this cycle's features |
| C — Security Principles | 🔴 Red | → | N-11 HIGH unchanged (week 2, escalation warning); C-02r debug code unchanged; no new security regressions |
| D — Data Storage & Schema Principles | 🟡 Amber | → | N-10 (schemaVersion) unchanged — worsened by 73-commit paxId migration without resolution; M-08 unchanged |
| E — Infrastructure & Integration Principles | 🟡 Amber | → | H-04 CRITICAL AGEING (IATA bag tag); H-05 CRITICAL AGEING (MS-to-MS calls); no IaC files found |
| F — Testing & CI | 🟡 Amber | ↑ | 5 CI pipelines now execute specific test projects (26 methods); Operations pipeline test project exists but not wired; 12 pipelines still vacuous — material improvement from 🔴 Red last cycle |
| G — Documentation Drift | 🟡 Amber | ↓ | N-15 new (isVip undocumented); N-14, N-13, N-09, N-05 unchanged; service-urls.md accuracy gap persists |

---

## 8. Governance gaps

The following gaps remain open.

1. **ADR register has only one entry.** ADR-001 (payment gateway deferral) remains the only ADR. Five decisions are overdue for formal capture: (a) `ScheduleServiceClient` timer-trigger exception — grant or deny (H-05 is now CRITICAL AGEING because this decision has not been made); (b) manifest-only seatmap occupancy as accepted design vs. acknowledged gap; (c) shared `Microservice:HostKey` authentication pattern; (d) DatabaseMcp tool as an approved developer data-access mechanism with defined scope; (e) paxId as the primary passenger identifier replacing passengerId across all services.

2. **No OpenAPI specs in repository.** Integration Principals require machine-readable OpenAPI 3.x specs version-controlled alongside service code. 73 commits merged this cycle with no contract-test gate. The `isVip` field (N-15) and the `basketId` parameter (N-09) are examples of capabilities that would be automatically documented if an OpenAPI spec were maintained.

3. **No consumer-driven contract tests.** Pact or equivalent absent between orchestration APIs and microservices. The paxId migration touched handlers across three services simultaneously — without contract tests, there is no automated verification that all consumers were updated consistently.

4. **No incident response plan discoverable.** Security Principals require a documented IR plan with a UK GDPR 72-hour breach notification procedure. Finding N-11 (DatabaseMcp bulk PII read with no audit trail) makes this gap more urgent: if the function key were compromised, the absence of application-layer audit logging makes it impossible to assess what data was accessed and whether notification obligations are triggered.

5. **Accounting MS not assessable.** Event-subscription stubs only; no inspectable business logic.

6. **Airport API and Finance API scaffolded only.** Not assessed. The `delivery.md:331` incorrect attribution of bag tag generation to the Airport API (N-05) is related to this gap.

7. **`service-urls.md` inaccuracy.** The DatabaseMcp tool is described as "Local only" but is deployed to Azure. `service-urls.md` must be accurate as it is used by operators and security reviewers to understand the actual service estate.

8. **No IaC files in repository.** Infrastructure Principals require all Azure resources to be defined as Infrastructure as Code (Bicep or Terraform). Seven consecutive reviews have noted this gap. Without IaC, it is impossible to assess whether managed identity, VNet integration, private endpoints, and NSG rules conform to principles. This governance gap has been present long enough to consider it a systemic risk.

---

## 9. Appendix — Scope of this review

### Documents read

| Document | Purpose |
|----------|---------|
| `documentation/principles/architecture-principals.md` | Governing architecture rules |
| `documentation/principles/security-principals.md` | Security requirements |
| `documentation/principles/data-principals.md` | Data storage and schema rules |
| `documentation/principles/infrastructure-principals.md` | Infrastructure and CI/CD rules |
| `documentation/principles/integration-principals.md` | API style and integration rules |
| `documentation/principles/coding-standards.md` | C# and project-level standards |
| `documentation/adr/ADR-001-payment-gateway-integration-deferred.md` | ADR registry |
| `documentation/api-reference.md` | Full endpoint catalogue |
| `documentation/design/order.md` | OrderData / passenger schema — isVip check (N-15) |
| `documentation/design/manage-booking.md` | Departure lockout documentation check (N-13) |
| `documentation/design/delivery.md` | Bag tag documentation check (N-05) |
| `documentation/service-urls.md` | Service registry accuracy check (N-11, N-14) |
| `documentation/design-review/design-review-2026-06-01.md` | Most recent prior review (full read) |
| `documentation/design-review/design-review-2026-05-25.md` | Second prior review (full read) |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| Delivery MS — OciCheckIn | `OciCheckInHandler.cs:381–382` — `Random.Shared.Next` confirmed unchanged (H-04, week 6) |
| Offer MS | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10`, `RollingInventoryImportHandler.cs:37,54`, `Program.cs:76–91` — H-05 confirmed unchanged; week 5 |
| DatabaseMcp tool | `Functions/McpFunction.cs` — semi-colon guard at line 231 noted; no audit log, schema allowlist, or row cap added; N-11 confirmed unchanged. `service-urls.md:57` confirmed still "Local only". |
| Order MS — Passengers | `Application/UpdateOrderPassengers/UpdateOrderPassengersHandler.cs:83–86` — `isVip` field pass-through confirmed; no design doc update found (N-15) |
| Order MS — Debug | `Application/GetOrderDebug/GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` — both confirmed still present (C-02r) |
| Customer MS | `Infrastructure/Persistence/CustomerDbContext.cs` — HasTrigger present for Customer, LoyaltyTransaction, Preferences, CustomerNote; TierConfig absent (M-08) |
| Operations API — HandleDelay | `HandleDelayHandler.cs:35` — `NotImplementedException` confirmed unchanged (N-08) |
| Ancillary MS | `GetSeatOffersHandler.cs:20` — M-03 TODO confirmed unchanged |
| GitHub Actions workflows | All 20 workflows audited for test execution; 5 now point to specific test project files (Delivery, Offer, Order, Schedule, Retail); Operations orchestration test project exists but not wired; 12 remain vacuous |
| Test projects | 10 test files across 6 test projects confirmed present; 26 `[Fact]`/`[Theory]` methods total |

### Commit reference

Review conducted against commit `e5bd88b` (tip of `main` as of 2026-06-29).
73 commits merged since prior review (`88ee2fc`, 2026-06-01).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS | Event-subscription stubs only; no inspectable business logic |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass; VIP flag FE change reviewed for correctness via commit diff only |
| Terminal app — full audit | No security-relevant changes beyond VIP flag (reviewed via commit diff) |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — persistent governance gap |
| Penetration testing / runtime security | Requires live environment access |
| Per-service API specs (`documentation/api-specs/`) | Selective spot-check only; no service-spec changes noted in this cycle's commits |
