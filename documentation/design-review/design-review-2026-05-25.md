# Apex Air — Weekly Design Review

**Date:** 2026-05-25
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-05-21.md

---

## 1. Executive summary

The platform posture is **Conforming with concerns** — held from the prior cycle with a material improvement and one new critical escalation. Thirty-nine commits delivered meaningful work this week: the five-week CRITICAL AGEING finding H-03 (deploy pipelines without test and CVE gates) is substantially resolved — all 16 deploy workflows now include `dotnet test` and CVE scanning, and the CVE gate immediately proved its value by blocking a build until high-severity vulnerable transitive NuGet packages were fixed. However, finding H-04 (bag tag sequence number generated randomly, violating IATA Resolution 740) has now been open for four consecutive reviews and escalates to **CRITICAL AGEING** this cycle. Two new low-to-medium findings are raised: an OrderData JSON document lacking a `schemaVersion` field (data principle violation now evidenced by the `paxId` schema addition) and a documentation gap on the `GET /v1/products` `basketId` parameter. The single most important action this week is replacing `Random.Shared.Next` in the bag tag generator with a persistent SQL `SEQUENCE` before check-in reaches production volume.

---

## 2. Critical findings (act this week)

### H-04 — Bag tag sequence number is randomly generated; IATA Resolution 740 uniqueness violated — CRITICAL AGEING (Week 4)

**Severity:** CRITICAL AGEING (escalated from HIGH — open for four consecutive reviews)
**Principle breached:** Architecture Principals — IATA standards alignment; "E-ticket numbers are immutable after issuance" extends to all IATA-mandated identifiers. IATA Resolution 740 requires bag tag licence plate numbers to be globally unique per airline per flight day.

