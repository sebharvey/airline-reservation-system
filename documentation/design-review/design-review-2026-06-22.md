# Apex Air — Weekly Design Review

**Date:** 2026-06-22
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-06-01.md

---

## 1. Executive summary

The platform posture is **Non-conforming** for the second consecutive week, unchanged from 2026-06-01. Two findings remain at CRITICAL AGEING: H-04 (bag tag random sequence, now week 6) and H-05 (Offer MS direct MS-to-MS calls, now week 5). N-11 (DatabaseMcp unrestricted PII read access, no audit logging) enters its second consecutive review as HIGH and will escalate to CRITICAL AGEING if still open in the next cycle. The dominant positive this cycle is a substantial resolution of the testing crisis: six xUnit test projects have been created with 26 real test methods, five deploy workflows now build and run tests against specific test project paths, and CLAUDE.md was updated to codify test maintenance as a key governance rule — the most meaningful improvement to dimension F in the review history. The most damaging new concern is that the 16-commit paxId migration — replacing `passengerId` string with `paxId` integer as the primary passenger identifier across Order MS, Delivery MS, and Operations API — was delivered without updating `order.md` JSON schema examples and without adding the `schemaVersion` field that data principles require before any breaking schema change, deepening finding N-10 from a principle gap to a concrete documentation drift regression. The single most important action this week remains resolving H-04 and H-05 — both are operationally critical and have no blocker preventing remediation.

---

## 2. Critical findings (act this week)

### H-04 — Bag tag sequence number randomly generated; IATA Resolution 740 uniqueness violated — CRITICAL AGEING (Week 6)

**Severity:** CRITICAL AGEING (week 6 — unchanged for six consecutive reviews)
**Principle breached:** Architecture Principals — IATA standards alignment; IATA Resolution 740 requires bag tag licence plate numbers to be globally unique per airline per flight day.

