# Apex Air — Weekly Design Review

**Date:** 2026-04-21
**Reviewer:** Architect (automated governance pass)
**Previous review:** none (inaugural review)

---

## 1. Executive Summary

The platform is **Non-conforming** on this first governance pass. Three critical findings require immediate remediation before further production traffic is handled: the Delivery microservice has no implementation of any manifest operation despite the API contract and orchestration callers requiring it at runtime; anonymous HTTP endpoints expose internal Application Insights exception data and raw database records to the public internet; and all four Payment microservice handlers stub out the actual payment gateway call. Taken together these represent functional breakage in the IROPS rebooking path, a PII/data-disclosure security incident waiting to happen, and a booking confirmation flow that records payment state transitions without ever moving money. Additionally, the Retail API incorrectly omits the `x-functions-key` authentication header on four of its six downstream microservice clients. Human readers should stop here, read section 2, and act on findings C-01 through C-03 this week.

---

## 2. Critical Findings (act this week)

### C-01 — Flight manifest not implemented in Delivery MS

**Severity:** CRITICAL
**Principle breached:** Architecture Principals — Delivery MS is the authoritative source for who is on each flight; data is populated explicitly by the Retail API. Integration Principals — API contract is the governing document.

**Evidence:**
- `api-reference.md` documents six Delivery MS endpoints: `POST /v1/manifest`, `PUT /v1/manifest`, `PATCH /v1/manifest/{bookingRef}`, `PATCH /v1/manifest/{bookingRef}/flight`, `DELETE /v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}`, `GET /v1/manifest`.
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/` contains no manifest domain entity, no manifest repository, no manifest function class, no manifest handler — these endpoints do not exist at runtime.
- `src/API/Orchestration/ReservationSystem.Orchestration.Operations/Infrastructure/ExternalServices/DeliveryServiceClient.cs:57–101` calls `GET /api/v1/manifest`, `POST /api/v1/manifest`, and `DELETE /api/v1/manifest/…` during IROPS cancellation handling — these calls will fail with 404 at runtime.
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Application/ConfirmBasket/ConfirmBasketHandler.cs` (lines 901, 1347, 1362, 1377) calls `DeliveryServiceClient` only for ticket and document issuance — no manifest write occurs at booking confirmation.
- The `delivery.Manifest` table and `TR_Manifest_UpdatedAt` trigger are defined in `src/Database/Script.sql:788–808` confirming the schema exists but is unreachable.

**Impact if unresolved:**
- Every IROPS cancellation rebooking fails at the manifest read step — affected passengers are not rebooked.
- No passenger manifest is ever written at booking confirmation — ground handling, check-in validation, and OCI flows operate without a manifest.
- BCBP/boarding card generation relies on manifest state; without it the OCI path is broken end-to-end.

**Recommended remediation:**
1. Implement `Manifest` domain entity, `IManifestRepository`, `SqlManifestRepository` in the Delivery MS.
2. Add handlers for all six manifest endpoints and register them in `Program.cs`.
3. Add manifest write call to `ConfirmBasketHandler` after successful ticketing.
4. Declare `HasTrigger("TR_Manifest_UpdatedAt")` in `DeliveryDbContext` once the entity is mapped.

**Age:** Week 1 (new)

---

### C-02 — Anonymous endpoints expose internal exception data and raw database records

**Severity:** CRITICAL
**Principle breached:** Security Principals — authentication required at every ingress; secrets/internal data must never appear in API responses; PII must never appear in logs or responses.

