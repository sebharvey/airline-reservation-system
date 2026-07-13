# Apex Air — Weekly Design Review

**Date:** 2026-07-13
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-06-01.md

---

## 1. Executive summary

The platform posture is **Non-conforming** — held for the second consecutive cycle and for the same reason: H-04 (bag tag random sequence) and H-05 (Offer MS direct MS-to-MS calls) remain unaddressed. H-04 now enters its sixth consecutive review cycle as CRITICAL AGEING; H-05 its fifth. The 42-day gap since the last review (2026-06-01 → 2026-07-13) makes the lack of remediation on these two findings especially difficult to accept — the calendar window for resolution has been more than generous. Against that, this cycle delivers the most significant testing improvement in the project's review history: six test projects now exist (Offer, Order, Schedule, Delivery, Operations, Retail) covering 11+ test classes, and five CI workflows now gate deployment on real test execution rather than zero-test `dotnet test` invocations. The CLAUDE.md test maintenance obligation was also formalised, adding a durable harness-enforced expectation. The single most important actions this week are closing H-04 and H-05 — the calendar time has been available; what is lacking is prioritisation.

---

## 2. Critical findings (act this week)

### H-04 — Bag tag sequence number randomly generated; IATA Resolution 740 uniqueness violated — CRITICAL AGEING (Week 6)

**Severity:** CRITICAL AGEING (week 6 — unchanged for six consecutive reviews; 42 calendar days since last review)
**Principle breached:** Architecture Principals — IATA standards alignment; IATA Resolution 740 requires bag tag licence plate numbers to be globally unique per airline per flight day.