**Evidence (unchanged for sixth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Application/OciCheckIn/OciCheckInHandler.cs:381–382` — `// TODO: In future, this 6-digit sequence number needs to be auto-incremented from a persistent counter rather than generated randomly. var sequence = Random.Shared.Next(0, 1_000_000).ToString("D6");`
- The same `Random.Shared.Next` generator is invoked for both OCI self check-in and agent check-in paths. No SQL `SEQUENCE` object, no persistent counter table, no change in six weeks.

**Impact if unresolved:** Duplicate bag tags at any meaningful check-in volume. Bag misrouting and loss. IATA Resolution 740 non-compliance. Operational safety issue.

**Recommended remediation (unchanged):** Replace `Random.Shared.Next` with `NEXT VALUE FOR delivery.BagTagSequence` (SQL Server `SEQUENCE` object in the `delivery` schema, scoped per airline numeric prefix). Pass the next sequence value into `GenerateBagTag()` from the repository. The mod-7 check-digit logic is correct and unchanged. Close the TODO comment when done.

**Age:** 6 weeks.

---

### H-05 — Offer MS timer trigger calls Schedule MS and Ancillary MS directly — CRITICAL AGEING (Week 5)

**Severity:** CRITICAL AGEING (week 5 — unchanged for five consecutive reviews)
**Principle breached:** Architecture Principals — "No direct microservice-to-microservice communication. This constraint applies without exception."

**Evidence (unchanged for fifth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/ScheduleServiceClient.cs:11` — self-granted exception comment: `"Note: direct MS-to-MS calls are an accepted exception for timer triggers."` No governance document or ADR grants this exception.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/SeatServiceClient.cs:10` — acknowledged violation: `"TODO: remove this cross-domain call — cabin counts are already stored in offer.FlightInventory.Cabins"`.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Application/RollingInventoryImport/RollingInventoryImportHandler.cs:29–37` — both clients injected and called.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Program.cs:77–94` — `ScheduleMs` and `AncillaryMs` HTTP clients registered.

**Impact if unresolved:** Deployment coupling (Offer MS lifecycle bound to Schedule MS and Ancillary MS contract stability). Obscured distributed traces. No circuit breaker — a Schedule MS or Ancillary MS outage at 01:00 UTC silently fails the nightly rolling inventory import.

**Recommended remediation (in preference order):**
1. `SeatServiceClient` — implement the acknowledged TODO: derive cabin counts from existing `offer.FlightInventory.Cabins` rows for the same `AircraftType`. The data is already present locally; no cross-service call is required.
2. `ScheduleServiceClient` — raise an ADR to formally grant or deny the exception. If granted, add circuit breaker, retry policy, timeout, and dead-letter alerting as compensating controls. If denied, move the timer trigger to a dedicated `RollingInventoryOrchestration` function in the Operations API.
3. In either case: delete the self-granted exception comment from `ScheduleServiceClient.cs:11` regardless of the resolution path.

**Age:** 5 weeks.

---

## 3. Status of prior findings

| Finding | Prior Severity | Status | Evidence note |
|---------|---------------|--------|--------------|
| H-04 — Bag tag sequence randomly generated | CRITICAL AGEING | **UNCHANGED** | `OciCheckInHandler.cs:381–382` — `Random.Shared.Next(0, 1_000_000)` unchanged. Week 6. |
| H-05 — Offer MS calls Schedule MS and Ancillary MS directly | CRITICAL AGEING | **UNCHANGED** | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10` unchanged. Week 5. |
| N-11 — DatabaseMcp PII exposure, documented as local-only but deployed to Azure | HIGH | **UNCHANGED** | `service-urls.md:57` still says "Local only — Azure Functions host". No audit logging, no schema allowlist, no api-spec document added. Week 2. |
| M-07 — No integration tests / CI test execution | MEDIUM | **SUBSTANTIALLY RESOLVED** | 6 xUnit test projects created with 26 test methods; 5 deploy workflows now execute real tests (Retail, Delivery, Offer, Order, Schedule). Residual gap: 12 workflows still execute 0 tests; Operations test project not wired into its pipeline. |
| H-03r — dotnet test gate executes zero tests | MEDIUM | **PARTIALLY RESOLVED** | 5 of 17 deploy workflows now run real tests against specific project paths. Remaining 12 workflows use `dotnet test --no-build` with no project path and execute 0 tests. Operations workflow (`main_reservation-system-db-api-operations.yml`) uses `pushd` into project dir with unqualified `dotnet test`, finding no test classes despite a test project existing at `src/API/Tests/ReservationSystem.Tests.Orchestration.Operations/`. |
| C-02r — Debug infrastructure retained in production code | MEDIUM | **UNCHANGED** | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs`, `AdminDebug*` Retail API functions still present. All authenticated; should be deleted. |
| N-01 — Seatmap booking-window occupancy gap | MEDIUM | **UNCHANGED** | Pre-booking seatmap derives occupancy from manifest only; in-progress `offer.SeatReservation` holds not surfaced. DB constraint preserves integrity. |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `GetSeatOffersHandler.cs:20` TODO unchanged. |
| N-10 — OrderData JSON lacks schemaVersion | MEDIUM | **REGRESSED** | paxId migration (16 commits this cycle) adds structural changes to OrderData without schemaVersion; `order.md` JSON examples still show `passengerId` without `paxId` in passengers or eTickets. Documentation no longer matches code. See section 4. |
| N-05 — Bag tag documentation incorrect | MEDIUM | **UNCHANGED** | `delivery.md:331` still says "bagTagNumber is null at purchase and populated by the Airport API when the bag is checked in." Delivery MS OCI handler generates the tag. |
| M-08 — TierConfig table not mapped with HasTrigger | LOW | **UNCHANGED** | `CustomerDbContext.cs` still has no `TierConfig` DbSet or `HasTrigger("TR_TierConfig_UpdatedAt")`. `TR_TierConfig_UpdatedAt` exists in `Script.sql`. |
| M-10 — TODO without issue reference committed to main | LOW | **UNCHANGED** | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs without author or tracking issue reference. |
| N-08 — HandleDelayHandler throws NotImplementedException | LOW | **UNCHANGED** | `HandleDelayHandler.cs:35` still throws `NotImplementedException()`; `api-reference.md` documents the endpoint as returning `200 OK`. |
| N-09 — GET /v1/products basketId parameter undocumented | LOW | **UNCHANGED** | `api-reference.md:98` still does not mention the optional `basketId` query parameter. |
| N-13 — Post-departure manage-booking lockout business rule undocumented | LOW | **UNCHANGED** | Departure lockout enforced in four handlers (`CancelOrderHandler`, `ChangeOrderHandler`, `AddOrderBagsHandler`, `UpdateOrderSeatsHandler`) still absent from `documentation/design/manage-booking.md` and `api-reference.md`. |
| N-14 — DatabaseMcp tool absent from api-reference.md | LOW | **UNCHANGED** | No entry added to `api-reference.md`. `service-urls.md` entry remains inaccurate (local-only). |

---

## 4. High findings

### N-11 — DatabaseMcp tool exposes unrestricted PII read access with no audit logging; documented as local-only but deployed to Azure (Week 2)

**Severity:** HIGH (Week 2 — escalates to CRITICAL AGEING next cycle if unresolved)
**Principle breached:** Security Principals — PII must never appear in logs, telemetry, or error messages; PCI DSS compliance scoped to Payment MS; audit logging required for all state-changing operations (spirit extended to PII access paths). Service registry accuracy: `service-urls.md` documents the tool as local-only when it is deployed to production Azure.

**Evidence (unchanged from 2026-06-01):**
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:85–101` — the `run_select_query` MCP tool accepts arbitrary SQL from any authenticated caller and executes it against the database connection string. No schema allowlist, no table blocklist, no row cap, no query audit log.
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:144–163` — `RunSelectQuery` opens `SqlConnection(ConnectionString)` and executes caller-supplied SQL verbatim. A holder of the function key can issue `SELECT * FROM customer.Customer`, `SELECT * FROM [identity].RefreshToken`, `SELECT * FROM payment.Payment`, or `SELECT * FROM [order].[Order]` (full `OrderData` JSON including travel documents and passenger PII).
- `documentation/service-urls.md:57` — "Local only — Azure Functions host, listens on `http://localhost:7071`". This remains incorrect: `.github/workflows/main_reservation-system-mcp-db.yml` deploys the tool to Azure slot `reservation-system-mcp-db` on every push to main.

**Impact if unresolved:** Any holder of the `reservation-system-mcp-db` function key has unrestricted read access to all PII across all schemas with no application-layer audit trail. The documentation discrepancy means operators cannot accurately assess the attack surface.

**Recommended remediation (unchanged):**
1. Correct `documentation/service-urls.md:57` to accurately reflect Azure deployment.
2. Add query audit logging: every `run_select_query` call must emit a structured log entry (query text, row count returned, timestamp, caller identity).
3. Enforce a schema allowlist: permit queries only against `offer.*` and `schedule.*`; block `customer.*`, `identity.*`, `payment.*`, and `order.*`. Document any broader access as an approved exception mirroring the Terminal debug modal in `security-principals.md`.
4. Add a row cap (e.g. `TOP 500`) to `RunSelectQuery` to prevent bulk data extraction.
5. Add an api-spec document (`documentation/api-specs/database-mcp.md`) describing the tool, its access controls, and the deliberate data access it enables.

**Age:** 2 weeks. **Will escalate to CRITICAL AGEING in the next review if unresolved.**

---

### N-10 (REGRESSED) — paxId migration delivered without schemaVersion; order.md JSON schemas no longer match code

**Severity:** HIGH (regressed from MEDIUM)
**Principle breached:** Data Storage Principals — "JSON documents must include a `schemaVersion` field; migration strategies for documents stored under previous versions must be defined before deploying breaking changes." Architecture Principals — "Update documentation atomically with code."

**Evidence:**
- 16 commits this cycle (`ff3f874`, `a67446b`, `041b602`, `f7eb1da`, `852a904`, `96fef3e`, `4ec75a3`, `379814b`, `f470763`, `fee15c4`, `d59334e`, `09f7d4c`, `d02a9f4`, `33cad98`) migrate the primary passenger identifier from `passengerId` (string, e.g. `"PAX-1"`) to `paxId` (integer, e.g. `1`) across `Order MS`, `Delivery MS`, and `Operations API`. This is a structural breaking change to the `OrderData` JSON document.
- `documentation/design/order.md:376,398,423,458,470,484,575,650,661,665,673,685,688` — all JSON examples throughout `order.md` still show `passengerId: "PAX-1"` in passenger nodes, eTickets, order items, and bag/seat/SSR entries. `paxId` does not appear in any passenger or eTicket JSON example (the `paxId` at line 565 is the unrelated customer notes field).
- No `schemaVersion` field has been added to `OrderData` in any commit this cycle or prior. Existing `OrderData` documents in the database pre-date both the original `paxId` addition (prior cycle) and this cycle's full migration, and cannot be reliably parsed by the updated code without a version discriminator.

**Impact if unresolved:** Application code now reads `paxId` as the primary identifier; any `OrderData` row written before this cycle's migration may lack `paxId` (containing only `passengerId`). The fallback reads added in some handlers (`d59334e`, `d02a9f4`) suggest the team is aware of the backwards-compatibility concern, but without `schemaVersion` the migration strategy is implicit and undocumented. Operators inspecting `order.md` will find a JSON schema that does not match what is actually stored.

**Recommended remediation:**
1. Add `"schemaVersion": 2` (integer) to `OrderData` JSON in all write paths, and update `order.md` examples to show `paxId` (integer) alongside `passengerId` (string kept for backwards compatibility during migration).
2. Define a formal migration strategy in `order.md`: document which schema version uses `passengerId`, which uses `paxId`, and when the `passengerId` fallback reads will be removed.
3. Update all `order.md` JSON schema examples to reflect current code.

---

## 5. Medium and low findings

### New finding this cycle

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| N-15 | Operations API test project exists but not wired into CI | LOW | F | `src/API/Tests/ReservationSystem.Tests.Orchestration.Operations/Application/CheckIn/CheckInHelperTests.cs` contains real xUnit tests but `.github/workflows/main_reservation-system-db-api-operations.yml` runs `pushd` into the project directory then `dotnet test --no-build`, finding no test classes. The test project path is not referenced. `CheckInHelperTests` — validating `ParseOrderLookups` and `ParsePaxToTicketMap` — will never execute in CI. | Platform / DevOps |

### Carried findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| H-03r | dotnet test gate executes 0 tests in 12 of 17 workflows | MEDIUM | F | Retail API, Delivery MS, Offer MS, Order MS, and Schedule MS workflows now run real tests. Admin API, Loyalty API, Customer MS, Identity MS, Payment MS, User MS, Ancillary MS, Exceptions MS, MCP tool, and both simulator workflows still run unqualified `dotnet test --no-build` from the project directory with no test project path, executing 0 test methods. | QA / Platform |
| C-02r | Debug infrastructure retained in production code | MEDIUM | C | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs`, three `AdminDebug*` Retail API functions, debug client methods. All authenticated; should be deleted. | Retail API / Order MS owner |
| N-01 | Seatmap booking-window occupancy gap | MEDIUM | A / B | Pre-booking seatmap shows manifest-only occupancy; in-progress `offer.SeatReservation` holds during booking window not surfaced. DB constraint preserves integrity. | Retail API / Offer MS owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A | `GetSeatOffersHandler.cs:20` TODO. Business logic belongs in Application layer. | Ancillary MS owner |
| N-05 | Bag tag documentation incorrect | MEDIUM | G | `delivery.md:331` attributes bag tag generation to Airport API; Delivery MS OCI handler generates it. | Delivery / Operations owner |
| M-08 | TierConfig table not mapped with HasTrigger | LOW | D | `TR_TierConfig_UpdatedAt` in `Script.sql`; `CustomerDbContext` has no mapping. | Customer MS owner |
| M-10 | TODO without issue reference on main | LOW | Coding Standards | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs without author or tracking issue. | All owners |
| N-08 | HandleDelayHandler throws NotImplementedException | LOW | B | `/v1/disruptions/delay` documented as returning `200 OK`; implementation throws. | Operations API owner |
| N-09 | GET /v1/products basketId parameter undocumented | LOW | G | `api-reference.md:98` omits the optional `basketId` query parameter and rule evaluation logic. | Retail API / Docs owner |
| N-13 | Post-departure manage-booking lockout undocumented | LOW | G | Departure lockout enforced in four handlers but absent from `manage-booking.md` and `api-reference.md`. | Retail API / Docs owner |
| N-14 | DatabaseMcp tool absent from api-reference.md | LOW | G | No entry in `api-reference.md`; `service-urls.md` entry inaccurate. | Platform Architect |
| N-15 | Operations API test project not wired into CI | LOW | F | `CheckInHelperTests` never executes; workflow uses unqualified `dotnet test` from project dir. New this cycle. | Platform / DevOps |

---

## 6. Observations and positive notes

- **M-07 substantially resolved — the most significant testing improvement in review history.** Commit `a4bae21` created four xUnit test projects (Offer MS, Order MS, Schedule MS, Retail API) with real test methods and updated the corresponding CI workflows to build and run them before publishing. Commit `22f76d3` subsequently added the Delivery MS test project and wired it into CI. From zero real test methods executing in any pipeline as of 2026-06-01, the platform now has 26 xUnit test methods executing in 5 deploy pipelines. This closes a regression that had been running since the deletion of the integration test workflows two cycles ago.

- **Test maintenance rule formally added to CLAUDE.md.** Commit `a5f734a` adds rule 13 to the key rules section: before committing any application code change, search `src/API/Tests/` for tests that exercise the modified code path, fix any that break, and add tests for new behaviour. Never leave tests in a failing state. This provides a durable governance anchor that applies to all future AI agent contributions.

- **paxId migration accompanied by tests.** The 16-commit paxId migration was accompanied by corresponding test updates and additions — `UpdateOrderBagsHandlerTests.cs`, `UpdateOrderCheckInHandlerTests.cs`, `UpdateOrderPassengersHandlerTests.cs`, `GetIropsOrdersHandlerTests.cs`, `WriteManifestHandlerTests.cs`, and `CheckInHelperTests.cs` were all updated or created alongside the code changes. This demonstrates the test maintenance rule being observed in practice immediately after it was documented, which is the desired pattern.

- **VIP flag persistence bug fixed.** Commit `f3a3600` fixes the VIP flag not being persisted to the passenger record and not being shown in the passenger view — a data integrity fix for a named passenger attribute.

- **NDC passengerId normalisation hardened.** Commit `1bd12b6` normalises an integer `passengerId` to the `PAX-n` string format expected by the web application, preventing a regression in the NDC booking path where integer passenger identifiers from the order confirmation flow would not map correctly back to UI state.

---

## 7. Conformance scorecard

| Dimension | Conformance | Trend vs 2026-06-01 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🔴 Red | → | H-04 CRITICAL AGEING week 6 unchanged; H-05 CRITICAL AGEING week 5 unchanged; N-01 seatmap gap unchanged |
| B — API Contract Conformance | 🟡 Amber | → | N-08 unchanged; N-13 unchanged; N-09 unchanged; no new contract regressions in paxId migration commits |
| C — Security Principles | 🔴 Red | → | N-11 HIGH week 2 (escalates next cycle); C-02r unchanged; no new security regressions |
| D — Data Storage & Schema Principles | 🔴 Red | ↓ | N-10 regressed to HIGH — paxId migration is a breaking OrderData schema change without schemaVersion; `order.md` JSON examples no longer match code; M-08 unchanged |
| E — Infrastructure & Integration Principles | 🟡 Amber | → | H-04/H-05 CRITICAL AGEING ongoing; simulator timer cadence adjusted (functional change only); no new infrastructure regressions |
| F — Testing & CI | 🟡 Amber | ↑ | M-07 substantially resolved — 26 real tests in 5 workflows; CLAUDE.md test rule added; 12 workflows still execute 0 tests; Operations test project not wired (N-15) |
| G — Documentation Drift | 🟡 Amber | ↓ | N-10 documentation drift concrete (order.md paxId gap); N-09, N-13, N-14, N-05 all unchanged; no new documentation fixes this cycle |

---

## 8. Governance gaps

The following gaps remain open. Gap 1 is escalated to urgent given N-11 week 2.

1. **ADR-002 urgently needed — DatabaseMcp PII access approval or denial.** N-11 (DatabaseMcp unrestricted PII read with no audit logging) has been open two weeks and escalates to CRITICAL AGEING next cycle. The resolution path requires a governance decision: either approve the tool as a developer PII access mechanism (with schema restrictions, audit logging, and a formal exception documented in `security-principals.md` mirroring the Terminal debug modal exception) or restrict it to non-PII schemas. Either path requires an ADR. Without one, the tool sits in an undecided state that accumulates risk with every deployment to production.

2. **ADR-002 / ADR-003 needed for ScheduleServiceClient timer trigger exception and paxId migration strategy.** The self-granted exception comment in `ScheduleServiceClient.cs:11` (H-05) requires a formal governance decision (grant or deny, with compensating controls). The `OrderData` paxId migration (N-10) requires a documented schema versioning and deprecation strategy. Both decisions are being made implicitly by commit history, not explicitly by architecture governance.

3. **No OpenAPI specs in repository.** Integration principles require machine-readable OpenAPI 3.x specs version-controlled alongside service code. 50+ commits/week with no contract-test gate remains the systemic risk to API stability.

4. **No consumer-driven contract tests.** Pact or equivalent between orchestration APIs and microservices absent. The new unit tests are valuable but do not substitute for contract verification between services.

5. **No incident response plan discoverable.** Security principles require a documented IR plan with UK GDPR 72-hour breach notification procedure. N-11 (DatabaseMcp unrestricted PII access, no audit trail) makes this gap more acute — if the function key were compromised, there would be no application-layer evidence of what data was accessed.

6. **Accounting MS not assessable.** Event-subscription stubs only; no business logic to inspect.

7. **Airport API and Finance API scaffolded only.** Not assessed. N-05 (`delivery.md:331` attributing bag tag generation to Airport API) remains related to this gap.

8. **`service-urls.md` accuracy gap for DatabaseMcp.** Tool is deployed to Azure but still documented as local-only. All service-urls.md entries should accurately reflect deployment state.

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
| `documentation/design/order.md` | OrderData JSON schema — paxId documentation gap (N-10) |
| `documentation/design/delivery.md` | Bag tag documentation accuracy check (N-05) |
| `documentation/design/manage-booking.md` | Departure lockout documentation check (N-13) |
| `documentation/service-urls.md` | Service registry accuracy check (N-11, N-14) |
| `documentation/tests.md` | Test guide update check |
| `CLAUDE.md` | Governance rule 13 addition confirmation |
| `documentation/design-review/design-review-2026-06-01.md` | Most recent prior review |
| `documentation/design-review/design-review-2026-05-25.md` | Second prior review |
| `documentation/design-review/design-review-2026-05-21.md` | Third prior review |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| Delivery MS — OciCheckIn | `OciCheckInHandler.cs:381–382` — bag tag `Random.Shared.Next` confirmed unchanged (H-04, week 6) |
| Offer MS | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10`, `RollingInventoryImportHandler.cs:29–37`, `Program.cs:77–94` — H-05 confirmed unchanged (week 5) |
| DatabaseMcp tool | `Functions/McpFunction.cs` — no audit logging, no schema allowlist confirmed unchanged (N-11, week 2) |
| Order MS — Debug | `Application/GetOrderDebug/GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` — C-02r confirmed still present |
| Operations API | `HandleDelayHandler.cs:35` — N-08 `NotImplementedException` confirmed unchanged |
| Ancillary MS | `GetSeatOffersHandler.cs:20` — M-03 TODO confirmed unchanged |
| Customer MS | `CustomerDbContext.cs` — M-08 TierConfig confirmed absent |
| Order MS — paxId migration | Representative commits inspected: `ff3f874`, `a67446b`, `041b602`, `f7eb1da`, `852a904`, `96fef3e` — paxId replaces passengerId as primary identifier; no `schemaVersion` added |
| Test projects | `src/API/Tests/` — all 6 test project directories listed; 10 test files, 26 test methods counted |
| GitHub Actions workflows | All 20 workflow files inspected — 5 with targeted `dotnet test` paths (Retail, Delivery, Offer, Order, Schedule); 12 with unqualified `dotnet test` executing 0 tests; Operations uses `pushd` pattern with no test project reference |
| `documentation/api-reference.md` | Line 98 — `basketId` still absent from `GET /v1/products` (N-09); departure lockout still absent from relevant order endpoints (N-13) |
| `documentation/service-urls.md` | Line 57 — DatabaseMcp still described as local-only (N-11, N-14) |

### Commit reference

Review conducted against commit `e5bd88b` (tip of `main` as of 2026-06-22).
~50 commits merged since prior review (`68cd181`, 2026-06-01), dominated by paxId passenger identifier migration and test infrastructure creation.

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS | Event-subscription stubs only; no inspectable business logic |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass |
| Terminal app | No security-relevant changes noted in commit log this cycle |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap from prior reviews |
| Penetration testing / runtime security | Requires live environment access |
| Per-service API specs (`documentation/api-specs/`) | Selective spot-check only; no service-spec changes noted in this cycle's commits |
