# Apex Air — Weekly Design Review

**Date:** 2026-05-18
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-05-11.md

---

## 1. Executive summary

The platform is **Non-conforming** for the fourth consecutive review. Three findings have now reached **CRITICAL AGEING**: C-02 (anonymous debug endpoints), C-03 (payment gateway not integrated), and H-03 (deploy pipelines lack test gates) have each been open for four consecutive reviews without remediation. A new HIGH finding has been identified: the Offer microservice timer trigger calls the Schedule microservice and Ancillary microservice directly, violating the "no direct microservice-to-microservice communication" principle — the `ScheduleServiceClient` carries a self-granted exception comment that has no basis in any governance document. Positively, the debug surface has not expanded this cycle (it held at seven endpoints), and 123 commits delivered meaningful capability — standby bookings, seat allocation overflow handling, aircraft registration clearing, and daily gate assignment — all following correct orchestration patterns. The single most important action this week is deletion of all anonymous debug endpoints, which have now been live for a month.

---

## 2. Critical findings (act this week)

### C-02 — Anonymous debug endpoints expose PII and internal data — CRITICAL AGEING

**Severity:** CRITICAL AGEING (Week 4 — escalated)
**Principle breached:** Security Principals — authentication required at every ingress; PII must never appear in API responses; internal data must never be externally reachable.

**Status: UNCHANGED.** Zero remediation activity in 123 commits this cycle. The debug surface is the same seven endpoints confirmed in the week-3 review — it has not grown further, but it has not shrunk either.

**Evidence (unchanged):**
- `src/API/Microservices/ReservationSystem.Microservices.Exceptions/Functions/ExceptionsFunction.cs:34` — `AuthorizationLevel.Anonymous`, `GET /v1/exceptions` — publicly exposes Application Insights exception records including stack traces and PII-bearing messages.
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Functions/AdminOrderManagementFunction.cs:368` — `AuthorizationLevel.Anonymous`, `GET /v1/admin/orders/{bookingRef}/debug` — raw Order rows.
- `AdminOrderManagementFunction.cs:392,415` — two further anonymous debug endpoints returning raw Ticket and Document rows.
- `src/API/Microservices/ReservationSystem.Microservices.Order/Functions/OrderFunction.cs:631` — `AuthorizationLevel.Anonymous`, `GET /v1/debug/orders/{bookingRef}` — raw Order data from the microservice layer, bypassing orchestration entirely.
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Functions/DocumentFunction.cs:180` — `AuthorizationLevel.Anonymous`, `GET /v1/debug/documents` — raw Document rows.
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Functions/TicketFunction.cs:195` — `AuthorizationLevel.Anonymous`, `GET /v1/debug/tickets` — raw Ticket rows.

**Supporting debug infrastructure still in place (unchanged):**
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Infrastructure/ExternalServices/DeliveryServiceClient.cs` — `GetTicketsDebugRawAsync`, `GetDocumentsDebugRawAsync`.
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Infrastructure/ExternalServices/OrderServiceClient.cs` — `GetOrderDebugRawAsync`.
- `src/API/Microservices/ReservationSystem.Microservices.Order/Application/GetOrderDebug/GetOrderDebugHandler.cs` and `GetOrderDebugQuery.cs` — dedicated debug classes in production code.

**Impact if unresolved:** Passenger names, itineraries, e-ticket numbers, passport data, and travel documents are accessible to any caller with a booking reference. One month of exposure. A reportable UK GDPR incident if access logs show external reads.

**Recommended remediation:** Delete all seven debug endpoint functions and their handler, query, and client method counterparts. Audit access logs before deletion. Four weeks open — this is the week to close it.

**Age:** 4 weeks.

---

### C-03 — Payment gateway not integrated; payment state recorded but money not moved — CRITICAL AGEING

**Severity:** CRITICAL AGEING (Week 4 — escalated)
**Principle breached:** Architecture Principals — price integrity; Security Principals — PCI DSS compliance requires real settlement.

**Status: UNCHANGED.** Zero commits touched the Payment MS gateway integration in 123 commits this cycle.

**Evidence (unchanged):**
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/AuthorisePayment/AuthorisePaymentHandler.cs:92` — `// TODO: Call payment gateway (e.g. Adyen, Stripe, Worldpay) to authorise the card.`
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/SettlePayment/SettlePaymentHandler.cs:60` — `// TODO: Call payment gateway to capture / settle the authorised funds.`
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/VoidPayment/VoidPaymentHandler.cs:52` — `// TODO: Call payment gateway to void / reverse the authorisation hold.`
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/RefundPayment/RefundPaymentHandler.cs:57` — `// TODO: Call payment gateway to process the refund against the original transaction.`

