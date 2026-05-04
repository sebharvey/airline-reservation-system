# Accounting — sequence diagrams

The Accounting domain is described in the design as event-driven (consuming `OrderConfirmed` and `OrderCancelled` events from the Order MS). **This domain is not currently implemented** — no Accounting microservice exists in the codebase and no event consumer is wired up.

The current implementation handles financial operations synchronously:

- **Refunds** are issued directly by the Retail API calling `POST /api/v1/payment/{paymentId}/refund` on the Payment MS inside `CancelOrderHandler`.
- **Revenue tracking** is implicit in the Payment MS event log (Authorise / Settle / Refund events per paymentId).

The diagrams below document the **intended design** for future implementation.

---

## Revenue recording (order confirmed) — intended design

When an order is confirmed in the Order MS, an `OrderConfirmed` event is intended to be published. The Accounting domain would consume this event to record the revenue transaction.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant AccountingMS as Accounting MS

    Note over RetailAPI,OrderMS: Order has been confirmed, payment settled

    RetailAPI->>OrderMS: POST /api/v1/orders/{orderId}/confirm
    Note over RetailAPI,OrderMS: Assigns booking reference
    OrderMS-->>RetailAPI: ConfirmedOrder (bookingReference)

    OrderMS-)AccountingMS: OrderConfirmed event (not yet implemented)
    Note over OrderMS,AccountingMS: {bookingReference, orderId,<br/>totalAmount, currencyCode,<br/>fareAmount, taxAmount,<br/>bookingType, confirmedAt}

    Note over AccountingMS: Record revenue entry:<br/>debit receivables, credit revenue account
```

---

## Refund recording (order cancelled) — current implementation

Refunds are currently issued synchronously by the Retail API. The intended event-driven flow to an Accounting MS is not yet implemented.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant PaymentMS as Payment MS

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/cancel
    Note over RetailAPI,OrderMS: reason=VoluntaryCancellation
    OrderMS-->>RetailAPI: Cancelled

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/refund
    Note over RetailAPI,PaymentMS: refundAmount, reason=VoluntaryCancellation
    PaymentMS-->>RetailAPI: Refunded
```
