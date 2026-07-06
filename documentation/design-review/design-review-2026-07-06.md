# Apex Air — Weekly Design Review
**Date:** 2026-07-06
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-06-01.md

---

## 1. Executive summary

The platform posture is **Non-conforming** for a second consecutive cycle, carried by two CRITICAL AGEING findings that have now been open for six and five weeks respectively with no progress. H-04 (bag tag random sequence, week 6) and H-05 (Offer MS direct MS-to-MS calls, week 5) remain byte-for-byte identical to their first appearance. The HIGH finding N-11 (DatabaseMcp unrestricted PII read access, no audit logging, incorrect documentation) also moves into week 2 without remediation. Against this, the cycle's single most significant achievement is a breakthrough on test coverage: six new test projects have been added containing 27 test methods across 11 test files, five of which are now correctly wired to their deploy workflows — substantially resolving the M-07 / H-03r gap that was a MEDIUM carrying concern since 2026-04-21. Two new LOW findings are raised: the `isVip` passenger field introduced this cycle is undocumented across all design documents (N-15), and the extensive `paxId` integer migration — spanning over a dozen commits — has not been reflected in `order.md`'s JSON schema examples (N-16). The single most important action this week remains unchanged: close H-04 by replacing `Random.Shared.Next` with a SQL Server `SEQUENCE` object.

---

## 2. Critical findings (act this week)

### H-04 — Bag tag sequence number randomly generated; IATA Resolution 740 uniqueness violated — CRITICAL AGEING (Week 6)

**Severity:** CRITICAL AGEING (week 6 — unchanged since the 2026-05-18 review)
**Principle breached:** Architecture Principals — IATA Standards Alignment; IATA Resolution 740 requires bag tag licence plate numbers to be globally unique per airline per flight day.

