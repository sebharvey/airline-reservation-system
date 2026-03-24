# Accounting domain

## Overview

The Accounting microservice is a pure event-consumer. It has no synchronous API surface called during the booking path. All input arrives via the Azure Service Bus event bus; it processes `OrderConfirmed`, `OrderChanged`, `OrderCancelled`, `TicketIssued`, `TicketVoided`, `DocumentIssued`, and `DocumentVoided` events to build and maintain the airline's financial records.

> **Current release scope:** The Accounting microservice Azure Functions project is **scaffolded as an empty shell** in this release. The event subscriptions, handlers, and database schema are defined but the financial reporting endpoints (balance sheet, P&L) are not implemented. The Finance API orchestration layer is likewise scaffolded as an empty shell. Full implementation is deferred to a future release.

The data model and event contracts are established now to ensure downstream financial systems can be integrated without breaking changes.

## Events consumed

| Event | Publisher | Action |
|-------|-----------|--------|
| `OrderConfirmed` | Order MS | Record fare revenue (or points liability for reward bookings) |
| `OrderChanged` | Order MS | Adjust revenue or points liability records |
| `OrderCancelled` | Order MS | Record refund and reverse points liability if applicable |
| `TicketIssued` | Delivery MS | Record ticket issuance for audit |
| `TicketVoided` | Delivery MS | Record ticket void |
| `DocumentIssued` | Delivery MS | Record ancillary (seat/bag) revenue |
| `DocumentVoided` | Delivery MS | Reverse ancillary revenue record |

## Reward booking accounting

- Points liability recording from reward booking `OrderConfirmed` events (`bookingType=Reward`)
- Points liability reversal from reward booking `OrderCancelled` events (`pointsReinstated`)
- Points adjustment tracking from reward booking `OrderChanged` events (`pointsAdjustment`)
- Separation of cash revenue (taxes/ancillaries) from points liability for reward bookings

## Implementation notes

- **Airport API:** Scaffold empty Azure Functions project (no endpoints beyond `/health` and hello-world smoke-test). Deployment pipeline and infrastructure provisioning in place. No business logic implemented at this stage. Future scope.
- **Finance API:** Scaffold empty Azure Functions project (no endpoints beyond `/health` and hello-world smoke-test). Deployment pipeline and infrastructure provisioning in place. No business logic implemented at this stage. Future scope.
- **Existing Offer microservice:** The `src/API/Microservices/ReservationSystem.Microservices.Offer` project was scaffolded from the template and contains generic CRUD stubs (create/get/delete/list) that do not reflect the real Offer domain. When building the Offer microservice, reuse this project as the starting point but remove all placeholder CRUD operations and replace them with the real Offer domain implementation as defined in the Data Schema -- Offer section and the Offer MS endpoint table in `api-reference.md`. The project structure, DI wiring, `host.json`, shared library references, and build pipeline should be preserved; only the placeholder application logic, domain entities, and SQL schema need to be replaced.
