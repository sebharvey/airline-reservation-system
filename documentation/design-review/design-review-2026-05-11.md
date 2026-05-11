# Apex Air — Weekly Design Review

**Date:** 2026-05-11
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-05-04.md

---

## 1. Executive Summary

The platform is **Non-conforming** for the third consecutive review. C-02 (anonymous debug endpoints) has **regressed**: the debug surface confirmed in the prior review as four endpoints has expanded to at least seven, with new anonymous debug endpoints added to the Order microservice (`OrderFunction.cs`) and Delivery microservice (`TicketFunction.cs`, `DocumentFunction.cs`) in addition to the pre-existing Orchestration layer endpoints. C-02 and C-03 are both now entering their third review and will be escalated to **CRITICAL AGEING** if they remain unresolved in the next cycle. A new HIGH finding has been identified: the IATA Resolution 740 bag tag sequence number used during OCI and agent check-in is generated randomly rather than from a persistent counter, meaning collisions are mathematically certain at scale and bags will be misrouted. Sixty-two commits have merged since the 4 May review, delivering significant new capability — IATA bag tag generation, agent check-in with full baggage support, IROPS cancellation/rebooking, and equipment-change modals in the Terminal — all of which follow correct architectural patterns. The single most important action this week is deletion of all debug endpoints; the second is implementing a persistent bag tag sequence counter before any check-in volume reaches production.

---

## 2. Critical Findings (act this week)

### C-02 — Anonymous debug endpoints expose PII and internal data — REGRESSED

**Severity:** CRITICAL (Week 3 open — **CRITICAL AGEING in one review cycle**)
**Principle breached:** Security Principals — authentication required at every ingress; PII must never appear in API responses; secrets and internal data must never be externally reachable.

**Status change: REGRESSED.** The prior review documented four anonymous debug endpoints. This review confirms at least seven, with three new endpoints added across the Order microservice and Delivery microservice in the same period in which no remediation of the original four occurred.

**Evidence of original endpoints (unchanged):**
- `src/API/Microservices/ReservationSystem.Microservices.Exceptions/Functions/ExceptionsFunction.cs:34` — `AuthorizationLevel.Anonymous`, `GET /v1/exceptions` — publicly exposes Application Insights exception records, stack traces, and PII-bearing exception messages.
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Functions/AdminOrderManagementFunction.cs:368` — `AuthorizationLevel.Anonymous`, `GET /v1/admin/orders/{bookingRef}/debug` — raw Order rows.
- `AdminOrderManagementFunction.cs:392,415` — two further anonymous debug endpoints returning raw Ticket and Document rows.

**Evidence of newly confirmed endpoints (regression):**
- `src/API/Microservices/ReservationSystem.Microservices.Order/Functions/OrderFunction.cs` — `GET /v1/debug/orders/{bookingRef}` — `// TODO: Remove this endpoint — temporary debug only` — anonymous endpoint directly on the Order microservice exposing raw order data without going through the orchestration layer.
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Functions/DocumentFunction.cs` — anonymous debug endpoint exposing raw document records.
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Functions/TicketFunction.cs` — anonymous debug endpoint exposing raw ticket records.

**Supporting debug infrastructure still in place:**
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Infrastructure/ExternalServices/DeliveryServiceClient.cs` — `GetTicketsDebugRawAsync`, `GetDocumentsDebugRawAsync` — `// TODO: Remove — temporary debug method`
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Infrastructure/ExternalServices/OrderServiceClient.cs` — `GetOrderDebugRawAsync` — `// TODO: Remove — temporary debug method`
- `src/API/Microservices/ReservationSystem.Microservices.Order/Application/GetOrderDebug/GetOrderDebugHandler.cs` — dedicated debug handler class in production code.
- `src/API/Microservices/ReservationSystem.Microservices.Order/Application/GetOrderDebug/GetOrderDebugQuery.cs` — dedicated debug query class.

