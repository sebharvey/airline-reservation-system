# Apex Air — Weekly Design Review

**Date:** 2026-05-04
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-04-21.md

---

## 1. Executive Summary

The platform is **Non-conforming** for the second consecutive review. Significant progress was made this cycle: the most damaging prior finding (C-01, missing manifest implementation) has been fully resolved, and four other findings (H-01, H-02, M-04, M-06) have also been closed. Despite this, the two remaining critical findings — anonymous endpoints exposing PII and internal data (C-02) and a payment path that logs state transitions but never moves money (C-03) — are now in their second review without remediation. C-02 constitutes a live UK GDPR exposure and warrants the same urgency this week as when it was first raised. Three new medium findings have been identified: a booking-window gap in seat occupancy caused by the seatmap migration to manifest-only data; a route mismatch between the Admin SSR implementation and its API contract; and a Terminal app debug modal that aggregates passenger PII and staff JWTs in the browser. C-02 deletion is the single most important action this week.

---

## 2. Critical Findings (act this week)

### C-02 — Anonymous endpoints expose internal exception data and raw database records

**Severity:** CRITICAL (Week 2 open — approaching CRITICAL AGEING)
**Principle breached:** Security Principals — authentication required at every ingress; PII must never appear in API responses; secrets and internal data must never be externally reachable.

