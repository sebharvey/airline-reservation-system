# Delivery — sequence diagrams

The Delivery microservice is called from within orchestration handlers — never directly from the web frontend. This file documents all delivery interactions grouped by the triggering capability.

---

## E-ticket issuance (booking confirmation)

Called from within `ConfirmBasketHandler` after the order is confirmed. The endpoint is `POST /api/v1/tickets`.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant DeliveryMS as Delivery MS
    participant OrderMS as Order MS

    RetailAPI->>DeliveryMS: POST /api/v1/tickets
    Note over RetailAPI,DeliveryMS: {bookingReference, passengers[],<br/>segments[], paymentId, fareAmount,<br/>currency, fareBasisCodes}
    DeliveryMS-->>RetailAPI: IssuedTickets
    Note over DeliveryMS,RetailAPI: [{passengerId, segmentIds,<br/>eTicketNumber}]

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/tickets
    Note over RetailAPI,OrderMS: Write e-ticket numbers back to order record
    OrderMS-->>RetailAPI: Updated
```

---

## Ancillary EMD issuance (booking confirmation)

EMDs are issued for each paid ancillary type (seats, bags, products) during the post-confirm parallel phase. EMD issuance failures are logged but do not block the confirmed order.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant DeliveryMS as Delivery MS

    opt Seat ancillaries
        loop For each paid seat selection
            RetailAPI->>DeliveryMS: POST /api/v1/documents
            Note over RetailAPI,DeliveryMS: {bookingReference,<br/>type=SeatAncillary,<br/>passengerId, segmentId,<br/>price, currency, paymentId}
            DeliveryMS-->>RetailAPI: Document issued (EMD)
        end
    end

    opt Bag ancillaries
        loop For each bag selection
            RetailAPI->>DeliveryMS: POST /api/v1/documents
            Note over RetailAPI,DeliveryMS: {type=BagAncillary,<br/>passengerId, segmentId,<br/>price, currency, paymentId}
            DeliveryMS-->>RetailAPI: Document issued (EMD)
        end
    end

    opt Product ancillaries
        loop For each product selection
            RetailAPI->>DeliveryMS: POST /api/v1/documents
            Note over RetailAPI,DeliveryMS: {type=ProductAncillary,<br/>passengerId, segmentId,<br/>price, currency, paymentId}
            DeliveryMS-->>RetailAPI: Document issued (EMD)
        end
    end
```

---

## Passenger manifest write (booking confirmation)

A manifest entry is written per segment after tickets are issued (requires eTicketNumbers), linking the order to each flight for IROPS and check-in use.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant DeliveryMS as Delivery MS

    loop Per segment
        RetailAPI->>DeliveryMS: POST /api/v1/manifest
        Note over RetailAPI,DeliveryMS: {bookingReference, orderId,<br/>flightNumber, departureDate,<br/>passengers[], issuedTickets[], seatAssignments}
        DeliveryMS-->>RetailAPI: Manifest written
    end
```

---

## E-ticket reissuance (change flight or IROPS)

The reissue endpoint voids old tickets and issues new ones atomically. Used for both voluntary flight changes and IROPS rebooking.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant DeliveryMS as Delivery MS
    participant OrderMS as Order MS

    RetailAPI->>DeliveryMS: POST /api/v1/tickets/reissue
    Note over RetailAPI,DeliveryMS: {bookingReference,<br/>reason=VoluntaryChange|IropsRebooking,<br/>existingTicketNumbers[], passengers[], newSegments[]}
    DeliveryMS-->>RetailAPI: NewIssuedTickets (new eTicketNumbers)

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/tickets
    Note over RetailAPI,OrderMS: Update order with new e-ticket numbers
    OrderMS-->>RetailAPI: Updated
```

---

## E-ticket void (standalone)

Used when voiding tickets independently (e.g., during cancellation flow).

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant DeliveryMS as Delivery MS

    loop For each e-ticket number
        RetailAPI->>DeliveryMS: PATCH /api/v1/tickets/{eTicketNumber}/void
        Note over RetailAPI,DeliveryMS: {reason}
        DeliveryMS-->>RetailAPI: Voided
    end
```

---

## Manifest retrieval

```mermaid
sequenceDiagram
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS

    OpsAPI->>DeliveryMS: GET /api/v1/manifest?flightNumber={fn}&departureDate={date}
    DeliveryMS-->>OpsAPI: ManifestResponse
    Note over DeliveryMS,OpsAPI: entries[]: {orderId, passengerId,<br/>eTicketNumber, seatNumber, checkInStatus}