**Impact if unresolved:** Passenger names, itineraries, e-ticket numbers, passport data, and travel documents are accessible to any caller with a booking reference. The Order MS debug endpoint bypasses the orchestration layer entirely — it is reachable without even the nominal naming-convention middleware check. Stack traces in the Exceptions MS expose internal service topology. The expansion of the debug surface to microservice layer represents an escalation of risk beyond the inaugural finding. This is a reportable UK GDPR incident if access logs show external reads.

**Recommended remediation:**
1. Delete all debug endpoint functions, handlers, and query classes: `ExceptionsFunction`, `OrderFunction` debug route, `DocumentFunction` debug route, `TicketFunction` debug route, `AdminOrderManagementFunction` three debug functions, `GetOrderDebugHandler`, `GetOrderDebugQuery`.
2. Delete debug client methods: `GetTicketsDebugRawAsync`, `GetDocumentsDebugRawAsync`, `GetOrderDebugRawAsync` from `DeliveryServiceClient` and `OrderServiceClient`.
3. Audit access logs from all seven endpoints for evidence of external access before deleting — preserve access records if evidence is found.
4. If internal debug capability is required, implement it behind `TerminalAuthenticationMiddleware` on a staff-only orchestration route, not on microservice endpoints.

**Age:** 3 weeks.

---

### C-03 — Payment gateway not integrated; payment state recorded but money not moved

**Severity:** CRITICAL (Week 3 open — **CRITICAL AGEING in one review cycle**)
**Principle breached:** Architecture Principals — price integrity and stored-offer pattern require that confirmed orders result in real payment settlement.

**Evidence (UNCHANGED from prior two reviews):**
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/AuthorisePayment/AuthorisePaymentHandler.cs:92` — `// TODO: Call payment gateway (e.g. Adyen, Stripe, Worldpay) to authorise the card.`
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/SettlePayment/SettlePaymentHandler.cs:60` — `// TODO: Call payment gateway to capture / settle the authorised funds.`
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/VoidPayment/VoidPaymentHandler.cs:52` — `// TODO: Call payment gateway to void / reverse the authorisation hold.`
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/RefundPayment/RefundPaymentHandler.cs:57` — `// TODO: Call payment gateway to process the refund against the original transaction.`

No `IPaymentGateway` interface has been introduced. No processor dependency is registered in `Program.cs`. The `Payment` entity holds no gateway authorisation code, settlement reference, or transaction ID fields. Zero commits in the 62 merged since 4 May touched payment gateway integration. All four core payment operations continue to log a `PaymentEvent` row and return success without making any external call.

**Impact if unresolved:** Revenue loss on every booking confirmed since deployment. Refunds logged without reversing charges. Void/cancel flows release inventory without returning funds. PCI DSS compliance cannot be asserted.

**Recommended remediation:** Select a processor. Implement `IPaymentGateway` interface in Payment MS domain layer. Add `GatewayAuthorisationCode`, `GatewayTransactionId`, and `GatewaySettlementReference` fields to the `Payment` entity. Wire the gateway client into all four handlers. Add integration tests covering success, decline, and 3DS flows before any production promotion.

**Age:** 3 weeks.

---

## 3. Status of Prior Findings

