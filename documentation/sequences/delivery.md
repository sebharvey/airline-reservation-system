# Delivery — sequence diagrams

The Delivery microservice is called from within orchestration handlers — never directly from the web frontend. This file documents all delivery interactions grouped by the triggering capability.

---

## E-ticket issuance (booking confirmation)

Called from within `ConfirmBasketHandler` after the order is confirmed.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant DeliveryMS as Delivery MS
    participant OrderMS as Order MS

    RetailAPI->>DeliveryMS: POST /api/v1/tickets/issue
    Note over RetailAPI,DeliveryMS: {bookingReference, passengers[],<br/>segments[], paymentId, fareAmount,<br/>currency}
    DeliveryMS-->>RetailAPI: IssuedTickets
    Note over DeliveryMS,RetailAPI: [{passengerId, segmentIds,<br/>eTicketNumber}]

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/etickets
    Note over RetailAPI,OrderMS: Write e-ticket numbers back to order record
    OrderMS-->>RetailAPI: Updated
```

---

## Ancillary EMD issuance (booking confirmation)

EMDs are issued for each paid ancillary type (seats, bags, products) during the post-confirm parallel phase.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant DeliveryMS as Delivery MS

    opt Seat ancillaries
        loop For each paid seat selection
            RetailAPI->>DeliveryMS: POST /api/v1/documents/issue
            Note over RetailAPI,DeliveryMS: {bookingReference,<br/>type=SeatAncillary,<br/>passengerId, segmentId,<br/>price, currency, paymentId}
            DeliveryMS-->>RetailAPI: Document issued (EMD)
        end
    end

    opt Bag ancillaries
        loop For each bag selection
            RetailAPI->>DeliveryMS: POST /api/v1/documents/issue
            Note over RetailAPI,DeliveryMS: {type=BagAncillary,<br/>passengerId, segmentId,<br/>price, currency, paymentId}
            DeliveryMS-->>RetailAPI: Document issued (EMD)
        end
    end

    opt Product ancillaries
        loop For each product selection
            RetailAPI->>DeliveryMS: POST /api/v1/documents/issue
            Note over RetailAPI,DeliveryMS: {type=ProductAncillary,<br/>passengerId, segmentId,<br/>price, currency, paymentId}
            DeliveryMS-->>RetailAPI: Document issued (EMD)
        end
    end
```

---

## Passenger manifest write (booking confirmation)

A manifest entry is written after all tickets and EMDs are issued, linking the order to each flight for IROPS and check-in use.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant DeliveryMS as Delivery MS

    RetailAPI->>DeliveryMS: POST /api/v1/manifest
    Note over RetailAPI,DeliveryMS: {bookingReference, orderId,<br/>segments[], issuedTickets[]}
    DeliveryMS-->>RetailAPI: Manifest written
```

---

## E-ticket void (change flight or cancellation)

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant DeliveryMS as Delivery MS

    loop For each e-ticket number on order
        RetailAPI->>DeliveryMS: POST /api/v1/tickets/{eTicketNumber}/void
        DeliveryMS-->>RetailAPI: Voided
    end
```

---

## E-ticket reissuance (change flight)

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant DeliveryMS as Delivery MS
    participant OrderMS as Order MS

    RetailAPI->>DeliveryMS: POST /api/v1/tickets/reissue
    Note over RetailAPI,DeliveryMS: {bookingReference,<br/>reason=VoluntaryChange,<br/>passengers[], newSegments[]}
    DeliveryMS-->>RetailAPI: NewIssuedTickets (new eTicketNumbers)

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/etickets
    Note over RetailAPI,OrderMS: Update order with new e-ticket numbers
    OrderMS-->>RetailAPI: Updated
```

---

## Coupon status update (check-in completion)

```mermaid
sequenceDiagram
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS

    OpsAPI->>DeliveryMS: PATCH /api/v1/tickets/coupon-status
    Note over OpsAPI,DeliveryMS: {bookingReference, departureAirport}<br/>Sets coupon status to 'C' (checked in)
    DeliveryMS-->>OpsAPI: Updated
```

---

## Boarding pass retrieval (check-in)

```mermaid
sequenceDiagram
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS

    OpsAPI->>DeliveryMS: GET /api/v1/boarding-docs
    Note over OpsAPI,DeliveryMS: {departureAirport,<br/>ticketNumbers[]}
    DeliveryMS-->>OpsAPI: BoardingDocsResponse
    Note over DeliveryMS,OpsAPI: boardingCards[]: ticketNumber,<br/>flightNumber, seatNumber,<br/>sequenceNumber, bcbpString
```

---

## Manifest retrieval (IROPS disruption)

Called from within `AdminDisruptionCancelHandler` to identify all passengers booked on a cancelled flight.

```mermaid
sequenceDiagram
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS

    OpsAPI->>DeliveryMS: GET /api/v1/manifest/{flightNumber}/{departureDate}
    DeliveryMS-->>OpsAPI: ManifestResponse
    Note over DeliveryMS,OpsAPI: entries[]: {orderId, passengerId,<br/>eTicketNumber, checkInStatus}
```