**Evidence (unchanged for fourth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Application/OciCheckIn/OciCheckInHandler.cs:381–382` — `// TODO: 6-digit sequence number needs to be auto-incremented from a persistent counter rather than generated randomly. var sequence = Random.Shared.Next(0, 1_000_000).ToString("D6");`
- Same generator invoked by both OCI self check-in and agent check-in paths. No persistent counter, no `SEQUENCE` object, no `BagTagCounter` table.

**Impact if unresolved:** A random 6-digit number across a small population (tens to hundreds of bags per flight) will produce collisions once check-in reaches any production scale. Duplicate bag tags cause misrouting or loss of passenger bags and represent IATA Resolution 740 non-compliance. This is an operational safety issue.

**Recommended remediation:** Replace `Random.Shared.Next` with `NEXT VALUE FOR delivery.BagTagSequence` (SQL Server `SEQUENCE` object in the `delivery` schema). Pass the next sequence value into `GenerateBagTag()` as a parameter from the repository. The mod-7 check-digit logic is correct and need not change. The `TODO` comment at line 381 must be closed before check-in surface is made available in any production-bound environment.

**Age:** 4 weeks.

---

## 3. Status of prior findings

| Finding | Prior Severity | Status | Evidence note |
|---------|---------------|--------|--------------|
| H-03 — Deploy pipelines lack test and CVE gates | CRITICAL AGEING | **SUBSTANTIALLY RESOLVED** (downgrade to MEDIUM — see section 5) | `dotnet test --no-build` + CVE gate added to all 16 deploy workflows (commit `0efc604`). CVE gate demonstrably blocked a build this cycle — high-severity vulnerable packages fixed in `51d582b`. Test step is structurally wired but runs 0 tests (no test classes in project directories); residual gap remains as M-07. |
| H-04 — Bag tag sequence randomly generated | HIGH | **UNCHANGED** → escalated to **CRITICAL AGEING** | `OciCheckInHandler.cs:382` — `Random.Shared.Next(0, 1_000_000)` unchanged; see section 2. Week 4. |
| H-05 — Offer MS calls Schedule MS and Ancillary MS directly | HIGH | **UNCHANGED** | `ScheduleServiceClient.cs:11` self-granted exception comment unchanged; `SeatServiceClient.cs:10` acknowledged violation TODO unchanged; `RollingInventoryImportHandler.cs:37,54` and `Program.cs:76–91` unchanged. Week 3. |
| C-02r — Debug infrastructure retained in production code | MEDIUM | **UNCHANGED** | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs`, three `AdminDebug*` functions in `AdminOrderManagementFunction.cs`, and debug client methods (`GetTicketsDebugRawAsync`, `GetDocumentsDebugRawAsync`, `GetOrderDebugRawAsync`) all still present. All endpoints remain authenticated (host key or JWT via `TerminalAuthenticationMiddleware`). Code should be deleted. |
| N-01 — Seatmap booking-window occupancy gap | MEDIUM | **UNCHANGED** | Pre-booking seatmap still derives occupancy from manifest only; in-progress `offer.SeatReservation` holds not surfaced to the seat picker. DB uniqueness constraint preserves integrity. Documentation is accurate. |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` TODOs both unchanged. |
| M-07 — No integration tests for orchestration APIs | MEDIUM | **UNCHANGED** | `dotnet test` step now wired in all deploy workflows, but runs 0 test methods — no test classes exist in any orchestration project directory. Retail, Loyalty, Admin, and Operations APIs remain uncovered. |
| N-05 — Bag tag response field undocumented | MEDIUM | **UNCHANGED** | `documentation/design/delivery.md:331` still states "bagTagNumber is null at purchase and populated by the Airport API when the bag is checked in." The Delivery MS OCI handler generates the tag; the Airport API does not. Description is factually incorrect and has been since the OCI check-in feature landed. |
| M-08 — `TierConfig` table not mapped with `HasTrigger` | LOW | **UNCHANGED** | `CustomerDbContext.cs` still has no `TierConfig` DbSet or `HasTrigger("TR_TierConfig_UpdatedAt")` declaration. `TR_TierConfig_UpdatedAt` exists in `Script.sql`. |
| M-10 — `TODO` without issue reference committed to main | LOW | **UNCHANGED** | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` still carry bare `// TODO:` markers without author or tracking issue reference. |
| N-08 — `HandleDelayHandler` throws `NotImplementedException` | LOW | **UNCHANGED** | `HandleDelayHandler.cs:35` still throws `NotImplementedException()`. `/v1/disruptions/delay` documented as operational. |

Resolved this cycle: none fully closed. H-03 substantially resolved — downgraded to MEDIUM (residual test coverage gap).

---

## 4. High findings

### H-05 — Offer MS timer trigger calls Schedule MS and Ancillary MS directly (week 3)

**Severity:** HIGH (Week 3 — escalates to CRITICAL AGEING if unresolved next cycle)
**Principle breached:** Architecture Principals — "No direct microservice-to-microservice communication. This constraint applies without exception."

**Evidence (unchanged):**
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/ScheduleServiceClient.cs:11` — self-granted exception comment: `"Note: direct MS-to-MS calls are an accepted exception for timer triggers."` No governance document grants this exception.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/SeatServiceClient.cs:10` — acknowledged violation: `"TODO: remove this cross-domain call — cabin counts are already stored in offer.FlightInventory.Cabins"`.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Application/RollingInventoryImport/RollingInventoryImportHandler.cs:37,54` — both clients injected and called.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Program.cs:76–91` — `ScheduleMs` and `AncillaryMs` HTTP clients registered.

**Impact if unresolved:** Deployment coupling, obscured distributed traces, no circuit breaker on 01:00 UTC timer trigger. A Schedule MS or Ancillary MS outage silently fails the nightly inventory import.

**Recommended remediation:** 1) `SeatServiceClient` — implement the acknowledged TODO: derive cabin counts from existing `offer.FlightInventory.Cabins` rows for the same `AircraftType`. 2) `ScheduleServiceClient` — raise an ADR to formally grant or deny the exception; if granted, add circuit breaker, retry, timeout, and dead-letter alerting as compensating controls; if denied, move the timer trigger into a dedicated Operations API orchestration function. 3) Delete the self-granted exception comment regardless of the path chosen.

**Age:** 3 weeks. Escalates to CRITICAL AGEING if still open in the 2026-06-01 review.

---

## 5. Medium and low findings

