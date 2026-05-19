# AI Agent Guide — Apex Air Reservation System

Read this file first before writing any code or documentation.

---

## Project overview

Apex Air is a Modern Airline Retailing platform implementing IATA ONE Order and NDC standards. It uses bounded-context microservices orchestrated by API layers, with an Angular web front-end. The platform covers: search, offer, basket, payment, ticketing, manage booking, check-in, loyalty, and disruption handling.

---

## Repository structure

```
/
├── CLAUDE.md                             ← YOU ARE HERE — read this first
├── README.md
├── documentation/
│   ├── system-overview.md                ← architecture, domain model, airline context, glossary
│   ├── design/                           ← domain-specific design (schemas, flows, business rules)
│   │   ├── schedule.md
│   │   ├── offer.md
│   │   ├── order.md
│   │   ├── manage-booking.md
│   │   ├── check-in.md
│   │   ├── payment.md
│   │   ├── delivery.md
│   │   ├── disruption.md
│   │   ├── ancillary.md
│   │   ├── ssr.md
│   │   ├── customer.md
│   │   ├── identity.md
│   │   ├── accounting.md
│   │   └── user.md
│   ├── service-urls.md                   ← live base URLs for all deployed services
│   ├── api-reference.md                  ← all API endpoints, verbs, and descriptions
│   ├── api.md                            ← how to build backend microservices and APIs
│   ├── web.md                            ← how to build the Angular web front-end
│   ├── tests.md                          ← how to write integration tests
│   ├── authentication.md                 ← JWT and refresh token flow
│   ├── api-specs/                        ← per-service detailed API specifications
│   ├── adr/                              ← architecture decision records (ADRs)
│   └── principles/
│       ├── architecture-principals.md
│       ├── integration-principals.md
│       ├── security-principals.md
│       ├── data-principals.md
│       ├── infrastructure-principals.md
│       └── coding-standards.md
├── src/
│   ├── API/
│   │   ├── Microservices/                ← standalone domain microservices (.NET 8, Azure Functions)
│   │   ├── Orchestration/                ← cross-domain orchestration APIs
│   │   ├── Shared/                       ← shared infrastructure (DB, JSON, HTTP helpers)
│   │   └── Tests/                        ← integration tests
│   ├── Web/                              ← Angular 21 web application
│   ├── Terminal/                          ← Contact Centre app (Angular)
│   └── Database/                         ← SQL schema script (Script.sql)
└── .claude/
    └── commands/
        └── new-api.md                    ← slash command for scaffolding a new API
```

---

## Documentation map

| File | When to read |
|------|-------------|
| `documentation/system-overview.md` | Always — before any work. Contains domain model, architecture, airline context. |
| `documentation/design/<domain>.md` | When working on a specific domain. Contains data schemas, sequence flows, business rules. |
| `documentation/service-urls.md` | When configuring downstream service calls. Live base URLs for all deployed APIs and microservices. |
| `documentation/api-reference.md` | When adding or modifying endpoints. Every endpoint with verb, path, description. |
| `documentation/api.md` | When building backend APIs. Clean architecture pattern, naming, conventions. |
| `documentation/web.md` | When building Angular pages (Web or Terminal). Routing, state management, API integration. |
| `documentation/web-standards.md` | When building the customer Web app. UX/UI patterns, visual design, flows, accessibility. |
| `documentation/terminal-standards.md` | When building the Terminal agent app. Layout, tables, forms, status display, operational UX. |
| `documentation/tests.md` | When writing integration tests. Test structure, patterns, checklist. |
| `documentation/authentication.md` | When working on auth flows. JWT, refresh tokens, middleware. |
| `documentation/api-specs/<service>.md` | When implementing a specific service API. Request/response shapes, error codes. |
| `documentation/api-specs/admin-api.md` | When working on the Admin API. Staff authentication flow, downstream dependencies, configuration. |
| `documentation/principles/*.md` | Architecture, integration, security, data, infrastructure, and coding rules. |
| `documentation/adr/*.md` | Architecture Decision Records — accepted decisions and their rationale. Read before raising a finding that may already be resolved by a recorded decision. |
| `documentation/test-harness.md` | When working on the test harness. File inventory, journey JSON schema, runtime variable placeholders, harness.js architecture. |
| `documentation/timer-triggers.md` | When working on scheduled background jobs. All timer trigger functions, their schedules, and what data they clean up or import. |
| `src/Database/Script.sql` | When working with data. Authoritative database schema with all tables, triggers, constraints. |
| `documentation/design/user.md` | When working on the User domain. Employee user account schema, login flow, account lockout. |

