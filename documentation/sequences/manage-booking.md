# Manage booking — sequence diagrams

Covers all post-sale order management flows: retrieve, change flight, add bags, update seats, update SSRs, and cancel.

---

## Retrieve order

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS

    Web->>RetailAPI: POST /v1/orders/retrieve
    Note over Web,RetailAPI: {bookingReference, lastName}
    RetailAPI->>OrderMS: POST /api/v1/orders/retrieve
    OrderMS-->>RetailAPI: ManagedOrderResponse
    RetailAPI-->>Web: ManagedOrderResponse
    Note over RetailAPI,Web: Full order: passengers, segments,<br/>fare conditions, e-ticket numbers

    alt Get order by reference
        Web->>RetailAPI: GET /v1/orders/{bookingRef}
        RetailAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
        OrderMS-->>RetailAPI: OrderResponse
        RetailAPI-->>Web: OrderResponse
    end
```

---

## Change flight

A voluntary flight change validates the new offer, optionally takes payment for the fare difference (add-collect), voids original tickets, releases the original inventory, updates the order, reissues tickets, and settles the payment.

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

    Note over RetailAPI: addCollect = max(0, newBaseFare − originalBaseFare)

    opt Revenue booking with add-collect > 0
        RetailAPI->>PaymentMS: POST /api/v1/payment/initialise
        PaymentMS-->>RetailAPI: changePaymentId
        RetailAPI->>PaymentMS: POST /api/v1/payment/{changePaymentId}/authorise
        Note over RetailAPI,PaymentMS: type=FareChange, addCollect amount
        PaymentMS-->>RetailAPI: Authorised
    end

    loop For each e-ticket on original order
        RetailAPI->>DeliveryMS: POST /api/v1/tickets/{eTicketNumber}/void
        DeliveryMS-->>RetailAPI: Voided
    end

    loop For each inventory segment on original order
        RetailAPI->>OfferMS: PATCH /api/v1/inventory/{inventoryId}/release
        Note over RetailAPI,OfferMS: cabinCode, orderId, reason=Sold
        OfferMS-->>RetailAPI: Released
    end

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/change
    Note over RetailAPI,OrderMS: newOfferId, flightNumber, departureDate,<br/>cabinCode, newFareAmounts, addCollect
    OrderMS-->>RetailAPI: Order updated (status=Changed)

    RetailAPI->>DeliveryMS: POST /api/v1/tickets/reissue
    Note over RetailAPI,DeliveryMS: bookingReference, reason=VoluntaryChange,<br/>passengers, new segments
    DeliveryMS-->>RetailAPI: NewIssuedTickets (new eTicketNumbers)

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/etickets
    Note over RetailAPI,OrderMS: Write new e-ticket numbers to order

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
    participant BagMS as Bag MS
    participant PaymentMS as Payment MS
    participant DeliveryMS as Delivery MS

    Web->>RetailAPI: POST /v1/orders/{bookingRef}/bags
    Note over Web,RetailAPI: bagSelections: [{bagOfferId,<br/>passengerId, segmentId}], payment

    RetailAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
    OrderMS-->>RetailAPI: Order (status=Confirmed/Changed, currencyCode)

    loop For each bag selection
        RetailAPI->>BagMS: GET /api/v1/bags/offers/{bagOfferId}
        BagMS-->>RetailAPI: BagOffer (price, tax, isValid)
    end

    RetailAPI->>PaymentMS: POST /api/v1/payment/initialise
    Note over RetailAPI,PaymentMS: method, totalBagAmount
    PaymentMS-->>RetailAPI: paymentId

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise
    Note over RetailAPI,PaymentMS: type=Bag, totalBagAmount, card details
    PaymentMS-->>RetailAPI: Authorised

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
    PaymentMS-->>RetailAPI: Settled

    RetailAPI->>PaymentMS: PATCH /api/v1/payment/{paymentId}/booking-reference
    PaymentMS-->>RetailAPI: Updated

    RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/bags
    Note over RetailAPI,OrderMS: bagSelections with paymentReference
    OrderMS-->>RetailAPI: Updated

    loop For each bag selection
        RetailAPI->>DeliveryMS: Issue BagAncillary document (EMD)
        Note over RetailAPI,DeliveryMS: bookingRef, passengerId,<br/>segmentId, price, paymentId
        DeliveryMS-->>RetailAPI: Document issued
    end

    RetailAPI-->>Web: AddOrderBagsResponse
    Note over RetailAPI,Web: bookingReference, totalBagAmount, paymentId
```