**Evidence (UNCHANGED from inaugural review):**
- `src/API/Microservices/ReservationSystem.Microservices.Exceptions/Functions/ExceptionsFunction.cs:34` — `[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/exceptions")]` — publicly exposes Application Insights exception records including full stack traces, internal class names, and potentially PII-bearing exception messages.
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Functions/AdminOrderManagementFunction.cs:368` — `[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/orders/{bookingRef}/debug")]` — exposes raw Order rows to any caller with a booking reference.
- `AdminOrderManagementFunction.cs:392,415` — two further anonymous debug endpoints returning raw Ticket and Document rows.
- All four endpoints still carry `// TODO: Remove — temporary debug endpoint` comments. No remediation activity is evident in the 135 commits merged since the prior review.

**Impact if unresolved:** External parties can extract passenger names, itineraries, payment references, and travel documents for any booking reference. Stack traces expose internal service topology and class names. This is a reportable UK GDPR incident if access logs show external reads against these endpoints.

**Recommended remediation:** Delete all four functions and their corresponding debug client methods (`GetTicketsDebugRawAsync`, `GetDocumentsDebugRawAsync`, `GetOrderDebugRawAsync`). Remove or restrict the Exceptions MS behind `TerminalAuthenticationMiddleware`. Assess access logs immediately for evidence of external access.

**Age:** 2 weeks. Will escalate to CRITICAL AGEING at week 4 if unresolved.

---

### C-03 — Payment gateway not integrated; payment state recorded but money not moved

**Severity:** CRITICAL (Week 2 open — approaching CRITICAL AGEING)
**Principle breached:** Architecture Principals — price integrity and stored-offer pattern require that confirmed orders result in real payment settlement.

**Evidence (UNCHANGED from inaugural review):**
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/AuthorisePayment/AuthorisePaymentHandler.cs:92` — `// TODO: Call payment gateway (e.g. Adyen, Stripe, Worldpay) to authorise the card.`
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/SettlePayment/SettlePaymentHandler.cs:60` — `// TODO: Call payment gateway to capture / settle the authorised funds.`
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/VoidPayment/VoidPaymentHandler.cs:52` — `// TODO: Call payment gateway to void / reverse the authorisation hold.`
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/RefundPayment/RefundPaymentHandler.cs:57` — `// TODO: Call payment gateway to process the refund against the original transaction.`

No `IPaymentGatewayClient` interface has been introduced. No processor dependency has been added to the Payment MS `Program.cs`. No commits in this cycle touched payment gateway integration.

**Impact if unresolved:** Revenue loss on every booking confirmed since deployment. Refunds are logged without reversing charges. PCI DSS compliance cannot be asserted.

**Recommended remediation:** Select a processor. Implement `IPaymentGatewayClient` interface in Payment MS and wire it into all four handlers. Add integration tests covering success, decline, and 3DS flows before promoting to production.

**Age:** 2 weeks. Will escalate to CRITICAL AGEING at week 4 if unresolved.

---

## 3. Status of Prior Findings

| Finding | Prior Severity | Status | Evidence |
|---------|---------------|--------|---------|
| C-01 — Flight manifest not implemented in Delivery MS | CRITICAL | **RESOLVED** | All 6 manifest handlers present; `IManifestRepository`/`EfManifestRepository` implemented; `HasTrigger("TR_Manifest_UpdatedAt")` declared (`DeliveryDbContext.cs:78`); `ConfirmBasketHandler.cs:145` calls `RunManifestWriteAsync` after ticketing |
| C-02 — Anonymous endpoints expose PII and exception data | CRITICAL | **UNCHANGED** | All four anonymous debug functions still present; no remediation commits found |
| C-03 — Payment gateway not integrated | CRITICAL | **UNCHANGED** | All four TODO stubs unchanged; no gateway client introduced |
| H-01 — Retail API missing `x-functions-key` on four clients | HIGH | **RESOLVED** | All seven Retail API clients now add `MicroserviceHostKey` header (`Retail/Program.cs:57–107`); shared key model is consistent with api-reference.md note |
| H-02 — Pervasive `Console.Error.WriteLine` in Retail API | HIGH | **RESOLVED** | Zero `Console.Error.WriteLine` / `Console.Write` instances remain in Retail orchestration layer |
| H-03 — Deploy pipelines build without running tests | HIGH | **UNCHANGED** | No `dotnet test` step in any of the 20 deploy workflows; integration test jobs remain separate optional triggers not gating deployment |
| M-01 — Exceptions MS not documented | MEDIUM | **UNCHANGED** | `ReservationSystem.Microservices.Exceptions` still absent from `api-reference.md` and `system-overview.md` |
| M-02 — Admin SSR endpoints undocumented | MEDIUM | **PARTIALLY RESOLVED** | `api-reference.md:89` now documents `GET /v1/admin/ssr/options`; however code route is `v1/admin/ssr` (no `/options` suffix); POST/PUT/DELETE remain undocumented; see new finding N-02 |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` still carry TODO comments deferring logic |
| M-04 — Missing `HasTrigger("TR_Manifest_UpdatedAt")` | MEDIUM | **RESOLVED** | `DeliveryDbContext.cs:76–80` correctly declares `HasTrigger("TR_Manifest_UpdatedAt")` inside `ToTable` builder |
| M-05 — Notes field added to OrderData without documentation | MEDIUM | **RESOLVED** | `documentation/design/order.md:555–568` now contains a "Notes schema" section with full field definitions and constraints |
| M-06 — Production Azure URLs hardcoded as defaults | MEDIUM | **RESOLVED** | No `azurewebsites.net` or `azure.com` literals found in any orchestration API source tree |
| M-07 — No integration tests for orchestration APIs | MEDIUM | **UNCHANGED** | No test classes referencing Retail, Loyalty, Admin, or Operations API flows found under `src/API/Tests/` |
| M-08 — `TierConfig` table not mapped with `HasTrigger` | LOW | **UNCHANGED** | `CustomerDbContext.cs` has no `TierConfig` DbSet or `HasTrigger("TR_TierConfig_UpdatedAt")` declaration; risk remains if EF Core access is added without this declaration |
| M-09 — `double` in `FarePricer.ComputeOccupancyRatio` | LOW | **RESOLVED** | `FarePricer.cs:15` now returns `decimal`; both `ComputeOccupancyRatio` and `ComputeDynamicPrice` are fully decimal-typed |
| M-10 — TODO comments without issue reference | LOW | **UNCHANGED** | Bare `// TODO:` markers remain in Payment MS handlers and elsewhere; coding standards require author and tracking issue |

Resolved findings (C-01, H-01, H-02, M-04, M-05, M-06, M-09) are closed and will not appear in future reviews.

---

## 4. High Findings

No new HIGH findings this review cycle. H-03 (deploy pipeline test gates) remains open and is reproduced in the status table above. Its remediation steps are unchanged: add `dotnet test` before every deploy action; add dependency CVE scanning; introduce staging slot swaps.

---

## 5. Medium & Low Findings

### New medium findings this cycle

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| N-01 | Seatmap uses manifest for seat occupancy — booking-window gap | MEDIUM | A — Boundary / B — Contract | `SeatmapFunction.cs:91–92` and `AdminSeatmapFunction.cs:90` now derive occupied seat numbers exclusively from `delivery.Manifest` entries. During the active booking window — after `offer.SeatReservation` is written but before ticketing and manifest write — a concurrently booking customer sees the contested seat as available. This degrades UX and increases conflict errors at confirmation. The `offer.SeatReservation` table continues to enforce database-level uniqueness, so data integrity is preserved, but the visual seat picker does not reflect in-progress holds. The api-reference.md description for both seatmap endpoints is also stale (see N-04). | Retail API / Offer MS owner |
| N-02 | Admin SSR route and documentation mismatch | MEDIUM | B — Contract / G — Documentation | `SsrManagementFunction.cs` implements four endpoints on route `v1/admin/ssr[/{ssrCode}]`; `api-reference.md:89` documents only `GET /v1/admin/ssr/options` — a route that does not exist in the code. The `/options` suffix is the public Retail API convention; the Admin API uses the bare `/ssr` path. POST/PUT/DELETE operations remain entirely absent from `api-reference.md`. Neither channel nor consumer can rely on the documented contract. | Admin API / Platform Architect |
| N-03 | Terminal HTTP Debug Modal captures PII and JWT tokens | MEDIUM | C — Security | `src/Terminal/src/app/interceptors/http-debug.interceptor.ts` intercepts every HTTP call in the Terminal app and records full request headers (including `Authorization: Bearer <JWT>`) and request/response bodies (including passenger names, passport numbers, and order PII) in-memory. This data is displayed in the API Debug Modal. Staff JWTs are short-lived (15 min) but their aggregation in a debug panel creates a screenshot/sharing risk. Security principle requires PII not to appear outside controlled application flows. If this panel is retained for production, it must filter out Authorization headers and scrub PII fields from bodies before display. | Terminal / Security |

### Carried medium and low findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| H-03 | Deploy pipelines lack test and CVE gates | HIGH | F — Testing / E — Infrastructure | No `dotnet test` step in any of the 20 deploy workflows; no security scanning gate; no staging slot swap before production. Full details and remediation unchanged from prior review. | Platform / DevOps |
| M-01 | Exceptions MS not documented | MEDIUM | G — Documentation | No entry in `api-reference.md`, `system-overview.md`, or any design doc. Governance without a spec is not possible. | Platform Architect |
| M-02 | Admin SSR — POST/PUT/DELETE still undocumented | MEDIUM | B / G | POST/PUT/DELETE `v1/admin/ssr` present in code, absent from docs. Partially subsumed by N-02. | Admin API owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A — Architecture | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` carry TODO comments placing business logic in the Function layer. Business logic belongs in Application handlers. | Ancillary MS owner |
| M-07 | No integration tests for orchestration APIs | MEDIUM | F — Testing | Retail, Loyalty, Admin, and Operations API — the most complex and business-critical services — have no integration tests. Test suites exist for eight microservices but not for the orchestration layer. | QA / Platform |
| M-08 | `TierConfig` table not mapped with `HasTrigger` | LOW | D — Data | `TR_TierConfig_UpdatedAt` defined in `Script.sql:1269`. CustomerDbContext has no `TierConfig` DbSet or `HasTrigger` declaration. Low risk while access remains via raw SQL; risk becomes concrete if EF Core access is added. | Customer MS owner |
| M-10 | `TODO` without issue reference committed to main | LOW | Coding Standards | Bare `// TODO:` markers remain in Payment MS and elsewhere. Coding standards require author and tracking issue reference. | All owners |
| N-04 | Seatmap endpoint descriptions stale in api-reference.md | LOW | G — Documentation | `api-reference.md:139` describes the admin seatmap as "Merges physical layout, seat pricing, and **hold status**." After commit `e83b309`, occupancy now derives from manifest, not inventory holds. Both seatmap entries should be updated. | Retail API owner |

---

## 6. Observations & Positive Notes

- **C-01 (manifest implementation) is a substantial resolution.** Implementing all six manifest endpoints, the domain entity, repository, DbContext mapping, HasTrigger declaration, and integrating the write call into `ConfirmBasketHandler` represents a large body of work done correctly. The OCI and IROPS paths that depended on this are now unblocked.

- **H-01 and H-02 resolved cleanly.** The Retail API's four missing host-key headers have been added uniformly. The `Console.Error.WriteLine` violations in critical booking paths have been fully removed. Both are clean and complete remediations.

- **M-05 resolved with substance.** The Notes schema documentation in `design/order.md` is detailed and correct — field definitions, constraints, mutability rules, and the system-generated notes pattern are all covered.

- **M-06 resolved.** Hardcoded production URLs eliminated from all orchestration API `Program.cs` files — removes the misconfigured-environment routing risk entirely.

- **FarePricer decimal hygiene (M-09) resolved.** `ComputeOccupancyRatio` now returns `decimal` throughout, removing the floating-point contamination of fare pricing inputs.

- **Manifest cleanup timer trigger added** (`15948a5` — daily cleanup via `DeleteExpiredManifestItemsHandler`). This follows the data lifecycle principle for automated purge jobs and is the right pattern.

- **135 commits merged since last review** demonstrates active development velocity. The volume of resolved findings from a single review cycle is encouraging; the same capacity should now be directed at C-02 and C-03.

- **retail-api.md updated** for the `segments[].flights[]` slice response shape in the same commit that changed the implementation (`6b74a82`). Atomic code-and-docs commits are the correct pattern.

---

## 7. Conformance Scorecard

| Dimension | Conformance | Trend vs 2026-04-21 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🟡 Amber | ↑ | C-01 resolved; seatmap now merges manifest from Delivery MS (architecturally sound orchestration but introduces booking-window occupancy gap — N-01) |
| B — API Contract Conformance | 🟡 Amber | ↑ | Manifest endpoints implemented; Admin SSR route mismatch (N-02) is new; slice response shape documented in api-specs; overall improving |
| C — Security Principles | 🔴 Red | → | C-02 anonymous endpoints unchanged; Terminal debug modal (N-03) is new; H-01 resolved is a positive but insufficient to lift to Amber |
| D — Data Storage & Schema Principles | 🟡 Amber | → | M-04 and M-05 resolved; M-08 TierConfig unchanged; InventoryHold schema restructure handled via idempotent ALTER TABLE in Script.sql (pragmatic but departs from migration pipeline principle) |
| E — Infrastructure & Integration Principles | 🟡 Amber | → | H-03 (deploy pipelines) unchanged; M-06 resolved; no new infrastructure findings |
| F — Testing & CI | 🔴 Red | → | H-03 unchanged; M-07 unchanged; no new test coverage for orchestration APIs |
| G — Documentation Drift | 🟡 Amber | ↑ | M-05 and M-09 resolved; N-04 (seatmap descriptions stale) and N-02 (SSR route mismatch) are new; retail-api.md updated atomically with slice change is positive |

---

## 8. Governance Gaps

The following gaps from the inaugural review remain open; no new governance gaps have been identified this cycle.

1. **No ADRs present.** `/documentation/adr/` contains no files. Significant decisions made in this cycle — using manifest as the source of truth for seatmap seat occupancy; shared-key model for microservice authentication — should be captured as ADRs before their rationale is lost.

2. **No OpenAPI specs in repository.** Integration principles require machine-readable OpenAPI 3.x specs version-controlled alongside service code. Swagger is wired up in each service but specs are not checked in; CI-based contract testing is not possible without them.

3. **No consumer-driven contract tests.** Architecture principles require Pact (or equivalent) contract tests between orchestration APIs and microservices. None exist. With 135 commits per review cycle, the absence of automated contract verification increases the probability of silent breaking changes.

4. **No incident response plan discoverable.** Security principles require a documented IR plan addressing the UK GDPR 72-hour breach notification obligation. C-02 is a candidate incident. No plan has been found in the repository.

5. **Accounting MS not assessable.** Event-subscription stubs only; no inspectable business logic. Cannot be assessed for conformance.

6. **Airport API and Finance API scaffolded only.** Not assessed.

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
| `documentation/system-overview.md` | Domain model and architecture |
| `documentation/api-reference.md` | Full endpoint catalogue |
| `documentation/api-specs/retail-api.md` | Retail API detailed specification |
| `documentation/design-review/design-review-2026-04-21.md` | Prior review (only review in history) |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| Delivery MS | All Application handlers, Domain entities, Infrastructure/Persistence, DbContext |
| Payment MS | All Application handlers |
| Retail API (Orchestration) | `Program.cs`, `ConfirmBasketHandler.cs`, `SeatmapFunction.cs`, `AdminSeatmapFunction.cs`, `AdminOrderManagementFunction.cs`, `AdminManifestFunction.cs` |
| Admin API (Orchestration) | `SsrManagementFunction.cs` |
| Operations API (Orchestration) | `DeliveryServiceClient.cs` |
| Offer MS | `FarePricer.cs`, `IOfferRepository.cs`, `SqlOfferRepository.cs`, `OfferFunction.cs`, `GetSeatAvailabilityHandler.cs` |
| Customer MS | `CustomerDbContext.cs` |
| Exceptions MS | `ExceptionsFunction.cs` |
| Terminal app | `http-debug.interceptor.ts`, `http-debug.service.ts` |
| GitHub Actions workflows | All 20 workflows |
| Database schema | `src/Database/Script.sql` — `offer.InventoryHold`, `delivery.Manifest` |

### Commit reference

Review conducted against commit `8ddd379` (tip of `main` as of 2026-05-04).
135 commits merged since prior review (`71593b7`, 2026-04-21).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS event subscriptions | Stub implementation only |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap from prior review |
| Penetration testing / runtime security | Requires live environment access |
| All api-specs except retail-api.md | Selected for spot-check; full audit deferred |