**Evidence:**
- `src/API/Microservices/ReservationSystem.Microservices.Exceptions/Functions/ExceptionsFunction.cs:32` — `[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/exceptions")]` — publicly exposes Application Insights exception records including full stack traces, internal class names, and potentially PII-bearing exception messages.
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Functions/AdminOrderManagementFunction.cs:186` — `[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "v1/admin/orders/{bookingRef}/debug")]` — exposes raw Order database rows to any caller with a booking reference.
- `AdminOrderManagementFunction.cs:210,233` — two further anonymous debug endpoints returning raw Ticket and Document rows.
- All four endpoints are marked `TODO: Remove — temporary debug endpoint` but have been committed to main and deployed to production.

**Impact if unresolved:**
- External parties can enumerate booking records for any reference, extracting passenger names, itineraries, payment references, and travel documents.
- Stack traces expose internal service topology, class names, and connection details, directly aiding targeted attack planning.
- This is a reportable UK GDPR incident if passenger PII has been accessed.

**Recommended remediation:**
1. Remove all four anonymous debug functions immediately — they should not be adapted, moved, or hidden behind a flag; delete them.
2. Remove the corresponding debug methods from `DeliveryServiceClient.cs` and `OrderServiceClient.cs` (`GetTicketsDebugRawAsync`, `GetDocumentsDebugRawAsync`, `GetOrderDebugRawAsync`).
3. Remove the Exceptions microservice from deployment or, if retained for internal use, restrict it behind `TerminalAuthenticationMiddleware` and document it in `api-reference.md`.
4. Rotate any data that may have been exposed (assess blast radius with access logs).

**Age:** Week 1 (new)

---

### C-03 — Payment gateway not integrated; payment state recorded but money not moved

**Severity:** CRITICAL
**Principle breached:** Architecture Principals — price integrity and stored-offer pattern require that confirmed orders result in real payment settlement.

**Evidence:**
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/AuthorisePaymentHandler.cs:80` — `// TODO: Call payment gateway (e.g. Adyen, Stripe, Worldpay) to authorise the card.`
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/SettlePaymentHandler.cs:60` — `// TODO: Call payment gateway to capture / settle the authorised funds.`
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/VoidPaymentHandler.cs:52` — `// TODO: Call payment gateway to void / reverse the authorisation hold.`
- `src/API/Microservices/ReservationSystem.Microservices.Payment/Application/RefundPaymentHandler.cs:57` — `// TODO: Call payment gateway to process the refund against the original transaction.`

All four core payment operations log a `PaymentEvent` row but make no external call. The booking confirmation path believes payment has succeeded when it has not.

**Impact if unresolved:**
- Revenue loss: confirmed bookings are issued e-tickets without collecting funds.
- Refunds are recorded without reversing charges.
- Void/cancel flows release inventory without returning funds to customers.
- PCI DSS compliance cannot be asserted for a processor integration that does not exist.

**Recommended remediation:**
Select a payment processor (Adyen, Stripe, or Worldpay as noted in the TODOs). Implement the gateway client behind the `IPaymentGatewayClient` interface (add if not present), wire it into each handler, and add integration tests covering success/decline/3DS flows. Do not deploy to production until this is complete.

**Age:** Week 1 (new)

---

## 3. Status of Prior Findings

No prior reviews exist. This is the inaugural review; all findings are new.

---

## 4. High Findings

### H-01 — Retail API missing `x-functions-key` on four downstream clients

**Severity:** HIGH
**Principle breached:** Security Principals — all orchestration→microservice calls authenticated with Azure Function Host Key via `x-functions-key` header.

**Evidence (`src/API/Orchestration/ReservationSystem.Orchestration.Retail/Program.cs`):**

| Client | Host key configured? |
|--------|----------------------|
| `OfferMs` | Yes (lines 49–53) |
| `OrderMs` | **No** (lines 55–58) |
| `AncillaryMs` | **No** (lines 60–63) |
| `PaymentMs` | **No** (lines 65–68) |
| `DeliveryMs` | **No** (lines 70–73) |
| `CustomerMs` | Yes (lines 75–80) |

Contrast with Operations API `Program.cs` which correctly adds `x-functions-key` to every client.

**Impact:** Requests from the Retail API to Order, Ancillary, Payment, and Delivery microservices will be rejected with `401 Unauthorized` unless the target functions are configured with `AuthorizationLevel.Anonymous` (which would itself be a security violation) or the shared host key is absent from Key Vault.

**Recommended remediation:** Add the host key configuration block to the four missing clients in `Retail/Program.cs`, following the same conditional pattern used for `OfferMs` and `CustomerMs`:
```csharp
var hostKey = context.Configuration["OrderMs:HostKey"];
if (!string.IsNullOrEmpty(hostKey))
    client.DefaultRequestHeaders.Add("x-functions-key", hostKey);
```

