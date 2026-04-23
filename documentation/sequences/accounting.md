# Accounting — sequence diagrams

The Accounting domain receives financial events published by the Order microservice. There are no direct accounting API calls from the web frontend or orchestration APIs — accounting is driven by domain events.

---

## Revenue recording (order confirmed)

When an order is confirmed in the Order MS, an `OrderConfirmed` event is published. The Accounting domain consumes this event to record the revenue transaction.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant AccountingMS as Accounting MS

    Note over RetailAPI,OrderMS: Order has been confirmed; payment settled

    RetailAPI->>OrderMS: POST /api/v1/orders/{orderId}/confirm
    Note over RetailAPI,OrderMS: Assigns booking reference;<br/>publishes OrderConfirmed event
    OrderMS-->>RetailAPI: ConfirmedOrder (bookingReference)

    OrderMS-)AccountingMS: OrderConfirmed event
    Note over OrderMS,AccountingMS: {bookingReference, orderId,<br/>totalAmount, currencyCode,<br/>fareAmount, taxAmount,<br/>bookingType, confirmedAt}

    Note over AccountingMS: Record revenue entry:<br/>debit receivables, credit revenue account
```

---

## Refund recording (order cancelled)

When an order is cancelled, an `OrderCancelled` event is published containing the refundable amount. The Accounting domain consumes this event to record the refund.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant AccountingMS as Accounting MS

    RetailAPI->>OrderMS: POST /api/v1/orders/{bookingRef}/cancel
    Note over RetailAPI,OrderMS: refundableAmount, cancellationFeeAmount,<br/>reason=VoluntaryCancellation;<br/>publishes OrderCancelled event
    OrderMS-->>RetailAPI: Cancelled

    OrderMS-)AccountingMS: OrderCancelled event
    Note over OrderMS,AccountingMS: {bookingReference, orderId,<br/>refundableAmount, cancellationFeeAmount,<br/>currencyCode, cancelledAt}

    Note over AccountingMS: Record refund obligation;<br/>update revenue ledger entries
```
