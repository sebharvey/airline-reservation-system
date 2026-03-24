# Manage booking

## Update passenger details

Passenger details may need updating post-booking for passports, name corrections, or contact information. Accurate **Advance Passenger Information (API)** is a regulatory requirement for international travel.

- PAX name or identity changes trigger e-ticket **reissuance** — a new e-ticket number is generated while the booking reference remains unchanged.
- Minor name corrections (a single transposed character) are typically applied as a waiver; anything beyond that is subject to the fare's change conditions.
- Passport number, nationality, date of birth, and document expiry must match the document presented at the border.

```mermaid
sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order [MS]
    participant DeliveryMS as Delivery [MS]

    Traveller->>Web: Navigate to manage booking

    Web->>RetailAPI: POST /v1/orders/retrieve (bookingReference, givenName, surname)
    RetailAPI->>OrderMS: POST /v1/orders/retrieve (bookingReference, givenName, surname)
    OrderMS-->>RetailAPI: 200 OK — order details (PAX details, itinerary, e-tickets)
    RetailAPI-->>Web: 200 OK — display current booking details

    Traveller->>Web: Update passenger details (e.g. name, passport, contact info)

    Web->>RetailAPI: PATCH /v1/orders/{bookingRef}/passengers (updated PAX details)
    RetailAPI->>OrderMS: PATCH /v1/orders/{bookingRef}/passengers (updated PAX details)
    OrderMS-->>RetailAPI: 200 OK — order updated

    opt Name change detected (givenName or surname modified)
        RetailAPI->>DeliveryMS: POST /v1/tickets/reissue (bookingReference, updated PAX name)
        DeliveryMS-->>RetailAPI: 200 OK — new e-ticket numbers issued (old numbers voided)
        Note over RetailAPI: Passport, contact, and travel document updates do not trigger reissuance
    end

    RetailAPI-->>Web: 200 OK — update confirmed (bookingReference, e-ticket numbers if reissued)
    Web-->>Traveller: Display updated booking confirmation
```

*Ref: manage booking - update passenger details; e-ticket reissuance is triggered only when the passenger name changes (name is encoded in the BCBP barcode string)*

## Change flight

A voluntary flight change is a customer-initiated itinerary modification governed entirely by the fare conditions of the originally purchased ticket.

- Changeability is fare-dependent: non-changeable, changeable with a fee, or fully flexible (no charge).
- A **reshop** is performed to obtain a live fare for the new itinerary; if the new base fare exceeds the original, an **add-collect** is due — fare difference plus any applicable change fee.
- Where the new fare is equal to or lower, the customer pays the change fee only; no residual value is returned.
- On confirmation, the original e-ticket is voided and a new e-ticket issued; seat ancillaries are not automatically transferred and must be reselected.