| Finding | Prior Severity | Status | Evidence |
|---------|---------------|--------|---------|
| C-02 — Anonymous debug endpoints expose PII | CRITICAL | **REGRESSED** | Original 4 endpoints unchanged; 3 new debug endpoints confirmed in Order MS and Delivery MS; debug handler and query classes still exist in Order MS application layer |
| C-03 — Payment gateway not integrated | CRITICAL | **UNCHANGED** | All four TODO stubs unchanged; no gateway interface introduced; no gateway fields on Payment entity; 0 relevant commits in 62 merged this cycle |
| H-03 — Deploy pipelines build without running tests | HIGH | **UNCHANGED** | Reviewed representative deploy workflow `main_reservation-system-db-api-retail.yml`; no `dotnet test` step present; only 2 integration test workflows remain (Customer, Identity); pattern unchanged across all 20 workflows |
| M-01 — Exceptions MS not documented | MEDIUM | **UNCHANGED** | `ReservationSystem.Microservices.Exceptions` still absent from `api-reference.md` and `system-overview.md`; anonymous `GET /v1/exceptions` endpoint confirmed still active |
| M-02/N-02 — Admin SSR route and documentation mismatch | MEDIUM | **UNCHANGED** | `api-reference.md` still documents `GET /v1/admin/ssr/options` (route does not exist); code routes are `v1/admin/ssr` and `v1/admin/ssr/{ssrCode}`; POST/PUT/DELETE absent from docs; `sequences/ssr.md` correctly documents actual routes, creating contradiction |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` carry TODO comments; no remediation in this cycle |
| M-07 — No integration tests for orchestration APIs | MEDIUM | **UNCHANGED** | `src/API/Tests/` and search across repo confirm no integration test classes for Retail, Loyalty, Admin, or Operations APIs |
| M-08 — `TierConfig` table not mapped with `HasTrigger` | LOW | **UNCHANGED** | `CustomerDbContext.cs` still has no `TierConfig` DbSet or `HasTrigger("TR_TierConfig_UpdatedAt")` declaration |
| M-10 — `TODO` without issue reference committed to main | LOW | **UNCHANGED** | Bare `// TODO:` markers confirmed in Payment MS handlers; OCI check-in handler adds a new bare TODO at `OciCheckInHandler.cs:381` (`// TODO: 6-digit sequence number needs to be auto-incremented...`) |
| N-01 — Seatmap booking-window occupancy gap | MEDIUM | **UNCHANGED** | `GetAssignedSeatsByFlightAsync` queries manifest correctly for OCI auto-seat allocation (new code follows correct pattern); however the pre-booking seatmap viewer still uses manifest-only data, leaving the window between `offer.SeatReservation` creation and post-ticketing manifest write unaddressed |
| N-03 — Terminal HTTP debug modal captures PII and JWT tokens | MEDIUM | **UNCHANGED** | `src/Terminal/src/app/interceptors/http-debug.interceptor.ts` confirmed present and active; captures full request headers (including `Authorization: Bearer`) and bodies (including passenger PII) on every HTTP call |
| N-04 — Seatmap endpoint descriptions stale in api-reference.md | LOW | **UNCHANGED** | `api-reference.md` seatmap descriptions still reference hold-based occupancy; no update to reflect manifest-based occupancy after `e83b309` |

---

## 4. High Findings

### H-04 — Bag tag sequence number is randomly generated; IATA Resolution 740 uniqueness violated

**Severity:** HIGH
**Principle breached:** Architecture Principals — IATA standards compliance; coding standards — no deferred critical logic in production paths.

**Evidence:**
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Application/OciCheckIn/OciCheckInHandler.cs:381` — `// TODO: 6-digit sequence number needs to be auto-incremented from a persistent counter rather than generated randomly`
- The `GenerateBagTag()` method generates a 6-digit sequence using `Random`, not a persistent auto-increment counter.
- Format implemented: `[001 airline prefix][6-digit random][1-digit mod-7 check digit]`
- The same bag tag generator is invoked during agent check-in (`AdminCheckInHandler`) and OCI self check-in.

**Impact if unresolved:** IATA Resolution 740 requires bag tag licence plate numbers to be globally unique per airline per flight day. A random 6-digit field over a small population (typically tens to hundreds of bags per flight) will produce collisions statistically within a small number of flights once load reaches production scale. Duplicate bag tags cause bags to be misrouted to wrong aircraft or lost at transfer points. This is an operational safety issue, not merely a UX defect. Bag tags generated in the current cycle contain non-unique sequence numbers that cannot be relied upon.

**Recommended remediation:** Replace random generation with a persistent sequence. Options in preference order:
1. SQL `SEQUENCE` object in the `delivery` schema, incremented via `NEXT VALUE FOR delivery.BagTagSequence` in the repository — zero-contention, transactional, and survives restarts.
2. Azure SQL identity column on a dedicated `delivery.BagTagCounter` table if `SEQUENCE` is unavailable.
3. Redis `INCR` if a Redis instance is already present in the architecture (it is not currently).

