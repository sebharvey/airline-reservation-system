# AI Agent Guide — Apex Air Reservation System

This file is the entry point for any AI agent working on this repository. Read it first, in full, before writing any code or documentation.

---

## Project Overview

Apex Air is a Modern Airline Retailing platform implementing IATA ONE Order and NDC standards. It is structured around bounded-context microservices orchestrated by a Retail API layer, with an Angular web front-end consuming those APIs.

The platform covers the full airline retailing journey: search, offer, basket, payment, ticketing, manage booking, check-in, loyalty, and disruption handling.

---

## Repository Structure

```
/
├── agents.md                          ← YOU ARE HERE — read this first
├── README.md
├── documentation/
│   ├── design.md                      ← canonical capability model and domain definitions
│   ├── api-reference.md               ← all API endpoints, verbs, and descriptions
│   ├── api.md                         ← how to build backend microservices and APIs
│   ├── web.md                         ← how to build the Angular web front-end
│   └── principles/
│       ├── architecture-principals.md ← DDD, microservices, orchestration rules
│       ├── integration-principals.md  ← REST conventions, versioning, idempotency
│       ├── security-principals.md     ← auth, PCI scope, data protection
│       ├── data-principals.md         ← data ownership, formats, retention
│       └── infrastructure-principals.md ← Azure, deployment, scaling
├── src/
│   ├── API/
│   │   ├── Template/                  ← reference scaffold — never modify
│   │   ├── Microservices/             ← standalone domain microservices
│   │   ├── Orchestration/             ← cross-domain orchestration APIs
│   │   └── Shared/                    ← shared infrastructure (DB, JSON, HTTP helpers)
│   └── Web/                           ← Angular 21 web application
└── .claude/
    └── commands/
        └── new-api.md                 ← slash command for scaffolding a new API
```

---

## Documentation Map

| File | Purpose |
|------|---------|
| `documentation/design.md` | The authoritative domain capability model. Every bounded context, feature, and sub-capability is defined here. **Always read this before writing any code or design.** |
| `documentation/api-reference.md` | Every API endpoint with its HTTP verb, path, and description. **Cross-reference this whenever adding or modifying endpoints.** |
| `documentation/api.md` | Step-by-step guide for building new backend APIs following the clean architecture pattern. References the `/new-api` scaffolding command. |
| `documentation/web.md` | Guide for building new Angular pages, components, and services. Covers routing, state management, and API integration patterns. |
| `documentation/principles/architecture-principals.md` | Core structural rules: DDD, microservices, no direct service-to-service calls, price integrity. |
| `documentation/principles/integration-principals.md` | REST conventions, HTTP status codes, versioning, idempotency, event-driven patterns. |
| `documentation/principles/security-principals.md` | Authentication, PCI DSS scoping, credential handling. |
| `documentation/principles/data-principals.md` | Data ownership, formats (ISO 8601, IATA codes, decimal money), retention. |
| `documentation/principles/infrastructure-principals.md` | Azure Functions, hosting, deployment, and scaling constraints. |

---

## How to Read the Documentation

Follow this order when familiarising yourself with an area before writing code:

1. **`documentation/design.md`** — understand the domain capability model for the area you are working in.
2. **`documentation/api-reference.md`** — understand what APIs already exist for that domain.
3. **`documentation/principles/architecture-principals.md`** — confirm the structural rules that apply.
4. **`documentation/principles/integration-principals.md`** — confirm the API and event conventions.
5. **`documentation/api.md`** or **`documentation/web.md`** — follow the relevant implementation guide.
6. **Existing code** — use the `src/` tree to verify how patterns are applied in practice before writing anything new.

Never skip step 1. Existing design decisions constrain every new capability.

---

## How to Design New Capability

When asked to design or implement a new feature, follow this workflow in order. **Do not write implementation code until the design is verified.**

### Step 1 — Read `design.md`

Read `documentation/design.md` in full for the relevant domains. Identify:
- Which bounded context owns the new capability.
- What related capabilities already exist.
- Which existing domain entities and value objects are involved.

