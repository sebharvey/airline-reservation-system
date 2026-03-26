# Accounting domain

## Overview

The Accounting microservice is a pure event-consumer. It has no synchronous API surface called during the booking path. All input arrives via the Azure Service Bus event bus; it processes domain events to build and maintain the airline's financial records.

> **Current release scope:** The Accounting microservice Azure Functions project is **scaffolded as an empty shell** in this release. The event subscriptions, handlers, and database schema are defined but the financial reporting endpoints (balance sheet, P&L) are not implemented. The Finance API orchestration layer is likewise scaffolded as an empty shell. Full implementation is deferred to a future release.

The data model and event contracts are established now to ensure downstream financial systems can be integrated without breaking changes.

## Events consumed

| Event | Source | Purpose |
|---|---|---|
| `OrderConfirmed` | Order MS | Revenue recording — order and ancillary revenue capture; revenue attribution by type (fare, seat, bag). For reward bookings (`bookingType=Reward`), records points liability separately from cash revenue. |
| `OrderChanged` | Order MS | Revenue adjustment for post-sale modifications (seat change, bag addition, flight change, SSR update, IROPS rebook). For reward booking changes, includes `pointsAdjustment` and updated `totalPointsAmount`. |
| `OrderCancelled` | Order MS | Refund identification and processing; contains `refundableAmount` and `originalPaymentId`. For reward bookings, includes `pointsReinstated` and `redemptionReference` to reverse the points liability entry. |
| `TicketIssued` | Delivery MS | Records revenue from e-ticket creation. |
| `TicketVoided` | Delivery MS | Reverses the revenue entry for a voided e-ticket. |
| `DocumentIssued` | Delivery MS | Records ancillary financial transactions from EMD-equivalent document creation. |
| `DocumentVoided` | Delivery MS | Reverses the ancillary financial entry for a voided document. |

## Reward booking accounting

- **Points liability recording** from reward booking `OrderConfirmed` events (`bookingType=Reward`)
- **Points liability reversal** from reward booking `OrderCancelled` events (`pointsReinstated`)
- **Points adjustment tracking** from reward booking `OrderChanged` events (`pointsAdjustment`)
- **Separation of cash revenue** (taxes/ancillaries) from points liability for reward bookings

## Implementation notes

### Airport API and Finance API

Both are included in the architecture diagram and will be needed in future. In this release, scaffold empty Azure Functions projects for each (no endpoints beyond `/health` and hello-world smoke-test) so that the deployment pipeline and infrastructure provisioning are in place. No business logic is implemented at this stage.

### Existing Offer microservice

The `src/API/Microservices/ReservationSystem.Microservices.Offer` project was scaffolded from the template and contains generic CRUD stubs (create/get/delete/list) that do not reflect the real Offer domain. **When building the Offer microservice, reuse this project as the starting point but remove all placeholder CRUD operations and replace them with the real Offer domain implementation** as defined in the Data Schema — Offer section and the Offer MS endpoint table in `api-reference.md`. The project structure, DI wiring, `host.json`, shared library references, and build pipeline should be preserved; only the placeholder application logic, domain entities, and SQL schema need to be replaced.