No `IPaymentGateway` interface, no gateway dependency in `Program.cs`, no `GatewayAuthorisationCode`/`GatewayTransactionId` fields on the `Payment` entity. Every booking confirmed since deployment has taken a payment record without moving any money.

**Impact if unresolved:** Revenue loss on every booking. PCI DSS compliance cannot be asserted. Refunds and voids log state transitions without reversing actual charges.

**Recommended remediation:** Select a processor (Adyen, Stripe, or Worldpay as noted in the TODO). Implement `IPaymentGateway` interface. Add gateway transaction fields to the Payment entity. Wire all four handlers. Add integration tests for success, decline, and 3DS flows.

**Age:** 4 weeks.

---

## 3. Status of prior findings

| Finding | Prior Severity | Status | Evidence |
|---------|---------------|--------|---------|
| C-02 — Anonymous debug endpoints expose PII | CRITICAL | **UNCHANGED** | Same 7 anonymous debug endpoints confirmed across Exceptions MS, Order MS, Delivery MS, Retail API; debug handler, query, and client methods all still present; no remediation commits found |
| C-03 — Payment gateway not integrated | CRITICAL | **UNCHANGED** | All four TODO stubs unchanged; no gateway interface or fields introduced; 0 relevant commits in 123 merged this cycle |
| H-03 — Deploy pipelines lack test and CVE gates | HIGH | **UNCHANGED** | `main_reservation-system-db-api-retail.yml` contains `dotnet build` only — no `dotnet test`; 20 deploy workflows unchanged; no CVE gate |
| H-04 — Bag tag sequence number randomly generated | HIGH | **UNCHANGED** | `OciCheckInHandler.cs:382` — `Random.Shared.Next(0, 1_000_000)` still in use; TODO at line 381 unchanged |
| M-01 — Exceptions MS not documented | MEDIUM | **UNCHANGED** | `ReservationSystem.Microservices.Exceptions` still absent from `api-reference.md` and `system-overview.md` |
| N-02 — Admin SSR route and documentation mismatch | MEDIUM | **UNCHANGED** | `api-reference.md:89` still documents `/v1/admin/ssr/options`; code routes `v1/admin/ssr[/{ssrCode}]`; POST/PUT/DELETE still undocumented |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` TODOs unchanged |
| M-07 — No integration tests for orchestration APIs | MEDIUM | **UNCHANGED** | `src/API/Tests/` contains only `ReservationSystem.Tests.csproj` — no test classes; Retail, Loyalty, Admin, and Operations APIs remain untested |
| N-01 — Seatmap booking-window occupancy gap | MEDIUM | **UNCHANGED** | Pre-booking seatmap still derives occupancy from manifest only; in-progress `offer.SeatReservation` holds not surfaced to the seat picker |
| N-03 — Terminal HTTP debug modal captures PII and JWT tokens | MEDIUM | **UNCHANGED** | `src/Terminal/src/app/interceptors/http-debug.interceptor.ts` still present and active |
| N-05 — Bag tag response field undocumented | MEDIUM | **UNCHANGED** | `CheckedInBag.BagTag` field and IATA 740 format still absent from design and API reference docs |
| M-08 — `TierConfig` table not mapped with `HasTrigger` | LOW | **UNCHANGED** | `CustomerDbContext.cs` has no `TierConfig` DbSet or `HasTrigger("TR_TierConfig_UpdatedAt")` declaration |
| M-10 — `TODO` without issue reference committed to main | LOW | **UNCHANGED** | Bare `// TODO:` markers confirmed in Payment MS handlers and `OciCheckInHandler.cs:381` |
| N-04 — Seatmap endpoint descriptions stale in api-reference.md | LOW | **UNCHANGED** | `api-reference.md:139` still describes admin seatmap as using "hold status"; now uses manifest |
| N-06 — Flight time-change IROPS throws `NotImplementedException` | LOW | **UNCHANGED** | `AdminDisruptionTimeHandler` still throws `NotImplementedException`; accurately documented as `501` |