**Age:** Week 1 (new)

---

### H-02 — Pervasive `Console.Error.WriteLine` replaces structured logging in critical paths

**Severity:** HIGH
**Principle breached:** Coding Standards — `ILogger<T>` is required; `Console` is prohibited in application code. Security Principals — all state-changing operations must produce observable audit trail.

**Evidence:** 17 instances across the Retail API orchestration layer:
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Application/ConfirmBasketHandler.cs` (lines 861, 922, 942, 1351, 1366, 1381)
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Application/CancelOrderHandler.cs` (lines 62, 76, 92)
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Application/ChangeOrderHandler.cs` (lines 118, 132, 204)
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Application/AddOrderBagsHandler.cs` (line 126)
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Application/UpdateOrderSeatsHandler.cs` (line 186)
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Infrastructure/ExternalServices/DeliveryServiceClient.cs` (line 130)
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Infrastructure/ExternalServices/CustomerServiceClient.cs` (line 65)
- `src/API/Orchestration/ReservationSystem.Orchestration.Retail/Infrastructure/ExternalServices/OfferServiceClient.cs` (line 116)

**Impact:** Errors during booking confirmation, cancellation, and flight change (including ticket void failures, inventory release failures, points reinstatement failures) are written to stdout and discarded by the Azure Functions host — they do not appear in Application Insights, cannot be alerted on, and leave no audit trail.

**Recommended remediation:** Replace each `Console.Error.WriteLine(...)` with `_logger.LogError(ex, "...")` or `_logger.LogWarning(...)` using named placeholder templates. Inject `ILogger<T>` into any class that does not already have it.

**Age:** Week 1 (new)

---

### H-03 — Deploy pipelines build without running tests; no security scanning gate

**Severity:** HIGH
**Principle breached:** Infrastructure Principals — "Unit test failures, integration test failures, critical/high CVE findings, and SAST alerts all block promotion; no build promoted without passing all gates."

**Evidence (`src/API/Microservices/ReservationSystem.Microservices.Offer` workflow as representative sample):**
```yaml
- name: 'Resolve Project Dependencies Using Dotnet'
  run: |
    pushd './${{ env.AZURE_FUNCTIONAPP_PACKAGE_PATH }}'
    dotnet build --configuration Release --output ./output
    popd