### H-03 residual — `dotnet test` gate is vacuous (no test classes exist)

The test step was added to all 16 deploy workflows — a significant structural improvement — but `dotnet test --no-build` runs from each project directory and finds 0 test projects. No test methods execute. The gate prevents nothing at the code level; the CVE gate is the only active protection. This residual concern inherits M-07 and remains until real test coverage is written.

### New findings this cycle

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| N-10 | `OrderData` JSON document lacks `schemaVersion` field | MEDIUM | D — Data Storage | Data principles require: "JSON documents must include a `schemaVersion` field; migration strategies for documents stored under previous versions must be defined before deploying breaking changes." `OrderData` has never included `schemaVersion`. The `paxId` addition this cycle (`b9808cd`) adds a new integer field alongside the existing `passengerId` string across passengers, order items, and e-tickets. Existing `OrderData` documents in the database pre-dating this commit lack `paxId`. Without `schemaVersion`, the application has no reliable way to distinguish old documents from new ones and cannot apply version-appropriate defaults. `order.md` OrderData examples do not document the `paxId` field in the JSON schema (the Notes-context `paxId` at `order.md:565` is a different, unrelated field). | Order MS owner |
| N-09 | `GET /v1/products` `basketId` parameter not documented in api-reference.md | LOW | G — Documentation | Commit `2e160b8` added an optional `basketId` query parameter to `GET /v1/products`. When supplied, the Retail API fetches the basket from Order MS and evaluates each product's `AvailabilityRules` against the basket context. `api-reference.md:98` still describes the endpoint as "Retrieve the active retail product catalogue from the Ancillary MS" with no mention of `basketId` or rule evaluation. | Retail API / Docs owner |

### Carried medium and low findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| H-05 | Offer MS calls Schedule MS and Ancillary MS directly | HIGH | A | See section 4. Week 3. Escalates next cycle. | Offer MS owner |
| C-02r | Debug infrastructure in production code | MEDIUM | C | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs`, three `AdminDebug*` Retail API functions, and debug client methods. All authenticated; still dead code that should be deleted. | Retail API / Order MS owner |
| N-01 | Seatmap booking-window occupancy gap | MEDIUM | A / B | Pre-booking seatmap shows manifest-only occupancy; in-progress `offer.SeatReservation` holds not surfaced. DB constraint preserves integrity. | Retail API / Offer MS owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` TODOs. Business logic belongs in Application layer. | Ancillary MS owner |
| M-07 | No integration tests for orchestration APIs | MEDIUM | F | Test step wired (H-03 resolved structurally) but executes 0 tests. Retail, Loyalty, Admin, and Operations APIs uncovered. | QA / Platform |
| N-05 | Bag tag documentation incorrect | MEDIUM | G | `delivery.md:331` attributes bag tag generation to the Airport API; Delivery MS OCI handler generates it. | Delivery / Operations owner |
| N-10 | `OrderData` JSON lacks `schemaVersion` | MEDIUM | D | New finding — see above. | Order MS owner |
| H-03r | `dotnet test` gate runs 0 tests | MEDIUM | F | Structural test gate present; no test classes to execute. Inherits M-07. | QA / Platform |
| M-08 | `TierConfig` table not mapped with `HasTrigger` | LOW | D | `TR_TierConfig_UpdatedAt` in `Script.sql`; `CustomerDbContext` has no mapping. | Customer MS owner |
| M-10 | `TODO` without issue reference on main | LOW | Coding Standards | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs remain. | All owners |
| N-08 | `HandleDelayHandler` throws `NotImplementedException` | LOW | B | `/v1/disruptions/delay` unimplemented; `api-reference.md` documents it as returning `200 OK`. | Operations API owner |
| N-09 | `GET /v1/products` `basketId` undocumented | LOW | G | New finding — see above. | Retail API / Docs owner |

---

## 6. Observations and positive notes

