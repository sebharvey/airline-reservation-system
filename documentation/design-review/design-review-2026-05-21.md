# Apex Air — Weekly Design Review

**Date:** 2026-05-21
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-05-18.md

---

## 1. Executive summary

The platform posture is **Conforming with concerns**, a genuine improvement on four consecutive Non-conforming reviews. This cycle delivered 47 commits that substantially addressed the longest-standing critical finding: all seven previously-identified debug endpoints now require authentication (host key for microservices; staff JWT via `TerminalAuthenticationMiddleware` for Admin-prefixed orchestration functions). An important correction is also recorded — prior reviews overstated the severity of C-02 by characterising the three `AdminDebug*` Retail API functions as unauthenticated; they were protected by `TerminalAuthenticationMiddleware`'s function-name prefix check all along. One critical finding remains: H-03 (deploy pipelines with no test gates) enters its fifth consecutive review as CRITICAL AGEING — a CI/CD gate has not been added despite four explicit recommendations. The single most important action this week is adding `dotnet test` before every deploy step.

---

## 2. Critical findings (act this week)

### H-03 — Deploy pipelines build without running tests — CRITICAL AGEING (Week 5)

**Severity:** CRITICAL AGEING (Week 5 — unchanged since inaugural review)
**Principle breached:** Infrastructure Principals — "Unit test failures, integration test failures, critical/high CVE findings, and SAST alerts all block promotion; no build promoted without passing all gates."

**Evidence (unchanged for fifth consecutive review):**
- `.github/workflows/main_reservation-system-db-api-retail.yml:39` — `dotnet build --configuration Release --output ./output` with no preceding `dotnet test` step.
- Same pattern confirmed across all 20 deploy workflows; the two integration test workflows (`integration-tests-customer-microservice.yml`, `integration-tests-identity-microservice.yml`) remain independent, non-gating triggers.
- Commit `79542aa` in this cycle removed the placeholder test project (`src/API/Tests/`) — that project held no test classes and its removal is not a regression, but the absence of tests in the deploy gate is unchanged.

**Impact if unresolved:** Any commit that breaks compilation is caught; any commit that introduces a runtime regression, logic error, or API contract break goes to production with no automated check. With 47 commits this cycle the exposure is material.

**Recommended remediation:**
1. Add `dotnet test --no-build` immediately after `dotnet build` in every deploy workflow. One change, one PR, closes this finding for all 20 workflows.
2. Add `dotnet list package --vulnerable --include-transitive` with failure on critical/high CVEs.
3. Introduce staging slot swap before production promotion.

**Age:** 5 weeks.

---

## 3. Status of prior findings