```mermaid
sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order [MS]
    participant OfferMS as Offer [MS]
    participant CustomerMS as Customer [MS]
    participant DeliveryMS as Delivery [MS]
    participant PaymentMS as Payment [MS]

    Traveller->>Web: Navigate to manage booking and request a flight change
    Web->>RetailAPI: POST /v1/orders/retrieve (bookingReference, givenName, surname)
    RetailAPI->>OrderMS: POST /v1/orders/retrieve
    OrderMS-->>RetailAPI: 200 OK — order detail (segments, fareBasisCode, isChangeable, changeFee, originalBaseFare, paymentReference, bookingType, loyaltyNumber, totalPointsAmount)
    RetailAPI-->>Web: 200 OK — current booking with change conditions

    alt Fare is not changeable (isChangeable = false)
        RetailAPI-->>Web: 422 Unprocessable — fare conditions do not permit a voluntary change
        Web-->>Traveller: This fare cannot be changed
    end

    Web-->>Traveller: Display current booking- traveller selects new date and/or flight

    Note over RetailAPI,OfferMS: Reshop — obtain live fare for the new itinerary at the same cabin class
    Web->>RetailAPI: POST /v1/search/slice (origin, destination, newDate, cabinCode, paxCount)
    RetailAPI->>OfferMS: POST /v1/search (origin, destination, newDate, cabinCode, paxCount)
    OfferMS-->>RetailAPI: 200 OK — available flights with live fares (newOfferId, newBaseFare, newTaxes, newTotal, newPointsPrice, isChangeable)
    RetailAPI-->>Web: 200 OK — available replacement flights and fares
    Web-->>Traveller: Display replacement options- traveller selects new flight

    alt Revenue booking — calculate add-collect in cash
        Note over RetailAPI: addCollect = max(0, newBaseFare − originalBaseFare)
        Note over RetailAPI: totalDue = changeFee + addCollect

        alt Add-collect or change fee applies (totalDue > 0)
            RetailAPI-->>Web: Fare summary (originalFare, newFare, changeFee, addCollect, totalDue)
            Web-->>Traveller: Confirm change and provide payment details
            Web->>RetailAPI: POST /v1/orders/{bookingRef}/change (newOfferId, totalDue, paymentDetails)
            RetailAPI->>PaymentMS: POST /v1/payment/authorise (amount=totalDue, currency, cardDetails, description=FareChange)
            PaymentMS-->>RetailAPI: 200 OK — paymentReference, authorisedAmount
        else No additional charge (totalDue = 0 — fully flexible fare, new fare equal or lower)
            RetailAPI-->>Web: Change summary — no additional payment required
            Web-->>Traveller: Confirm change
            Web->>RetailAPI: POST /v1/orders/{bookingRef}/change (newOfferId)
        end

    else Reward booking — recalculate points difference
        Note over RetailAPI: pointsDifference = newPointsPrice − originalPointsAmount
        Note over RetailAPI: taxDifference = max(0, newTaxes − originalTaxes)

        alt New flight requires more points (pointsDifference > 0)
            RetailAPI->>CustomerMS: GET /v1/customers/{loyaltyNumber}
            CustomerMS-->>RetailAPI: 200 OK — current pointsBalance
            Note over RetailAPI: Verify pointsBalance >= pointsDifference
            RetailAPI-->>Web: Points summary (originalPoints, newPoints, pointsDifference, taxDifference)
            Web-->>Traveller: Confirm change — additional points and/or tax payment required
            Web->>RetailAPI: POST /v1/orders/{bookingRef}/change (newOfferId, pointsDifference, taxDifference, paymentDetails)
            RetailAPI->>CustomerMS: POST /v1/customers/{loyaltyNumber}/points/authorise (points=pointsDifference, bookingRef)
            CustomerMS-->>RetailAPI: 200 OK — redemptionReference for additional points
        else New flight requires fewer or equal points (pointsDifference <= 0)
            RetailAPI-->>Web: Change summary — points difference will be reinstated
            Web-->>Traveller: Confirm change
            Web->>RetailAPI: POST /v1/orders/{bookingRef}/change (newOfferId)
        end

        opt Tax difference applies (taxDifference > 0)
            RetailAPI->>PaymentMS: POST /v1/payment/authorise (amount=taxDifference, currency, cardDetails, description=RewardChangeTaxes)
            PaymentMS-->>RetailAPI: 200 OK — paymentReference, authorisedAmount
        end
    end

    RetailAPI->>OfferMS: POST /v1/inventory/hold (newInventoryId, cabinCode, seats=paxCount)
    OfferMS-->>RetailAPI: 200 OK — seats held on new flight

    loop For each e-ticket on the changed segment
        RetailAPI->>DeliveryMS: PATCH /v1/tickets/{eTicketNumber}/void
        DeliveryMS-->>RetailAPI: 200 OK — original e-ticket voided
    end

    RetailAPI->>DeliveryMS: DELETE /v1/manifest/{bookingRef}/flight/{originalFlightNumber}/{originalDepartureDate}
    DeliveryMS-->>RetailAPI: 200 OK — manifest entries removed for original flight

    RetailAPI->>OfferMS: POST /v1/inventory/release (originalInventoryId, cabinCode, seats=paxCount)
    OfferMS-->>RetailAPI: 200 OK — seats released from original flight- SeatsAvailable incremented

    alt Revenue booking
        RetailAPI->>OrderMS: PATCH /v1/orders/{bookingRef}/change (cancelledSegmentId, newOfferId, changeFee, addCollect, paymentReference)
    else Reward booking
        RetailAPI->>OrderMS: PATCH /v1/orders/{bookingRef}/change (cancelledSegmentId, newOfferId, pointsDifference, redemptionReference, taxPaymentReference, bookingType=Reward)
    end
    OrderMS-->>RetailAPI: 200 OK — order updated (OrderStatus=Changed)- OrderChanged event published

    RetailAPI->>DeliveryMS: POST /v1/tickets/reissue (bookingReference, voidedETicketNumbers, newSegments)
    DeliveryMS-->>RetailAPI: 200 OK — new e-ticket numbers issued

    RetailAPI->>DeliveryMS: POST /v1/manifest (newInventoryId, bookingReference, newETicketNumbers, passengerIds)
    DeliveryMS-->>RetailAPI: 201 Created — manifest entries written for new flight

    opt Reward booking — settle points adjustment
        alt Additional points were redeemed (pointsDifference > 0)
            RetailAPI->>CustomerMS: POST /v1/customers/{loyaltyNumber}/points/settle (redemptionReference)
            CustomerMS-->>RetailAPI: 200 OK — additional points deducted
        else Points to be reinstated (pointsDifference < 0)
            RetailAPI->>CustomerMS: POST /v1/customers/{loyaltyNumber}/points/reinstate (points=abs(pointsDifference), bookingRef, reason=FlightChange)
            CustomerMS-->>RetailAPI: 200 OK — surplus points restored to balance
        end
    end

    alt Revenue booking — payment was collected (totalDue > 0)
        RetailAPI->>PaymentMS: POST /v1/payment/{paymentReference}/settle (settledAmount=totalDue)
        PaymentMS-->>RetailAPI: 200 OK — add-collect and change fee settled
    else Reward booking — tax difference was collected (taxDifference > 0)
        RetailAPI->>PaymentMS: POST /v1/payment/{paymentReference}/settle (settledAmount=taxDifference)
        PaymentMS-->>RetailAPI: 200 OK — tax difference settled
    end

    RetailAPI-->>Web: 200 OK — change confirmed (new itinerary, new e-ticket numbers)
    Web-->>Traveller: Change confirmed — new itinerary and updated e-ticket details displayed
```