- **H-03 substantially resolved after five weeks.** All 16 deploy workflows now include `dotnet test --no-build` and a CVE vulnerability scan. The CVE gate demonstrated real value in this very cycle: commit `51d582b` fixed two high-severity vulnerable transitive NuGet dependencies (`System.Net.Http` and `System.Text.RegularExpressions`) that were blocking deployment. A long-open critical finding driven to the finish line in a single PR is a meaningful delivery.

- **Product availability rules correctly moved to the Retail API.** Commit `2e160b8` removed all rule evaluation logic from the Angular web application and implemented it in `ProductsFunction` (Retail API). The implementation correctly fetches basket context from Order MS through the orchestration layer and evaluates rules server-side. Business logic in the UI is a security anti-pattern (client-side filtering is bypassable); this change correctly enforces the rule in the authoritative API layer. The `basketId` parameter is optional and backwards-compatible.

- **SSR amendment cutoff enforced in the API.** Commit `7b305f8` adds a 24-hour pre-departure gate to `PATCH /v1/orders/{bookingRef}/ssrs` in the Retail API (`OrderFunction.cs`), returning `422 Unprocessable` when within the cutoff window. This matches the design documented in `ssr.md:145–147` and closes a previously unenforced business rule.

- **SSR category constraint expanded correctly.** The SQL `CHK_SsrCatalogue_Cat` constraint in `Script.sql` was extended to include `Medical` and `Assistance` categories, enabling MEDA, STCR, OXYG, MAAS, UMNR, and EXST codes. This aligns the database constraint with the SSR design document.

- **IROPS rebook reliability improvements.** Commits `3f98078` (retry capability for failed rebookings) and `a52a426` (60-minute ticketing cutoff enforcement) and `35b6e94` (correct UTC date calculation for cross-midnight BST flights) collectively make the disruption path more robust. The cross-midnight BST fix is a subtle correctness improvement that would have produced incorrect cutoff evaluations on flights departing between midnight and 01:00 during British Summer Time.

- **Disruption cancellation status code corrected.** Commit `82a0cfa` updated `api-reference.md` and `disruption.md` to reflect the actual `200 OK` response (not `202 Accepted`) from `POST /v1/disruptions/cancellation`. Documentation matched to implementation is the correct governance posture.

- **Schedule UTC time fields documented.** Commit `97bd9fa` updated `documentation/design/schedule.md` to document UTC time fields on `FlightSchedule`. Atomic code-and-docs commits continue to be the pattern used.

---

## 7. Conformance scorecard

| Dimension | Conformance | Trend vs 2026-05-21 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🔴 Red | → | H-05 unchanged (Offer MS MS-to-MS calls, Week 3); N-01 seatmap gap unchanged |
| B — API Contract Conformance | 🟡 Amber | → | Disruption status code corrected; N-08 unchanged; N-09 new (products basketId undocumented); no new regressions in new endpoints |
| C — Security Principles | 🟡 Amber | → | C-02r unchanged — all debug endpoints authenticated but dead code still present; no new security concerns in this cycle's features |
| D — Data Storage & Schema Principles | 🟡 Amber | ↓ | M-08 unchanged; N-10 new — `OrderData` JSON lacks `schemaVersion` and the paxId addition was deployed without one; data principle violation now concrete |
| E — Infrastructure & Integration Principles | 🟡 Amber | ↑ | H-03 substantially resolved — CVE gate active and operational; CVE fix demonstrably applied; H-04 escalates to CRITICAL AGEING |
| F — Testing & CI | 🔴 Red | ↑ | H-03 structural gate added (improvement); test step still executes 0 test methods; M-07 unchanged; overall test coverage of orchestration APIs remains zero |
| G — Documentation Drift | 🟡 Amber | → | N-09 new (products basketId); N-05 unchanged; several fixes this cycle (disruption status, schedule UTC fields, identity set-password, SSR cutoff in api-reference) |

---

## 8. Governance gaps

The following gaps remain open. No new governance gaps identified this cycle.

1. **ADR-001 exists but further ADRs are overdue.** Decisions warranting ADR capture: (a) manifest-only seatmap occupancy as accepted design (rather than a bug), (b) shared `Microservice:HostKey` authentication pattern, (c) product availability rule evaluation at Retail API layer. Without ADRs, the rationale behind these design choices is not durable.

