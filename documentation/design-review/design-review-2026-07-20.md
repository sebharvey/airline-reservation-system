# Apex Air — Weekly Design Review

**Date:** 2026-07-20
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-06-01.md

---

## 1. Executive summary

The platform posture is **Non-conforming** — held from the previous cycle. This review covers 80 commits across an approximately 7-week gap since the 2026-06-01 review. The most significant positive development is the introduction of real unit test projects for five services (Offer, Order, Schedule, Retail, Delivery), each wired into their respective CI pipelines — this partially resolves the long-running M-07/H-03r testing gap. There are two CRITICAL AGEING findings: H-04 (bag tag random sequence, approximately 12 weeks open) and H-05 (Offer MS MS-to-MS calls, approximately 11 weeks open). Neither has received any remediation despite repeated escalations. The HIGH finding N-11 (DatabaseMcp PII exposure) is now 7 weeks old and entirely unaddressed. The paxId migration — the bulk of this cycle's 80 commits — worsens an existing schema documentation gap (N-10): `paxId` is now the primary passenger identifier across all handlers but is absent from the `order.md` JSON schema examples, and the `OrderData` document still has no `schemaVersion` field. **The single most important action this week remains the same as it has been for over two months: replace `Random.Shared.Next` in the bag tag generator with a persistent SQL SEQUENCE.**

---

## 2. Critical findings (act this week)

### H-04 — Bag tag sequence number randomly generated; IATA Resolution 740 uniqueness violated — CRITICAL AGEING (~Week 12)

**Severity:** CRITICAL AGEING (~12 weeks since first identification — unchanged across every review)
**Principle breached:** Architecture Principals — IATA standards alignment; IATA Resolution 740 requires bag tag licence plate numbers to be globally unique per airline per flight day.

**Evidence (unchanged):**
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Application/OciCheckIn/OciCheckInHandler.cs:381–382` — `// TODO: In future, this 6-digit sequence number needs to be auto-incremented from a persistent counter rather than generated randomly. var sequence = Random.Shared.Next(0, 1_000_000).ToString("D6");`
- Same `Random.Shared.Next` generator used for both OCI self check-in and agent check-in paths. No SQL `SEQUENCE` object, no persistent counter table, no remediation in 80 commits.

**Impact if unresolved:** Duplicate bag tags at any meaningful check-in volume. Bag misrouting and loss. IATA Resolution 740 non-compliance. Operational safety issue.

**Recommended remediation:** Replace `Random.Shared.Next` with `NEXT VALUE FOR delivery.BagTagSequence` (SQL Server `SEQUENCE` object in the `delivery` schema, scoped per airline numeric prefix). Pass the next sequence value into `GenerateBagTag()` from the repository. The mod-7 check-digit logic is correct and unchanged. This is a targeted change of approximately five lines; there is no technical blocker.

**Age:** ~12 weeks.

---

### H-05 — Offer MS timer trigger calls Schedule MS and Ancillary MS directly — CRITICAL AGEING (~Week 11)

**Severity:** CRITICAL AGEING (~11 weeks open — unchanged across every review)
**Principle breached:** Architecture Principals — "No direct microservice-to-microservice communication. This constraint applies without exception."

**Evidence (unchanged):**
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/ScheduleServiceClient.cs:11` — self-granted exception comment: `"Note: direct MS-to-MS calls are an accepted exception for timer triggers."` No governance document or ADR grants this exception.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/SeatServiceClient.cs:10` — acknowledged violation: `"TODO: remove this cross-domain call — cabin counts are already stored in offer.FlightInventory.Cabins"`.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Application/RollingInventoryImport/RollingInventoryImportHandler.cs:37,54` — both clients injected and called.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Program.cs:77,85` — `ScheduleMs` and `AncillaryMs` HTTP clients still registered.

**Impact if unresolved:** Deployment coupling between Offer MS, Schedule MS, and Ancillary MS. Obscured distributed traces. A Schedule MS or Ancillary MS outage at 01:00 UTC silently fails the nightly rolling inventory import with no circuit breaker.