---

## Reading order

1. **`documentation/system-overview.md`** — understand the domain model and architecture.
2. **`documentation/design/<domain>.md`** — understand the specific domain you are working in.
3. **`documentation/api-reference.md`** — understand what endpoints exist.
4. **`documentation/principles/architecture-principals.md`** — confirm structural rules.
5. **`documentation/api.md`**, **`documentation/web.md`**, **`documentation/web-standards.md`**, or **`documentation/terminal-standards.md`** — follow the implementation and design guides for the relevant app.
6. **Existing code in `src/`** — verify how patterns are applied before writing anything new.

---

## Key rules — never violate these

1. **No direct microservice-to-microservice calls.** All cross-domain synchronous calls route through an orchestration API.
2. **Domain names are fixed.** Offer, Order, Payment, Delivery, Customer, Identity, Accounting, Ancillary, Schedule, Disruption, Loyalty — used verbatim everywhere.
3. **Stored Offer pattern.** Prices locked at search time via stored offer snapshot. Orders retrieve by `OfferId`, never re-price on confirmation.
4. **E-ticket numbers are immutable.** Changes trigger reissuance, not amendment.
5. **Monetary amounts use `DECIMAL`, never floating-point.**
6. **All timestamps are ISO 8601 UTC.**
7. **IATA codes throughout.** Airport codes `CHAR(3)` uppercase, passenger types `ADT`/`CHD`/`INF`/`YTH`, aircraft codes 4-char (e.g. `A351`).
8. **EF Core DbContexts must declare triggers.** Any `ToTable` mapping for a table with an `AFTER` trigger in `Script.sql` must include `HasTrigger("<trigger-name>")`.
9. **JSON field names are camelCase; database column names are PascalCase.**
10. **URI paths use kebab-case; query parameters use camelCase.**
11. **API versioning is URI-path-only:** `/v1/`, `/v2/`.
12. **`createdAt`/`updatedAt` are database-generated** — never written by application code, never accepted in request bodies.

---

## How to design new capability

1. **Read `documentation/system-overview.md`** — identify which bounded context owns the capability.
2. **Read `documentation/design/<domain>.md`** — understand existing capabilities and data schemas.
3. **Read `documentation/api-reference.md`** — confirm no conflicting endpoint exists.
4. **Draft the design** using the same format, terminology, and domain names as existing docs.
5. **Conflict check** — verify no duplication, no cross-domain data ownership violations, no direct MS-to-MS calls.
6. **Update documentation atomically with code** — `design/<domain>.md` and `api-reference.md` in the same commit.

---

## How to write documentation

- Use `#` for file title, `##` for major sections, `###` for sub-sections.
- Sentence case for headings.
- Bullet lists with `- **Term** — description` for capability entries.
- Pipe tables with `| Method | Endpoint | Description |` for API entries.
- Declarative, present-tense, direct style.
- New documentation files must be listed in the Documentation Map above.

### Mermaid diagrams

- **Never use semicolons (`;`) inside text labels.** GitHub's Mermaid renderer treats `;` as a statement terminator inside `Note` text, message labels, and any other string value — this silently breaks the diagram. Use a comma or a dash instead.
  - Bad: `Note over A,B: Step 1; step 2`
  - Good: `Note over A,B: Step 1, step 2`

---

## Related slash commands

| Command | Purpose |
|---------|---------|
| `/new-api <ApiName> <EntityName> <ApiType>` | Scaffold a new clean-architecture Azure Functions API from the template. See `.claude/commands/new-api.md`. |
