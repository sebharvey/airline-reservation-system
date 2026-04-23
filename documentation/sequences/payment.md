# Payment — sequence diagrams

The Payment microservice is never called directly from the web frontend. All payment interactions are orchestrated from within the Retail Orchestration API handlers. This file documents the payment service call patterns as they appear across different capabilities, plus the admin payment reporting flows.

---

## Payment lifecycle (booking confirmation)

Called from within `ConfirmBasketHandler`. A single payment record covers the full booking including ancillaries; each product type is authorised and settled as a sequential event pair on the same `paymentId`.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant PaymentMS as Payment MS

    RetailAPI->>PaymentMS: POST /api/v1/payment/initialise
    Note over RetailAPI,PaymentMS: {method, currencyCode,<br/>amount=totalBookingAmount,<br/>description}
    PaymentMS-->>RetailAPI: {paymentId}

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise
    Note over RetailAPI,PaymentMS: {type=Fare, amount=fareAmount,<br/>cardNumber, expiryDate, cvv,<br/>cardholderName}
    PaymentMS-->>RetailAPI: Authorised

    Note over RetailAPI: Order confirmed; booking reference assigned

    RetailAPI->>PaymentMS: PATCH /api/v1/payment/{paymentId}/booking-reference
    Note over RetailAPI,PaymentMS: {bookingReference}
    PaymentMS-->>RetailAPI: Updated

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
    Note over RetailAPI,PaymentMS: {amount=fareAmount}
    PaymentMS-->>RetailAPI: Settled

    opt Seat ancillaries present
        RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise
        Note over RetailAPI,PaymentMS: {type=Seat, amount=seatAmount}
        PaymentMS-->>RetailAPI: Authorised
        RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
        PaymentMS-->>RetailAPI: Settled
    end

    opt Bag ancillaries present
        RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise
        Note over RetailAPI,PaymentMS: {type=Bag, amount=bagAmount}
        PaymentMS-->>RetailAPI: Authorised
        RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
        PaymentMS-->>RetailAPI: Settled
    end

    opt Product ancillaries present
        RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise
        Note over RetailAPI,PaymentMS: {type=Product, amount=productAmount}
        PaymentMS-->>RetailAPI: Authorised
        RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
        PaymentMS-->>RetailAPI: Settled
    end
```

---

## Payment authorisation failure handling

If card authorisation fails, the payment record is voided and the draft order is deleted before the error is returned to the caller.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant PaymentMS as Payment MS
    participant OrderMS as Order MS

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise
    PaymentMS-->>RetailAPI: 402 / 422 Authorisation failed

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/void
    Note over RetailAPI,PaymentMS: reason=PaymentAuthorisationFailure
    PaymentMS-->>RetailAPI: Voided

    RetailAPI->>OrderMS: DELETE /api/v1/orders/{orderId}
    Note over RetailAPI,OrderMS: Delete draft order
    OrderMS-->>RetailAPI: Deleted

    RetailAPI-->>RetailAPI: Propagate error to caller
```

---

## Payment lifecycle (post-sale ancillary — bags or seats)

Called from within `AddOrderBagsHandler` and `UpdateOrderSeatsHandler`. Each post-sale ancillary purchase uses its own payment record.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant PaymentMS as Payment MS

    RetailAPI->>PaymentMS: POST /api/v1/payment/initialise
    Note over RetailAPI,PaymentMS: {method, currencyCode,<br/>amount=ancillaryAmount,<br/>description}
    PaymentMS-->>RetailAPI: {paymentId}

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise
    Note over RetailAPI,PaymentMS: {type=Bag|Seat, amount, card details}
    PaymentMS-->>RetailAPI: Authorised

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
    PaymentMS-->>RetailAPI: Settled

    RetailAPI->>PaymentMS: PATCH /api/v1/payment/{paymentId}/booking-reference
    PaymentMS-->>RetailAPI: Updated
```

---

## Payment lifecycle (change flight with add-collect)

Called from within `ChangeOrderHandler` when the new fare exceeds the original. Payment is authorised before the change is applied and settled after tickets are reissued.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant PaymentMS as Payment MS

    RetailAPI->>PaymentMS: POST /api/v1/payment/initialise
    Note over RetailAPI,PaymentMS: {method, currencyCode,<br/>amount=addCollect}
    PaymentMS-->>RetailAPI: {changePaymentId}

    RetailAPI->>PaymentMS: POST /api/v1/payment/{changePaymentId}/authorise
    Note over RetailAPI,PaymentMS: {type=FareChange, amount=addCollect}
    PaymentMS-->>RetailAPI: Authorised

    Note over RetailAPI: Void original tickets, release inventory,<br/>update order, reissue tickets

    RetailAPI->>PaymentMS: POST /api/v1/payment/{changePaymentId}/settle
    PaymentMS-->>RetailAPI: Settled
```

---

## Admin payment reporting

The Admin API provides read-only access to payment records for staff. No mutations are exposed.

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant AdminAPI as Admin API
    participant PaymentMS as Payment MS

    Terminal->>AdminAPI: GET /v1/admin/payments?date=YYYY-MM-DD
    AdminAPI->>PaymentMS: GET /api/v1/payment?date=YYYY-MM-DD
    PaymentMS-->>AdminAPI: PaymentListResponse
    AdminAPI-->>Terminal: [AdminPaymentListItemResponse]
    Note over AdminAPI,Terminal: paymentId, bookingReference,<br/>method, amount, status, eventCount

    Terminal->>AdminAPI: GET /v1/admin/payments/{paymentId}
    AdminAPI->>PaymentMS: GET /api/v1/payment/{paymentId}
    PaymentMS-->>AdminAPI: PaymentRecord
    AdminAPI-->>Terminal: AdminPaymentResponse

    Terminal->>AdminAPI: GET /v1/admin/payments/{paymentId}/events
    AdminAPI->>PaymentMS: GET /api/v1/payment/{paymentId}/events
    PaymentMS-->>AdminAPI: PaymentEventsResponse
    AdminAPI-->>Terminal: [AdminPaymentEventResponse]
    Note over AdminAPI,Terminal: eventType (Authorise/Settle/Void),<br/>productType, amount, createdAt
```