2. **No OpenAPI specs in repository.** Integration principles require machine-readable OpenAPI 3.x specs version-controlled alongside service code. This gap makes CI-based contract testing impossible and leaves the `basketId` parameter on `GET /v1/products` undiscoverable except by reading source.

3. **No consumer-driven contract tests.** Pact or equivalent between orchestration APIs and microservices remains absent. 39 commits/week with no automated contract verification.

4. **No incident response plan discoverable.** Security principles require a documented IR plan including a UK GDPR 72-hour breach notification procedure. The C-02 exposure (now resolved) underscored the concrete risk this gap represents.

5. **Accounting MS not assessable.** Event-subscription stubs only; no business logic to inspect.

6. **Airport API and Finance API scaffolded only.** Not assessed. The `delivery.md:331` incorrect statement that the Airport API populates bag tags (finding N-05) is related to this gap — the Airport API spec may itself be inconsistent with the implemented behaviour.

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
| `documentation/design/delivery.md` | Bag tag documentation accuracy check (N-05) |
| `documentation/design/order.md` | OrderData JSON schema and paxId field documentation (N-10) |
| `documentation/design/ssr.md` | SSR amendment cutoff documentation check |
| `documentation/design-review/design-review-2026-05-21.md` | Most recent prior review |
| `documentation/design-review/design-review-2026-05-18.md` | Second prior review |
| `documentation/design-review/design-review-2026-05-11.md` | Third prior review |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| GitHub Actions workflows | Full audit — all 21 workflow files assessed for test and CVE gates; 16 deploy workflows confirmed with `dotnet test` and CVE scanning; 3 Static Web Apps workflows correctly omitted |
| Delivery MS — OciCheckIn | `OciCheckInHandler.cs:381–382` — bag tag random sequence confirmed unchanged (H-04) |
| Offer MS | `ScheduleServiceClient.cs`, `SeatServiceClient.cs`, `RollingInventoryImportHandler.cs`, `Program.cs:76–91` — H-05 status confirmed unchanged |
| Order MS — Debug | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs`, `OrderFunction.cs:631` — C-02r confirmed present |
| Order MS — paxId change | `ConfirmOrderHandler.cs`, `UpdateOrderBagsHandler.cs`, `ChangeOrderHandler.cs` — paxId addition reviewed; no `schemaVersion` present |
| Retail API — AdminOrderManagement | `AdminOrderManagementFunction.cs:362,387,410` — AdminDebug* functions confirmed `AuthorizationLevel.Anonymous` but protected by `TerminalAuthenticationMiddleware` prefix check |
| Shared middleware | `TerminalAuthenticationMiddleware.cs` — full read; prefix gate `functionName.StartsWith("Admin")` at line 55 confirmed |
| Retail API — ProductsFunction | Full read — `basketId` parameter wired correctly; calls `_orderServiceClient.GetBasketAsync` and `_productServiceClient` only (correct orchestration); `basketId` absent from api-reference.md (N-09) |
| Retail API — OrderFunction | SSR amendment cutoff gate confirmed at lines 296–350 |
| Ancillary MS | `GetSeatOffersHandler.cs:20`, `GetSeatOfferHandler.cs:20` — M-03 TODOs confirmed |
| Customer MS | `CustomerDbContext.cs` — M-08 TierConfig confirmed absent |
| Operations API — HandleDelay | `HandleDelayHandler.cs:35` — N-08 `NotImplementedException` confirmed |
| Database | `Script.sql` — `CHK_SsrCatalogue_Cat` constraint expansion confirmed (Medical, Assistance categories added) |

### Commit reference

Review conducted against commit `316fd85` (tip of `main` as of 2026-05-25).
39 commits merged since prior review (`473ac58`, 2026-05-21).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS | Event-subscription stubs only; no inspectable business logic |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass |
| Terminal app | No security-relevant changes noted in commit log this cycle |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap from prior reviews |
| Penetration testing / runtime security | Requires live environment access |
| All api-specs | Selective spot-check only; N-09 found via api-reference.md audit |