---

## 4. High findings

### H-03 — Deploy pipelines build without running tests — CRITICAL AGEING (escalated from HIGH)

**Severity:** CRITICAL AGEING (Week 4 — escalated this review cycle)
**Principle breached:** Infrastructure Principals — unit test failures, integration test failures, and critical CVE findings must block promotion; no build promoted without passing all gates.

**Status: UNCHANGED** for the fourth consecutive review. `main_reservation-system-db-api-retail.yml:39` contains `dotnet build --configuration Release --output ./output` with no preceding or following `dotnet test`. The same pattern repeats across all 20 deploy workflows. The two integration test workflows (`integration-tests-customer-microservice.yml`, `integration-tests-identity-microservice.yml`) remain independent triggers, not gating deployment.

**Recommended remediation:** Add `dotnet test --no-build` before every `Azure/functions-action` step. Add `dotnet list package --vulnerable --include-transitive` with failure on critical/high CVEs. Introduce staging slot swap before production promotion.

**Age:** 4 weeks.

---

### H-04 — Bag tag sequence number is randomly generated; IATA Resolution 740 uniqueness violated

**Severity:** HIGH (Week 2 open)

**Status: UNCHANGED.** `OciCheckInHandler.cs:382` still uses `Random.Shared.Next(0, 1_000_000)`. No SQL `SEQUENCE` or counter table has been introduced. The TODO at line 381 ("In future, this 6-digit sequence number needs to be auto-incremented from a persistent counter") remains unresolved.

**Age:** 2 weeks.

---

### H-05 — Offer MS timer trigger calls Schedule MS and Ancillary MS directly (microservice-to-microservice violation)

**Severity:** HIGH (Week 1 — new finding; violation pre-dates this review cycle but was not identified in prior reviews)
**Principle breached:** Architecture Principals — "No direct microservice-to-microservice communication. This constraint applies without exception."

**Evidence:**
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/ScheduleServiceClient.cs:11` — `"Note: direct MS-to-MS calls are an accepted exception for timer triggers."` — a self-granted exception with no basis in any governance document. The `ScheduleServiceClient` calls the Schedule MS `GET /v1/schedules` endpoint directly from the Offer MS.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/SeatServiceClient.cs:10-11` — `"TODO: remove this cross-domain call — cabin counts are already stored in offer.FlightInventory.Cabins"` — the Offer MS calls the Ancillary MS `GET /v1/aircraft-types` endpoint directly. The TODO acknowledges this is a violation.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Application/RollingInventoryImport/RollingInventoryImportHandler.cs:37,54` — both clients are injected and called in the `RollingInventoryImport` timer trigger handler.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Program.cs:83-92` — `"AncillaryMs"` and `"ScheduleMs"` HTTP clients registered directly in the Offer MS. Both send host keys, mitigating the authentication risk, but the architectural violation is present regardless.

**Context:** Both calls are read-only and occur in a background timer trigger (`RollingInventoryImport` at 01:00 UTC), not in the booking confirmation path. Host key authentication is applied to both. The `SeatServiceClient` TODO was introduced in PR #1145 with a clear acknowledgement of the violation. The `ScheduleServiceClient` comment claims an exception that the architecture principles do not grant.

**Impact if unresolved:** Direct MS-to-MS coupling creates a deployment dependency (Offer MS release cycle is now coupled to Schedule MS and Ancillary MS contract stability), obscures system behaviour in distributed traces, and undermines the governance principle. At scale, the absence of a circuit breaker on the timer trigger means a Schedule MS outage at 01:00 UTC could silently fail to extend the inventory window.