### Step 2 — Draft the capability design

Write a capability description in the same format and level of detail as the existing entries in `design.md`. Use the same heading hierarchy:

```
- **Domain** — one-line description
  - Capability Group
    - Sub-capability 1
    - Sub-capability 2
```

Use the exact same domain names, terminology, and ubiquitous language that appear in `design.md`. Do not invent new terms.

### Step 3 — Cross-reference `api-reference.md`

Review `documentation/api-reference.md` to:
- Confirm no endpoint with the same path and verb already exists.
- Identify related endpoints that the new capability must integrate with or respect.

Draft any new endpoints in the same table format used in `api-reference.md`:

```markdown
| `METHOD` | `/v1/path` | Description |
```

Follow the verb conventions documented in `api-reference.md` (e.g. `POST` for orchestration actions, `PATCH` for microservice partial updates).

### Step 4 — Conflict check against `design.md`

Re-read the full relevant sections of `design.md` and verify that the new design:
- Does not duplicate a capability that already exists under a different name.
- Does not assign ownership of data to a domain that does not own it.
- Does not introduce direct microservice-to-microservice calls (see `architecture-principals.md`).
- Is consistent with the price integrity, idempotency, and event-driven patterns already established.

If any conflict is found, **revise the design and repeat Steps 2–4** until the design is clean.

### Step 5 — Verify and finalise

Before writing any code:
- Confirm the new `design.md` additions use the correct domain name and terminology.
- Confirm the new `api-reference.md` entries use the correct HTTP verbs, path casing (kebab-case), and versioning prefix (`/v1/`).
- Confirm the new capability does not break or contradict any existing design entry.

Only proceed to implementation once all checks pass.

### Step 6 — Update documentation atomically with code

When implementing, update `design.md` and `api-reference.md` in the same commit as the code changes. Documentation must never lag behind the implementation.

---

## How to Write New Documentation

When generating new documentation files for this project, match the style and conventions of the existing files:

- Use `#` for the file title, `##` for major sections, `###` for sub-sections.
- Use sentence case for headings (not title case).
- Use bullet lists with `- **Term** — description` for capability entries (matching `design.md`).
- Use pipe tables with `| Method | Endpoint | Description |` headers for API entries (matching `api-reference.md`).
- Write in declarative, present-tense, direct style — no passive voice, no filler phrases.
- Every new documentation file must be listed in the **Documentation Map** table in this file (`agents.md`).

---

## Key Rules — Never Violate These

These rules are enforced by the principles documents and are non-negotiable:

1. **No direct microservice-to-microservice calls.** All cross-domain synchronous calls route through an orchestration API. See `architecture-principals.md`.
2. **Domain names are fixed.** The eight core domains — Offer, Order, Payment, Delivery, Customer, Identity, Accounting, Seat — plus Bag, Schedule, Disruption, Loyalty are defined in `design.md`. Use them verbatim in all code, schemas, and documentation.
3. **Stored Offer pattern.** Prices are locked at search time via a stored offer snapshot. Orders retrieve by `OfferId`, never re-price on confirmation.
4. **E-ticket numbers are immutable.** A change triggers reissuance, not amendment.
5. **Monetary amounts use `DECIMAL`, never floating-point.**
6. **All timestamps are ISO 8601 UTC.**
7. **IATA codes throughout.** Airport codes (`CHAR(3)` uppercase), passenger types (`ADT`, `CHD`, `INF`, `YTH`), aircraft codes (4-char Apex Air convention).
8. **JSON field names are camelCase; database column names are PascalCase.**
9. **URI paths use kebab-case; query parameters use camelCase.**
10. **API versioning is URI-path-only:** `/v1/`, `/v2/` — no header or query-param versioning.

---

## Related Slash Commands

| Command | Purpose |
|---------|---------|
| `/new-api <ApiName> <EntityName> <ApiType>` | Scaffold a new clean-architecture Azure Functions API from the template. See `.claude/commands/new-api.md` for full usage. |