**Recommended remediation (in preference order):**
1. `SeatServiceClient` — implement the acknowledged TODO: derive cabin counts from existing `offer.FlightInventory.Cabins` rows for the same `AircraftType`. The data is already present locally.
2. `ScheduleServiceClient` — raise an ADR formally granting or denying the exception. If granted: add circuit breaker, retry policy, timeout, and dead-letter alerting. If denied: move the timer trigger to a dedicated orchestration function in the Operations API.
3. Delete the self-granted exception comment from `ScheduleServiceClient.cs:11` regardless of the resolution path.

**Age:** ~11 weeks.

---

## 3. Status of prior findings

| Finding | Prior Severity | Status | Evidence note |
|---------|---------------|--------|--------------|
| H-04 — Bag tag sequence randomly generated | CRITICAL AGEING | **UNCHANGED** | `OciCheckInHandler.cs:381–382` — `Random.Shared.Next(0, 1_000_000)` unchanged across 80 commits. Now ~week 12. |
| H-05 — Offer MS calls Schedule MS and Ancillary MS directly | CRITICAL AGEING | **UNCHANGED** | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10`, `RollingInventoryImportHandler.cs:37,54`, `Program.cs:77,85` — all unchanged. Now ~week 11. |
| N-11 — DatabaseMcp PII exposure; local-only documentation incorrect | HIGH | **UNCHANGED** | `McpFunction.cs:144–163` — no audit logging, no schema allowlist, no row cap added. `service-urls.md:57` still describes tool as "Local only." Not in `api-reference.md`. 7 weeks open without remediation. |
| C-02r — Debug infrastructure retained in production code | MEDIUM | **UNCHANGED** | `GetOrderDebugHandler.cs` and `GetOrderDebugQuery.cs` still present in `Order.Application.GetOrderDebug/`. |
| N-01 — Seatmap booking-window occupancy gap | MEDIUM | **UNCHANGED** | Pre-booking seatmap derives occupancy from manifest only; in-progress `offer.SeatReservation` holds not surfaced. |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` TODOs unchanged. |
| M-07 — No integration tests for orchestration APIs | MEDIUM | **PARTIALLY RESOLVED** | 5 of 17 CI workflows now run real test projects (Retail, Offer, Order, Schedule, Delivery). 12 workflows still have vacuous `dotnet test` steps. Admin, Loyalty, Operations, Customer, Identity, Payment, User, Ancillary, Exception, MCP, Simulator workflows remain uncovered. See section 4 for new N-17. |
| H-03r — `dotnet test` gate runs 0 tests | MEDIUM | **PARTIALLY RESOLVED** | 5 workflows now run real tests; 12 remain vacuous. The Operations test project exists but is not wired. Downgraded from MEDIUM to LOW (see section 5). |
| N-05 — Bag tag response field undocumented | MEDIUM | **UNCHANGED** | `documentation/design/delivery.md:331` still attributes bag tag generation to Airport API; Delivery MS OCI handler generates it. |
| N-10 — OrderData JSON lacks `schemaVersion` | MEDIUM | **UNCHANGED / WORSENED** | No `schemaVersion` field added. 80-commit paxId migration further diverges OrderData schema from documentation: `paxId` (integer) is now primary passenger identifier in all production handlers, but `order.md:573–614` still shows only `passengerId: "PAX-1"` in JSON examples. See also new finding N-16. |
| M-08 — `TierConfig` table not mapped with `HasTrigger` | LOW | **UNCHANGED** | `CustomerDbContext.cs` still has no `TierConfig` DbSet or `HasTrigger("TR_TierConfig_UpdatedAt")`. `TR_TierConfig_UpdatedAt` exists in `Script.sql`. |
| M-10 — TODO without issue reference committed to main | LOW | **UNCHANGED** | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs remain without author or tracking issue reference. |
| N-08 — `HandleDelayHandler` throws `NotImplementedException` | LOW | **UNCHANGED** | `HandleDelayHandler.cs:35` still throws `NotImplementedException()`; `api-reference.md:321` documents `/v1/disruptions/delay` as returning `200 OK`. |
| N-09 — `GET /v1/products` `basketId` parameter undocumented | LOW | **UNCHANGED** | `api-reference.md:98` still does not document the optional `basketId` query parameter or the rule evaluation logic it triggers. |
| N-13 — Post-departure manage-booking lockout rule undocumented | LOW | **UNCHANGED** | Departure lockout enforcement is in `CancelOrderHandler`, `ChangeOrderHandler`, `AddOrderBagsHandler`, `UpdateOrderSeatsHandler` but absent from `documentation/design/manage-booking.md` and `api-reference.md`. |
| N-14 — DatabaseMcp tool absent from `api-reference.md` | LOW | **UNCHANGED** | No entry for the DatabaseMcp tool or its endpoints in `api-reference.md`. |