**Evidence (unchanged for sixth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Application/OciCheckIn/OciCheckInHandler.cs:381–382` — `// TODO: In future, this 6-digit sequence number needs to be auto-incremented from a persistent counter rather than generated randomly. var sequence = Random.Shared.Next(0, 1_000_000).ToString("D6");`
- Same `Random.Shared.Next` generator invoked for both OCI self check-in and agent check-in paths. No SQL `SEQUENCE` object, no persistent counter table, no code change in six weeks.

**Impact if unresolved:** Duplicate bag tags at any meaningful check-in volume. Bag misrouting and loss. IATA Resolution 740 non-compliance. Operational safety issue.

**Recommended remediation:** Replace `Random.Shared.Next` with `NEXT VALUE FOR delivery.BagTagSequence` (SQL Server `SEQUENCE` object in the `delivery` schema, scoped per airline numeric prefix). Pass the next sequence value into `GenerateBagTag()` from the repository. The mod-7 check-digit logic is correct and unchanged. Close the TODO comment when done.

**Age:** 6 weeks (6 consecutive review cycles).

---

### H-05 — Offer MS timer trigger calls Schedule MS and Ancillary MS directly — CRITICAL AGEING (Week 5)

**Severity:** CRITICAL AGEING (week 5 — open for five consecutive reviews; escalated to CRITICAL AGEING in 2026-06-01 review)
**Principle breached:** Architecture Principals — "No direct microservice-to-microservice communication. This constraint applies without exception."

**Evidence (unchanged for fifth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/ScheduleServiceClient.cs:11` — self-granted exception comment: `"Note: direct MS-to-MS calls are an accepted exception for timer triggers."` No governance document or ADR grants this exception.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/SeatServiceClient.cs:10` — acknowledged violation: `"TODO: remove this cross-domain call — cabin counts are already stored in offer.FlightInventory.Cabins"`.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Application/RollingInventoryImport/RollingInventoryImportHandler.cs:37,54` — both clients injected and called.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Program.cs:76–91` — `ScheduleMs` and `AncillaryMs` HTTP clients registered.

**Impact if unresolved:** Deployment coupling (Offer MS lifecycle bound to Schedule MS and Ancillary MS contract stability). Obscured distributed traces. No circuit breaker — a Schedule MS or Ancillary MS outage at the nightly timer trigger time silently fails the rolling inventory import.

**Recommended remediation (in preference order):**
1. `SeatServiceClient` — implement the acknowledged TODO: derive cabin counts from existing `offer.FlightInventory.Cabins` rows for the same `AircraftType`. Data is present locally; no cross-service call is required.
2. `ScheduleServiceClient` — raise an ADR to formally grant or deny the exception. If granted, add circuit breaker, retry policy, timeout, and dead-letter alerting as compensating controls. If denied, move the timer trigger to a dedicated `RollingInventoryOrchestration` function in the Operations API.
3. In either case: delete the self-granted exception comment from `ScheduleServiceClient.cs:11`.

**Age:** 5 weeks (5 consecutive review cycles).

---

## 3. Status of prior findings

| Finding | Prior Severity | Status | Evidence note |
|---------|---------------|--------|--------------|
| H-04 — Bag tag sequence randomly generated | CRITICAL AGEING | **UNCHANGED** | `OciCheckInHandler.cs:381–382` — `Random.Shared.Next(0, 1_000_000)` unchanged. Week 6. |
| H-05 — Offer MS calls Schedule MS and Ancillary MS directly | CRITICAL AGEING | **UNCHANGED** | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10` unchanged. Week 5. |
| N-11 — DatabaseMcp tool PII exposure, no audit logging, deployed to Azure | HIGH | **UNCHANGED** | `McpFunction.cs:144–163` — unrestricted SELECT access unchanged; no audit logging added; no schema allowlist; no row cap; `service-urls.md:57` still states "Local only" despite production Azure deployment. |
| C-02r — Debug infrastructure retained in production code | MEDIUM | **UNCHANGED** | `Application/GetOrderDebug/GetOrderDebugHandler.cs` and `GetOrderDebugQuery.cs` still present in Order MS. |
| N-01 — Seatmap booking-window occupancy gap | MEDIUM | **UNCHANGED** | Pre-booking seatmap derives occupancy from manifest only; in-progress `offer.SeatReservation` holds not surfaced. DB constraint preserves integrity. |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `Application/Seat/GetSeatOffers/GetSeatOffersHandler.cs:20` and `GetSeatOffer/GetSeatOfferHandler.cs:20` TODOs unchanged. |
| M-07 / H-03r — No integration tests; dotnet test gate runs 0 tests | MEDIUM | **SUBSTANTIALLY RESOLVED** | Six test projects created (Offer, Order, Schedule, Delivery, Operations, Retail); 5 CI workflows now build and execute real test projects (Delivery, Offer, Order, Schedule, Retail). Residual gap: Operations API workflow (`main_reservation-system-db-api-operations.yml`) still uses old pushd pattern and runs 0 tests despite `ReservationSystem.Tests.Orchestration.Operations` test project existing. See N-15. |
| N-05 — Bag tag response field undocumented | MEDIUM | **UNCHANGED** | `documentation/design/delivery.md:331` still incorrectly attributes bag tag generation to the Airport API; Delivery MS OCI handler generates it. |
| N-10 — OrderData JSON lacks schemaVersion | MEDIUM | **UNCHANGED / AMPLIFIED** | No `schemaVersion` field added to OrderData. The paxId migration (13+ commits this cycle replacing `passengerId` string with `paxId` integer across handlers) and the addition of `isVip` boolean to passenger nodes deepen this gap — two more schema-level evolutions deployed without a version discriminator. |
| M-08 — TierConfig table not mapped with HasTrigger | LOW | **UNCHANGED** | `CustomerDbContext.cs` still has no `TierConfig` DbSet or `HasTrigger("TR_TierConfig_UpdatedAt")`. `TR_TierConfig_UpdatedAt` exists in `Script.sql`. |
| M-10 — TODO without issue reference committed to main | LOW | **UNCHANGED** | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs remain without author or tracking issue reference. |
| N-08 — HandleDelayHandler throws NotImplementedException | LOW | **UNCHANGED** | `HandleDelayHandler.cs:35` still throws `NotImplementedException()`; `api-reference.md:321` documents the endpoint as returning `200 OK`. |
| N-09 — GET /v1/products basketId parameter undocumented | LOW | **UNCHANGED** | `api-reference.md:98` still does not mention the optional `basketId` query parameter or rule evaluation logic. |
| N-13 — Post-departure manage-booking lockout business rule undocumented | LOW | **UNCHANGED** | The departure lockout enforcement added in `CancelOrderHandler`, `ChangeOrderHandler`, `AddOrderBagsHandler`, and `UpdateOrderSeatsHandler` remains absent from `documentation/design/manage-booking.md` and `api-reference.md`. |
| N-14 — DatabaseMcp tool absent from api-reference.md | LOW | **UNCHANGED** | No entry for the DatabaseMcp tool in `api-reference.md`; CLAUDE.md documentation map not updated to reference a spec file. |

Resolved this cycle: none.

---

## 4. High findings

### N-11 — DatabaseMcp tool exposes unrestricted PII read access with no audit logging; documented as local-only but deployed to Azure (Week 2)

**Severity:** HIGH (Week 2 — carried from 2026-06-01 review; no remediation applied)
**Principle breached:** Security Principals — PII must never appear in logs or accessible without audit trail; PCI DSS compliance; absence of query audit logging violates the spirit of "all state-changing operations must produce a structured, immutable audit log entry" extended to PII access paths. Infrastructure Principals — managed identity and Key Vault patterns (connection string provenance unverifiable from code alone).

**Evidence (unchanged):**
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:144–163` — `RunSelectQuery` opens a `SqlConnection(ConnectionString)` and executes caller-supplied SQL verbatim. No schema allowlist, no table blocklist, no row cap, no query audit log.
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:85–101` — `run_select_query` tool accepts an arbitrary SELECT or CTE; a holder of the function key can query `customer.Customer`, `identity.RefreshToken`, `payment.Payment`, `order.Order`, etc.
- `documentation/service-urls.md:57` — still describes the tool as "Local only — Azure Functions host, listens on `http://localhost:7071`". This is factually incorrect; `.github/workflows/main_reservation-system-mcp-db.yml` deploys it to Azure on every push to main.

**Impact if unresolved:** Any holder of the function key has unrestricted bulk read access to all PII across all schemas with no application-layer audit trail.

**Recommended remediation (see 2026-06-01 review section 4 for full details):**
1. Correct `documentation/service-urls.md:57` to reflect Azure deployment.
2. Add query audit logging (query text, row count, timestamp, caller identity).
3. Enforce a schema allowlist (e.g., `offer.*`, `schedule.*` only; block `customer.*`, `identity.*`, `payment.*`, `order.*`).
4. Add a row cap (e.g., TOP 500) to `RunSelectQuery`.
5. Verify the database connection uses a read-only, schema-restricted SQL login.

**Age:** 2 weeks (2 consecutive review cycles).

---

## 5. Medium and low findings

### New findings this cycle

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| N-15 | Operations API CI workflow does not execute the Operations test project | MEDIUM | F | `ReservationSystem.Tests.Orchestration.Operations` test project exists at `src/API/Tests/ReservationSystem.Tests.Orchestration.Operations/` and contains `CheckInHelperTests.cs` with real test methods. However, `main_reservation-system-db-api-operations.yml` uses the old `pushd` pattern — it runs `dotnet test` from within the Operations API project directory, which contains no test classes. The test project is never built or executed by CI. | Platform / Operations API owner |
| N-16 | `isVip` boolean field added to passenger data without API contract or design documentation | LOW | G | Commit `f4a4197` added `isVip?: boolean` to `Passenger` (Angular `order.model.ts`), `OrderPassenger` (`order.service.ts`), and `BasketPassenger` (`new-order.service.ts`). The field is sent in the basket passenger payload from the Terminal app to the Retail API and propagates into `OrderData` JSON. No corresponding update to `api-reference.md` (PUT `/v1/basket/{basketId}/passengers`), `documentation/design/order.md` (OrderData schema), or any API spec. This further amplifies N-10 — another JSON field added to `OrderData` without a `schemaVersion` increment. | Retail API / Docs owner |

### Carried medium and low findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| H-04 | Bag tag sequence randomly generated | CRITICAL AGEING | A | See section 2. Week 6. | Delivery MS owner |
| H-05 | Offer MS calls Schedule MS and Ancillary MS directly | CRITICAL AGEING | A | See section 2. Week 5. | Offer MS owner |
| C-02r | Debug infrastructure retained in production code | MEDIUM | C | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` in Order MS `Application/GetOrderDebug/`. All authenticated; still dead code. | Retail API / Order MS owner |
| N-01 | Seatmap booking-window occupancy gap | MEDIUM | A / B | Pre-booking seatmap shows manifest-only occupancy; in-progress `offer.SeatReservation` holds not surfaced. DB constraint preserves integrity. | Retail API / Offer MS owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A | `GetSeatOffersHandler.cs:20`, `GetSeatOfferHandler.cs:20` TODOs. Business logic belongs in Application layer. | Ancillary MS owner |
| N-05 | Bag tag documentation incorrect | MEDIUM | G | `delivery.md:331` attributes bag tag generation to Airport API; Delivery MS OCI handler generates it. | Delivery / Operations owner |
| N-10 | OrderData JSON lacks schemaVersion | MEDIUM | D | No `schemaVersion` field; paxId migration (13+ commits) and `isVip` addition (N-16) make this gap increasingly concrete and risky for document parsing. | Order MS owner |
| M-08 | TierConfig table not mapped with HasTrigger | LOW | D | `TR_TierConfig_UpdatedAt` in `Script.sql`; `CustomerDbContext` has no mapping. | Customer MS owner |
| M-10 | TODO without issue reference on main | LOW | Coding Standards | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs remain. | All owners |
| N-08 | HandleDelayHandler throws NotImplementedException | LOW | B | `/v1/disruptions/delay` documented as returning `200 OK`; implementation throws. | Operations API owner |
| N-09 | GET /v1/products basketId parameter undocumented | LOW | G | `api-reference.md:98` still does not mention optional `basketId` or rule evaluation. | Retail API / Docs owner |
| N-13 | Post-departure manage-booking lockout undocumented | LOW | G | Departure lockout business rule enforced in code but absent from `manage-booking.md` and `api-reference.md`. | Retail API / Docs owner |
| N-14 | DatabaseMcp tool absent from api-reference.md | LOW | G | No entry in `api-reference.md`; no spec file; CLAUDE.md documentation map not updated. | Platform Architect |
| N-15 | Operations API CI runs 0 tests despite having a test project | MEDIUM | F | `main_reservation-system-db-api-operations.yml` uses pushd pattern; `CheckInHelperTests.cs` never executed in CI. | Platform / Operations API owner |
| N-16 | isVip field added to passenger data without documentation | LOW | G | `isVip?: boolean` in Terminal/Web passenger models propagates to OrderData JSON; not documented in api-reference.md or order.md. | Retail API / Docs owner |

---

## 6. Observations and positive notes

- **Test coverage — the biggest improvement in this project's review history.** Commit `a4bae21` created four unit test projects in a single PR (Offer, Order, Schedule, Retail), joined by a Delivery test project wired into CI (`22f76d3`). The repository went from zero executing tests in any CI pipeline (the worst finding in the 2026-06-01 review) to five services with deployment-gated, real test execution. The test files are substantive: `WriteManifestHandlerTests.cs`, `SearchOffersHandlerTests.cs`, `UpdateOrderCheckInHandlerTests.cs`, `UpdateOrderBagsHandlerTests.cs`, `GetIropsOrdersHandlerTests.cs`, `UpdateOrderPassengersHandlerTests.cs`, `ImportSchedulesHandlerTests.cs`, `CheckInHelperTests.cs`, `DeliveryServiceClientManifestTests.cs`, and `SearchFlightsHandlerTests.cs` all exercise real application handlers with mocked dependencies. This is the single most valuable change across any week in this review series.

- **CLAUDE.md test maintenance obligation now formalised.** Commit `a5f734a` (docs: require test validation and update on every code change) and the associated `b437508` (CLAUDE.md update) add a durable, harness-read requirement that tests must be searched, fixed if broken, and extended if new behaviour is introduced — before any commit to application code. This obligates every future agent-authored PR to treat tests as a first-class deliverable, not an afterthought.

- **Comprehensive CI test wiring.** The Delivery, Offer, Order, Schedule, and Retail CI workflows (`main_reservation-system-db-microservice-delivery.yml`, `main_reservation-system-db-microservice-offer.yml`, etc.) all now correctly build the test project as a separate step and run `dotnet test` against the test project path — not from the application project directory. The `paths` triggers in these workflows also include the test project directory, so changes to test files correctly trigger CI runs.

- **paxId migration drives internal consistency.** The 13-commit paxId migration series (PRs #1308 through #1321) completes the transition from the legacy `passengerId` string format (e.g., "PAX-1") to the canonical integer `paxId` (e.g., 1) as the primary passenger identifier across Order, Delivery, and Operations handlers. The migration includes fallback handling for legacy documents. Tests in `CheckInHelperTests.cs` and `UpdateOrderCheckInHandlerTests.cs` exercise both old and new document shapes, which is the correct pattern for a schema-evolution without `schemaVersion`.

- **Dynamic pricing staircase chart added to stock keeper.** Commit `98606fe` adds a visual pricing chart to the Terminal stock keeper flight detail screen. This is a product improvement with no governance concern.

---

## 7. Conformance scorecard

| Dimension | Conformance | Trend vs 2026-06-01 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🔴 Red | → | H-04 CRITICAL AGEING week 6 unchanged; H-05 CRITICAL AGEING week 5 unchanged; N-01 seatmap gap unchanged |
| B — API Contract Conformance | 🟡 Amber | → | N-08 unchanged; N-09 unchanged; N-13 unchanged; no new contract regressions in new features |
| C — Security Principles | 🔴 Red | → | N-11 HIGH unchanged (DatabaseMcp PII exposure, no audit logging, incorrect docs); C-02r unchanged |
| D — Data Storage & Schema Principles | 🟡 Amber | ↓ | M-08 unchanged; N-10 amplified by paxId migration and isVip addition — two more JSON schema evolutions without schemaVersion |
| E — Infrastructure & Integration Principles | 🟡 Amber | → | H-04 CRITICAL AGEING; H-05 CRITICAL AGEING; CVE gate remains active |
| F — Testing & CI | 🟡 Amber | ↑↑ | Major improvement: 5 services now have CI-gated real tests; CLAUDE.md obligation added; Operations API test project not wired (N-15) is the only material remaining gap |
| G — Documentation Drift | 🟡 Amber | ↓ | N-05, N-09, N-13, N-14 unchanged; N-16 new (isVip); paxId migration not documented in order.md |

---

## 8. Governance gaps

All prior gaps remain open. No new governance gaps identified this cycle.

1. **ADR register has only one entry; several decisions remain undocumented.** ADR-001 (payment gateway deferral) is the only record. Decisions warranting ADR capture: (a) H-05 — ScheduleServiceClient MS-to-MS exception, either granted or denied; (b) manifest-only seatmap occupancy as accepted design (N-01, open five cycles); (c) shared `Microservice:HostKey` authentication pattern; (d) DatabaseMcp tool as an approved developer PII access mechanism (or explicit denial with schema restrictions).

2. **No OpenAPI specs in repository.** Integration principles require machine-readable OpenAPI 3.x specs version-controlled alongside service code. The paxId migration, `isVip` addition, and post-departure lockout are all undiscoverable from the API contract alone.

3. **No consumer-driven contract tests.** Pact or equivalent between orchestration APIs and microservices remains absent. The 80 commits since the last review include significant OrderData schema changes (paxId, isVip) with no contract test gate to catch consumer breaks.

4. **No incident response plan discoverable.** Security principles require a documented IR plan with a UK GDPR 72-hour breach notification procedure. N-11 (DatabaseMcp unrestricted PII read) makes this gap more urgent: no application-layer audit trail exists to assess data access scope if the function key were compromised.

5. **Accounting MS not assessable.** Event-subscription stubs only; no business logic to inspect.

6. **Airport API and Finance API scaffolded only.** Not assessed.

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
| `documentation/design-review/design-review-2026-06-01.md` | Most recent prior review |
| `documentation/design-review/design-review-2026-05-25.md` | Second prior review |
| `documentation/design-review/design-review-2026-05-21.md` | Third prior review |
| `documentation/service-urls.md` | Service registry accuracy check (N-11) |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| Delivery MS — OciCheckIn | `OciCheckInHandler.cs:381–382` — bag tag `Random.Shared.Next` confirmed unchanged (H-04, week 6) |
| Offer MS | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10`, `RollingInventoryImportHandler.cs`, `Program.cs:76–91` — H-05 confirmed unchanged (week 5) |
| DatabaseMcp tool | `Functions/McpFunction.cs:80–163` — `run_select_query` confirmed still unrestricted; `service-urls.md:57` still states "Local only" |
| Order MS — Debug | `Application/GetOrderDebug/GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` — C-02r confirmed still present |
| Customer MS — DbContext | `CustomerDbContext.cs` — M-08 TierConfig mapping confirmed absent |
| Operations API — HandleDelay | `HandleDelayHandler.cs:35` — N-08 `NotImplementedException` confirmed unchanged |
| Ancillary MS | `Application/Seat/GetSeatOffers/GetSeatOffersHandler.cs:20`, `GetSeatOffer/GetSeatOfferHandler.cs:20` — M-03 TODOs confirmed unchanged |
| GitHub Actions workflows | All 20 workflows audited; Delivery, Offer, Order, Schedule, Retail workflows confirmed running real test projects; Operations API and Loyalty API confirmed using old `pushd` pattern (0 tests); MCP tool workflow unchanged |
| Test projects | All 6 test project directories confirmed present; 11 test class files enumerated |
| VIP flag | `f4a4197` diff reviewed — Angular-only changes (`order.model.ts`, `order.service.ts`, `new-order.service.ts`, Terminal templates); no C# backend changes; no documentation updates (N-16) |
| paxId migration | Commits #1308–#1323 reviewed at summary level; `CheckInHelperTests.cs` read to confirm test coverage of new paxId path |
| CLAUDE.md | `a5f734a` and `b437508` reviewed — test maintenance obligation now formalised |

### Commit reference

Review conducted against commit `3cde845` (tip of `main` as of 2026-07-13).
80 commits merged since prior review (`68cd181`, 2026-06-01).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS | Event-subscription stubs only; no inspectable business logic |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass; VIP flag reviewed for documentation drift only |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap from prior reviews |
| Penetration testing / runtime security | Requires live environment access |
| Per-service API specs (`documentation/api-specs/`) | Not inspected this cycle; no service-spec changes noted in commit log |
| paxId migration correctness | Individual handler changes reviewed at commit-log level; full correctness audit deferred |
