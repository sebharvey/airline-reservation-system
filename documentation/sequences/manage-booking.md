# Manage booking — sequence diagrams

Covers all post-sale order management flows: retrieve, change flight, add bags, update seats, add check-in ancillaries, update SSRs, and cancel.

---

## Retrieve order

`GetOrderHandler` fetches the order record and both ticket and document data in parallel.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant DeliveryMS as Delivery MS

    Web->>RetailAPI: POST /v1/orders/retrieve
    Note over Web,RetailAPI: {bookingReference, lastName}
    RetailAPI->>OrderMS: POST /api/v1/orders/retrieve
    OrderMS-->>RetailAPI: Order
    RetailAPI-->>Web: ManagedOrderResponse
    Note over RetailAPI,Web: Full order: passengers, segments,<br/>fare conditions, e-ticket numbers

    Web->>RetailAPI: GET /v1/orders/{bookingRef}
    Note over Web,RetailAPI: Requires manage-booking JWT or staff JWT

    par
        RetailAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
        OrderMS-->>RetailAPI: OrderRecord
    and
        RetailAPI->>DeliveryMS: GET /api/v1/tickets?bookingRef={bookingRef}
        DeliveryMS-->>RetailAPI: IssuedTickets[]
    and
        RetailAPI->>DeliveryMS: GET /api/v1/documents?bookingRef={bookingRef}
        DeliveryMS-->>RetailAPI: Documents[] (EMDs)
    end

    RetailAPI-->>Web: ManagedOrderResponse
```

---

## Change flight

A voluntary flight change validates the new offer, optionally takes payment for any fare difference (add-collect), releases the original inventory, updates the order, reissues tickets (which internally voids old tickets), then settles payment and updates the manifest.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant OfferMS as Offer MS
    participant PaymentMS as Payment MS
    participant DeliveryMS as Delivery MS

    Web->>RetailAPI: POST /v1/orders/{bookingRef}/change
    Note over Web,RetailAPI: {newOfferId, payment?{method,<br/>cardNumber, expiryDate, cvv}}

    RetailAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
    OrderMS-->>RetailAPI: Order (status=Confirmed/Changed,<br/>isChangeable, originalBaseFare)

    RetailAPI->>OfferMS: GET /api/v1/offers/{newOfferId}
    OfferMS-->>RetailAPI: NewOffer (newBaseFare, flightDetails)

    RetailAPI->>OfferMS: POST /api/v1/offers/{newOfferId}/reprice
    OfferMS-->>RetailAPI: Repriced offer (validated pricing)

    Note over RetailAPI: addCollect = max(0, newBaseFare - originalBaseFare)

    opt Revenue booking with add-collect > 0
        RetailAPI->>PaymentMS: POST /api/v1/payment/initialise
        PaymentMS-->>RetailAPI: changePaymentId
        RetailAPI->>PaymentMS: POST /api/v1/payment/{changePaymentId}/authorise
        Note over RetailAPI,PaymentMS: type=FareChange, addCollect amount
        PaymentMS-->>RetailAPI: Authorised
    end

    loop For each original inventory segment
        RetailAPI->>OfferMS: POST /api/v1/inventory/release
        Note over RetailAPI,OfferMS: inventoryId, cabinCode, orderId,<br/>releaseType=Cancellation
        OfferMS-->>RetailAPI: Released
    end

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/change
    Note over RetailAPI,OrderMS: newOfferId, flightNumber, departureDate,<br/>cabinCode, newFareAmounts, addCollect
    OrderMS-->>RetailAPI: Order updated (status=Changed)

    RetailAPI->>DeliveryMS: POST /api/v1/tickets/reissue
    Note over RetailAPI,DeliveryMS: bookingReference, reason=VoluntaryChange,<br/>existingTicketNumbers[], passengers[], newSegments[]
    DeliveryMS-->>RetailAPI: NewIssuedTickets (new eTicketNumbers)

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/tickets
    Note over RetailAPI,OrderMS: Write new e-ticket numbers to order

    loop Per new segment
        RetailAPI->>DeliveryMS: PATCH /api/v1/manifest/{bookingRef}/flight/{oldFlight}/{oldDate}
        Note over RetailAPI,DeliveryMS: Update manifest to new flight
        DeliveryMS-->>RetailAPI: Manifest updated
    end

    opt Revenue booking with add-collect > 0
        RetailAPI->>PaymentMS: POST /api/v1/payment/{changePaymentId}/settle
        PaymentMS-->>RetailAPI: Settled
    end

    RetailAPI-->>Web: ChangeOrderResponse
    Note over RetailAPI,Web: bookingReference, newFlightNumber,<br/>newDepartureDate, totalDue, newETicketNumbers
```