---

## 4. High findings

### N-11 — DatabaseMcp tool: unrestricted PII read access, no audit logging, incorrect documentation (week 7)

**Severity:** HIGH (7 weeks open — no remediation in this cycle's 80 commits)
**Principle breached:** Security Principals — PII must never appear without structured audit trails; "All state-changing operations must produce a structured, immutable audit log entry" (by extension, all PII-access paths). Infrastructure Principals — managed identity and access. Documentation accuracy (service-urls.md states "Local only").

**Evidence:**
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:144–163` — `RunSelectQuery` accepts arbitrary SQL and executes it verbatim against the database. No schema allowlist, no table blocklist, no row cap, no query audit log.
- `documentation/service-urls.md:57` — still describes the tool as "Local only." `.github/workflows/main_reservation-system-mcp-db.yml` deploys it to Azure production on every push to main.
- No `api-reference.md` entry and no `api-specs/database-mcp.md` document.

**Impact if unresolved:** Any holder of the function key has unrestricted bulk read access to all PII across all schemas (`customer.*`, `identity.*`, `payment.*`, `order.*`) with no application-layer audit trail. Documentation discrepancy prevents accurate operator risk assessment.

**Recommended remediation (unchanged from 2026-06-01):**
1. Correct `service-urls.md:57` to reflect Azure deployment and HTTPS URL.
2. Add query audit logging: every `run_select_query` call must emit a structured log entry with the query text, row count returned, and timestamp.
3. Enforce a schema allowlist (e.g. `offer.*`, `schedule.*` only) or add a row cap (TOP 500) as minimum bulk-extraction controls.
4. Add `api-specs/database-mcp.md` and an `api-reference.md` entry documenting scope, auth, and data access boundaries.

**Age:** 7 weeks.

---

## 5. Medium and low findings

### New findings this cycle

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| N-16 | OrderData passenger schema diverges further from documentation | MEDIUM | D, G | 80-commit paxId migration establishes `paxId` (integer) as the primary passenger identifier across all production handlers (`ConfirmOrderHandler.cs:191`, `UpdateOrderPassengersHandler.cs:83`, `WriteManifestHandler`, IROPS handlers, check-in handlers). `order.md:573–614` JSON schema example shows only `passengerId: "PAX-1"` with no `paxId`. The VIP flag (`isVip`) added in commit `f3a3600` also appears in `OrderData` without any documentation. `OrderData` still lacks a `schemaVersion` field to distinguish schema versions. New engineers reading `order.md` will build against a schema that no longer matches production. | Order MS / Docs owner |
| N-17 | Operations test project not wired to CI workflow | MEDIUM | F | `src/API/Tests/ReservationSystem.Tests.Orchestration.Operations/` contains `CheckInHelperTests.cs` with real test methods. The Operations API CI workflow (`main_reservation-system-db-api-operations.yml`) does not reference this test project — its `dotnet test` step runs from within the Operations API project directory and finds zero tests. Additionally, the workflow `paths:` trigger does not include `src/API/Tests/ReservationSystem.Tests.Orchestration.Operations/**`, so test file changes do not trigger the pipeline. | QA / Platform |
| N-15 | VIP flag (`isVip`) added to OrderData without documentation | LOW | G | Commit `f3a3600` adds `isVip` as a settable field on passenger nodes in `UpdateOrderPassengersHandler.cs:83–86`. The field flows through booking creation and passenger updates but is absent from `documentation/design/order.md`, `documentation/design/manage-booking.md`, `documentation/design/customer.md`, and `documentation/api-reference.md`. | Order MS / Docs owner |
| N-18 | Loyalty API: three points endpoints throw `NotImplementedException` while documented as operational | LOW | B | `src/API/Orchestration/ReservationSystem.Orchestration.Loyalty/Functions/PointsFunction.cs:69,85,101` — `Settle`, `Reverse`, and `Reinstate` functions throw `NotImplementedException` directly. These endpoints (`POST /v1/customers/{loyaltyNumber}/points/settle`, `/reverse`, `/reinstate`) are documented in `api-reference.md:575–576` as operational. Pattern is identical to N-08 (HandleDelayHandler). | Loyalty API owner |

### Carried medium and low findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| C-02r | Debug infrastructure retained in production | MEDIUM | C | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` still present. All authenticated; dead code that should be deleted. | Retail API / Order MS owner |
| N-01 | Seatmap booking-window occupancy gap | MEDIUM | A / B | Pre-booking seatmap shows manifest-only occupancy; in-progress `offer.SeatReservation` holds not surfaced. DB uniqueness constraint preserves integrity. | Retail API / Offer MS owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A | `GetSeatOffersHandler.cs:20`, `GetSeatOfferHandler.cs:20` TODOs. Business logic belongs in Application layer. | Ancillary MS owner |
| N-05 | Bag tag documentation incorrect | MEDIUM | G | `delivery.md:331` attributes bag tag generation to Airport API; Delivery MS OCI handler generates it. | Delivery / Operations owner |
| N-10 | OrderData JSON lacks `schemaVersion` | MEDIUM | D | No `schemaVersion` field; paxId migration worsens gap. See N-16. | Order MS owner |
| H-03r | `dotnet test` vacuous in 12 of 17 workflows | LOW | F | Now a residual gap: Admin, Loyalty, Operations (despite test project existing), Customer, Identity, Payment, User, Ancillary, Exception, MCP, Simulator, Timatic workflows still have bare `dotnet test` steps that execute zero tests. | QA / Platform |
| M-08 | `TierConfig` table not mapped with `HasTrigger` | LOW | D | `TR_TierConfig_UpdatedAt` in `Script.sql`; `CustomerDbContext` has no mapping. | Customer MS owner |
| M-10 | TODO without issue reference on main | LOW | Coding Standards | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs. | All owners |
| N-08 | `HandleDelayHandler` throws `NotImplementedException` | LOW | B | `/v1/disruptions/delay` undocumented as unimplemented; `api-reference.md:321` shows `200 OK`. | Operations API owner |
| N-09 | `GET /v1/products` `basketId` undocumented | LOW | G | `api-reference.md:98` still omits optional `basketId` parameter and rule evaluation logic. | Retail API / Docs owner |
| N-13 | Post-departure lockout rule undocumented | LOW | G | Enforced in four handlers; absent from `manage-booking.md` and `api-reference.md`. | Retail API / Docs owner |
| N-14 | DatabaseMcp absent from `api-reference.md` | LOW | G | No entry in `api-reference.md`; `service-urls.md` entry is inaccurate. | Platform Architect |

---

## 6. Observations and positive notes

- **Testing posture materially improved.** The most significant positive change across 80 commits: `a4d0a8e` added test projects for Offer, Order, Schedule, and Retail; `22f76d3` added and wired the Delivery MS test project; multiple PRs added real test methods (e.g. `SearchFlightsHandlerTests`, `CreateOrderHandlerTests`, `UpdateOrderBagsHandlerTests`, `WriteManifestHandlerTests`, `CheckInHelperTests`). Five CI pipelines now gate deployment on real unit test execution. This closes the most dangerous aspect of M-07 and H-03r for half the services — a meaningful step.

- **Test obligation codified in CLAUDE.md.** PR #1303 (commit `b437508`) added rule 13 to CLAUDE.md: "Tests must be validated and updated with every code change." The same PR also added CLAUDE.md guidance that tests broken by a change must be fixed in the same commit. This is the correct process control and will prevent future regressions of the kind seen in the 2026-06-01 cycle (where integration test workflows were deleted).

- **Flight manifest empty-pax bug found and fixed with test.** Commit `a0083ed` fixed a data integrity regression introduced during the paxId migration: `ManifestPassengerEntry` was missing `paxId`, causing every manifest entry to deserialise with `PaxId = 0` on the Delivery MS side and be silently skipped — leaving the flight management screen with zero passengers. The fix is accompanied by `DeliveryServiceClientManifestTests.cs` asserting the field is correctly serialised. This pattern (finding a regression, fixing it, adding a test) is exactly what the test obligation principle calls for.

- **paxId migration systematically completed.** A coordinated 20-commit migration across Order MS, Retail API, Operations API, and Delivery MS replaces string `passengerId` parsing with integer `paxId` as the primary passenger key, with backwards-compatible fallback for documents created before the migration. The scope and discipline of the migration — covering IROPS handlers, check-in handlers, manifest writing, bag updates, and passenger updates — is commendable even though the documentation has not been updated to match.

- **Simulator timer cadence corrected.** Commits `f152618`, `51c96e8`, `35f5667` iterated the simulator timer intervals to appropriate values for the demo environment (24-hour cadence), avoiding unnecessary load from 15-minute or 30-minute triggers in a non-production context.

---

## 7. Conformance scorecard

| Dimension | Conformance | Trend vs 2026-06-01 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🔴 Red | → | H-04 CRITICAL AGEING ~week 12 unchanged; H-05 CRITICAL AGEING ~week 11 unchanged; N-01 seatmap gap unchanged |
| B — API Contract Conformance | 🟡 Amber | ↓ | N-08 unchanged; N-18 new (three Loyalty API endpoints stub NotImplementedException); N-16 (paxId schema divergence); N-09, N-13 unchanged |
| C — Security Principles | 🔴 Red | → | N-11 HIGH unchanged (7 weeks, no remediation); C-02r unchanged |
| D — Data Storage & Schema Principles | 🟡 Amber | ↓ | N-10 worsened — paxId migration deepens schema/documentation divergence without schemaVersion; N-16 new; M-08 unchanged |
| E — Infrastructure & Integration Principles | 🟡 Amber | → | H-04 CRITICAL AGEING (bag tag); H-05 CRITICAL AGEING (MS-to-MS); no new infrastructure violations |
| F — Testing & CI | 🟡 Amber | ↑ | Significant improvement: 5 of 17 CI pipelines now run real tests; Operations test project exists but not wired (N-17); 12 pipelines still vacuous; CLAUDE.md test obligation rule added |
| G — Documentation Drift | 🔴 Red | ↓ | N-15 new (VIP flag undocumented); N-16 new (paxId/isVip absent from order.md); N-05, N-09, N-13, N-14 unchanged; `service-urls.md` DatabaseMcp still inaccurate |

---

## 8. Governance gaps

The following gaps remain open. All carry forward from prior reviews.

1. **ADR register stagnant — H-05 exception still ungoverned.** The self-granted exception in `ScheduleServiceClient.cs:11` has existed for approximately 11 weeks without an ADR being raised to approve or deny it. This is the most overdue ADR decision: either grant the exception with documented compensating controls, or deny it and move the timer trigger to the Operations API. The ADR process was established with ADR-001 and has not been used since.

2. **No OpenAPI specs in repository.** Integration principles require machine-readable OpenAPI 3.x specs version-controlled alongside service code. 80 commits merged with no contract spec changes. paxId is now the primary passenger identifier but has no machine-readable contract.

3. **No consumer-driven contract tests.** Pact or equivalent between orchestration APIs and microservices remains absent.

4. **No incident response plan discoverable.** Security principles require a documented IR plan with a UK GDPR 72-hour breach notification procedure. N-11 (DatabaseMcp with no audit trail) remains a concrete example of why this gap matters.

5. **Accounting MS not assessable.** Event-subscription stubs only; no business logic to inspect.

6. **Airport API and Finance API scaffolded only.** Not assessed. `delivery.md:331` incorrect attribution of bag tag generation to Airport API (N-05) remains related.

7. **`service-urls.md` accuracy.** DatabaseMcp described as local-only when deployed to Azure production (N-11, N-14). No update in 7 weeks.

8. **OrderData schema is undocumented relative to production.** `order.md` passenger JSON example is pre-paxId. The `isVip` field exists in production but is not in any documentation. No `schemaVersion` field exists to allow version detection. This is both a governance gap (no migration strategy documented for the paxId change) and a documentation gap.

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
| `documentation/design/order.md` | OrderData schema check for paxId and VIP flag |
| `documentation/design/manage-booking.md` | Departure lockout documentation check (N-13) |
| `documentation/design/delivery.md` | Bag tag documentation check (N-05) |
| `documentation/service-urls.md` | Service registry accuracy check (N-11, N-14) |
| `documentation/design-review/design-review-2026-06-01.md` | Most recent prior review |
| `documentation/design-review/design-review-2026-05-25.md` | Second prior review |
| `documentation/design-review/design-review-2026-05-21.md` | Third prior review |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| Delivery MS — OciCheckIn | `OciCheckInHandler.cs:378–386` — bag tag `Random.Shared.Next` confirmed unchanged (H-04, ~week 12) |
| Offer MS | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10`, `RollingInventoryImportHandler.cs:37,54`, `Program.cs:77,85` — H-05 confirmed unchanged; ~week 11 |
| DatabaseMcp tool | `Functions/McpFunction.cs` — full read; no audit logging, no schema allowlist, no row cap added (N-11) |
| Order MS — Debug | `Application/GetOrderDebug/GetOrderDebugHandler.cs` — confirmed still present (C-02r) |
| Order MS — paxId migration | `UpdateOrderPassengersHandler.cs:83–86` — paxId and isVip fields confirmed in production code; not in documentation (N-16, N-15) |
| Customer MS | `CustomerDbContext.cs` — M-08 TierConfig mapping confirmed absent |
| Operations API | `HandleDelayHandler.cs:35` — N-08 `NotImplementedException` confirmed unchanged |
| Loyalty API | `PointsFunction.cs:69,85,101` — Settle/Reverse/Reinstate confirmed throwing `NotImplementedException` (N-18); `TokenVerificationMiddleware.cs` — JWT auth confirmed via middleware |
| GitHub Actions workflows | All 17 deploy workflows audited — 5 have real test project paths; 12 remain vacuous; Operations test project (`ReservationSystem.Tests.Orchestration.Operations`) exists but not wired (N-17) |
| `documentation/api-reference.md` | Line 98 — `basketId` absent from `GET /v1/products` (N-09); line 321 — `/v1/disruptions/delay` still `200 OK` (N-08); lines 575–576 — Loyalty API points settle/reverse/reinstate documented as operational (N-18) |
| `documentation/service-urls.md` | Line 57 — DatabaseMcp still "Local only" (N-11, N-14 partial) |

### Commit reference

Review conducted against commit `3cde845` (tip of `main` as of 2026-07-20).
80 commits merged since prior review (`68cd181`, 2026-06-01).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS | Event-subscription stubs only; no inspectable business logic |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap from prior reviews |
| Penetration testing / runtime security | Requires live environment access |
| Per-service API specs (`documentation/api-specs/`) | Selective spot-check only; no spec changes noted in this cycle's commits |
| Retail API ConfirmBasketHandler | paxId migration touched this file (commit `3cde845`); full re-read deferred; critical paxId serialisation bug fixed and tested |