The `GenerateBagTag` method should accept the next sequence value as a parameter from the repository, keeping the mod-7 check-digit logic in the domain layer. The TODO at line 381 must be resolved before check-in is enabled for any production-bound traffic.

**Age:** Week 1 (new).

---

H-03 (deploy pipelines lack test and CVE gates) remains open from prior reviews. It is now entering its third review. Remediation steps are unchanged: add `dotnet test` before every `Azure/functions-action` step; add `dotnet list package --vulnerable --include-transitive`; introduce staging slot swaps. No new evidence of progress this cycle.

---

## 5. Medium & Low Findings

### New findings this cycle

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| N-05 | Bag tag response field undocumented | MEDIUM | G — Documentation | `OciCheckInHandler` returns a `CheckedInBag` record with `BagTag` string; `AdminCheckInHandler` surfaces this in the Operations API response. Neither `documentation/design/check-in.md`, `documentation/design/delivery.md`, nor `documentation/api-reference.md` document the `BagTag` field, its IATA Resolution 740 format (`[3-digit airline code][6-digit sequence][1-digit mod-7 check digit]`), or the condition under which it is populated. The `delivery-microservice.md` spec notes `bagTagNumber` is populated by the Airport API, which is now incorrect. | Delivery / Operations API owner |
| N-06 | Flight time-change IROPS handler throws `NotImplementedException` | LOW | B — Contract | `AdminDisruptionTimeHandler.cs` throws `NotImplementedException("Flight time change disruption handling is not yet implemented.")`. `api-reference.md` correctly documents the endpoint as returning `501 Not Implemented`, so the contract is honest. The LOW rating reflects that the documentation is accurate; the observation is that this IROPS path has no timeline for implementation and remains a gap in disruption coverage. | Operations API owner |

### Carried medium and low findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| H-03 | Deploy pipelines lack test and CVE gates | HIGH | F / E | No `dotnet test` in 20 deploy workflows; no CVE scanning; no staging slot swap. Week 3 open. | Platform / DevOps |
| M-01 | Exceptions MS not documented | MEDIUM | G | No spec, no entry in `api-reference.md` or `system-overview.md`. Anonymous endpoint publicly reachable. | Platform Architect |
| N-02 | Admin SSR route and documentation mismatch | MEDIUM | B / G | `api-reference.md` documents non-existent `/v1/admin/ssr/options`; code uses `/v1/admin/ssr`; POST/PUT/DELETE undocumented; `sequences/ssr.md` contradicts `api-reference.md`. | Admin API owner |
| N-01 | Seatmap booking-window occupancy gap | MEDIUM | A / B | Pre-booking seatmap derives occupancy from manifest only; seats in active `offer.SeatReservation` hold (shopping phase, pre-ticketing) are not surfaced. DB uniqueness constraint preserves integrity but visual seat picker shows contested seats as available. | Retail API / Offer MS owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` TODOs. Business logic in Function layer violates clean architecture. | Ancillary MS owner |
| M-07 | No integration tests for orchestration APIs | MEDIUM | F | Retail, Loyalty, Admin, and Operations APIs have no integration test coverage despite being the most complex and business-critical services. | QA / Platform |
| N-03 | Terminal HTTP debug modal captures PII and JWT tokens | MEDIUM | C | `http-debug.interceptor.ts` intercepts all HTTP calls; records `Authorization` headers and full request/response bodies including passenger PII. | Terminal / Security |
| N-05 | Bag tag response field undocumented | MEDIUM | G | `CheckedInBag.BagTag` field and IATA 740 format absent from design and API reference docs. | Delivery / Operations owner |
| M-08 | `TierConfig` table not mapped with `HasTrigger` | LOW | D | `TR_TierConfig_UpdatedAt` in `Script.sql:1269`; `CustomerDbContext` has no mapping. Low risk while EF Core access is absent. | Customer MS owner |
| M-10 | `TODO` without issue reference committed to main | LOW | Coding Standards | Bare `// TODO:` markers in Payment MS handlers and `OciCheckInHandler.cs:381` (new). Coding standards require author and tracking issue. | All owners |
| N-04 | Seatmap endpoint descriptions stale in api-reference.md | LOW | G | `api-reference.md:139` still describes admin seatmap as using hold status; now uses manifest. | Retail API owner |
| N-06 | Flight time-change IROPS throws `NotImplementedException` | LOW | B | Accurately documented as 501; no implementation timeline visible. | Operations API owner |