---

## Add bags post-sale

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant PaymentMS as Payment MS
    participant DeliveryMS as Delivery MS

    Web->>RetailAPI: POST /v1/orders/{bookingRef}/bags
    Note over Web,RetailAPI: bagSelections: [{passengerId, segmentId,<br/>price, tax, currency}], payment

    RetailAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
    OrderMS-->>RetailAPI: Order (status=Confirmed/Changed)

    RetailAPI->>PaymentMS: POST /api/v1/payment/initialise
    Note over RetailAPI,PaymentMS: method, totalBagAmount
    PaymentMS-->>RetailAPI: paymentId

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise
    Note over RetailAPI,PaymentMS: type=Bag, totalBagAmount, card details
    PaymentMS-->>RetailAPI: Authorised

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
    PaymentMS-->>RetailAPI: Settled

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/bags
    Note over RetailAPI,OrderMS: bagSelections with paymentReference
    OrderMS-->>RetailAPI: Updated

    loop For each bag selection
        RetailAPI->>DeliveryMS: POST /api/v1/documents
        Note over RetailAPI,DeliveryMS: bookingRef, type=BagAncillary,<br/>passengerId, segmentId, price, paymentId
        DeliveryMS-->>RetailAPI: Document issued (EMD)
    end

    alt Any step fails
        RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/void
        PaymentMS-->>RetailAPI: Voided
    end

    RetailAPI-->>Web: AddOrderBagsResponse
    Note over RetailAPI,Web: bookingReference, totalBagAmount, paymentId
```

---

## Update seats post-sale

Seat reassignment is a free order update. The Retail API enriches seat price and tax from the Seat MS then updates the order record — no payment or inventory management occurs in this handler.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant SeatMS as Seat MS

    Web->>RetailAPI: PATCH /v1/orders/{bookingRef}/seats
    Note over Web,RetailAPI: seatSelections: [{passengerId,<br/>segmentId, seatNumber, seatOfferId}]

    RetailAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
    OrderMS-->>RetailAPI: Order (status=Confirmed/Changed)

    loop For each seat selection (parallel)
        RetailAPI->>SeatMS: GET /api/v1/seat-offers/{seatOfferId}
        SeatMS-->>RetailAPI: SeatOffer (price, tax)
        Note over RetailAPI: Enrich price and tax from Seat MS
    end

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/seats
    Note over RetailAPI,OrderMS: Enriched seat selections
    OrderMS-->>RetailAPI: Updated

    RetailAPI-->>Web: UpdateOrderSeatsResponse (updated=true)
```

---

## Check-in ancillaries (paid seats and bags at check-in)

Passengers purchasing ancillaries during the check-in flow use `CheckInAncillariesHandler`. A single payment record covers all selected ancillaries for the check-in session.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant PaymentMS as Payment MS
    participant DeliveryMS as Delivery MS

    Web->>RetailAPI: POST /v1/checkin/{bookingRef}/ancillaries
    Note over Web,RetailAPI: {seatSelections[], bagSelections[],<br/>payment{method, cardDetails}}

    RetailAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
    OrderMS-->>RetailAPI: Order (Confirmed/Changed)

    RetailAPI->>PaymentMS: POST /api/v1/payment/initialise
    Note over RetailAPI,PaymentMS: method, totalAncillaryAmount
    PaymentMS-->>RetailAPI: paymentId

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise
    Note over RetailAPI,PaymentMS: type=Ancillary, totalAmount, card details
    PaymentMS-->>RetailAPI: Authorised

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
    PaymentMS-->>RetailAPI: Settled

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/seats
    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/bags

    loop For each seat selection
        RetailAPI->>DeliveryMS: POST /api/v1/documents (type=SeatAncillary EMD)
        DeliveryMS-->>RetailAPI: Document issued
    end

    loop For each bag selection
        RetailAPI->>DeliveryMS: POST /api/v1/documents (type=BagAncillary EMD)
        DeliveryMS-->>RetailAPI: Document issued
    end

    alt Any step fails
        RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/void
        PaymentMS-->>RetailAPI: Voided
    end

    RetailAPI-->>Web: CheckInAncillariesResponse (paymentId, EMDs)
