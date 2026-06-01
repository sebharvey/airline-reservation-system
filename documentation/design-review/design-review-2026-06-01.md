# Apex Air — Weekly Design Review

**Date:** 2026-06-01
**Reviewer:** Architect (automated governance pass)
**Previous review:** design-review-2026-05-25.md

---

## 1. Executive summary

The platform posture is **Non-conforming** — the first regression from "Conforming with concerns" in two cycles. Three findings are now at CRITICAL AGEING: H-04 (bag tag random sequence, week 5) and H-05 (Offer MS direct MS-to-MS calls, which escalates to CRITICAL AGEING this cycle as warned in the 2026-05-25 review). A new HIGH finding has been identified: the `DatabaseMcp` Azure Functions tool — introduced this cycle — accepts arbitrary SELECT queries against all database schemas including PII-bearing tables (`customer.*`, `order.*`, `identity.*`, `payment.*`) with no query audit logging, while its documentation incorrectly states it is "local only" when a GitHub Actions workflow deploys it to the production Azure slot. The most damaging regression this cycle is the deletion of the only two integration test workflows in the repository (`integration-tests-identity-microservice.yml`, `integration-tests-customer-microservice.yml`): combined with the deploy-workflow `dotnet test` gate that executes zero test classes, the platform now has **no automated test execution in any CI pipeline**. The single most important action this week is closing H-04 and H-05 — both have been open long enough that escalation to CRITICAL AGEING is justified, and neither has a legitimate blocker.

---

## 2. Critical findings (act this week)

### H-04 — Bag tag sequence number randomly generated; IATA Resolution 740 uniqueness violated — CRITICAL AGEING (Week 5)

**Severity:** CRITICAL AGEING (week 5 — unchanged for five consecutive reviews)
**Principle breached:** Architecture Principals — IATA standards alignment; IATA Resolution 740 requires bag tag licence plate numbers to be globally unique per airline per flight day.

