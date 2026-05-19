# ADR-001 — Payment gateway integration deferred for demo system

**Status:** Accepted
**Date:** 2026-05-19
**Deciders:** Platform Architect
**Relates to:** Design review finding C-03 (all cycles 2026-04-21 through 2026-05-18)

---

## Context

The Payment microservice implements the full authorise / settle / void / refund lifecycle and correctly records all payment state transitions in the `payment.Payment` and `payment.PaymentEvent` tables. The four application handlers (`AuthorisePaymentHandler`, `SettlePaymentHandler`, `VoidPaymentHandler`, `RefundPaymentHandler`) each contain a `// TODO` stub where a call to an external payment processor (Adyen, Stripe, Worldpay, or equivalent) would be placed in a production system.

The automated architecture review has flagged this as finding C-03 — CRITICAL AGEING — on each of four consecutive weekly passes (2026-04-21, 2026-05-04, 2026-05-11, 2026-05-18), citing revenue loss on every booking and an inability to assert PCI DSS compliance.

This system is a **demonstration platform**. It is not connected to live card networks, does not process real transactions, and is not deployed in a context where PCI DSS compliance is required. No real money is at risk. The purpose of the platform is to demonstrate the IATA ONE Order / NDC architecture, domain model, orchestration patterns, and API design — not to provide a production-ready payment integration.

---

## Decision

**Payment gateway integration is intentionally deferred and will not be implemented in this codebase in its current form.**

The four TODO stubs are retained as markers to guide future development should the platform ever be extended into a production system. They are not defects; they are documented placeholders in a demo context.

This decision is intentional and understood by all stakeholders. The architecture review finding C-03 should be treated as **closed — accepted by decision** from this point forward.

---

## Consequences

- The `// TODO` stubs in the four Payment MS handlers remain in place and are not to be removed, as they document the integration points for any future production implementation.
- No `IPaymentGateway` interface, no gateway dependency injection, and no `GatewayAuthorisationCode` / `GatewayTransactionId` fields will be added to this codebase unless the platform scope changes.
- Automated architecture reviews must not re-raise C-03 as an open finding. If the review tooling cannot be configured to suppress it, reviewers should mark it closed on sight by reference to this ADR.
- If this platform is ever taken toward production use, this ADR must be revisited and superseded. At that point the required work would be: select a processor, implement `IPaymentGateway`, add gateway transaction fields to the Payment entity, wire all four handlers, and add integration tests covering success, decline, and 3DS flows.

---

## Alternatives considered

| Alternative | Reason rejected |
|-------------|----------------|
| Implement a full gateway integration now | Out of scope for a demo platform — adds significant compliance overhead (PCI DSS SAQ, tokenisation, 3DS) with no benefit in a non-production context |
| Implement a stub / simulator gateway that mimics real behaviour | Adds code complexity and maintenance burden. The TODO stubs already communicate the integration point clearly. A simulator would be appropriate only if end-to-end payment flow testing were a stated requirement |
| Delete the TODO stubs | Removes the intent signal for future developers. Stubs are retained deliberately |