---

## Update seats post-sale

Two variants exist: free seat reassignment (order update only) and paid seat purchase (payment, inventory, EMD issuance).

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant SeatMS as Seat MS
    participant OfferMS as Offer MS
    participant PaymentMS as Payment MS
    participant DeliveryMS as Delivery MS

    Web->>RetailAPI: PATCH /v1/orders/{bookingRef}/seats
    Note over Web,RetailAPI: seatSelections: [{passengerId,<br/>segmentId, seatNumber, seatOfferId?}]

    RetailAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
    OrderMS-->>RetailAPI: Order (status=Confirmed/Changed)

    alt Free seat change (no seatOfferId)
        RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/seats
        OrderMS-->>RetailAPI: Updated
        RetailAPI-->>Web: UpdateOrderSeatsResponse (updated=true)

    else Paid seat purchase
        loop For each paid seat selection
            RetailAPI->>SeatMS: GET /api/v1/seat-offers/{seatOfferId}
            SeatMS-->>RetailAPI: SeatOffer (price, tax, isChargeable, isSelectable)
        end

        RetailAPI->>PaymentMS: POST /api/v1/payment/initialise
        PaymentMS-->>RetailAPI: paymentId

        RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise
        Note over RetailAPI,PaymentMS: type=Seat, totalSeatAmount
        PaymentMS-->>RetailAPI: Authorised

        loop For each inventory group (inventoryId + cabinCode)
            RetailAPI->>OfferMS: Hold seat inventory
            OfferMS-->>RetailAPI: Held
        end
        RetailAPI->>OfferMS: Sell seat inventory
        OfferMS-->>RetailAPI: Sold

        RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
        PaymentMS-->>RetailAPI: Settled

        RetailAPI->>PaymentMS: PATCH /api/v1/payment/{paymentId}/booking-reference
        PaymentMS-->>RetailAPI: Updated

        RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/seats
        Note over RetailAPI,OrderMS: All selections (paid + free) with paymentReference
        OrderMS-->>RetailAPI: Updated

        loop For each paid seat
            RetailAPI->>DeliveryMS: Issue SeatAncillary document (EMD)
            DeliveryMS-->>RetailAPI: Document issued
        end

        RetailAPI-->>Web: UpdateOrderSeatsResponse
        Note over RetailAPI,Web: updated=true, totalSeatAmount, paymentId
    end
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

Cancellation voids tickets, releases inventory, reinstates loyalty points for reward bookings, and cancels the order record. Refund processing is handled downstream by the Accounting domain via the OrderCancelled event published by the Order MS.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant DeliveryMS as Delivery MS
    participant OfferMS as Offer MS
    participant CustomerMS as Customer MS

    Web->>RetailAPI: POST /v1/orders/{bookingRef}/cancel

    RetailAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
    OrderMS-->>RetailAPI: Order (status=Confirmed/Changed,<br/>fareConditions, eTickets, inventoryIds,<br/>bookingType, loyaltyNumber?)

    loop For each e-ticket
        RetailAPI->>DeliveryMS: POST /api/v1/tickets/{eTicketNumber}/void
        DeliveryMS-->>RetailAPI: Voided
    end

    loop For each inventory segment
        RetailAPI->>OfferMS: PATCH /api/v1/inventory/{inventoryId}/release
        OfferMS-->>RetailAPI: Released
    end

    opt Reward booking with points redeemed
        RetailAPI->>CustomerMS: POST /api/v1/customers/{loyaltyNumber}/points/reinstate
        Note over RetailAPI,CustomerMS: points, reason=VoluntaryCancellation
        CustomerMS-->>RetailAPI: Points reinstated
    end

    RetailAPI->>OrderMS: POST /api/v1/orders/{bookingRef}/cancel
    Note over RetailAPI,OrderMS: refundableAmount, cancellationFeeAmount,<br/>reason=VoluntaryCancellation
    OrderMS-->>RetailAPI: Cancelled (publishes OrderCancelled event)

    RetailAPI-->>Web: CancelOrderResponse
    Note over RetailAPI,Web: bookingReference, status=Cancelled,<br/>refundableAmount, refundInitiated
```
