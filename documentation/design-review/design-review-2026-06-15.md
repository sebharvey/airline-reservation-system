# Apex Air — Weekly Design Review

**Date:** 2026-06-15
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-06-01.md

---

## 1. Executive summary

The platform posture is **Non-conforming** for the second consecutive cycle, driven by two CRITICAL AGEING findings that have now accumulated a combined 11 weeks of remediation debt without action. H-04 (bag tag random sequence, week 6) and H-05 (Offer MS direct MS-to-MS calls, week 5) remain unchanged at `OciCheckInHandler.cs:382` and `ScheduleServiceClient.cs:11` respectively. Against that backdrop, this cycle delivers the single most important CI improvement in the platform's review history: five deploy workflows now execute real test methods against actual test projects, reversing the "zero test execution" regression identified in the 2026-06-01 review. The paxId migration — 73 commits across 14 days spanning Order MS, Delivery MS, and IROPS handlers — is thorough and systematic, but deepens the N-10 schemaVersion risk by extending the divergence between old and new `OrderData` documents. The single most important action this week remains the same as last week: replace `Random.Shared.Next` with a SQL `SEQUENCE` in the bag tag generator and raise the H-05 ADR.

---

## 2. Critical findings (act this week)

### H-04 — Bag tag sequence number randomly generated; IATA Resolution 740 uniqueness violated — CRITICAL AGEING (Week 6)

**Severity:** CRITICAL AGEING (week 6 — unchanged for six consecutive reviews)
**Principle breached:** Architecture Principals — IATA standards alignment; IATA Resolution 740 requires bag tag licence plate numbers to be globally unique per airline per flight day.

