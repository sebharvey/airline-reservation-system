# Apex Air — Weekly Design Review

**Date:** 2026-06-08
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-06-01.md

---

## 1. Executive summary

The platform posture is **Non-conforming** for the second consecutive cycle, though with one meaningful improvement. H-04 (bag tag random sequence) enters its sixth week and H-05 (Offer MS direct microservice calls) its fifth — both remain CRITICAL AGEING with no change in evidence. The most significant positive development this cycle is the addition of four unit test projects (Offer, Order, Schedule, Retail) with real test methods, each wired explicitly into their corresponding deploy workflow — this reverses the test regression from the prior cycle and lifts the Testing dimension from Red to Amber. The HIGH finding N-11 (DatabaseMcp unrestricted PII read access deployed to Azure, documented as local-only) remains fully unaddressed. A new LOW finding is raised: the `isVip` flag was added to `OrderData` JSON in `UpdateOrderPassengersHandler` this cycle without any documentation update in `order.md` or `api-reference.md`, continuing the pattern that produced N-10. The single most important action this week is still resolving H-04 and H-05 — both are operationally dangerous and have been open long enough to constitute a governance failure.

---

## 2. Critical findings (act this week)

### H-04 — Bag tag sequence number randomly generated; IATA Resolution 740 uniqueness violated — CRITICAL AGEING (Week 6)

**Severity:** CRITICAL AGEING (week 6 — unchanged for six consecutive reviews)
**Principle breached:** Architecture Principals — IATA standards alignment; IATA Resolution 740 requires bag tag licence plate numbers to be globally unique per airline per flight day.