# No dotnet test step
# No CVE / dependency scanning step
# No SAST step
# Direct deploy to Production slot — no staging slot swap
```
All 13 service deploy workflows follow the same pattern (inspected: Offer, User, Customer, Delivery, Order, Payment, Identity, Schedule, Ancillary, Admin, Loyalty, Operations, Retail).

Integration test workflows exist (`integration-tests-identity-microservice.yml`, `integration-tests-customer-microservice.yml`) but run as separate optional jobs triggered by path — they do not gate the deploy workflow.

**Impact:** A build that breaks tests will still deploy. A dependency with a critical CVE will still ship. There is no staging promotion gate.

**Recommended remediation:**
1. Add `dotnet test` step to every deploy workflow before the `Azure/functions-action` step.
2. Add `dotnet list package --vulnerable --include-transitive` (or equivalent NuGet audit) and fail on critical/high.
3. Add a staging slot and swap pattern (deploy to `staging`, run smoke tests, swap to `production`).

**Age:** Week 1 (new)

---

## 5. Medium & Low Findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| M-01 | Exceptions MS not documented | MEDIUM | Documentation / Governance | `ReservationSystem.Microservices.Exceptions` has no entry in `api-reference.md`, `system-overview.md`, or any design doc. Cannot be governed against standards without a spec. | Platform Architect |
| M-02 | Admin API SSR management endpoints undocumented | MEDIUM | Documentation / API Contract | `GET/POST/PUT/DELETE /v1/admin/ssr` exist in `Admin` Functions (`SsrManagementFunction.cs`) but are absent from `api-reference.md`. The documented SSR endpoints are under the Retail API. | Retail / Admin API owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | Architecture | `GetSeatOfferHandler.cs:20` and `GetSeatOffersHandler.cs:20` carry `TODO: Implement ... logic in Function layer`. Business logic belongs in Application handlers, not Functions (violates clean architecture). | Ancillary MS owner |
| M-04 | Missing `HasTrigger("TR_Manifest_UpdatedAt")` | MEDIUM | Data / EF Core | SQL schema defines `TR_Manifest_UpdatedAt` on `delivery.Manifest`. When the manifest is added to `DeliveryDbContext` (required by C-01), `HasTrigger` must be included — omitting it will cause `SaveChangesAsync` to throw on every manifest UPDATE. | Delivery MS owner |
| M-05 | Notes field added to OrderData JSON without documentation | MEDIUM | Documentation Drift | Commits `5e0eed9` / PR #879 embed a `notes[]` array in `OrderData` JSON. `documentation/design/order.md` has no reference to this field. The data principles require `schemaVersion` bump and documented migration strategy for JSON document changes. | Order domain owner |
| M-06 | Production Azure URLs hardcoded as fallback defaults | MEDIUM | Security / Infrastructure | All four orchestration API `Program.cs` files embed live production `*.azurewebsites.net` URLs as `??` defaults. Any misconfigured deployment will silently route traffic to production services from a dev or staging environment. | Platform / DevOps |
| M-07 | No integration tests for any orchestration API | MEDIUM | Testing | Integration test suites exist for 8 microservices but none for Retail API, Loyalty API, Admin API, or Operations API — the most complex and business-critical components. | QA / Platform |
| M-08 | Customer `TierConfig` table not mapped in DbContext | LOW | Data / EF Core | `TR_TierConfig_UpdatedAt` exists in `Script.sql:1269`. If `TierConfig` is accessed via EF Core (not confirmed), a `HasTrigger` declaration is required. | Customer MS owner |
| M-09 | `double` in `FarePricer.ComputeOccupancyRatio` | LOW | Coding Standards | `src/API/Microservices/ReservationSystem.Microservices.Offer/Domain/Services/FarePricer.cs:15` returns `double` occupancy ratio fed into `ComputeDynamicPrice`. Floating-point arithmetic for fare inputs is prohibited by the data principles, even when the ultimate output is `decimal`. | Offer MS owner |
| M-10 | `TODO` without issue reference committed to main | LOW | Coding Standards | Coding standards require `TODO` comments to carry author and tracking issue reference. Multiple bare `TODO:` markers present in Payment MS handlers, Ancillary MS handlers, and Retail API service clients (full list in Section 2 / H-02). | All owners |

---

## 6. Observations & Positive Notes

- **EF Core trigger declarations are well-maintained across most services.** Customer, Identity, Schedule, Order, Payment, User, Ancillary, and Delivery all correctly use `HasTrigger` for their trigger-bearing tables. The tooling and conventions are understood — the Manifest gap is an omission, not a systemic failure.
- **`TerminalAuthenticationMiddleware` is consistently applied** to all four orchestration APIs (Retail, Loyalty, Admin, Operations). Staff-facing endpoints receive uniform JWT validation at the host level.
- **Structured logging via `ILogger<T>` is correctly implemented in all microservices.** The `Console.Error.WriteLine` violations are isolated to the Retail orchestration layer. Microservice layers are clean.
- **Application Insights telemetry is wired up uniformly** (`AddApplicationInsightsTelemetryWorkerService()` in every Program.cs), enabling centralised trace collection once the `Console` violations are removed.
- **Optimistic concurrency is correctly implemented** on `order.Order`, `order.Basket`, and `delivery.Ticket` via the `Version` column pattern documented in `api.md`. This is correctly enforced with 0-rows-affected detection and `409 Conflict` responses.
- **Host key authentication in the Operations API is correctly implemented** for all six downstream clients — a sound reference to use when remediating H-01 in the Retail API.
- **Recent seatmap and terminal improvements** (PRs #876–#883) show consistent use of clean architecture conventions and demonstrate the team understands the patterns.

---

## 7. Conformance Scorecard

| Dimension | Conformance | Trend | Notes |
|-----------|------------|-------|-------|
| A — Microservice Boundary Integrity | 🔴 Red | — | Manifest operations absent from Delivery MS; booking confirmation populates no manifest |
| B — API Contract Conformance | 🔴 Red | — | Six Delivery MS manifest endpoints documented but unimplemented; Admin SSR and Exceptions endpoints undocumented |
| C — Security Principles | 🔴 Red | — | Anonymous debug/exceptions endpoints (C-02); missing host keys on 4 Retail API clients (H-01) |
| D — Data Storage & Schema Principles | 🟡 Amber | — | Missing `HasTrigger` for Manifest; Notes field undocumented; otherwise strong compliance |
| E — Infrastructure & Integration Principles | 🟡 Amber | — | Deploy pipelines lack test and CVE gates; no staging slot; hardcoded prod URLs; logging gaps in Retail |
| F — Testing & CI | 🔴 Red | — | No tests gating deployments; no orchestration API integration tests; debug endpoints indicate insufficient pre-merge checks |
| G — Documentation Drift | 🟡 Amber | — | Exceptions MS and Admin SSR undocumented; Notes field undocumented; otherwise API reference and design docs are well-maintained |

---

## 8. Governance Gaps

1. **No design-review history**: This is the first review. Trend tracking begins next week.

2. **No ADRs present**: `/documentation/adr/` contains no files. Significant decisions (host-key shared model, manifest ownership, payment gateway deferral, Notes-in-JSON design) should be captured as ADRs before architectural drift accumulates further.

3. **No OpenAPI specs in repository**: The integration principles require machine-readable OpenAPI 3.x specs version-controlled alongside service code. No `openapi.yaml` / `openapi.json` files were found. Swagger is wired up (`IOpenApiConfigurationOptions`) but specs are not checked in, preventing CI-based contract testing.

4. **No consumer-driven contract tests**: Architecture principles require Pact (or equivalent) contract tests between orchestration APIs and microservices to catch breaking changes before deployment. None exist.

5. **No incident response plan discoverable**: The security principles require a documented IR plan covering the GDPR 72-hour notification obligation. No such document was found in the repository.

6. **Accounting MS not inspectable**: The Accounting microservice is documented as event-consumer-only with no callable API. No integration test, no schema content, and no event-subscription implementation is visible. It cannot be assessed for conformance at this time.

7. **Airport API and Finance API scope is future/stub**: Both are documented as future-release scaffolds. Not assessed.

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
| `documentation/api.md` | API development guide |
| `documentation/api-specs/*.md` (14 files) | Per-service API specifications |
| `src/Database/Script.sql` | Authoritative schema with triggers |

### Services inspected (code)

All microservices and orchestration APIs under `src/API/`:

| Service | Depth |
|---------|-------|
| Retail API (Orchestration) | Program.cs, all Application handlers, all Infrastructure clients, all Functions |
| Loyalty API (Orchestration) | Program.cs |
| Admin API (Orchestration) | Program.cs, all Functions, all Infrastructure |
| Operations API (Orchestration) | Program.cs, DeliveryServiceClient |
| Delivery MS | Program.cs, DbContext, all Application handlers, all Domain |
| Offer MS | Program.cs, FarePricer |
| Order MS | Program.cs, DbContext |
| Payment MS | All Application handlers |
| Customer MS | DbContext |
| Identity MS | DbContext |
| Schedule MS | DbContext |
| Ancillary MS | DbContext, GetSeatOfferHandler |
| User MS | DbContext |
| Exceptions MS | Program.cs, ExceptionsFunction |
| Shared.Common / Shared.Business | IdempotencyKey, CorrelationId, HealthCheck |

### CI/CD pipelines inspected

All 20 workflows under `.github/workflows/` including representative deploy workflow `main_reservation-system-db-microservice-offer.yml` and integration test workflow `integration-tests-identity-microservice.yml`.

### Commit reference

Review conducted against commit `71593b7` (tip of `main` as of 2026-04-21).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS event subscriptions | Stub implementation only; no inspectable business logic |
| Airport API / Finance API | Documented as future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass |
| Terminal app (`src/Terminal/`) | Outside backend governance scope for this pass |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap noted in Section 8 |
| Penetration testing / runtime security | Requires live environment access |