**Evidence (unchanged for sixth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Application/OciCheckIn/OciCheckInHandler.cs:382` — `var sequence = Random.Shared.Next(0, 1_000_000).ToString("D6");`
- The TODO comment at line 381 identifying this as a known gap remains. No SQL `SEQUENCE` object, no persistent counter, no change in six weeks.

**Impact if unresolved:** Duplicate bag tags at any meaningful check-in volume. Bag misrouting and loss. IATA Resolution 740 non-compliance. Operational safety issue.

**Recommended remediation:** Replace `Random.Shared.Next` with `NEXT VALUE FOR delivery.BagTagSequence` (SQL Server `SEQUENCE` object in the `delivery` schema, scoped per airline numeric prefix). Pass the next sequence value into `GenerateBagTag()` from the repository. The mod-7 check-digit logic is correct and unchanged. Close the TODO comment when done.

**Age:** 6 weeks. No remediation action in any prior cycle.

---

### H-05 — Offer MS timer trigger calls Schedule MS and Ancillary MS directly — CRITICAL AGEING (Week 5)

**Severity:** CRITICAL AGEING (week 5 — unchanged for five consecutive reviews)
**Principle breached:** Architecture Principals — "No direct microservice-to-microservice communication. This constraint applies without exception."

**Evidence (unchanged for fifth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/ScheduleServiceClient.cs:11` — self-granted exception comment unchanged.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/SeatServiceClient.cs:10` — acknowledged violation TODO unchanged.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Application/RollingInventoryImport/RollingInventoryImportHandler.cs:37,54` — both clients injected and called.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Program.cs:77,85` — `ScheduleMs` and `AncillaryMs` HTTP clients registered.

**Impact if unresolved:** Deployment coupling (Offer MS lifecycle bound to Schedule MS and Ancillary MS). No circuit breaker — a Schedule MS or Ancillary MS outage at 01:00 UTC silently fails the nightly rolling inventory import.

**Recommended remediation (unchanged):**
1. `SeatServiceClient` — implement the acknowledged TODO: derive cabin counts from existing `offer.FlightInventory.Cabins` rows for the same `AircraftType`. The data is already present locally.
2. `ScheduleServiceClient` — raise an ADR to formally grant or deny the exception. If granted, add circuit breaker, retry policy, timeout, and dead-letter alerting as compensating controls. If denied, move the timer trigger to a dedicated `RollingInventoryOrchestration` function in the Operations API.
3. Delete the self-granted exception comment from `ScheduleServiceClient.cs:11` regardless of the resolution path.

**Age:** 5 weeks.

---

## 3. Status of prior findings

| Finding | Prior Severity | Status | Evidence note |
|---------|---------------|--------|--------------|
| H-04 — Bag tag sequence randomly generated | CRITICAL AGEING (week 5) | **UNCHANGED → week 6** | `OciCheckInHandler.cs:382` — `Random.Shared.Next(0,1_000_000)` unchanged. |
| H-05 — Offer MS calls Schedule MS and Ancillary MS directly | CRITICAL AGEING (week 4) | **UNCHANGED → week 5** | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10`, `RollingInventoryImportHandler.cs:37,54`, `Program.cs:77,85` all unchanged. |
| N-11 — DatabaseMcp tool unrestricted PII read; documented as local-only | HIGH (week 1) | **UNCHANGED → week 2** | `McpFunction.cs:85–163` unchanged. No schema allowlist, no audit log, no row cap added. `service-urls.md:57` local-only description unchanged. |
| C-02r — Debug infrastructure retained in production code | MEDIUM | **UNCHANGED** | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` still present in `Order.Application.GetOrderDebug/`. Three `AdminDebug*` Retail API functions and debug client methods unchanged. |
| N-01 — Seatmap booking-window occupancy gap | MEDIUM | **UNCHANGED** | Pre-booking seatmap derives occupancy from manifest only; in-progress `offer.SeatReservation` holds not surfaced. DB constraint preserves integrity. |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` TODOs unchanged. |
| M-07 — No integration tests for orchestration APIs; all CI test execution removed | MEDIUM | **PARTIALLY RESOLVED** | Five deploy workflows (Retail API, Order MS, Offer MS, Delivery MS, Schedule MS) now explicitly build and run test projects with real test methods. Operations test project exists (`CheckInHelperTests.cs`) but not wired into CI. Admin, Loyalty, Customer, Identity, Payment, and Ancillary still have zero test coverage in CI. |
| H-03r — dotnet test gate runs 0 tests | MEDIUM | **PARTIALLY RESOLVED** (downgrade to LOW) | Five workflows now run real test methods — the "zero test execution" regression is substantially reversed. Twelve workflows still have no test project wired. Downgraded from MEDIUM; residual gap tracked as H-03r (LOW) below. |
| N-05 — Bag tag response field undocumented | MEDIUM | **UNCHANGED** | `documentation/design/delivery.md:331` still states "bagTagNumber is null at purchase and populated by the Airport API when the bag is checked in." The Delivery MS OCI handler generates the tag; the Airport API does not. |
| N-10 — OrderData JSON lacks schemaVersion | MEDIUM | **UNCHANGED — risk elevated** | No `schemaVersion` field added. The 73-commit paxId migration this cycle extended `paxId` integer reads into `OciCheckIn`, IROPS, Delivery manifest, and additional Order handlers. Each new handler reading `paxId` from documents that pre-date the paxId addition increases the risk of silent data loss on legacy documents. The schemaVersion gap is now more urgent than when first raised. |
| M-08 — TierConfig table not mapped with HasTrigger | LOW | **UNCHANGED** | `CustomerDbContext.cs` still has no `TierConfig` DbSet or `HasTrigger("TR_TierConfig_UpdatedAt")`. `TR_TierConfig_UpdatedAt` exists in `Script.sql`. |
| M-10 — TODO without issue reference committed to main | LOW | **UNCHANGED** | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs remain. |
| N-08 — HandleDelayHandler throws NotImplementedException | LOW | **UNCHANGED** | `HandleDelayHandler.cs:35` still throws `NotImplementedException()`. `api-reference.md` documents the endpoint as returning `200 OK`. |
| N-09 — GET /v1/products basketId parameter undocumented | LOW | **UNCHANGED** | `api-reference.md:98` still does not mention the optional `basketId` query parameter or the rule evaluation logic it triggers. |
| N-13 — Post-departure manage-booking lockout business rule undocumented | LOW | **UNCHANGED** | Departure lockout enforced in code; `documentation/design/manage-booking.md` and `api-reference.md` endpoint descriptions still do not document the rule. |
| N-14 — DatabaseMcp tool absent from api-reference.md | LOW | **UNCHANGED** | No entry for the DatabaseMcp tool in `api-reference.md`. No `api-specs/database-mcp.md` created. |

---

## 4. High findings

### N-11 — DatabaseMcp tool exposes unrestricted PII read access with no audit logging; documented as local-only but deployed to Azure (Week 2)

**Severity:** HIGH (Week 2 — unchanged)
**Principle breached:** Security Principals — PII must not appear in logs, telemetry, or error messages; all state-changing or PII-accessing operations must produce a structured, immutable audit log entry; no embedded credentials permitted.

**Evidence (unchanged from 2026-06-01):**
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:85–101` — `run_select_query` MCP tool accepts arbitrary SQL `SELECT` or CTE from any authenticated caller and executes it directly. No schema allowlist, no table blocklist, no row cap, no query audit log.
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:144–163` — `RunSelectQuery` opens a `SqlConnection(ConnectionString)` and executes caller-supplied SQL verbatim. Caller can issue `SELECT * FROM customer.Customer`, `SELECT * FROM [identity].RefreshToken`, `SELECT * FROM payment.Payment`, or `SELECT * FROM [order].[Order]` (full OrderData JSON).
- `documentation/service-urls.md:57` — still describes the tool as "Local only — Azure Functions host, listens on `http://localhost:7071`". It is deployed to the production Azure slot on every push to main.
- The tool correctly uses `AuthorizationLevel.Function` — unauthenticated access is not possible. The concern is query scope and the absence of audit trails.

**Impact if unresolved:** Any holder of the `reservation-system-mcp-db` function key has unrestricted read access to all PII across all schemas. Bulk extraction of passenger records, travel documents, and payment data is possible without leaving any application-layer audit trail.

**Recommended remediation:**
1. Correct `documentation/service-urls.md:57` to reflect deployment to Azure.
2. Add `documentation/api-specs/database-mcp.md` describing the tool, its purpose, and access controls.
3. Add query audit logging: every `run_select_query` call must emit a structured log entry with the query text, row count returned, caller identity, and timestamp.
4. Enforce a schema allowlist: permit queries only against `offer.*` and `schedule.*` schemas; block `customer.*`, `identity.*`, `payment.*`, `order.*`.
5. Add a row cap (e.g. `TOP 500`) to `RunSelectQuery` to prevent bulk extraction in a single call.
6. Verify the database connection string used by the tool is a read-only, schema-restricted SQL login.

**Age:** 2 weeks.

---

## 5. Medium and low findings

### Medium findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| N-10 | OrderData JSON lacks schemaVersion — risk elevated by paxId migration | MEDIUM | D | `OrderData` has no `schemaVersion` field. 73 commits this cycle extended paxId integer reads into `OciCheckIn`, IROPS, Delivery, and additional Order handlers. Legacy documents without `paxId` will now silently yield `paxId=0` at runtime across more code paths. Urgency is higher than when first raised (2026-05-25). | Order MS owner |
| N-05 | Bag tag documentation incorrect | MEDIUM | G | `delivery.md:331` attributes bag tag generation to Airport API; Delivery MS OCI handler generates it. | Delivery / Operations owner |
| C-02r | Debug infrastructure retained in production code | MEDIUM | C | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs`, three `AdminDebug*` Retail API functions, and debug client methods. All authenticated; still dead code. | Retail API / Order MS owner |
| N-01 | Seatmap booking-window occupancy gap | MEDIUM | A / B | Pre-booking seatmap shows manifest-only occupancy; in-progress `offer.SeatReservation` holds not surfaced. DB constraint preserves integrity. | Retail API / Offer MS owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A | `GetSeatOffersHandler.cs:20`, `GetSeatOfferHandler.cs:20` TODOs. Business logic belongs in Application layer. | Ancillary MS owner |
| M-07 residual | Integration test coverage gaps remain | MEDIUM | F | Admin API, Loyalty API, Customer MS, Identity MS, Payment MS, Ancillary MS, and User MS still have zero test coverage in CI. Operations test project (`CheckInHelperTests.cs`) exists but is not wired into the Operations API deploy workflow. | QA / Platform |

### Low findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| H-03r residual | 12 of 17 deploy workflows still have no test project wired | LOW | F | Admin, Loyalty, Operations, Customer, Identity, Payment, User, Ancillary, Exception, MCP, and Simulator workflows have no test gate beyond build and CVE scan. | QA / Platform |
| M-08 | TierConfig table not mapped with HasTrigger | LOW | D | `TR_TierConfig_UpdatedAt` in `Script.sql`; `CustomerDbContext` has no mapping. | Customer MS owner |
| M-10 | TODO without issue reference committed to main | LOW | Coding Standards | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs remain. | All owners |
| N-08 | HandleDelayHandler throws NotImplementedException | LOW | B | `/v1/disruptions/delay` documented as `200 OK`; `HandleDelayHandler.cs:35` throws `NotImplementedException`. | Operations API owner |
| N-09 | GET /v1/products basketId parameter undocumented | LOW | G | `api-reference.md:98` does not mention the optional `basketId` query parameter or rule evaluation logic. | Retail API / Docs owner |
| N-13 | Post-departure manage-booking lockout business rule undocumented | LOW | G | Departure lockout enforced in code (422 on all-departed itinerary, per-segment gate for bag/seat requests); absent from `design/manage-booking.md` and `api-reference.md` endpoint descriptions. | Retail API / Docs owner |
| N-14 | DatabaseMcp tool absent from api-reference.md | LOW | G | No entry in `api-reference.md`; no `api-specs/database-mcp.md`. `service-urls.md` entry is inaccurate (local-only). | Platform Architect |

---

## 6. Observations and positive notes

- **Most significant CI improvement in six review cycles.** Five deploy workflows (Order MS, Offer MS, Delivery MS, Schedule MS, Retail API) now explicitly build and run dedicated test projects with real test methods. The 2026-06-01 regression — "zero test execution in any CI pipeline" — is substantially reversed. This is the right direction; the structural pattern (build test project, then run it) is correct and should be replicated for the remaining 12 workflows.

- **Delivery test project wired into CI this cycle (commit `22f76d3`).** The Delivery MS CI workflow now builds `ReservationSystem.Tests.Microservices.Delivery` and runs `WriteManifestHandlerTests.cs` as a gating step before deployment. The pattern correctly adds the test project to the solution's deployment trigger path so that changes to test code also trigger a rebuild and run.

- **Test maintenance obligation codified in CLAUDE.md (PR #1303).** Key rule 13 now explicitly requires searching for existing tests before committing any code change, fixing broken tests in the same commit, and adding tests for new behaviour. This formalises the expectation at the contributor level, not merely at the governance layer.

- **paxId migration systematic and consistent.** Across 73 commits this cycle, the migration from `passengerId` (string) to `paxId` (integer) has been applied to OCI check-in, IROPS order lookups, Delivery manifest writes, UpdateOrderCheckIn, UpdateOrderBags, UpdateOrderPassengers, GetOrder, and seat item lookups. Each commit is atomic and well-described. The approach of extending `paxId` as an additional path (rather than replacing `passengerId` immediately) is a defensible migration strategy. Remaining risk is the schemaVersion gap (N-10) — not the migration code itself.

- **Operations test project created.** `ReservationSystem.Tests.Orchestration.Operations` was added this cycle with `CheckInHelperTests.cs`. It is not yet wired into the Operations API CI workflow, but its existence is the prerequisite for doing so.

---

## 7. Conformance scorecard

| Dimension | Conformance | Trend vs 2026-06-01 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🔴 Red | → | H-04 CRITICAL AGEING week 6 unchanged; H-05 CRITICAL AGEING week 5 unchanged; N-01 seatmap gap unchanged |
| B — API Contract Conformance | 🟡 Amber | → | N-08 unchanged; N-13 unchanged; N-09 unchanged |
| C — Security Principles | 🔴 Red | → | N-11 HIGH week 2 unchanged — unrestricted PII read access with no audit logging; C-02r unchanged |
| D — Data Storage & Schema Principles | 🟡 Amber | ↓ | M-08 unchanged; N-10 risk elevated by paxId migration extending into more code paths without schemaVersion |
| E — Infrastructure & Integration Principles | 🟡 Amber | → | H-04 CRITICAL AGEING (bag tag); H-05 CRITICAL AGEING (MS-to-MS calls); CVE gate active |
| F — Testing & CI | 🟡 Amber | ↑↑ | Substantial improvement: 5 workflows now run real test methods (up from 0); Operations test project created; 12 workflows still have no test gate |
| G — Documentation Drift | 🟡 Amber | → | N-05, N-09, N-13, N-14 all unchanged; test obligation codified in CLAUDE.md |

---

## 8. Governance gaps

The following gaps remain open. No new governance gaps identified this cycle.

1. **ADR register has only one entry; several decisions are overdue.** ADR-001 (payment gateway deferral) remains the only recorded decision. H-05 urgently needs an ADR. Decisions warranting capture: (a) H-05 — Offer MS timer trigger exception; (b) N-01 — manifest-only seatmap occupancy as accepted design (open 4 cycles); (c) N-11 — DatabaseMcp tool as an approved developer PII access mechanism (or explicit denial with schema restrictions).

2. **No OpenAPI specs in repository.** Integration principles require machine-readable OpenAPI 3.x specs version-controlled alongside service code. Absent; blocks CI-based contract testing. The paxId migration this cycle (which changes the OrderData shape) would be undetectable without contract tests.

3. **No consumer-driven contract tests.** Pact or equivalent between orchestration APIs and microservices absent. 73 commits this cycle with no automated contract verification.

4. **No incident response plan discoverable.** Security principles require a documented IR plan with a UK GDPR 72-hour breach notification procedure. N-11 (DatabaseMcp unrestricted PII access without audit logging) makes this gap operationally risky: if the function key were compromised, there would be no application-layer audit trail to establish what was accessed or when.

5. **Accounting MS not assessable.** Event-subscription stubs only; no business logic to inspect.

6. **Airport API and Finance API scaffolded only.** Not assessed. The N-05 attribution error (bag tag generation incorrectly documented as the Airport API's responsibility) remains related to this gap.

7. **service-urls.md accuracy.** DatabaseMcp tool described as local-only but deployed to Azure (N-11 / N-14). `service-urls.md` must function as an accurate operator reference.

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
| `documentation/design/delivery.md` | Bag tag documentation check (N-05) |
| `documentation/design/manage-booking.md` | Departure lockout documentation check (N-13) |
| `documentation/service-urls.md` | Service registry accuracy check (N-11, N-14) |
| `documentation/design-review/design-review-2026-06-01.md` | Most recent prior review |
| `documentation/design-review/design-review-2026-05-25.md` | Second prior review |
| `documentation/design-review/design-review-2026-05-21.md` | Third prior review |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| Delivery MS — OciCheckIn | `OciCheckInHandler.cs:382` — bag tag `Random.Shared.Next` confirmed unchanged (H-04, week 6) |
| Offer MS | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10`, `RollingInventoryImportHandler.cs:37,54`, `Program.cs:77,85` — H-05 confirmed unchanged (week 5) |
| DatabaseMcp tool | `Functions/McpFunction.cs:85–163` — unchanged from 2026-06-01 (N-11, week 2) |
| Order MS — Debug | `Application/GetOrderDebug/GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` — files confirmed still present (C-02r) |
| Operations API | `HandleDelayHandler.cs:35` — N-08 `NotImplementedException` confirmed unchanged |
| GitHub Actions workflows | All 20 workflows audited for test project wiring; 5 confirmed with real test project references (Order, Offer, Delivery, Schedule, Retail); 12 confirmed without (Admin, Loyalty, Operations, Customer, Identity, Payment, User, Ancillary, Exception, MCP, Simulators) |
| Test projects | 6 test projects confirmed with test files (10 test class files total); Operations test project confirmed unlinked from CI |
| `documentation/api-reference.md` | Lines 67, 98 — basketId gap (N-09) and products description confirmed unchanged |
| `documentation/design/delivery.md:331` | Bag tag Airport API attribution confirmed unchanged (N-05) |
| paxId migration commits | Representative commits reviewed: `OciCheckInHandler`, `WriteManifestHandler`, `GetIropsOrdersHandler`, `UpdateOrderCheckInHandler`, `UpdateOrderBagsHandler`, `UpdateOrderPassengersHandler` — migration pattern consistent and correct |

### Commit reference

Review conducted against commit `e5bd88b` (tip of `main` as of 2026-06-15).
73 commits merged since prior review (`68cd181`, 2026-06-01).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS | Event-subscription stubs only; no inspectable business logic |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass |
| Terminal app | No security-relevant changes noted in commit log this cycle |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap from prior reviews |
| Penetration testing / runtime security | Requires live environment access |
| Per-service API specs (`documentation/api-specs/`) | Selective spot-check only; no spec changes noted in this cycle's commits |