```

---

## Manifest seat update

Updates the seat number for a single manifest entry (e.g., seat reassignment at departure).

```mermaid
sequenceDiagram
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS

    OpsAPI->>DeliveryMS: PATCH /api/v1/manifest/{eTicketNumber}/seat
    Note over OpsAPI,DeliveryMS: {inventoryId, seatNumber} — set null to clear seat
    DeliveryMS-->>OpsAPI: Updated
```

---

## Manifest SSR update (post-booking SSR change)

Replaces SSR codes on all manifest entries for a booking reference.

```mermaid
sequenceDiagram
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS

    OpsAPI->>DeliveryMS: PATCH /api/v1/manifest/{bookingReference}
    Note over OpsAPI,DeliveryMS: {ssrCodes[]} — replaces existing SSR codes<br/>on all manifest entries for this booking
    DeliveryMS-->>OpsAPI: Updated
```

---

## Manifest rebook (IROPS)

Updates manifest entries when a passenger is rebooked onto a replacement flight.

```mermaid
sequenceDiagram
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS

    OpsAPI->>DeliveryMS: PATCH /api/v1/manifest/{bookingReference}/flight/{fromFlightNumber}/{departureDate}
    Note over OpsAPI,DeliveryMS: {toFlightNumber, toDepartureDate,<br/>toInventoryId, newETicketNumbers[]}
    DeliveryMS-->>OpsAPI: Manifest updated to replacement flight
```

---

## Manifest delete (flight cancellation cleanup)

Removes manifest entries for a cancelled flight.

```mermaid
sequenceDiagram
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS

    OpsAPI->>DeliveryMS: DELETE /api/v1/manifest/{bookingReference}/flight/{flightNumber}/{departureDate}
    DeliveryMS-->>OpsAPI: Entries removed
```

---

## Online check-in (OCI)

Called from within `OciCheckInHandler`. Timatic validation runs inside the Delivery MS; both `documentcheck` and `apischeck` run per passenger. A failure from either check rejects the entire check-in. On success, coupon status is updated to `C` (checked in) and unassigned seats are auto-allocated within the correct cabin.

```mermaid
sequenceDiagram
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS
    participant Timatic as Timatic Simulator

    OpsAPI->>DeliveryMS: POST /api/v1/oci/checkin
    Note over OpsAPI,DeliveryMS: {departureAirport, tickets[],<br/>cabinConfigs (optional)}

    loop Phase 1 — Timatic validation per passenger
        DeliveryMS->>Timatic: POST /autocheck/v1/documentcheck
        Timatic-->>DeliveryMS: status OK or FAILED

        DeliveryMS->>Timatic: POST /autocheck/v1/apischeck
        Timatic-->>DeliveryMS: apisStatus ACCEPTED or REJECTED
    end

    alt Any Timatic check failed
        DeliveryMS-->>OpsAPI: 422 — document or APIS failure details
    else All passengers passed
        Note over DeliveryMS: Update coupon status to C (checked in)<br/>Auto-assign seat from cabin if no seat recorded
        DeliveryMS-->>OpsAPI: checkedInTicketNumbers[]
    end
```

---

## Boarding pass retrieval

```mermaid
sequenceDiagram
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS

    OpsAPI->>DeliveryMS: POST /api/v1/oci/boarding-docs
    Note over OpsAPI,DeliveryMS: {departureAirport, ticketNumbers[]}
    DeliveryMS->>DeliveryMS: Filter to segments checked in at departure airport<br/>Generate BCBP string per segment
    DeliveryMS-->>OpsAPI: BoardingDocsResponse
    Note over DeliveryMS,OpsAPI: boardingCards[]: ticketNumber,<br/>flightNumber, seatNumber,<br/>sequenceNumber, bcbpString
```

---

## Watchlist management

The watchlist is maintained in the Delivery MS and checked during every check-in.

```mermaid
sequenceDiagram
    participant Terminal as Admin UI
    participant DeliveryMS as Delivery MS

    Terminal->>DeliveryMS: GET /api/v1/watchlist-entries
    DeliveryMS-->>Terminal: WatchlistEntries[]

    Terminal->>DeliveryMS: POST /api/v1/watchlist-entries
    Note over Terminal,DeliveryMS: {givenName, surname, dateOfBirth,<br/>passportNumber}
    DeliveryMS-->>Terminal: WatchlistEntry (watchlistId)

    Terminal->>DeliveryMS: PUT /api/v1/watchlist-entries/{watchlistId}
    Note over Terminal,DeliveryMS: Updated passenger details
    DeliveryMS-->>Terminal: Updated

    Terminal->>DeliveryMS: DELETE /api/v1/watchlist-entries/{watchlistId}
    DeliveryMS-->>Terminal: 204 No Content
```