**Evidence (unchanged for fifth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Delivery/Application/OciCheckIn/OciCheckInHandler.cs:381–382` — `// TODO: In future, this 6-digit sequence number needs to be auto-incremented from a persistent counter rather than generated randomly. var sequence = Random.Shared.Next(0, 1_000_000).ToString("D6");`
- Same `Random.Shared.Next` generator invoked for both OCI self check-in and agent check-in paths. No SQL `SEQUENCE` object, no persistent counter table, no change in five weeks.

**Impact if unresolved:** Duplicate bag tags at any meaningful check-in volume. Bag misrouting and loss. IATA Resolution 740 non-compliance. Operational safety issue.

**Recommended remediation:** Replace `Random.Shared.Next` with `NEXT VALUE FOR delivery.BagTagSequence` (SQL Server `SEQUENCE` object in the `delivery` schema, scoped per airline numeric prefix). Pass the next sequence value into `GenerateBagTag()` from the repository. The mod-7 check-digit logic is correct and unchanged. Close the TODO comment when done.

**Age:** 5 weeks.

---

### H-05 — Offer MS timer trigger calls Schedule MS and Ancillary MS directly — CRITICAL AGEING (Week 4, escalated this cycle)

**Severity:** CRITICAL AGEING (escalated from HIGH — open four consecutive reviews; escalation triggered per 2026-05-25 review warning)
**Principle breached:** Architecture Principals — "No direct microservice-to-microservice communication. This constraint applies without exception."

**Evidence (unchanged for fourth consecutive review):**
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/ScheduleServiceClient.cs:11` — self-granted exception comment: `"Note: direct MS-to-MS calls are an accepted exception for timer triggers."` No governance document or ADR grants this exception.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Infrastructure/ExternalServices/SeatServiceClient.cs:10` — acknowledged violation: `"TODO: remove this cross-domain call — cabin counts are already stored in offer.FlightInventory.Cabins"`.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Application/RollingInventoryImport/RollingInventoryImportHandler.cs:37,54` — both clients injected and called.
- `src/API/Microservices/ReservationSystem.Microservices.Offer/Program.cs:76–91` — `ScheduleMs` and `AncillaryMs` HTTP clients registered.

**Impact if unresolved:** Deployment coupling (Offer MS lifecycle bound to Schedule MS and Ancillary MS contract stability). Obscured distributed traces. No circuit breaker — a Schedule MS or Ancillary MS outage at 01:00 UTC silently fails the nightly rolling inventory import.

**Recommended remediation (in preference order):**
1. `SeatServiceClient` — implement the acknowledged TODO: derive cabin counts from existing `offer.FlightInventory.Cabins` rows for the same `AircraftType`. The data is already present locally; no cross-service call is required.
2. `ScheduleServiceClient` — raise an ADR to formally grant or deny the exception. If granted, add circuit breaker, retry policy, timeout, and dead-letter alerting as compensating controls. If denied, move the timer trigger to a dedicated `RollingInventoryOrchestration` function in the Operations API.
3. In either case: delete the self-granted exception comment from `ScheduleServiceClient.cs:11` regardless of the resolution path.

**Age:** 4 weeks.

---

## 3. Status of prior findings

| Finding | Prior Severity | Status | Evidence note |
|---------|---------------|--------|--------------|
| H-04 — Bag tag sequence randomly generated | CRITICAL AGEING | **UNCHANGED** | `OciCheckInHandler.cs:381–382` — `Random.Shared.Next(0, 1_000_000)` unchanged. Week 5. |
| H-05 — Offer MS calls Schedule MS and Ancillary MS directly | HIGH | **UNCHANGED → CRITICAL AGEING** | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10` unchanged. Escalated per 2026-05-25 warning. |
| C-02r — Debug infrastructure retained in production code | MEDIUM | **UNCHANGED** | `GetOrderDebugHandler.cs` and `GetOrderDebugQuery.cs` still present in `Order.Application.GetOrderDebug/`. |
| N-01 — Seatmap booking-window occupancy gap | MEDIUM | **UNCHANGED** | Pre-booking seatmap derives occupancy from manifest only; in-progress `offer.SeatReservation` holds not surfaced. |
| M-03 — Seat offer logic deferred to Function layer | MEDIUM | **UNCHANGED** | `GetSeatOffersHandler.cs:20` and `GetSeatOfferHandler.cs:20` TODOs unchanged. |
| M-07 — No integration tests for orchestration APIs | MEDIUM | **REGRESSED** | Commits `4884d5a` and `01e00ed` deleted `integration-tests-identity-microservice.yml` and `integration-tests-customer-microservice.yml` — the only two CI workflows that executed any test methods. Platform now has zero test execution in any pipeline. |
| N-05 — Bag tag response field undocumented | MEDIUM | **UNCHANGED** | `documentation/design/delivery.md:331` still states "bagTagNumber is null at purchase and populated by the Airport API." The Delivery MS OCI handler generates the tag; the Airport API does not. |
| N-10 — OrderData JSON lacks schemaVersion | MEDIUM | **UNCHANGED** | No `schemaVersion` field added to OrderData. |
| H-03r — dotnet test gate runs 0 tests | MEDIUM | **REGRESSED** | Integration test workflows deleted this cycle remove the only test execution from CI. The deploy-workflow `dotnet test` step still runs but finds zero test classes. |
| M-08 — TierConfig table not mapped with HasTrigger | LOW | **UNCHANGED** | `CustomerDbContext.cs` still has no `TierConfig` DbSet or `HasTrigger("TR_TierConfig_UpdatedAt")`. `TR_TierConfig_UpdatedAt` exists in `Script.sql`. |
| M-10 — TODO without issue reference committed to main | LOW | **UNCHANGED** | `OciCheckInHandler.cs:381` and `SeatServiceClient.cs:10` bare TODOs remain without author or tracking issue reference. |
| N-08 — HandleDelayHandler throws NotImplementedException | LOW | **UNCHANGED** | `HandleDelayHandler.cs:35` still throws `NotImplementedException()`; `api-reference.md:321` documents the endpoint as returning `200 OK`. |
| N-09 — GET /v1/products basketId parameter undocumented | LOW | **UNCHANGED** | `api-reference.md:98` still does not mention the optional `basketId` query parameter or the rule evaluation logic it triggers. |

---

## 4. High findings

### N-11 — DatabaseMcp tool exposes unrestricted PII read access with no audit logging; documented as local-only but deployed to Azure (new)

**Severity:** HIGH
**Principle breached:** Security Principals — "PII must never appear in logs, telemetry, or error messages"; "PCI DSS compliance must be maintained and card data scoped entirely to the Payment microservice"; "All state-changing operations must produce a structured, immutable audit log entry" (spirit extended to data access for PII compliance). Infrastructure Principals — "All Azure resources authenticate via Managed Identities; no embedded credentials permitted" (connection string provenance is unverifiable from code alone).

**Evidence:**
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:85–101` — the `run_select_query` MCP tool accepts an arbitrary SQL `SELECT` or CTE from any authenticated caller and executes it directly against the database connection string. There is no schema allowlist, no table blocklist, no row cap, and no query audit log.
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Functions/McpFunction.cs:144–163` — `RunSelectQuery` opens a `SqlConnection(ConnectionString)` and executes the caller-supplied SQL verbatim. A holder of the function key can issue `SELECT * FROM customer.Customer` (names, DOB, passport numbers, contact details), `SELECT * FROM [identity].RefreshToken` (hashed refresh tokens), `SELECT * FROM payment.Payment` (last-4 digits, card type, payment records), or `SELECT * FROM [order].[Order]` (full `OrderData` JSON including travel documents and passenger PII).
- `src/API/Tools/ReservationSystem.Tools.DatabaseMcp/Program.cs` — `ConfigureFunctionsWorkerDefaults()` only; no Key Vault reference, no managed identity configuration. The `ConnectionString` is obtained from `config.GetConnectionString("Database")`; the permissions of this credential are not constrained in code.
- `documentation/service-urls.md:57` — describes the tool as "Local only — Azure Functions host, listens on `http://localhost:7071`". This is incorrect: `.github/workflows/main_reservation-system-mcp-db.yml` deploys the tool to Azure slot `reservation-system-mcp-db` in production on every push to main.
- The tool does have `AuthorizationLevel.Function` (`McpFunction.cs:29`) — a function host key is required on every request. This is meaningful access control; the concern is not unauthenticated access. The concern is the scope of what an authenticated caller can do and the absence of audit trails.

**Impact if unresolved:** Any holder of the `reservation-system-mcp-db` function key (including any Claude Code agent session that has been given the key) has unrestricted read access to all PII across all schemas. Bulk extraction of passenger records, travel documents, and payment data is possible without leaving any application-layer audit trail. The documentation discrepancy (local-only vs. deployed-to-Azure) means operators cannot assess the actual attack surface of this tool.

**Recommended remediation:**
1. Correct `documentation/service-urls.md:57` to accurately reflect that the tool is deployed to Azure and accessible via HTTPS.
2. Add an api-spec document (e.g. `documentation/api-specs/database-mcp.md`) describing the tool, its purpose, access controls, and the deliberate data access it enables.
3. Add query audit logging: every `run_select_query` call must emit a structured log entry containing the query text, the row count returned, and a timestamp. This satisfies the audit principle for any PII access path.
4. Enforce a schema allowlist: permit queries only against `offer.*` and `schedule.*` schemas from this tool; block `customer.*`, `identity.*`, `payment.*`, and `order.*`. If broader access is genuinely required, document it as an approved exception (mirroring the Terminal debug modal exception in `security-principals.md`) with a separate, more restricted query tool.
5. Add a row cap (e.g. TOP 500) to `RunSelectQuery` to prevent bulk data extraction in a single call.
6. Verify the database connection string used by the tool connects as a read-only, schema-restricted SQL login — not the full application credential.

**Age:** 1 week (new finding).

---

## 5. Medium and low findings

### Regression — Integration test workflows deleted

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| M-07 | No integration tests — all CI test execution removed | MEDIUM | F | Commits `4884d5a` and `01e00ed` deleted the only two CI workflows that ran test methods: `integration-tests-identity-microservice.yml` and `integration-tests-customer-microservice.yml`. Combined with deploy-workflow `dotnet test` steps that execute zero test classes, the platform now has **no automated test execution of any kind in CI**. This is a concrete regression from the prior cycle. | QA / Platform |
| H-03r | dotnet test gate in deploy workflows executes zero tests — reinforced regression | MEDIUM | F | The structural `dotnet test` gate added in the 2026-05-25 cycle remains in all 17 deploy workflows (including the new MCP tool workflow). However, with the deletion of the only real test workflows this cycle, the gate is now the sole remaining test step — and it still runs zero test methods. The gate protects against nothing at the code level. | QA / Platform |

### Carried medium and low findings

| # | Title | Severity | Area | Description | Recommended Owner |
|---|-------|----------|------|-------------|-------------------|
| H-04 | Bag tag sequence randomly generated | CRITICAL AGEING | A / E | See section 2. Week 5. | Delivery MS owner |
| H-05 | Offer MS calls Schedule MS and Ancillary MS directly | CRITICAL AGEING | A | See section 2. Week 4. | Offer MS owner |
| C-02r | Debug infrastructure retained in production code | MEDIUM | C | `GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs`, three `AdminDebug*` Retail API functions, and debug client methods. All authenticated; still dead code. | Retail API / Order MS owner |
| N-01 | Seatmap booking-window occupancy gap | MEDIUM | A / B | Pre-booking seatmap shows manifest-only occupancy; in-progress `offer.SeatReservation` holds not surfaced. DB constraint preserves integrity. | Retail API / Offer MS owner |
| M-03 | Seat offer logic deferred to Function layer | MEDIUM | A | `GetSeatOffersHandler.cs:20`, `GetSeatOfferHandler.cs:20` TODOs. Business logic belongs in Application layer. | Ancillary MS owner |
| N-05 | Bag tag documentation incorrect | MEDIUM | G | `delivery.md:331` attributes bag tag generation to Airport API; Delivery MS OCI handler generates it. | Delivery / Operations owner |
| N-10 | OrderData JSON lacks schemaVersion | MEDIUM | D | `OrderData` has no `schemaVersion` field. The `paxId` addition (prior cycle) exposed the gap; no remediation. | Order MS owner |
| M-08 | TierConfig table not mapped with HasTrigger | LOW | D | `TR_TierConfig_UpdatedAt` in `Script.sql`; `CustomerDbContext` has no mapping. | Customer MS owner |
| M-10 | TODO without issue reference on main | LOW | Coding Standards | `OciCheckInHandler.cs:381`, `SeatServiceClient.cs:10` bare TODOs without author or issue reference. | All owners |
| N-08 | HandleDelayHandler throws NotImplementedException | LOW | B | `api-reference.md:321` documents `/v1/disruptions/delay` as returning `200 OK`; implementation throws `NotImplementedException`. | Operations API owner |
| N-09 | GET /v1/products basketId parameter undocumented | LOW | G | `api-reference.md:98` still does not mention the optional `basketId` parameter or rule evaluation logic. | Retail API / Docs owner |
| N-13 | Post-departure manage-booking lockout business rule undocumented | LOW | G | Commits `0795ddd` and `4b7576d` added departure-lockout enforcement to `CancelOrderHandler`, `ChangeOrderHandler`, `AddOrderBagsHandler`, and `UpdateOrderSeatsHandler`. The rule (422 when all segments have departed; per-segment gate for bag/seat requests) is enforced in code but absent from `documentation/design/manage-booking.md` and not reflected in the endpoint descriptions in `api-reference.md`. | Retail API / Docs owner |
| N-14 | DatabaseMcp tool absent from api-reference.md | LOW | G | The `DatabaseMcp` Azure Functions tool has no entry in `api-reference.md` and no api-spec document. `service-urls.md` has an entry but describes it incorrectly as local-only. The CLAUDE.md documentation map should also be updated to reference any new spec file. | Platform Architect |

---

## 6. Observations and positive notes

- **Post-departure lockout correctly enforced at both API and UI layers.** Commits `0795ddd` and `4b7576d` implement the departure lockout on change, cancel, add-bags, and add-seats — both in the Retail API handlers (returning `422` when all segments have departed, or when the specific requested segments have departed) and in the Angular web front-end (hiding action tiles and replacing seat/bag buttons with a "This flight has departed" notice). Security principles state "A check that exists only in the UI provides no security guarantee" — this implementation correctly places the authoritative check in the API and mirrors it in the UI for UX. The per-segment granularity (unflown return legs remain modifiable) is the correct behaviour for multi-segment bookings.

- **Payment record visibility fixed for post-sale bag and seat purchases.** Commit `bba47d2` adds the missing `UpdateOrderPaymentsAsync` call after settlement in both `AddOrderBagsHandler` and `UpdateOrderSeatsHandler`. Previously, a payment was created and settled in the Payment MS but never appended to the order's `payments[]` array, making it invisible in the retrieve-order response. This data integrity fix closes a gap that would have caused reconciliation issues between the order view and the payment ledger.

- **Sales performance endpoint correctly documented atomically.** Commit `f4ec938` added the `/v1/admin/inventory/{inventoryId}/sales-performance` endpoint and `139daed` refactored the logic into the application handler layer — both changes accompanied by a corresponding `api-reference.md:139` entry. The atomic code-and-docs pattern continues to be applied correctly on new capability.

- **Claude Code hook system improved.** Commit `61f7b50` renames `.claude/test-settings.json` to `.claude/settings.json` (so the hook configuration is actually loaded by Claude Code), extends the PostToolUse hook to cover `Edit` tool calls on `.cs` files (previously a blind spot for the build validator), and moves to structured `additionalContext` JSON output so build errors are injected into the agent's context via the canonical feedback channel. These are meaningful improvements to the automated guardrail system.

- **DatabaseMcp tool uses AuthorizationLevel.Function.** Despite the governance concerns raised in section 4, the MCP tool does require a function host key on every call. This is the correct minimum baseline for a developer data-access tool and aligns with the authentication pattern used for all microservice-to-orchestration calls.

---

## 7. Conformance scorecard

| Dimension | Conformance | Trend vs 2026-05-25 | Notes |
|-----------|------------|---------------------|-------|
| A — Microservice Boundary Integrity | 🔴 Red | → | H-04 CRITICAL AGEING week 5 unchanged; H-05 escalates to CRITICAL AGEING week 4; N-01 seatmap gap unchanged |
| B — API Contract Conformance | 🟡 Amber | ↓ | N-08 unchanged; N-13 new (departure lockout business rule undocumented in design and api-reference); N-09 unchanged |
| C — Security Principles | 🔴 Red | ↓ | N-11 new HIGH finding — DatabaseMcp PII exposure with no audit logging, deployed to Azure but documented as local-only; C-02r unchanged |
| D — Data Storage & Schema Principles | 🟡 Amber | → | M-08 unchanged; N-10 unchanged; no new schema violations in this cycle's commits |
| E — Infrastructure & Integration Principles | 🟡 Amber | → | H-04 CRITICAL AGEING (bag tag); H-05 CRITICAL AGEING (MS-to-MS calls); MCP tool workflow correctly has CVE gate and dotnet test step |
| F — Testing & CI | 🔴 Red | ↓ | M-07 REGRESSED — integration test workflows deleted; deploy-gate `dotnet test` still executes zero tests; platform has no automated test execution in any CI pipeline |
| G — Documentation Drift | 🟡 Amber | ↓ | N-05 unchanged; N-09 unchanged; N-13 new (departure lockout); N-14 new (DatabaseMcp undocumented in api-reference); service-urls.md incorrect for MCP tool |

---

## 8. Governance gaps

The following gaps remain open. One is escalated.

1. **ADR register has only one entry; several decisions remain undocumented.** ADR-001 (payment gateway deferral) was a good precedent. The `ScheduleServiceClient` self-granted exception (H-05) urgently needs either an approved ADR or a denial. Decisions warranting ADR capture: (a) manifest-only seatmap occupancy as accepted design (N-01 — MEDIUM for three cycles); (b) shared `Microservice:HostKey` authentication pattern; (c) DatabaseMcp tool as an approved developer PII access mechanism (or explicit denial with schema restrictions).

2. **No OpenAPI specs in repository.** Integration principles require machine-readable OpenAPI 3.x specs version-controlled alongside service code. 49 commits/week with no contract-test gate remains the most systemic risk to API stability. The new `DatabaseMcp` tool has no spec; the departure lockout behaviour has no machine-readable contract.

3. **No consumer-driven contract tests.** Pact or equivalent between orchestration APIs and microservices absent. The deletion of the identity and customer integration tests this cycle worsens this gap materially.

4. **No incident response plan discoverable.** Security principles require a documented IR plan with a UK GDPR 72-hour breach notification procedure. Finding N-11 (DatabaseMcp unrestricted PII read access without audit logging) makes this gap more urgent: if the function key were compromised, there would be no application-layer audit trail to assess what data had been accessed.

5. **Accounting MS not assessable.** Event-subscription stubs only; no business logic to inspect.

6. **Airport API and Finance API scaffolded only.** Not assessed. The `delivery.md:331` incorrect attribution of bag tag generation to the Airport API (N-05) remains related to this gap.

7. **`service-urls.md` accuracy gap for new MCP tool.** The tool is described as local-only but is deployed to Azure. `service-urls.md` must be maintained as an accurate service registry for operators and security reviewers.

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
| `documentation/design/manage-booking.md` | Departure lockout documentation check (N-13) |
| `documentation/design/delivery.md` | Bag tag documentation check (N-05) |
| `documentation/service-urls.md` | Service registry accuracy check (N-11, N-14) |
| `documentation/design-review/design-review-2026-05-25.md` | Most recent prior review |
| `documentation/design-review/design-review-2026-05-21.md` | Second prior review |
| `documentation/design-review/design-review-2026-05-18.md` | Third prior review |

### Services and code inspected

| Service / Area | Depth |
|---------------|-------|
| Delivery MS — OciCheckIn | `OciCheckInHandler.cs:378–386` — bag tag `Random.Shared.Next` confirmed unchanged (H-04, week 5) |
| Offer MS | `ScheduleServiceClient.cs:11`, `SeatServiceClient.cs:10`, `RollingInventoryImportHandler.cs:37,54`, `Program.cs:76–91` — H-05 confirmed unchanged; escalated to CRITICAL AGEING |
| DatabaseMcp tool | `Functions/McpFunction.cs` — full read; `Program.cs` — full read; `ReservationSystem.Tools.DatabaseMcp.csproj` — referenced; `.github/workflows/main_reservation-system-mcp-db.yml` — deployment scope confirmed (N-11) |
| Order MS — Debug | `Application/GetOrderDebug/GetOrderDebugHandler.cs`, `GetOrderDebugQuery.cs` — files confirmed still present (C-02r) |
| Order MS — Application handlers | `AddOrderBagsHandler.cs`, `UpdateOrderSeatsHandler.cs`, `CancelOrderHandler.cs`, `ChangeOrderHandler.cs` — departure lockout guard confirmed via git diff; `UpdateOrderPaymentsAsync` fix confirmed (bba47d2) |
| Ancillary MS | `GetSeatOffersHandler.cs:20`, `GetSeatOfferHandler.cs:20` — M-03 TODOs confirmed unchanged |
| Customer MS | `CustomerDbContext.cs` — M-08 TierConfig mapping confirmed absent |
| Operations API | `HandleDelayHandler.cs:35` — N-08 `NotImplementedException` confirmed unchanged |
| GitHub Actions workflows | All 20 workflows listed; integration test workflows confirmed deleted (`4884d5a`, `01e00ed`); `dotnet test` gate confirmed in all 17 remaining deploy workflows including new MCP tool workflow |
| `documentation/api-reference.md` | Line 98 — `basketId` still absent from `GET /v1/products` (N-09); line 321 — `/v1/disruptions/delay` still documented as `200 OK`; line 139 — sales-performance correctly documented |

### Commit reference

Review conducted against commit `68cd181` (tip of `main` as of 2026-06-01).
49 commits merged since prior review (`316fd85`, 2026-05-25).

### Areas deferred

| Area | Reason |
|------|--------|
| Accounting MS | Event-subscription stubs only; no inspectable business logic |
| Airport API / Finance API | Future-release stubs |
| Angular web front-end (`src/Web/`) | Outside backend governance scope for this pass; departure lockout FE change reviewed for correctness via commit diff only |
| Azure infrastructure / Bicep / Terraform | No IaC files found in repository — governance gap from prior reviews |
| Penetration testing / runtime security | Requires live environment access |
| Per-service API specs (`documentation/api-specs/`) | Selective spot-check only; no service-spec changes noted in this cycle's commits |