**Evidence (unchanged for sixth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Application/OciCheckIn/OciCheckInHandler.cs:381–382` — `// TODO: In future, this 6-digit sequence number needs to be auto-incremented from a persistent counter rather than generated randomly. var sequence = Random.Shared.Next(0, 1_000_000).ToString("D6");`
- Both OCI self check-in and agent check-in paths share the same `Random.Shared.Next` generator. No SQL `SEQUENCE` object, no persistent counter table has been added in six review cycles.

**Impact if unresolved:** Duplicate bag tags at any production check-in volume. Bag misrouting and loss. IATA Resolution 740 non-compliance. Operational safety issue.

**Recommended remediation:** Replace `Random.Shared.Next` with `NEXT VALUE FOR delivery.BagTagSequence` (SQL Server `SEQUENCE` object in the `delivery` schema, scoped per airline numeric prefix). Pass the next sequence value into `GenerateBagTag()` from the repository. The mod-7 check-digit logic is correct and unchanged. Close the TODO comment when done.

**Age:** 6 weeks. The coding standards introduced this cycle (CLAUDE.md rule 13: tests must be updated with every code change) should also be applied to this remediation — a repository-layer unit test covering the sequence generation path must accompany the fix.

---

### H-05 — Offer MS timer trigger calls Schedule MS and Ancillary MS directly — CRITICAL AGEING (Week 5)

**Severity:** CRITICAL AGEING (week 5 — escalated to CRITICAL AGEING in prior cycle; unchanged for five consecutive reviews)
**Principle breached:** Architecture Principals — "No direct microservice-to-microservice communication. This constraint applies without exception."

**Evidence (unchanged for fifth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/ScheduleServiceClient.cs:11` — self-granted exception comment: `"Note: direct MS-to-MS calls are an accepted exception for timer triggers."` No governance document or ADR grants this exception.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/SeatServiceClient.cs:10` — acknowledged violation: `"TODO: remove this cross-domain call — cabin counts are already stored in offer.FlightInventory.Cabins"`.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Application/RollingInventoryImport/RollingInventoryImportHandler.cs:37,54` — both clients injected and called.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Program.cs:77–91` — `ScheduleMs` and `AncillaryMs` HTTP clients registered.

**Impact if unresolved:** Deployment coupling (Offer MS lifecycle bound to Schedule MS and Ancillary MS contract stability). Obscured distributed traces. No circuit breaker — a Schedule MS or Ancillary MS outage at 01:00 UTC silently fails the nightly rolling inventory import.

**Recommended remediation (in preference order):**
1. `SeatServiceClient` — implement the acknowledged TODO: derive cabin counts from existing `offer.FlightInventory.Cabins` rows for the same `AircraftType`. The data is already present locally; no cross-service call is required.
2. `ScheduleServiceClient` — raise an ADR to formally grant or deny the exception. If granted, add circuit breaker, retry policy, timeout, and dead-letter alerting as compensating controls. If denied, move the timer trigger to a dedicated `RollingInventoryOrchestration` function in the Operations API.
3. In either case: delete the self-granted exception comment from `ScheduleServiceClient.cs:11`.

**Age:** 5 weeks.

---

## 3. Status of prior findings

| Finding | Prior Severity | Status | Evidence note |
|---------|---------------|--------|--------------|
| H-04 — Bag tag sequence randomly generated | CRITICAL AGEING | **UNCHANGED** | `OciCheckInHandler.cs:381–382` — `Random.Shared.Next(0, 1_000_000)` unchanged. Week 6. |
| H-05 — Offer MS calls Schedule MS and Ancillary MS directly | CRITICAL AGEING | **UNCHANGED** | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10`, `RollingInventoryImportHandler.cs:37,54`, `Program.cs:77–91` all unchanged. Week 5. |
| N-11 — DatabaseMcp unrestricted PII access, deployed to Azure, documented as local-only | HIGH | **UNCHANGED** | `McpFunction.cs:144–163` — no schema allowlist, no row cap, no audit logging added. `service-urls.md:57` still states "Local only." No api-spec document created. |
| M-07 — No integration tests for orchestration APIs | MEDIUM | **PARTIALLY RESOLVED** | 4 unit test projects (Offer, Order, Schedule, Retail) added with real test methods; Retail, Offer, Order, Schedule deploy workflows now explicitly build and execute the corresponding test project. 13 remaining workflows still run `dotnet test --no-build` without a project path — vacuous gate unchanged for those services. |
| H-03r — `dotnet test` gate executes zero tests | MEDIUM | **PARTIALLY RESOLVED** | Same evidence as M-07 — 4 workflows now execute real tests; 13 remain vacuous. |
| C-02r — Debug infrastructure retained in production code | MEDIUM | **UNCHANGED** | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` in `Order.Application.GetOrderDebug/` still present; `DeliveryServiceClient.GetTicketsDebugRawAsync`, `DeliveryServiceClient.GetDocumentsDebugRawAsync`, `OrderServiceClient.GetOrderDebugRawAsync` still present; three `AdminDebug*` functions in `AdminOrderManagementFunction.cs:372,396,419` still present. |
| N-01 — Seatmap booking-window occupancy gap | MEDIUM | **UNCHANGED** | Pre-booking seatmap derives occupancy from manifest only; in-progress `offer.SeatReservation` holds not surfaced. DB constraint preserves integrity. |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `GetSeatOffersHandler.cs:20` — `// TODO: Implement seat offer generation using seatmap + pricing + offer logic in Function layer` unchanged. |
| N-05 — Bag tag response field documented incorrectly | MEDIUM | **UNCHANGED** | `documentation/design/delivery.md:331` — still states "bagTagNumber is null at purchase and populated by the Airport API when the bag is checked in." The Delivery MS OCI handler generates the tag; the Airport API is not involved. |
| N-10 — OrderData JSON lacks schemaVersion | MEDIUM | **UNCHANGED** | No `schemaVersion` field added to `OrderData` schema or documentation. The `isVip` addition this cycle (see N-15 below) adds further undocumented schema drift to an already unversioned document. |
| M-08 — TierConfig table not mapped with HasTrigger | LOW | **UNCHANGED** | `CustomerDbContext.cs` — no `TierConfig` DbSet or `HasTrigger("TR_TierConfig_UpdatedAt")` declaration. `TR_TierConfig_UpdatedAt` exists in `Script.sql`. |
| M-10 — TODO without issue reference committed to main | LOW | **UNCHANGED** | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs remain without author or tracking issue reference, in violation of coding standards. |
| N-08 — HandleDelayHandler throws NotImplementedException | LOW | **UNCHANGED** | `HandleDelayHandler.cs:35` — still throws `NotImplementedException()`. `api-reference.md:321` documents `/v1/disruptions/delay` as returning `200 OK`. |
| N-09 — GET /v1/products basketId parameter undocumented | LOW | **UNCHANGED** | `api-reference.md:98` — still no mention of `basketId` query parameter or rule evaluation logic. |
| N-13 — Post-departure manage-booking lockout rule undocumented | LOW | **UNCHANGED** | Departure lockout enforced in `CancelOrderHandler`, `ChangeOrderHandler`, `AddOrderBagsHandler`, `UpdateOrderSeatsHandler` but absent from `documentation/design/manage-booking.md` and `api-reference.md` endpoint descriptions. |
| N-14 — DatabaseMcp absent from api-reference.md | LOW | **UNCHANGED** | No entry in `api-reference.md`; no api-spec document in `documentation/api-specs/`; `CLAUDE.md` documentation map not updated. |

---

## 4. High findings

### N-11 — DatabaseMcp tool exposes unrestricted PII read access with no audit logging; documented as local-only but deployed to Azure — Week 2

**Severity:** HIGH (week 2 — unchanged)
**Principle breached:** Security Principals — PII must not be accessible without audit trail; "All state-changing operations must produce a structured, immutable audit log entry" (extended in spirit to PII data access paths). Infrastructure Principals — "service-urls.md" must accurately describe deployed services. Data Storage Principals — schema ownership and access control patterns.

**Evidence (unchanged from 2026-06-01):**
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:85–101` — `run_select_query` tool accepts arbitrary SQL SELECT or CTE against any schema with no table blocklist, no schema allowlist, no row cap, and no query audit log.
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:144–163` — `RunSelectQuery` opens a `SqlConnection(ConnectionString)` and executes caller-supplied SQL verbatim. A holder of the function key can query `customer.*`, `identity.*`, `payment.*`, and `order.*` without restriction.
- `documentation/service-urls.md:57` — still states "Local only — Azure Functions host, listens on `http://localhost:7071`." The tool is deployed to production Azure slot `reservation-system-mcp-db` via `.github/workflows/main_reservation-system-mcp-db.yml` on every push to main.

**Impact if unresolved:** Unrestricted bulk PII extraction (passenger names, dates of birth, passport numbers, contact details, payment records) by any holder of the function key, with no application-layer audit trail. The gap between documentation (local-only) and reality (deployed to Azure) means operators cannot accurately assess the attack surface.

**Recommended remediation (same as prior cycle):**
1. Correct `documentation/service-urls.md:57` to reflect actual Azure deployment.
2. Add structured query audit logging to every `run_select_query` call (query text, row count returned, timestamp).
3. Enforce a schema allowlist — permit queries only against `offer.*` and `schedule.*`; block `customer.*`, `identity.*`, `payment.*`, `order.*`. Document any broader access as an approved exception in `security-principals.md`.
4. Add a row cap (`TOP 500` or equivalent) to prevent bulk extraction in a single call.
5. Verify the database connection string connects as a read-only, schema-restricted SQL login.
6. Create `documentation/api-specs/database-mcp.md` and add to `api-reference.md` and `CLAUDE.md` documentation map.

**Age:** 2 weeks.

---

## 5. Medium and low findings

### New finding this cycle

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| N-15 | `isVip` flag added to OrderData without documentation | LOW | G — Documentation | Commit `f3a3600` adds `isVip` field handling to `UpdateOrderPassengersHandler.cs:83–86` — the field is patched into the `OrderData` JSON document for individual passengers. The field is not documented in `documentation/design/order.md` (which defines the `OrderData` schema and passenger object shape) and is not mentioned in `api-reference.md` for `PATCH /v1/orders/{bookingRef}/passengers` or the admin equivalent. This continues the undocumented schema evolution pattern that produced N-10 (`paxId`). Until `OrderData` has a `schemaVersion` (N-10), documenting the current field set in `order.md` is the minimum mitigation. | Order MS / Retail API owner |

### Partially resolved — testing (carried from prior cycle)

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| M-07 | Integration tests for orchestration APIs — partial coverage only | MEDIUM | F | 4 unit test projects added (Offer, Order, Schedule, Retail); each is wired into its deploy workflow and actually executes tests. However 13 deploy workflows — Admin API, Loyalty API, Operations API, Customer MS, Delivery MS, Identity MS, Payment MS, User MS, MCP tool, Ancillary MS, Exceptions MS, both Simulators — still run `dotnet test --no-build` without a project path and execute zero tests. These vacuous gates protect nothing for those 13 services. | QA / Platform |
| H-03r | `dotnet test` gate vacuous in 13 workflows | MEDIUM | F | Same as M-07 — structural gate present but no test classes found for 13 services. The 4 correctly-wired workflows are the model to follow. | QA / Platform |

### Carried medium and low findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| C-02r | Debug infrastructure retained in production code | MEDIUM | C | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs`, three `AdminDebug*` Retail API functions, debug client methods. All authenticated; still dead code. | Retail API / Order MS owner |
| N-01 | Seatmap booking-window occupancy gap | MEDIUM | A / B | Pre-booking seatmap shows manifest-only occupancy; in-progress `offer.SeatReservation` holds not surfaced. DB constraint preserves integrity. | Retail API / Offer MS owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A | `GetSeatOffersHandler.cs:20` TODO. Business logic belongs in Application layer. | Ancillary MS owner |
| N-05 | Bag tag documentation incorrect | MEDIUM | G | `delivery.md:331` attributes bag tag generation to Airport API; Delivery MS OCI handler generates it. | Delivery / Operations owner |
| N-10 | `OrderData` JSON lacks `schemaVersion` | MEDIUM | D | No `schemaVersion` field. N-15 (isVip) worsens the unversioned schema gap. | Order MS owner |
| M-08 | `TierConfig` table not mapped with `HasTrigger` | LOW | D | `TR_TierConfig_UpdatedAt` in `Script.sql`; `CustomerDbContext` has no mapping. | Customer MS owner |
| M-10 | TODO without issue reference on main | LOW | Coding Standards | `OciCheckInHandler.cs:381`, `SeatServiceClient.cs:10` bare TODOs without author or tracking issue. | All owners |
| N-08 | `HandleDelayHandler` throws `NotImplementedException` | LOW | B | `/v1/disruptions/delay` documented as operational; implementation throws `NotImplementedException`. | Operations API owner |
| N-09 | `GET /v1/products` `basketId` parameter undocumented | LOW | G | `api-reference.md:98` still missing `basketId` query parameter and rule evaluation description. | Retail API / Docs owner |
| N-13 | Post-departure manage-booking lockout undocumented | LOW | G | Rule enforced in handlers but absent from `manage-booking.md` and `api-reference.md`. | Retail API / Docs owner |
| N-14 | DatabaseMcp absent from `api-reference.md` | LOW | G | No api-reference entry, no api-spec, `CLAUDE.md` doc map not updated. | Platform Architect |
| N-15 | `isVip` flag undocumented in OrderData | LOW | G | `isVip` patched into `OrderData` passenger objects in `UpdateOrderPassengersHandler.cs:83–86`; not in `order.md` or `api-reference.md`. | Order MS owner |

---

## 6. Observations and positive notes

- **Unit test projects added for four services — a genuine reversal of the prior cycle's regression.** Commit `a4baa21` introduces `ReservationSystem.Tests.Microservices.Offer`, `ReservationSystem.Tests.Microservices.Order`, `ReservationSystem.Tests.Microservices.Schedule`, and `ReservationSystem.Tests.Orchestration.Retail`. Each project contains real xUnit test classes with Arrange/Act/Assert structure and mocked dependencies. The corresponding deploy workflows for Offer, Order, Schedule, and Retail were updated to explicitly build and run these test projects before publishing — the correct wiring pattern. The subsequent bug-fix commits (`a8e4e6e`, `c19fc04`, `5462660`, `475750c`) demonstrate that the test gate is live and real: compilation errors in test projects blocked the workflow until fixed.

- **Test maintenance obligation added to CLAUDE.md.** Commit `a5f734a` adds rule 13 to CLAUDE.md: "Tests must be validated and updated with every code change. Before committing any change to application code, search `src/API/Tests/` for tests that exercise the modified code path. If existing tests would be broken by the change, fix them in the same commit. If the change introduces new behaviour, add tests for it. Never leave tests in a failing state." This is an important governance enhancement that makes the test expectation explicit to all contributors including automated agents.

- **NDC passengerId normalisation corrected.** Commits `1bd12b6`, `a27ec8a`, `1c773e6` fix an NDC interoperability gap where `passengerId` was stored as an integer in the basket but expected as a `PAX-n` string in order confirmation. The fix normalises the format at the source (basket) and handles the conversion at the Order MS. NDC channel conformance and internal consistency both improved.

- **Dynamic pricing staircase chart added to Stock Keeper.** Commit `98606fe` adds a staircase price chart to the Terminal flight detail screen, visualising price thresholds over time. No governance concerns; meaningful operational tooling for revenue managers.

- **VIP flag fix correctly applied at both API and application layers.** Commit `f3a3600` fixes `isVip` not being persisted in `UpdateOrderPassengersHandler.cs`. The fix correctly applies the JSON patch within the handler — no cross-domain concern. The only gap is documentation (recorded as N-15).

---

## 7. Conformance scorecard

| Dimension | Conformance | Trend vs 2026-06-01 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🔴 Red | → | H-04 CRITICAL AGEING week 6 unchanged; H-05 CRITICAL AGEING week 5 unchanged; N-01 seatmap gap unchanged |
| B — API Contract Conformance | 🟡 Amber | → | N-08 unchanged; N-09 unchanged; N-13 unchanged; no new contract regressions in new features |
| C — Security Principles | 🔴 Red | → | N-11 HIGH unchanged — DatabaseMcp unrestricted PII access, deployed to Azure, documented as local-only; C-02r unchanged |
| D — Data Storage & Schema Principles | 🟡 Amber | → | M-08 unchanged; N-10 unchanged; isVip addition (N-15) adds further unversioned schema evolution |
| E — Infrastructure & Integration Principles | 🟡 Amber | → | H-04 CRITICAL AGEING; H-05 CRITICAL AGEING; no new infra regressions |
| F — Testing & CI | 🟡 Amber | ↑ | 4 services now have real unit tests in CI (Offer, Order, Schedule, Retail); CLAUDE.md rule 13 formalises test obligation; 13 deploy workflows still vacuous; no integration test coverage for any service |
| G — Documentation Drift | 🟡 Amber | → | N-05, N-09, N-13, N-14 all unchanged; N-15 new (isVip undocumented); atomic code-and-docs pattern applied to other changes this cycle |

---

## 8. Governance gaps

The following gaps remain open. Priority noted where escalated.

1. **ADR register remains at one entry; further ADRs overdue.** ADR-001 (payment gateway deferral) remains the only ADR. Decisions pending formalisation: (a) the `ScheduleServiceClient` self-granted MS-to-MS exception (H-05 — now at 5 weeks, the absence of an ADR is itself part of the finding); (b) manifest-only seatmap occupancy as an accepted design (N-01 — 6 cycles open); (c) DatabaseMcp as an approved developer data-access mechanism with explicit scope and controls, or explicit denial (N-11). Without ADRs these patterns cannot be correctly applied or challenged by future contributors.

2. **No OpenAPI specs in repository.** Integration principles require machine-readable OpenAPI 3.x specs version-controlled alongside service code. The `isVip` field addition and the `basketId` parameter on `GET /v1/products` would both be caught by CI-based contract tests if specs existed. 43 commits this cycle with no contract-test gate.

3. **No consumer-driven contract tests.** Pact or equivalent absent. The 4 new unit test projects are a start; they test handler logic in isolation but do not verify the API contracts exposed to consumers.

4. **No incident response plan discoverable.** Security principals require a documented IR plan with a UK GDPR 72-hour breach notification procedure. This gap is heightened by N-11: if the DatabaseMcp function key were compromised, there is no application-layer audit trail to assess what data had been accessed, and no documented containment procedure.

5. **Accounting MS not assessable.** Event-subscription stubs only; no business logic to inspect.

6. **Airport API and Finance API scaffolded only.** Not assessed. The `delivery.md:331` incorrect attribution of bag tag generation to the Airport API (N-05) remains related to this gap.

7. **No IaC in repository.** Infrastructure principals require all Azure resources defined as Bicep or Terraform, version-controlled and applied via CI/CD. No IaC files found in any prior review. Manual portal provisioning cannot be ruled out for production resources.

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
| `documentation/service-urls.md` | Service registry accuracy check (N-11) |
| `documentation/design-review/design-review-2026-06-01.md` | Most recent prior review |
| `documentation/design-review/design-review-2026-05-25.md` | Second prior review |
| `documentation/design-review/design-review-2026-05-21.md` | Third prior review |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| Delivery MS — OciCheckIn | `OciCheckInHandler.cs:378–386` — bag tag `Random.Shared.Next` confirmed unchanged (H-04, week 6) |
| Offer MS | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10`, `RollingInventoryImportHandler.cs:37,54`, `Program.cs:77–91` — H-05 confirmed unchanged (week 5) |
| DatabaseMcp tool | `Functions/McpFunction.cs` — full read; no schema allowlist, no audit logging, no row cap — N-11 unchanged |
| `documentation/service-urls.md:57` | Still "Local only" — N-11 deployment documentation inaccuracy unchanged |
| Order MS — Debug | `Application/GetOrderDebug/GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` — files confirmed still present (C-02r) |
| Retail API — Admin debug | `AdminOrderManagementFunction.cs:372,396,419`, `OrderServiceClient.GetOrderDebugRawAsync`, `DeliveryServiceClient.GetTicketsDebugRawAsync`, `DeliveryServiceClient.GetDocumentsDebugRawAsync` — C-02r confirmed unchanged |
| Ancillary MS | `GetSeatOffersHandler.cs:20` — M-03 TODO confirmed unchanged |
| Customer MS | `CustomerDbContext.cs` — M-08 TierConfig mapping confirmed absent |
| Operations API | `HandleDelayHandler.cs:35` — N-08 `NotImplementedException` confirmed unchanged |
| `documentation/design/delivery.md:331` | N-05 bag tag attribution confirmed unchanged |
| Order MS — UpdateOrderPassengers | `UpdateOrderPassengersHandler.cs:83–86` — `isVip` field added this cycle; not in `order.md` or `api-reference.md` (N-15) |
| GitHub Actions workflows | All 20 workflows audited; 4 (Retail, Offer, Order, Schedule) now explicitly build and run test projects; 13 remaining still run `dotnet test --no-build` without a project path (vacuous) |
| Test projects | `ReservationSystem.Tests.Orchestration.Retail`, `ReservationSystem.Tests.Microservices.Offer`, `ReservationSystem.Tests.Microservices.Order`, `ReservationSystem.Tests.Microservices.Schedule` — all read; real test methods confirmed present |
| CLAUDE.md | Rule 13 (test maintenance obligation) confirmed added in commit `a5f734a` |

### Commit reference

Review conducted against commit `7a5d02b` (tip of `main` as of 2026-06-08).
43 commits merged since prior review (`68cd181`, 2026-06-01).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS | Event-subscription stubs only; no inspectable business logic |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass; NDC passengerId fix reviewed for correctness via commit diff |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap from all prior reviews |
| Penetration testing / runtime security | Requires live environment access |
| Per-service API specs (`documentation/api-specs/`) | Selective spot-check only; no service-spec changes noted in this cycle's commits |
| `documentation/design/order.md` | Spot-checked for `isVip` and `schemaVersion` only; full review deferred |