```

---

## Update SSRs post-sale

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS

    Web->>RetailAPI: PATCH /v1/orders/{bookingRef}/ssrs
    Note over Web,RetailAPI: ssrSelections: [{passengerId,<br/>segmentId, ssrCode}]
    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/ssrs
    OrderMS-->>RetailAPI: 204 No Content
    RetailAPI-->>Web: 204 No Content
```

---

## Cancel order

Cancellation marks the order as cancelled, releases all inventory, and issues a refund via the Payment MS. Loyalty points reinstatement for reward bookings is not currently handled in the cancel flow.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant OfferMS as Offer MS
    participant PaymentMS as Payment MS

    Web->>RetailAPI: POST /v1/orders/{bookingRef}/cancel

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/cancel
    Note over RetailAPI,OrderMS: reason=VoluntaryCancellation
    OrderMS-->>RetailAPI: Cancelled (bookingRef, status=Cancelled)

    RetailAPI->>OfferMS: POST /api/v1/inventory/release
    Note over RetailAPI,OfferMS: All held inventory segments,<br/>releaseType=Cancellation
    OfferMS-->>RetailAPI: Released

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/refund
    Note over RetailAPI,PaymentMS: refundAmount, reason=VoluntaryCancellation
    PaymentMS-->>RetailAPI: Refunded

    RetailAPI-->>Web: CancelOrderResponse
    Note over RetailAPI,Web: bookingReference, status=Cancelled,<br/>refundInitiated
```

---

## Admin — manifest view and seat management

Staff can view the full flight manifest and reassign passenger seats. Manifest is the source of truth for seat occupancy at departure.

```mermaid
sequenceDiagram
    participant Terminal as Admin UI
    participant RetailAPI as Retail API
    participant DeliveryMS as Delivery MS
    participant OrderMS as Order MS

    Terminal->>RetailAPI: GET /v1/admin/manifest?flightNumber={fn}&departureDate={date}
    RetailAPI->>DeliveryMS: GET /api/v1/manifest?flightNumber={fn}&departureDate={date}
    DeliveryMS-->>RetailAPI: ManifestEntries[]
    RetailAPI-->>Terminal: ManifestResponse

    Terminal->>RetailAPI: POST /v1/admin/manifest/assign-seat
    Note over Terminal,RetailAPI: {eTicketNumber, bookingReference,<br/>inventoryId, passengerId, seatNumber}
    RetailAPI->>DeliveryMS: PATCH /api/v1/manifest/{eTicketNumber}/seat
    Note over RetailAPI,DeliveryMS: {inventoryId, seatNumber}
    DeliveryMS-->>RetailAPI: Updated

    Note over RetailAPI: Best-effort sync to order record
    RetailAPI-)OrderMS: PATCH /api/v1/orders/{bookingRef}/seats

    RetailAPI-->>Terminal: Updated

    Terminal->>RetailAPI: POST /v1/admin/manifest/release-seat
    Note over Terminal,RetailAPI: {eTicketNumber, bookingReference,<br/>inventoryId, passengerId}
    RetailAPI->>DeliveryMS: PATCH /api/v1/manifest/{eTicketNumber}/seat
    Note over RetailAPI,DeliveryMS: {inventoryId, seatNumber=null}
    DeliveryMS-->>RetailAPI: Updated (seat cleared)

    Note over RetailAPI: Best-effort sync to order record
    RetailAPI-)OrderMS: PATCH /api/v1/orders/{bookingRef}/seats

    RetailAPI-->>Terminal: Updated
```

---

## Admin — order notes

```mermaid
sequenceDiagram
    participant Terminal as Admin UI
    participant RetailAPI as Retail API
    participant OrderMS as Order MS

    Terminal->>RetailAPI: POST /v1/admin/orders/{bookingRef}/notes
    Note over Terminal,RetailAPI: {noteType, message}
    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/notes
    OrderMS-->>RetailAPI: NoteId
    RetailAPI-->>Terminal: 201 Created {noteId}

    Terminal->>RetailAPI: PUT /v1/admin/orders/{bookingRef}/notes/{noteId}
    Note over Terminal,RetailAPI: {noteType, message}
    RetailAPI->>OrderMS: PUT /api/v1/orders/{bookingRef}/notes/{noteId}
    OrderMS-->>RetailAPI: Updated
    RetailAPI-->>Terminal: 204 No Content

    Terminal->>RetailAPI: DELETE /v1/admin/orders/{bookingRef}/notes/{noteId}
    RetailAPI->>OrderMS: DELETE /api/v1/orders/{bookingRef}/notes/{noteId}
    OrderMS-->>RetailAPI: Deleted
    RetailAPI-->>Terminal: 204 No Content
```