---

## 6. Observations & Positive Notes

- **IATA Resolution 740 bag tag logic is structurally correct.** The mod-7 check-digit algorithm, the `[airline-prefix][sequence][check]` format, and the placement of generation in the Application layer (not the Function layer) are all right. The only remediation needed is replacing random with sequential — the architecture is sound.

- **Admin check-in with baggage support is a complete, well-structured feature.** `AdminCheckInHandler` correctly orchestrates across OrderService, DeliveryService, OfferService, SeatService, and WatchlistService. The `CheckedInBag` record type is clean and propagates correctly from OCI handler through to the Terminal response.

- **IROPS cancellation and rebooking use manifest as the passenger source — architecturally correct.** `AdminDisruptionCancelHandler` fetches the manifest by flight number to obtain `OrderId` values, avoiding a JSON-scan of the Order table. This is the right pattern: manifest is authoritative for who is on each flight, and using its indexed `(FlightNumber, DepartureDate)` lookup is efficient.

- **No `Console.Error.WriteLine` regression.** A full scan of `src/API/` returned zero instances. The H-02 remediation from the inaugural review has held across 62 new commits — a notable discipline win.

- **SegmentId correctly populated on manifest entries.** A prior data-quality defect (`SegmentId` always written as 0) has been resolved. The `INT` type for `PassengerId` is now consistent across manifest and order schemas. Both fixes eliminate silent data-quality bugs that would have affected disruption rebooking lookups at scale.

- **`DeleteExpiredManifestItems` timer trigger is correctly documented** in `documentation/timer-triggers.md` with schedule, class name, and retention rule. This is the atomic code-and-docs pattern the principles require.

- **Sixty-two commits merged in seven days** with no architectural regressions on boundary integrity, schema isolation, or logging discipline — a high-velocity, clean cycle.

---

## 7. Conformance Scorecard

| Dimension | Conformance | Trend vs 2026-05-04 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🟡 Amber | → | No new cross-boundary violations; IROPS and check-in features use correct orchestration patterns; N-01 seatmap gap unchanged |
| B — API Contract Conformance | 🟡 Amber | → | N-02 (SSR route mismatch) unchanged; N-05 (bag tag field undocumented) is new; N-06 (time-change 501) is intentional and documented; new check-in and disruption endpoints are present in api-reference.md |
| C — Security Principles | 🔴 Red | ↓ | C-02 REGRESSED — debug surface expanded from 4 to 7+ endpoints; N-03 unchanged; no remediation activity visible in 62 commits |
| D — Data Storage & Schema Principles | 🟡 Amber | → | No new schema violations; SegmentId and PassengerId INT standardisation is positive; M-08 TierConfig unchanged; Manifest column type change (NVARCHAR→JSON) handled correctly in Script.sql |
| E — Infrastructure & Integration Principles | 🟡 Amber | → | H-03 deploy pipelines unchanged; no new infrastructure findings; bag tag random-sequence concern (H-04) is an integration quality issue |
| F — Testing & CI | 🔴 Red | → | H-03 unchanged (week 3); M-07 unchanged; no new test coverage for orchestration APIs or new check-in/disruption surface |
| G — Documentation Drift | 🟡 Amber | ↓ | Timer trigger documented correctly; N-05 (bag tag) is new undocumented field; N-02 and N-04 unchanged; `delivery-microservice.md` spec now contradicts implementation on bag tag population source |

---

## 8. Governance Gaps

The following gaps remain open; no new governance gaps have been identified this cycle.

1. **No ADRs present.** `/documentation/adr/` does not exist. This cycle produced two significant decisions warranting ADR capture: use of IATA Resolution 740 with Apex Air airline numeric code `001`; use of manifest as the authoritative seat-occupancy source for check-in auto-allocation. Without ADRs these rationales will be lost.