*Ref: manage booking - voluntary flight change with reshop, add-collect (revenue) or points recalculation (reward), and e-ticket reissuance*

## Cancel booking

A voluntary cancellation is a customer-initiated request governed by the fare conditions of the originally issued ticket.

- Fares are non-refundable (full forfeiture), partially refundable (fixed cancellation fee deducted), or fully refundable (total amount returned).
- Regardless of refundability, the e-ticket must be voided and inventory released — a cancelled booking must not hold seat inventory.
- When a refund is due, the Order MS publishes an `OrderCancelled` event (containing `refundableAmount` and `originalPaymentReference`) to the Accounting system via the event bus. The Accounting system is responsible for initiating and settling the refund with the external payments provider — this is handled entirely outside the reservation system's synchronous booking path.
- Government-imposed taxes (e.g. UK Air Passenger Duty) may be refundable even on non-refundable fares; selective tax refund handling is out of scope for this phase.

> **Refund responsibility boundary:** Refund execution is fully external to the reservation system. The reservation system raises the `OrderCancelled` event with the refundable amount and payment reference; the Accounting system consumes this event and issues the refund directly to the payment provider. The reservation system's Payment MS (`POST /v1/payment/{paymentReference}/refund`) is not called as part of the voluntary cancellation flow — it exists only for scenarios where the reservation system itself must initiate a refund programmatically (e.g. automated reversals triggered by payment failures during the bookflow).