| Finding | Prior Severity | Status | Evidence note |
|---------|---------------|--------|--------------|
| C-02 — Anonymous debug endpoints expose PII | CRITICAL AGEING | **SUBSTANTIALLY RESOLVED** (downgrade to MEDIUM for cleanup) | All 7 endpoints now require authentication. Microservice debug endpoints secured with `AuthorizationLevel.Function` (host key) per PR #1253/#1256. AdminDebug* functions in Retail API were protected by `TerminalAuthenticationMiddleware` (function-name prefix check at `TerminalAuthenticationMiddleware.cs:55`) all along — prior reviews misstated these as unauthenticated. Remaining concern: debug handler/query classes and service client debug methods still exist in production code; see section 5. |
| C-03 — Payment gateway not integrated | CRITICAL AGEING | **RESOLVED (ADR-001)** | ADR-001 formally accepts gateway deferral for the demo platform. Payment MS TODO stubs retained as integration markers per ADR. Finding closed permanently. |
| H-03 — Deploy pipelines lack test and CVE gates | CRITICAL AGEING | **UNCHANGED** | No `dotnet test` in any of 20 deploy workflows; see section 2. |
| H-04 — Bag tag sequence number randomly generated | HIGH | **UNCHANGED** | `OciCheckInHandler.cs:382` — `Random.Shared.Next(0, 1_000_000)` unchanged. Week 3. |
| H-05 — Offer MS directly calls Schedule MS and Ancillary MS | HIGH | **UNCHANGED** | `ScheduleServiceClient.cs:11` self-granted exception comment unchanged; `SeatServiceClient.cs:10` TODO unchanged. Offer MS Program.cs:76–91 still registers `ScheduleMs` and `AncillaryMs` HTTP clients. Both now send the shared `Microservice:HostKey` (PR #1255 — improvement in auth, not in architecture). Week 2. |
| M-01 — Exceptions MS not documented | MEDIUM | **RESOLVED** | `documentation/api-reference.md:647` — full Exceptions Microservice section with endpoint, response schema, and KQL query description. `system-overview.md` updated. |
| N-02 — Admin SSR route and documentation mismatch | MEDIUM | **RESOLVED** | `documentation/api-reference.md:89–92` — correct routes (`/v1/admin/ssr`, `/v1/admin/ssr/{ssrCode}`) with all four HTTP verbs documented. Commit `471d9a0`. |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` TODOs unchanged. |
| M-07 — No integration tests for orchestration APIs | MEDIUM | **UNCHANGED** | Placeholder test project removed (`79542aa`) — it had no test classes. No integration tests for Retail, Loyalty, Admin, or Operations APIs exist. |
| N-01 — Seatmap booking-window occupancy gap | MEDIUM | **PARTIALLY RESOLVED** | Documentation updated (`d3bc0a0`) — offer.md and api-reference.md now accurately describe manifest-derived occupancy. Implementation gap remains: `offer.SeatReservation` holds are not surfaced to the seatmap visual picker; DB uniqueness constraint preserves integrity but contested seats show as available during the booking window. |
| N-03 — Terminal HTTP debug modal captures PII and JWT tokens | MEDIUM | **RESOLVED** | JWT tokens masked to `Bearer ***` (`http-debug.interceptor.ts:20`). Approved exception formally recorded in `documentation/principles/security-principals.md` (inline block, PII capture intentional for staff diagnostic use). Commit `ff3c907`. |
| N-05 — Bag tag response field undocumented | MEDIUM | **UNCHANGED** | `CheckedInBag.BagTag` field and IATA Resolution 740 format still absent from `documentation/design/check-in.md` and `documentation/design/delivery.md`. |
| N-07 — Standby booking feature undocumented | MEDIUM | **RESOLVED** | `documentation/design/offer.md:333–` — full "Standby bookings" section with flow description, standby search differences, Staff fare pricing, inventory behavior, and Mermaid sequence diagram. Commit `2dbf623`. |
| M-08 — `TierConfig` table not mapped with `HasTrigger` | LOW | **UNCHANGED** | `CustomerDbContext.cs` — no `TierConfig` DbSet or `HasTrigger("TR_TierConfig_UpdatedAt")`. |
| M-10 — `TODO` without issue reference committed to main | LOW | **PARTIALLY RESOLVED** | Payment MS TODOs retained per ADR-001 (acceptable). `OciCheckInHandler.cs:381` TODO unchanged. `SeatServiceClient.cs:10` TODO unchanged. |
| N-04 — Seatmap endpoint descriptions stale in api-reference.md | LOW | **RESOLVED** | `api-reference.md:139` — updated to "manifest-derived occupancy". Commit `0d77314`. |
| N-06 — Flight time-change IROPS throws `NotImplementedException` | LOW | **RESOLVED** | `AdminDisruptionTimeHandler.cs` fully implemented: calls `_offerClient.UpdateInventoryTimesAsync` and `_deliveryClient.UpdateManifestFlightTimesAsync`; returns `AdminDisruptionTimeResponse` with counts of updated records. Commit `193d29a`. |

Resolved this cycle (now closed): C-03, M-01, N-02, N-03, N-04, N-06, N-07.

---

## 4. High findings

### H-04 — Bag tag sequence number is randomly generated; IATA Resolution 740 uniqueness violated

**Severity:** HIGH (Week 3 open)
**Principle breached:** Architecture Principals — IATA standards compliance; the 6-digit bag tag sequence number must be unique per airline per flight day.

**Evidence (unchanged):**
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Application/OciCheckIn/OciCheckInHandler.cs:381–382` — `// TODO: 6-digit sequence number needs to be auto-incremented from a persistent counter rather than generated randomly. var sequence = Random.Shared.Next(0, 1_000_000).ToString("D6");`
- Same generator used for both OCI self check-in and agent check-in paths.

**Impact if unresolved:** Duplicate bag tags at production check-in volumes; misrouted or lost bags; IATA Resolution 740 non-compliance.

**Recommended remediation:** Replace `Random.Shared.Next` with `NEXT VALUE FOR delivery.BagTagSequence` (SQL Server SEQUENCE object in `delivery` schema). Pass the sequence value into `GenerateBagTag()` as a parameter from the repository.

**Age:** 3 weeks.

---

### H-05 — Offer MS timer trigger calls Schedule MS and Ancillary MS directly (MS-to-MS violation)

**Severity:** HIGH (Week 2 open)
**Principle breached:** Architecture Principals — "No direct microservice-to-microservice communication. This constraint applies without exception."

**Evidence (unchanged):**
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/ScheduleServiceClient.cs:11` — `"Note: direct MS-to-MS calls are an accepted exception for timer triggers."` Self-granted exception with no basis in any governance document or ADR.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/SeatServiceClient.cs:10–11` — `"TODO: remove this cross-domain call — cabin counts are already stored in offer.FlightInventory.Cabins"` — violation acknowledged.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Application/RollingInventoryImport/RollingInventoryImportHandler.cs:37,54` — both clients injected and used.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Program.cs:76–91` — `"ScheduleMs"` and `"AncillaryMs"` HTTP clients registered. Both now correctly send the shared `Microservice:HostKey` (PR #1255), mitigating the authentication concern, but the architectural violation is unchanged.

**Recommended remediation (in preference order):**
1. `SeatServiceClient`: implement the acknowledged TODO — derive cabin counts from existing `offer.FlightInventory.Cabins` rows for the same `AircraftType`.
2. `ScheduleServiceClient`: raise an ADR granting a formally approved exception for read-only timer trigger calls with compensating controls (circuit breaker, retry, timeout, dead-letter alerting), or introduce a dedicated `RollingInventoryOrchestration` function in the Operations API.
3. In either case, delete the self-granted exception comment from `ScheduleServiceClient.cs:11`.

**Age:** 2 weeks.

---

## 5. Medium and low findings

### Downgraded from CRITICAL — C-02 cleanup remaining

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| C-02r | Debug infrastructure retained in production code | MEDIUM | C — Security | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` in `Order.Application.GetOrderDebug/`; `DeliveryServiceClient.GetTicketsDebugRawAsync`, `DeliveryServiceClient.GetDocumentsDebugRawAsync`; `OrderServiceClient.GetOrderDebugRawAsync`; three debug endpoint functions in `AdminOrderManagementFunction.cs` (now JWT-gated but still expose raw data). All now authenticated — the PII leak risk is mitigated. The code should still be deleted: debug infrastructure has no place in a production codebase, and retaining it invites accidental re-exposure if auth is ever relaxed. | Retail API / Order MS owner |

### New medium findings this cycle

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| N-08 | `HandleDelayHandler` (`/v1/disruptions/delay`) throws `NotImplementedException` | LOW | B — Contract | `src/API/Orchestration/ReservationSystem.Orchestration.Operations/Application/HandleDelay/HandleDelayHandler.cs` throws `NotImplementedException` with a warning log; `api-reference.md:318` documents the endpoint as returning `200 OK`. Similar to the now-resolved N-06; the Operations API documentation is honest but the implementation is incomplete. The separate `AdminDisruptionTimeHandler` (IROPS admin flow) was correctly implemented this cycle. | Operations API owner |

### Carried medium and low findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| H-03 | Deploy pipelines lack test and CVE gates | CRITICAL AGEING | F / E | See section 2. Week 5. | Platform / DevOps |
| H-04 | Bag tag sequence randomly generated | HIGH | B / E | See section 4. Week 3. | Delivery MS owner |
| H-05 | Offer MS calls Schedule MS and Ancillary MS directly | HIGH | A | See section 4. Week 2. | Offer MS owner |
| N-01 | Seatmap booking-window occupancy gap | MEDIUM | A / B | Pre-booking seatmap shows manifest-only occupancy; in-progress `offer.SeatReservation` holds during the booking window not surfaced. DB constraint preserves integrity. Documentation now accurate. | Retail API / Offer MS owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` TODOs; business logic in Function layer. | Ancillary MS owner |
| M-07 | No integration tests for orchestration APIs | MEDIUM | F | Retail, Loyalty, Admin, and Operations APIs have no integration test coverage. Placeholder test project removed. | QA / Platform |
| N-05 | Bag tag response field undocumented | MEDIUM | G | `CheckedInBag.BagTag` and IATA Resolution 740 format absent from design and API reference docs. | Delivery / Operations owner |
| C-02r | Debug infrastructure in production code | MEDIUM | C | GetOrderDebugHandler, debug client methods, three AdminDebug* functions still present; now authenticated but should be deleted. | Retail API / Order MS owner |
| M-08 | `TierConfig` table not mapped with `HasTrigger` | LOW | D | `TR_TierConfig_UpdatedAt` in `Script.sql`; `CustomerDbContext` has no mapping. | Customer MS owner |
| M-10 | `TODO` without issue reference on main | LOW | Coding Standards | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs remain; coding standards require author and tracking issue. | All owners |
| N-08 | `HandleDelayHandler` throws `NotImplementedException` | LOW | B | FOS delay endpoint (`/v1/disruptions/delay`) documented as functional but unimplemented. | Operations API owner |

---

## 6. Observations and positive notes

- **C-02 substantially resolved and a prior-review misassessment corrected.** All seven debug endpoints now require authentication. Importantly, this review identified that the three `AdminDebug*` functions in `AdminOrderManagementFunction.cs` were protected by `TerminalAuthenticationMiddleware`'s prefix check all along — prior reviews incorrectly concluded they were fully anonymous. The microservice-level debug endpoints were genuinely anonymous and were correctly secured this cycle (PR #1253 and #1256 — `AuthorizationLevel.Function` applied uniformly). This is the most significant security improvement in five review cycles.

- **Microservice host key authentication applied uniformly.** PR #1256 (`18aab04`) requires a host key on all microservice HTTP triggers. PR #1254 (`ba56a5c`) updates all orchestration APIs to use a shared `Microservice:HostKey` configuration value. PR #1255 (`f91710f`) extends the same to the Offer MS's cross-microservice clients. The result is a uniform host key pattern across all microservice–orchestration API authentication.

- **Flight delay disruption (IROPS) implemented.** `AdminDisruptionTimeHandler` now calls Offer MS `UpdateInventoryTimesAsync` and Delivery MS `UpdateManifestFlightTimesAsync`, returning affected counts. N-06 (open since 2026-05-11) is closed. The implementation correctly orchestrates both the schedule and manifest updates without direct MS-to-MS calls.

- **Five documentation findings resolved in a single cycle.** M-01 (Exceptions MS), N-02 (Admin SSR), N-03 (Terminal debug exception), N-04 (seatmap descriptions), N-07 (standby booking) — all closed. The documentation portfolio is materially better than it was on 2026-05-18.

- **ADR-001 formalises payment gateway deferral.** The finding that generated the most repeated CRITICAL AGEING escalations (C-03) now has a formal, stakeholder-approved decision record. This is the correct governance response for an intentional demo-platform constraint: record the rationale, document the integration points, and allow the review process to move on.

- **Basket confirmation parallelisation (PR #1258) is architecturally correct.** `ConfirmBasketHandler.cs:139` uses `Task.WhenAll` across seven independent post-confirmation tasks (inventory hold, ticket issuance, payment settle, customer points, EMD issuance). The compensation path (`VoidAsync` on `ticketsTask.IsFaulted`) is present. Reprice calls at line 190 are correctly parallelised across independent segments.

- **`actions/checkout` upgraded from v3 to v4** (`fe000a9`) — removes a deprecated CI action version before it starts producing warnings.

- **`missing-sql-indexes.sql`** added to `src/Database/` is a diagnostic DMV query tool that generates `CREATE INDEX` recommendations. It makes no schema changes itself. No governance concern.

---

## 7. Conformance scorecard

| Dimension | Conformance | Trend vs 2026-05-18 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🔴 Red | → | H-05 unchanged (Offer MS direct MS-to-MS calls); N-01 seatmap gap partially resolved (docs now accurate, code gap remains) |
| B — API Contract Conformance | 🟢 Green | ↑ | N-02, N-04, N-06, N-07 all resolved this cycle; N-08 (HandleDelayHandler stub) is LOW; no regressions in new endpoints |
| C — Security Principles | 🟡 Amber | ↑↑ | C-02 substantially resolved — all debug endpoints now authenticated; prior-review misassessment on Admin-prefixed functions corrected; N-03 resolved; remaining concern is dead debug code in production (C-02r, MEDIUM) |
| D — Data Storage & Schema Principles | 🟡 Amber | → | M-08 TierConfig unchanged; no new schema violations; missing-sql-indexes.sql is a diagnostic tool only |
| E — Infrastructure & Integration Principles | 🔴 Red | → | H-03 CRITICAL AGEING (week 5) — deploy pipelines still have no test gate despite five recommendations; H-04 and H-05 unchanged |
| F — Testing & CI | 🔴 Red | → | H-03 unchanged; M-07 unchanged; placeholder test project removed (correct hygiene, not an improvement in coverage) |
| G — Documentation Drift | 🟢 Green | ↑↑ | Five findings closed this cycle; only N-05 (bag tag) and C-02r (debug endpoint docs) remain; standby booking documented completely |

---

## 8. Governance gaps

The following gaps remain open. One is escalated in urgency.

1. **ADR process now active — ADR-001 is a good start; more ADRs needed.** The `/documentation/adr/` directory now exists and contains one decision record. Two further decisions made this cycle warrant ADR capture: (a) use of manifest-only occupancy for seatmap display (abandoning SeatReservation-based occupancy) — this changes N-01 from a bug to an accepted design choice but the rationale is undocumented; (b) uniform shared `Microservice:HostKey` authentication pattern replacing per-microservice keys. Without an ADR, engineers re-implementing or testing against these patterns have no documented rationale.

2. **No OpenAPI specs in repository.** Integration principles require machine-readable OpenAPI 3.x specs version-controlled alongside service code. Specs are still not checked in. With 47 commits per cycle the absence of CI-based contract testing is increasingly risky.

3. **No consumer-driven contract tests.** Pact or equivalent between orchestration APIs and microservices remains absent. 47 commits/week with no contract test gate.

4. **No incident response plan discoverable.** Security principles require a documented IR plan with a UK GDPR 72-hour breach notification procedure. C-02's prior five-week exposure makes this gap concrete rather than theoretical — access logs from the microservice-level debug endpoints should be reviewed to confirm no external access occurred before they were secured.

5. **Accounting MS not assessable.** Event-subscription stubs only; no business logic to inspect.

6. **Airport API and Finance API scaffolded only.** Not assessed. The `delivery-microservice.md` spec still describes `bagTagNumber` as populated by the Airport API; this contradicts the implemented behaviour (Delivery MS OCI handler generates the tag). A documentation correction is needed.

---

## 9. Appendix — Scope of this review

### Documents read

| Document | Purpose |
|----------|---------|
| `documentation/principles/architecture-principals.md` | Governing architecture rules |
| `documentation/principles/security-principals.md` | Security requirements (including new approved Terminal debug exception) |
| `documentation/principles/data-principals.md` | Data storage and schema rules |
| `documentation/principles/infrastructure-principals.md` | Infrastructure and CI/CD rules |
| `documentation/principles/integration-principals.md` | API style and integration rules |
| `documentation/principles/coding-standards.md` | C# and project-level standards |
| `documentation/adr/ADR-001-payment-gateway-integration-deferred.md` | Accepted decision — C-03 closure |
| `documentation/api-reference.md` | Full endpoint catalogue |
| `documentation/design/offer.md` | Standby booking and seatmap occupancy documentation |
| `documentation/design/check-in.md` | Bag tag documentation check (N-05) |
| `documentation/design/delivery.md` | Bag tag field documentation check (N-05) |
| `documentation/design-review/design-review-2026-05-18.md` | Most recent prior review |
| `documentation/design-review/design-review-2026-05-11.md` | Second prior review |
| `documentation/design-review/design-review-2026-05-04.md` | Third prior review |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| Exceptions MS | `ExceptionsFunction.cs` — `AuthorizationLevel.Function` confirmed |
| Order MS | `OrderFunction.cs:631` — debug endpoint `AuthorizationLevel.Function` confirmed; `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` still present |
| Delivery MS | `DocumentFunction.cs:180`, `TicketFunction.cs:195` — debug endpoints `AuthorizationLevel.Function` confirmed; `OciCheckInHandler.cs:381–382` — bag tag Random unchanged |
| Retail API | `AdminOrderManagementFunction.cs:362,387,410` — AdminDebug* functions JWT-gated via `TerminalAuthenticationMiddleware` (function-name prefix check confirmed); `DeliveryServiceClient.cs`, `OrderServiceClient.cs` — debug methods still present |
| Shared middleware | `TerminalAuthenticationMiddleware.cs:55` — prefix gate logic read in full |
| Offer MS | `ScheduleServiceClient.cs`, `SeatServiceClient.cs`, `RollingInventoryImportHandler.cs`, `Program.cs:76–91` — H-05 status confirmed unchanged |
| Ancillary MS | `GetSeatOffersHandler.cs:20`, `GetSeatOfferHandler.cs:20` — M-03 TODOs confirmed |
| Customer MS | `CustomerDbContext.cs` — M-08 TierConfig confirmed absent |
| Operations API | `AdminDisruptionTimeHandler.cs` — flight delay implementation confirmed; `HandleDelayHandler.cs` — N-08 stub confirmed; `DisruptionFunction.cs` — middleware chain confirmed (TerminalAuthenticationMiddleware applied globally) |
| Retail API confirm handler | `ConfirmBasketHandler.cs` — parallelisation and compensation logic reviewed |
| GitHub Actions workflows | Representative deploy workflow confirmed; 20-workflow pattern unchanged |
| Terminal app | `http-debug.interceptor.ts` — JWT masking confirmed |

### Commit reference

Review conducted against commit `473ac58` (tip of `main` as of 2026-05-21).
47 commits merged since prior review (`16d8b0e`, 2026-05-18).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS | Event-subscription stubs only; no inspectable business logic |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass |
| Terminal app — new PNR modal | Boundary check only; detailed review deferred |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap from prior reviews |
| Penetration testing / runtime security | Requires live environment access |
| All api-specs (per-service detailed specs) | Selective spot-check only; full audit deferred |