**Recommended remediation (in preference order):**
1. For `SeatServiceClient`: implement the TODO — derive cabin counts from existing `offer.FlightInventory.Cabins` rows rather than fetching from Ancillary MS. The data is already present locally.
2. For `ScheduleServiceClient`: either (a) introduce a dedicated `RollingInventoryOrchestration` capability in the Operations API that owns the timer trigger and calls both Schedule MS and Offer MS, or (b) raise an ADR proposing a formally approved exception for read-only timer trigger calls, with compensating controls (circuit breakers, retry policy, timeouts on all outbound calls, dead-letter alerting).
3. Regardless of the approach chosen, delete the self-granted exception comment from `ScheduleServiceClient`.

**Age:** Week 1 (new finding; `SeatServiceClient` introduced in PR #1145 before last review; `ScheduleServiceClient` pre-existing).

---

## 5. Medium and low findings

### New findings this cycle

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| N-07 | Standby booking feature undocumented | MEDIUM | G — Documentation | `SearchOffersHandler.cs:31-55` implements `bookingType == "Standby"` search logic (includes sold-out flights, skips seat availability checks). `HoldInventoryCommand.cs:9` carries a `StandbyPriority` field. `OfferFunction.cs:509,522` parses and propagates `standbyPriority` from the HTTP request. `offer.md` documents only `bookingType: "Revenue"` as an example; no description of the standby search flow, standby priority, or Staff fare family is present in `offer.md` or `api-reference.md`. The `delivery.md` manifest schema documents `BookingType: Standby` correctly but this is the delivery context only. The full offer→hold→order flow for standby bookings has no design documentation. | Offer MS / Retail API owner |

### Carried medium and low findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| H-03 | Deploy pipelines lack test and CVE gates | CRITICAL AGEING | F / E | Escalated — see section 4. No `dotnet test` in 20 deploy workflows. Week 4 open. | Platform / DevOps |
| M-01 | Exceptions MS not documented | MEDIUM | G | No spec, no entry in `api-reference.md` or `system-overview.md`. Anonymous endpoint publicly reachable. | Platform Architect |
| N-02 | Admin SSR route and documentation mismatch | MEDIUM | B / G | `api-reference.md` documents non-existent `/v1/admin/ssr/options`; code uses `/v1/admin/ssr`; POST/PUT/DELETE undocumented. | Admin API owner |
| N-01 | Seatmap booking-window occupancy gap | MEDIUM | A / B | Pre-booking seatmap derives occupancy from manifest only; seats in active `offer.SeatReservation` holds are not surfaced. DB uniqueness constraint preserves integrity but visual seat picker shows contested seats as available. | Retail API / Offer MS owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` TODOs. Business logic belongs in Application layer. | Ancillary MS owner |
| M-07 | No integration tests for orchestration APIs | MEDIUM | F | Retail, Loyalty, Admin, and Operations APIs have no integration test coverage. Test project has no `.cs` files. | QA / Platform |
| N-03 | Terminal HTTP debug modal captures PII and JWT tokens | MEDIUM | C | `http-debug.interceptor.ts` intercepts all HTTP calls; records `Authorization` headers and full request/response bodies including PII. | Terminal / Security |
| N-05 | Bag tag response field undocumented | MEDIUM | G | `CheckedInBag.BagTag` field and IATA 740 format absent from design and API reference docs. | Delivery / Operations owner |
| N-07 | Standby booking feature undocumented | MEDIUM | G | See new finding above. | Offer MS / Retail API owner |
| M-08 | `TierConfig` table not mapped with `HasTrigger` | LOW | D | `TR_TierConfig_UpdatedAt` in `Script.sql`; `CustomerDbContext` has no mapping. | Customer MS owner |
| M-10 | `TODO` without issue reference committed to main | LOW | Coding Standards | Bare `// TODO:` markers in Payment MS handlers, `OciCheckInHandler.cs:381`, and `SeatServiceClient.cs:10`. | All owners |
| N-04 | Seatmap endpoint descriptions stale in api-reference.md | LOW | G | `api-reference.md:139` still references "hold status"; now uses manifest. | Retail API owner |
| N-06 | Flight time-change IROPS throws `NotImplementedException` | LOW | B | Accurately documented as `501`; no implementation timeline visible. | Operations API owner |

---

## 6. Observations and positive notes

- **Debug surface held at seven endpoints.** In the prior cycle C-02 regressed as new endpoints were added. This cycle the count did not grow. While the endpoints remain and remediation is overdue, the absence of further expansion is noted.

- **Claude Code hooks system adds architectural guardrails.** Commit `c8150ed` introduced `scripts/hooks/pre_tool_use.py` and `post_tool_use.py` as a PreToolUse hook that blocks unauthorised `.csproj` project references from one microservice to another. This is a proactive, tooling-level enforcement of the microservice boundary principle — one of the patterns that produced finding H-05 should now be caught automatically going forward.

- **IROPS rebook performance improvement.** Commit `0092985` parallelises independent HTTP calls in the rebook-order handler. This is the correct pattern — identify independent downstream calls, use `Task.WhenAll`, reduce wall-clock latency without sacrificing correctness. The `c5bbfd1` commit also replaced a full-flight order scan with a direct booking reference lookup, eliminating an O(n) database scan on the hot IROPS path.

- **Daily gate assignment timer trigger documented atomically.** Commits `8d2146f` and `2bf8bac` added the `DailyAircraftGateAssignment` trigger and updated `timer-triggers.md` in the same batch — the atomic code-and-docs pattern the principles require.

- **Standby booking simulation uses the correct architecture.** The simulator's standby booking flow calls the Retail API, which orchestrates across Offer MS and Order MS. The standby logic (`bookingType`, `standbyPriority`) is confined to the offer and order domains with no cross-schema or direct MS calls. The implementation architecture is correct; the gap is documentation only (N-07).

- **Seat allocation overflow handled correctly.** Commit `ff882d2` adds overflow cabin allocation in the auto-assign-seats path. The handler reads the manifest from Delivery MS via the Operations API and the seatmap from Ancillary MS via the Operations API — correct orchestration layer usage throughout.

- **No new `Console.Error.WriteLine` instances.** A targeted scan of `src/API/` confirmed zero instances — the H-02 remediation discipline has held across another 123 commits.

---

## 7. Conformance scorecard

| Dimension | Conformance | Trend vs 2026-05-11 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🔴 Red | ↓ | H-05 (Offer MS directly calls Schedule MS and Ancillary MS in timer trigger) confirmed; self-granted exception comment not grounded in governance; N-01 seatmap gap unchanged |
| B — API Contract Conformance | 🟡 Amber | → | N-02 (SSR mismatch) unchanged; N-07 (standby booking undocumented) is new; N-04 and N-05 unchanged; no regressions in this cycle's new endpoints |
| C — Security Principles | 🔴 Red | → | C-02 CRITICAL AGEING — seven anonymous debug endpoints unchanged for fourth consecutive review; N-03 unchanged; no remediation activity |
| D — Data Storage & Schema Principles | 🟡 Amber | → | No new schema violations; M-08 TierConfig unchanged; no new data-layer changes requiring assessment |
| E — Infrastructure & Integration Principles | 🟡 Amber | → | H-03 deploy pipelines CRITICAL AGEING; H-04 (bag tag) unchanged; H-05 new — Offer MS MS-to-MS calls; no new infrastructure findings otherwise |
| F — Testing & CI | 🔴 Red | → | H-03 CRITICAL AGEING (4 weeks); M-07 unchanged (no tests in orchestration APIs); test project has no test classes |
| G — Documentation Drift | 🔴 Red | ↓ | N-07 (standby booking undocumented) is new; N-02, N-04, N-05 unchanged; timer trigger update is a positive; overall drift is worsening as new features outpace documentation |

---

## 8. Governance gaps

The following gaps remain open. No new governance gaps have been identified this cycle.

1. **No ADRs present.** `/documentation/adr/` does not exist. H-05 makes this gap more urgent: the `ScheduleServiceClient` comment claims an accepted exception for MS-to-MS calls in timer triggers, but there is no ADR to record the decision, the constraints, and the compensating controls. If the team intends to grant this exception, it needs to be in a formal ADR that the governance process can evaluate.

2. **No OpenAPI specs in repository.** Integration principles require machine-readable OpenAPI 3.x specs version-controlled alongside service code. Standby booking endpoints and standby-specific request fields (`standbyPriority`, `bookingType: "Standby"`) are not captured in any machine-readable contract.

3. **No consumer-driven contract tests.** Pact or equivalent between orchestration APIs and microservices remains absent. 123 commits per cycle makes the absence of automated contract verification increasingly risky.

4. **No incident response plan discoverable.** Security principles require a documented IR plan for UK GDPR 72-hour breach notification. C-02's fourth-week exposure makes this gap urgent.

5. **Accounting MS not assessable.** Event-subscription stubs only. New standby booking and seat/bag sale events generated in this cycle may require Accounting to handle new event types — this cannot be verified.

6. **Airport API and Finance API scaffolded only.** Not assessed. The `delivery-microservice.md` spec describes the Airport API as the populator of bag tags — this is now incorrect following earlier check-in changes and remains inconsistent.

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
| `documentation/api-reference.md` | Full endpoint catalogue |
| `documentation/design/offer.md` | Offer domain design — standby booking coverage check |
| `documentation/timer-triggers.md` | Timer trigger schedule and data lifecycle |
| `documentation/design-review/design-review-2026-05-11.md` | Most recent prior review |
| `documentation/design-review/design-review-2026-05-04.md` | Second prior review |
| `documentation/design-review/design-review-2026-04-21.md` | Inaugural review |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| Exceptions MS | `ExceptionsFunction.cs` — anonymous endpoint confirmed |
| Order MS | `OrderFunction.cs` — debug endpoint confirmed; `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` |
| Delivery MS | `DocumentFunction.cs`, `TicketFunction.cs` — debug endpoints confirmed |
| Retail API | `AdminOrderManagementFunction.cs` — three debug endpoints confirmed; `DeliveryServiceClient.cs`, `OrderServiceClient.cs` — debug client methods |
| Payment MS | All four application handlers — gateway integration status confirmed unchanged |
| Offer MS | `SeatServiceClient.cs`, `ScheduleServiceClient.cs`, `RollingInventoryImportHandler.cs`, `Program.cs` — MS-to-MS violation confirmed; `SearchOffersHandler.cs`, `HoldInventoryHandler.cs`, `HoldInventoryCommand.cs` — standby logic reviewed; `OfferFunction.cs` — standby priority parsing |
| Ancillary MS | `GetSeatOffersHandler.cs`, `GetSeatOfferHandler.cs` — M-03 TODO confirmed |
| Operations API | `AdminCheckInHandler.cs` — seat allocation overflow reviewed |
| Admin API | `SsrManagementFunction.cs` — N-02 route mismatch confirmed |
| Customer MS | `CustomerDbContext.cs` — M-08 TierConfig mapping confirmed absent |
| Terminal app | `http-debug.interceptor.ts` — N-03 interceptor confirmed present |
| GitHub Actions workflows | `main_reservation-system-db-api-retail.yml` — H-03 no test step confirmed; workflow list confirms 20 deploy workflows unchanged |
| Hooks system | `.claude/test-settings.json`, `scripts/hooks/pre_tool_use.py` — hook guardrails reviewed |

### Commit reference

Review conducted against commit `16d8b0e` (tip of `main` as of 2026-05-18).
123 commits merged since prior review (`8b18839`, 2026-05-11).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS | Event-subscription stubs only; no inspectable business logic |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass |
| Terminal app — non-security features | New IROPS rebooking integration under flight management not assessed beyond boundary check |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap from prior reviews |
| Penetration testing / runtime security | Requires live environment access |
| All api-specs except offer-microservice.md spot-check | Full audit deferred; selective check for standby booking documentation |