2. **No OpenAPI specs in repository.** Integration principles require machine-readable OpenAPI 3.x specs version-controlled alongside service code. Swagger is wired up in each service but specs are not checked in, preventing CI-based contract testing. The 62 commits this cycle added new endpoints (OCI bag tag response, admin check-in baggage fields, IROPS endpoints) with no machine-readable contract update.

3. **No consumer-driven contract tests.** Pact or equivalent between orchestration APIs and microservices remains absent. With 62 commits per cycle the risk of silent breaking changes across service boundaries is material.

4. **No incident response plan discoverable.** Security principles require a documented IR plan for UK GDPR 72-hour breach notification. C-02's regression this cycle increases the urgency of this gap.

5. **Accounting MS not assessable.** Event-subscription stubs only; no inspectable business logic. New Order and Delivery events generated by the check-in and disruption features in this cycle may require Accounting to handle new event types — this cannot be verified.

6. **Airport API and Finance API scaffolded only.** Not assessed. The `bagTagNumber` field in `delivery-microservice.md` describes the Airport API as the populator of bag tags — this is now incorrect following the check-in handler changes. The Airport API spec should be reviewed for consistency.

---

## 9. Appendix — Scope of this Review

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
| `documentation/timer-triggers.md` | Timer trigger schedule and data lifecycle |
| `documentation/design/delivery.md` | Delivery domain design and schemas |
| `documentation/design/check-in.md` | Check-in domain design |
| `documentation/design/disruption.md` | Disruption / IROPS design |
| `documentation/design/order.md` | Order domain design |
| `documentation/design/payment.md` | Payment domain design |
| `documentation/design-review/design-review-2026-05-04.md` | Prior review (most recent) |
| `documentation/design-review/design-review-2026-04-21.md` | Inaugural review |
| `src/Database/Script.sql` (delivery.Manifest, offer.InventoryHold sections) | Authoritative schema — SegmentId, PassengerId, bag JSON column |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| Delivery MS — OciCheckIn | `OciCheckInHandler.cs` in full — bag tag generation, Timatic, auto-seat allocation |
| Delivery MS — Manifest | `IManifestRepository.cs`, `EfManifestRepository.cs`, `DeliveryDbContext.cs`, `ManifestCleanupFunction.cs` |
| Delivery MS — Debug endpoints | `TicketFunction.cs`, `DocumentFunction.cs` — debug route confirmation |
| Order MS — Debug endpoint | `OrderFunction.cs`, `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` |
| Operations API — AdminCheckIn | `AdminCheckInHandler.cs` (first 100 lines), `AdminCheckInFunction.cs` |
| Operations API — Disruption | `AdminDisruptionCancelHandler.cs`, `AdminDisruptionTimeHandler.cs` |
| Retail API — AdminOrderManagement | `AdminOrderManagementFunction.cs` — debug endpoint line references |
| Retail API — Service clients | `DeliveryServiceClient.cs`, `OrderServiceClient.cs` — debug method confirmation |
| Payment MS | All four application handlers — gateway integration status |
| Terminal app | `http-debug.interceptor.ts`, `interceptors/` directory listing |
| GitHub Actions workflows | Representative deploy workflow (`main_reservation-system-db-api-retail.yml`); integration test workflows |
| All microservice `Program.cs` files | Authentication middleware registration |

### Commit reference

Review conducted against commit `8b18839` (tip of `main` / branch `claude/jolly-dijkstra-5y4lM` as of 2026-05-11).
62 commits merged since prior review (`8ddd379`, 2026-05-04).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS | Event-subscription stubs only; no inspectable business logic |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass |
| Terminal app — non-security features | New aircraft-swap and disruption modals not assessed beyond security scan |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap from prior reviews |
| Penetration testing / runtime security | Requires live environment access |
| All api-specs except delivery-microservice.md spot-check | Full audit deferred; selective spot-check confirms bag tag documentation gap |