```mermaid
sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order [MS]
    participant OfferMS as Offer [MS]
    participant CustomerMS as Customer [MS]
    participant DeliveryMS as Delivery [MS]
    participant AccountingMS as Accounting [MS]

    Traveller->>Web: Navigate to manage booking and request cancellation
    Web->>RetailAPI: POST /v1/orders/retrieve (bookingReference, givenName, surname)
    RetailAPI->>OrderMS: POST /v1/orders/retrieve
    OrderMS-->>RetailAPI: 200 OK — order detail (segments, isRefundable, cancellationFee, totalPaid, originalPaymentReference, bookingType, loyaltyNumber, totalPointsAmount, redemptionReference)

    alt Revenue booking
        Note over RetailAPI: refundableAmount = isRefundable ? (totalPaid − cancellationFee) : 0
        RetailAPI-->>Web: 200 OK — booking with cancellation conditions and refundable amount
        Web-->>Traveller: Display cancellation terms (refundableAmount, cancellationFee if applicable, or no refund notice)
    else Reward booking
        Note over RetailAPI: pointsToRestore = totalPointsAmount (full points restoration on cancellation)
        Note over RetailAPI: taxRefundable = isRefundable ? (totalTaxesPaid − cancellationFee) : 0
        RetailAPI-->>Web: 200 OK — booking with cancellation conditions, points to be restored, and tax refund amount
        Web-->>Traveller: Display cancellation terms (points to be restored, tax refund if applicable)
    end

    Traveller->>Web: Confirm cancellation
    Web->>RetailAPI: POST /v1/orders/{bookingRef}/cancel

    loop For each e-ticket on the booking
        RetailAPI->>DeliveryMS: PATCH /v1/tickets/{eTicketNumber}/void
        DeliveryMS-->>RetailAPI: 200 OK — e-ticket voided
    end

    RetailAPI->>DeliveryMS: DELETE /v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}
    DeliveryMS-->>RetailAPI: 200 OK — manifest entries removed

    RetailAPI->>OfferMS: POST /v1/inventory/release (inventoryId, cabinCode, seats=paxCount)
    OfferMS-->>RetailAPI: 200 OK — seats released- SeatsAvailable incremented

    opt Reward booking — restore redeemed points to customer balance
        RetailAPI->>CustomerMS: POST /v1/customers/{loyaltyNumber}/points/reinstate (points=totalPointsAmount, bookingRef, reason=VoluntaryCancellation)
        CustomerMS-->>RetailAPI: 200 OK — points restored to balance, Reinstate transaction appended
        Note over CustomerMS: PointsBalance incremented, LoyaltyTransaction appended (type=Reinstate)
    end

    RetailAPI->>OrderMS: PATCH /v1/orders/{bookingRef}/cancel (reason=VoluntaryCancellation, cancellationFee, bookingType)
    OrderMS-->>RetailAPI: 200 OK — OrderStatus=Cancelled- OrderChanged event published

    alt Revenue booking — refund is due (isRefundable = true)
        OrderMS-)AccountingMS: OrderCancelled event (bookingRef, refundableAmount=totalPaid−cancellationFee, originalPaymentReference)
        RetailAPI-->>Web: 200 OK — booking cancelled- refund raised with Accounting system
        Web-->>Traveller: Booking cancelled — your refund will be processed by the Accounting team
    else Revenue booking — non-refundable fare (isRefundable = false)
        RetailAPI-->>Web: 200 OK — booking cancelled- no refund applicable for this fare type
        Web-->>Traveller: Booking cancelled — no refund will be issued
    else Reward booking — points restored, tax refund if applicable
        opt Tax refund is due (taxRefundable > 0)
            OrderMS-)AccountingMS: OrderCancelled event (bookingRef, refundableAmount=taxRefundable, originalPaymentReference, bookingType=Reward)
        end
        RetailAPI-->>Web: 200 OK — booking cancelled- points restored to loyalty account
        Web-->>Traveller: Booking cancelled — points have been restored to your account
    end
```

*Ref: manage booking - voluntary cancellation with inventory release, points restoration (reward bookings), and accounting refund event*