**Evidence (unchanged for sixth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Application/OciCheckIn/OciCheckInHandler.cs:381–382` — `// TODO: In future, this 6-digit sequence number needs to be auto-incremented from a persistent counter rather than generated randomly. var sequence = Random.Shared.Next(0, 1_000_000).ToString("D6");`
- Identical generator invoked for both OCI self check-in and agent check-in paths. No SQL `SEQUENCE` object, no persistent counter, no database change.
- This cycle added a test project for the Delivery MS (`ReservationSystem.Tests.Microservices.Delivery`) with a `WriteManifestHandlerTests.cs` file, but no test was added for the bag tag generation path.

**Impact if unresolved:** Duplicate bag tags at any production check-in volume. Bag misrouting and loss. IATA Resolution 740 non-compliance. Operational safety risk.

**Recommended remediation:** Replace `Random.Shared.Next` with `NEXT VALUE FOR delivery.BagTagSequence` (SQL Server `SEQUENCE` object in the `delivery` schema). Pass the sequence value into `GenerateBagTag()` as a parameter from the repository. The mod-7 check-digit logic is correct and unchanged. Close the TODO comment.

**Age:** 6 weeks.

---

### H-05 — Offer MS timer trigger calls Schedule MS and Ancillary MS directly — CRITICAL AGEING (Week 5)

**Severity:** CRITICAL AGEING (week 5 — escalated from HIGH in the 2026-06-01 review)
**Principle breached:** Architecture Principals — "No direct microservice-to-microservice communication. This constraint applies without exception."

**Evidence (unchanged for fifth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/ScheduleServiceClient.cs:11` — self-granted exception comment: `"Note: direct MS-to-MS calls are an accepted exception for timer triggers."` No governance document or ADR grants this exception.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/SeatServiceClient.cs:10` — acknowledged violation: `"TODO: remove this cross-domain call — cabin counts are already stored in offer.FlightInventory.Cabins"`.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Application/RollingInventoryImport/RollingInventoryImportHandler.cs:37,54` — both clients injected and called.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Program.cs:76–91` — `ScheduleMs` and `AncillaryMs` HTTP clients registered.

**Impact if unresolved:** Deployment coupling (Offer MS lifecycle bound to Schedule MS and Ancillary MS contract stability). Obscured distributed traces. No circuit breaker — a Schedule MS or Ancillary MS outage at 01:00 UTC silently fails the nightly rolling inventory import.

**Recommended remediation (in preference order):**
1. `SeatServiceClient` — implement the acknowledged TODO: derive cabin counts from existing `offer.FlightInventory.Cabins` rows for the same `AircraftType`. The data is already present locally; no cross-service call is required.
2. `ScheduleServiceClient` — raise an ADR to formally grant or deny the exception. If granted, add circuit breaker, retry policy, timeout, and dead-letter alerting. If denied, move the timer trigger to a dedicated `RollingInventoryOrchestration` function in the Operations API.
3. In either case: delete the self-granted exception comment from `ScheduleServiceClient.cs:11`.

**Age:** 5 weeks.

---

## 3. Status of prior findings

| Finding | Prior Severity | Status | Evidence note |
|---------|---------------|--------|--------------|
| H-04 — Bag tag sequence randomly generated | CRITICAL AGEING | **UNCHANGED** | `OciCheckInHandler.cs:382` — `Random.Shared.Next(0, 1_000_000)` confirmed unchanged. Week 6. |
| H-05 — Offer MS calls Schedule MS and Ancillary MS directly | CRITICAL AGEING | **UNCHANGED** | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10` confirmed unchanged. Week 5. |
| N-11 — DatabaseMcp PII exposure; local-only documentation incorrect | HIGH | **UNCHANGED** | No schema allowlist, no row cap, no audit logging added. `service-urls.md:57` still states "Local only" despite Azure deployment confirmed in `main_reservation-system-mcp-db.yml`. Week 2. |
| M-07 — No integration tests for orchestration APIs | MEDIUM | **SUBSTANTIALLY RESOLVED** (downgrade — see section 5) | 6 new test projects, 27 test methods, 5 wired to deploy workflows. Residual gap: Operations deploy workflow still vacuous; 7+ services untested. |
| H-03r — dotnet test gate runs 0 tests | MEDIUM | **SUBSTANTIALLY RESOLVED** (downgrade — see section 5) | Retail, Delivery, Offer, Order, and Schedule deploy workflows now target real test project paths. Operations, Admin, Loyalty, Customer, Identity, Payment, User, and MCP tool workflows still run `dotnet test --no-build` finding no test classes. |
| C-02r — Debug infrastructure retained in production code | MEDIUM | **UNCHANGED** | `GetOrderDebugHandler.cs` and `GetOrderDebugQuery.cs` still present in `Order.Application.GetOrderDebug/`. |
| N-01 — Seatmap booking-window occupancy gap | MEDIUM | **UNCHANGED** | Pre-booking seatmap derives occupancy from manifest only; in-progress `offer.SeatReservation` holds not surfaced. |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `Application/Seat/GetSeatOffers/GetSeatOffersHandler.cs` and `Application/Seat/GetSeatOffer/GetSeatOfferHandler.cs` TODOs confirmed present — handlers return `null`. Files have been reorganised into `Application/Seat/` subdirectory but logic remains unimplemented. |
| N-05 — Bag tag response field undocumented | MEDIUM | **UNCHANGED** | `documentation/design/delivery.md:331` still states "bagTagNumber is null at purchase and populated by the Airport API when the bag is checked in." The Delivery MS OCI handler generates the tag; the Airport API does not. |
| N-10 — OrderData JSON lacks schemaVersion | MEDIUM | **UNCHANGED** (worsening — see N-16) | No `schemaVersion` field added. The `paxId` integer migration this cycle (12+ commits) changes the passenger identifier format across OrderData without a version discriminator; see N-16. |
| M-08 — TierConfig table not mapped with HasTrigger | LOW | **UNCHANGED** | `CustomerDbContext.cs` still has no `TierConfig` DbSet or `HasTrigger("TR_TierConfig_UpdatedAt")`. |
| M-10 — TODO without issue reference committed to main | LOW | **UNCHANGED** | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs remain without author or tracking issue reference. |
| N-08 — HandleDelayHandler throws NotImplementedException | LOW | **UNCHANGED** | `HandleDelayHandler.cs:35` still throws `NotImplementedException()`; `api-reference.md:321` documents the endpoint as returning `200 OK`. |
| N-09 — GET /v1/products basketId parameter undocumented | LOW | **UNCHANGED** | `api-reference.md:98` still does not mention the optional `basketId` query parameter or the rule evaluation logic it triggers. |
| N-13 — Post-departure manage-booking lockout undocumented | LOW | **UNCHANGED** | The departure lockout enforcement (422 when all segments have departed) remains absent from `documentation/design/manage-booking.md` and `api-reference.md` endpoint descriptions. |
| N-14 — DatabaseMcp absent from api-reference.md | LOW | **UNCHANGED** | No entry in `api-reference.md`; `service-urls.md:57` entry is factually incorrect. |

---

## 4. High findings

### N-11 — DatabaseMcp tool exposes unrestricted PII read access with no audit logging; documented as local-only but deployed to Azure (week 2)

**Severity:** HIGH (week 2 — unchanged from 2026-06-01)
**Principle breached:** Security Principals — PII must not appear in logs, telemetry, or error messages (spirit extended to unaudited data access paths); PCI DSS compliance scoped to Payment MS; audit logging required for state-changing and PII-accessing operations. Infrastructure Principals — managed identity patterns.

**Evidence (unchanged):**
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:85–101` — `run_select_query` MCP tool accepts an arbitrary SQL `SELECT` or CTE from any authenticated caller and executes it against the full database without a schema allowlist, row cap, or audit log.
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:144–163` — `RunSelectQuery` opens a `SqlConnection(ConnectionString)` and executes caller-supplied SQL verbatim.
- `documentation/service-urls.md:57` — still describes the tool as "Local only — Azure Functions host, listens on `http://localhost:7071`". The `.github/workflows/main_reservation-system-mcp-db.yml` workflow deploys it to Azure on every push to main.

**Impact if unresolved:** Any holder of the `reservation-system-mcp-db` function key has unrestricted read access to all PII across all schemas with no application-layer audit trail. Bulk extraction of passenger records, travel documents, and payment data is possible silently.

**Recommended remediation:** (1) Correct `service-urls.md:57` to reflect Azure deployment. (2) Add per-call audit logging: query text, row count, timestamp. (3) Enforce a schema allowlist (permit `offer.*`, `schedule.*`; block `customer.*`, `identity.*`, `payment.*`, `order.*`). (4) Add a `TOP 500` row cap to `RunSelectQuery`. (5) Verify the connection string connects as a read-only, schema-restricted SQL login.

**Age:** 2 weeks.

---

## 5. Medium and low findings

### M-07 / H-03r residual — Test coverage gap for Operations, Admin, Loyalty, Customer, Identity, Payment, User

The test breakthrough this cycle substantially resolves M-07 and H-03r. Five deploy workflows now correctly reference real test project paths and will gate deploys on failing tests:

| Workflow | Test project wired |
|----------|--------------------|
| `main_reservation-system-db-api-retail.yml` | `ReservationSystem.Tests.Orchestration.Retail` (2 test files, 27 methods) |
| `main_reservation-system-db-microservice-delivery.yml` | `ReservationSystem.Tests.Microservices.Delivery` (1 test file) |
| `main_reservation-system-db-microservice-offer.yml` | `ReservationSystem.Tests.Microservices.Offer` (1 test file) |
| `main_reservation-system-db-microservice-order.yml` | `ReservationSystem.Tests.Microservices.Order` (5 test files) |
| `main_reservation-system-db-microservice-schedule.yml` | `ReservationSystem.Tests.Microservices.Schedule` (1 test file) |

Residual gap: `ReservationSystem.Tests.Orchestration.Operations` exists with `CheckInHelperTests.cs` but `main_reservation-system-db-api-operations.yml` still runs the generic `dotnet test --no-build` (finding 0 test classes). The Admin, Loyalty, Customer, Identity, Payment, User, Ancillary, Exception, and MCP tool workflows remain unlinked to any test project.

### New findings this cycle

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| N-15 | `isVip` passenger field undocumented | LOW | G — Documentation | Commits `f4a4197` and `f3a3600` add a VIP flag (`isVip: boolean`) to passenger data across order creation and management. The field is handled in `UpdateOrderPassengersHandler.cs:83–85`, surfaced in the Terminal app (`new-order.html`, `order-detail.html`), and present in `bookflow-journey.json:364`. There is no mention of `isVip` in `documentation/design/order.md` (OrderData passenger schema), `documentation/design/customer.md`, or `documentation/api-reference.md` (PATCH `/v1/orders/{bookingRef}/passengers`). The "update documentation atomically with code" rule was not followed. | Order MS / Retail API / Docs owner |
| N-16 | `order.md` JSON examples use legacy `passengerId: "PAX-N"` string format after paxId integer migration | MEDIUM | G / D | 12+ commits (`33cad98`, `09f7d4c`, `852a904`, `96fef3e`, `f7eb1da`, `a67446b`, `4ec75a3`, `379814b`, `50b562b`, `d59334e`, `fee15c4`, `d02a9f4`, `a0083ed`) migrate the codebase from string `passengerId: "PAX-N"` to integer `paxId: N` as the primary passenger identifier across Order MS, Delivery MS, and all orchestration handlers. `documentation/design/order.md` still shows `passengerId: "PAX-1"` / `"PAX-2"` in every JSON example (lines 376, 398, 575, 597, 661–666, 684–689). The Mermaid sequence diagram at `order.md:162` still labels the manifest call with `passengerId`. Without `schemaVersion` on OrderData (N-10, open for two cycles), it is impossible for a reader to determine which format is authoritative from documentation alone. | Order MS / Docs owner |

### Carried medium and low findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| H-04 | Bag tag sequence randomly generated | CRITICAL AGEING | A / E | See section 2. Week 6. | Delivery MS owner |
| H-05 | Offer MS calls Schedule MS and Ancillary MS directly | CRITICAL AGEING | A | See section 2. Week 5. | Offer MS owner |
| N-11 | DatabaseMcp PII exposure | HIGH | C | See section 4. Week 2. | Platform Architect |
| C-02r | Debug infrastructure in production code | MEDIUM | C | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` still present in Order MS; all authenticated; delete them. | Retail API / Order MS owner |
| N-01 | Seatmap booking-window occupancy gap | MEDIUM | A / B | Pre-booking seatmap shows manifest-only occupancy; in-progress `offer.SeatReservation` holds not surfaced. | Retail API / Offer MS owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A | `GetSeatOffersHandler.cs` and `GetSeatOfferHandler.cs` return `null` with TODO. | Ancillary MS owner |
| N-05 | Bag tag documentation incorrect | MEDIUM | G | `delivery.md:331` attributes bag tag generation to Airport API; Delivery MS OCI handler generates it. | Delivery / Operations owner |
| N-10 | OrderData JSON lacks schemaVersion | MEDIUM | D | No `schemaVersion` field; worsened by paxId migration (N-16). | Order MS owner |
| M-08 | TierConfig table not mapped with HasTrigger | LOW | D | `TR_TierConfig_UpdatedAt` in `Script.sql`; `CustomerDbContext` has no mapping. | Customer MS owner |
| M-10 | TODO without issue reference on main | LOW | Coding Standards | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs. | All owners |
| N-08 | HandleDelayHandler throws NotImplementedException | LOW | B | `/v1/disruptions/delay` documented as operational; implementation throws `NotImplementedException`. | Operations API owner |
| N-09 | GET /v1/products basketId undocumented | LOW | G | `api-reference.md:98` missing optional `basketId` parameter and rule evaluation logic. | Retail API / Docs owner |
| N-13 | Post-departure manage-booking lockout undocumented | LOW | G | 422 departure lockout enforced in code; absent from `manage-booking.md` and `api-reference.md`. | Retail API / Docs owner |
| N-14 | DatabaseMcp absent from api-reference.md | LOW | G | No api-reference entry; `service-urls.md:57` incorrectly describes it as local-only. | Platform Architect |

---

## 6. Observations and positive notes

- **Test coverage breakthrough — the most significant quality improvement in five review cycles.** Six new test projects were added this cycle: `ReservationSystem.Tests.Microservices.Offer`, `ReservationSystem.Tests.Microservices.Order` (five handler test classes), `ReservationSystem.Tests.Microservices.Schedule`, `ReservationSystem.Tests.Microservices.Delivery`, `ReservationSystem.Tests.Orchestration.Operations`, and `ReservationSystem.Tests.Orchestration.Retail`. Five of these are correctly wired to their deploy workflows. There are now 27 test methods in CI that actually execute. This closes the most persistent quality gap in the review history and demonstrates real momentum.

- **Test obligation formalised in governance artefacts.** Commit `a5f734a` adds a "Test maintenance obligation" section to `documentation/tests.md` requiring test validation before every commit. Commit `b437508` adds rule 13 to `CLAUDE.md`: "Tests must be validated and updated with every code change." Encoding this obligation in both agent-facing instructions and human-facing docs is the right governance move.

- **paxId integer migration substantially complete.** Over a dozen commits this cycle (`33cad98` through `a0083ed`) migrate the primary passenger identifier from the string `"PAX-N"` format to an integer `paxId` across the Order MS, Delivery MS, and all orchestration handlers. The migration is internally consistent in code. Documentation (N-16) has not caught up, but the implementation work is complete and coherent.

- **NDC passengerId normalisation fix.** Commit `1bd12b6` correctly normalises an integer `passengerId` from NDC bookings to the `"PAX-N"` string format expected internally, preventing a type mismatch in `ConfirmOrderHandler` that would have caused NDC-originated orders to fail passenger lookups.

- **Delivery MS test project wired to CI on commit day.** The `WriteManifestHandlerTests.cs` test file (`09f7d4c`) was added in the same PR sequence as the paxId migration to `WriteManifestHandler.cs` — exactly the atomicity the test maintenance obligation demands. The intent is correct; the remaining gap (bag tag path not tested) is noted above.

- **Simulator timer trigger finalised.** After two intermediate values (15 minutes, 30 minutes), commit `67ecfe2` settles on 24-hour simulator triggers, matching a daily schedule import cadence.

---

## 7. Conformance scorecard

| Dimension | Conformance | Trend vs 2026-06-01 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🔴 Red | → | H-04 CRITICAL AGEING week 6; H-05 CRITICAL AGEING week 5; N-01 unchanged |
| B — API Contract Conformance | 🟡 Amber | ↓ | N-16 new (order.md examples stale after paxId migration); N-08, N-09, N-13 unchanged |
| C — Security Principles | 🔴 Red | → | N-11 HIGH week 2 unchanged; C-02r unchanged |
| D — Data Storage & Schema Principles | 🟡 Amber | ↓ | M-08 unchanged; N-10 worsened by paxId migration without schemaVersion; N-16 new |
| E — Infrastructure & Integration Principles | 🟡 Amber | → | H-04 and H-05 CRITICAL AGEING unchanged; test gate improvement partially resolves E-dimension risk |
| F — Testing & CI | 🟡 Amber | ↑↑ | Six new test projects; 5 workflows now gate on real tests; Operations workflow gap; 7+ services still untested |
| G — Documentation Drift | 🟡 Amber | ↓ | N-15 new (VIP undocumented); N-16 new (paxId migration not reflected in order.md); N-05, N-09, N-13, N-14 unchanged |

---

## 8. Governance gaps

The following gaps remain open from prior reviews. No new governance gaps identified this cycle.

1. **ADR register has only one entry; several decisions remain undocumented.** The `ScheduleServiceClient` self-granted exception (H-05) urgently needs either an approved ADR granting the exception with compensating controls, or a denial. Decisions still awaiting ADR capture: (a) manifest-only seatmap occupancy as accepted design; (b) shared `Microservice:HostKey` authentication pattern; (c) DatabaseMcp as an approved developer data-access mechanism or its explicit denial and restriction.

2. **No OpenAPI specs in repository.** Integration Principals require machine-readable OpenAPI 3.x specs version-controlled alongside service code. With active paxId migration and VIP field additions this cycle, the absence of a machine-readable contract means these changes cannot be caught by a contract test gate.

3. **No consumer-driven contract tests.** Pact or equivalent between orchestration APIs and microservices remains absent. The paxId migration changes the wire format of the Delivery MS WriteManifest request; without contract tests, a regression in any consumer would be caught only at runtime.

4. **No incident response plan discoverable.** Security Principals require a documented IR plan including a UK GDPR 72-hour breach notification procedure. The open N-11 finding (DatabaseMcp unrestricted PII read access without audit logging) makes this gap more urgent: if the function key were compromised there would be no application-layer evidence of what data was accessed.

5. **Accounting MS not assessable.** Event-subscription stubs only; no business logic to inspect.

6. **Airport API and Finance API scaffolded only.** Not assessed. The `delivery.md:331` incorrect attribution of bag tag generation to the Airport API (N-05) remains related to this gap.

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
| `documentation/api-reference.md` | Full endpoint catalogue (spot-checked for N-09, N-13, N-14, N-15, N-16) |
| `documentation/design/order.md` | OrderData schema — paxId migration impact (N-10, N-16) |
| `documentation/design/delivery.md` | Bag tag documentation (N-05, H-04) |
| `documentation/design/manage-booking.md` | Departure lockout documentation (N-13) |
| `documentation/service-urls.md` | Service registry accuracy (N-11, N-14) |
| `documentation/tests.md` | Test obligation update |
| `CLAUDE.md` | Rule 13 test mandate |
| `documentation/design-review/design-review-2026-06-01.md` | Most recent prior review |
| `documentation/design-review/design-review-2026-05-25.md` | Second prior review |
| `documentation/design-review/design-review-2026-05-21.md` | Third prior review |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| Delivery MS — OciCheckIn | `OciCheckInHandler.cs:381–382` — `Random.Shared.Next` confirmed unchanged (H-04, week 6) |
| Offer MS | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10`, `RollingInventoryImportHandler.cs:37,54`, `Program.cs:76–91` — H-05 confirmed unchanged |
| DatabaseMcp tool | `Functions/McpFunction.cs:85–163` — no allowlist, no audit logging, no row cap; `service-urls.md:57` — still "Local only" (N-11, week 2) |
| Order MS — Debug | `Application/GetOrderDebug/GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` — confirmed present (C-02r) |
| Ancillary MS — Seat handlers | `Application/Seat/GetSeatOffers/GetSeatOffersHandler.cs`, `Application/Seat/GetSeatOffer/GetSeatOfferHandler.cs` — TODOs confirmed (M-03) |
| Customer MS | `CustomerDbContext.cs` — M-08 TierConfig mapping confirmed absent |
| Operations API — HandleDelay | `HandleDelayHandler.cs:35` — NotImplementedException confirmed (N-08) |
| Order MS — paxId migration | `ConfirmOrderHandler.cs`, `UpdateOrderPassengersHandler.cs`, `UpdateOrderBagsHandler.cs`, `GetOrderHandler.cs`, `GetIropsOrdersHandler.cs` — paxId integer migration confirmed complete in code |
| Order MS — VIP flag | `UpdateOrderPassengersHandler.cs:83–85` — `isVip` handling confirmed; no documentation found (N-15) |
| Retail API — DeliveryServiceClient | `ManifestPassengerEntry` model confirmed with both `paxId` (int) and `passengerId` (string) fields |
| Operations API — DeliveryServiceClient | `OciCheckInTicket` model — `[JsonIgnore] PaxId` confirmed as non-deserialized; `passengerId` (string) still the wire format for OCI tickets |
| GitHub Actions workflows | All 20 workflows reviewed; 5 correctly target specific test project paths; 12 still run generic `dotnet test --no-build` (0 tests) |
| Test projects | `src/API/Tests/` — 11 test files, 27 `[Fact]` / `[Theory]` methods confirmed |
| `documentation/api-reference.md` | Line 98 — `basketId` absent from `GET /v1/products` (N-09); line 321 — `/v1/disruptions/delay` documented as `200 OK` (N-08) |
| `documentation/design/order.md` | Lines 162, 376, 398, 575, 597, 661–689 — `passengerId: "PAX-N"` format still present (N-16) |

### Commit reference

Review conducted against commit `3cde845` (tip of `main` as of 2026-07-06).
~50 commits merged since prior review (`68cd181`, 2026-06-01).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS | Event-subscription stubs only; no inspectable business logic |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass |
| Terminal app | No new security-relevant changes identified in commit log beyond VIP flag (reviewed via code) |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap from prior reviews |
| Penetration testing / runtime security | Requires live environment access |
| Per-service API specs (`documentation/api-specs/`) | Selective spot-check only; no spec changes noted in this cycle's commits |
