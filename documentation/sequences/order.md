# Order — sequence diagrams

Covers the basket lifecycle and booking confirmation flow. The basket is built incrementally (passengers, seats, bags, SSRs, products) and then confirmed with payment to produce a confirmed order with e-tickets.

---

## Create basket

`CreateBasketHandler` creates the basket in the Order MS, then fetches each offer from the Offer MS (by offerId with session scope, falling back to session-unscoped) and adds it to the basket.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant OfferMS as Offer MS

    Web->>RetailAPI: POST /v1/basket
    Note over Web,RetailAPI: {offerId(s), passengerCount,<br/>bookingType, loyaltyNumber?}

    RetailAPI->>OrderMS: POST /api/v1/basket
    Note over RetailAPI,OrderMS: currency, bookingType, loyaltyNumber
    OrderMS-->>RetailAPI: CreateBasketResponse (basketId, expiresAt)

    loop For each offerId
        RetailAPI->>OfferMS: GET /api/v1/offers/{offerId}?sessionId={sessionId}
        OfferMS-->>RetailAPI: Offer (fares, flight details, cabinCode)
        Note over RetailAPI: Falls back to session-unscoped<br/>GET /api/v1/offers/{offerId} if not found

        RetailAPI->>OrderMS: POST /api/v1/basket/{basketId}/offers
        Note over RetailAPI,OrderMS: offerId, enriched flight and fare data
        OrderMS-->>RetailAPI: 200 OK
    end

    RetailAPI-->>Web: CreateBasketResponse (basketId)
```

---

## Update basket — passengers

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS

    Web->>RetailAPI: PUT /v1/basket/{basketId}/passengers
    Note over Web,RetailAPI: passengers: [{givenName, surname,<br/>type, dateOfBirth, loyaltyNumber?}]
    RetailAPI->>OrderMS: PUT /api/v1/basket/{basketId}/passengers
    OrderMS-->>RetailAPI: 204 No Content
    RetailAPI-->>Web: 204 No Content
```

---

## Update basket — seats

The Retail API enriches each seat selection with authoritative price and tax from the Seat MS before storing. All enrichment calls run in parallel.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant SeatMS as Seat MS
    participant OrderMS as Order MS

    Web->>RetailAPI: PUT /v1/basket/{basketId}/seats
    Note over Web,RetailAPI: seatSelections: [{seatOfferId, passengerId,<br/>segmentId, seatNumber, price, tax, currency}]

    loop Per seat (parallel — WhenAll)
        RetailAPI->>SeatMS: GET /api/v1/seat-offers/{seatOfferId}
        SeatMS-->>RetailAPI: SeatOffer (position, price, tax)
        Note over RetailAPI: Overwrite client price and tax<br/>with Seat MS authoritative values
    end

    RetailAPI->>OrderMS: PUT /api/v1/basket/{basketId}/seats
    Note over RetailAPI,OrderMS: Enriched selections with locked pricing
    OrderMS-->>RetailAPI: 204 No Content

    RetailAPI->>OrderMS: GET /api/v1/basket/{basketId}
    OrderMS-->>RetailAPI: Basket

    RetailAPI-->>Web: BasketResponse (updated totalSeatAmount)
```

---

## Update basket — bags

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS

    Web->>RetailAPI: PUT /v1/basket/{basketId}/bags
    Note over Web,RetailAPI: bagSelections: [{bagOfferId, passengerId,<br/>segmentId, bagSequence, freeBagsIncluded,<br/>additionalBags, price, tax, currency}]

    RetailAPI->>OrderMS: PUT /api/v1/basket/{basketId}/bags
    OrderMS-->>RetailAPI: 204 No Content

    RetailAPI->>OrderMS: GET /api/v1/basket/{basketId}
    OrderMS-->>RetailAPI: Basket

    RetailAPI-->>Web: BasketResponse (updated totalBagAmount)
```

---

## Update basket — SSRs

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS

    Web->>RetailAPI: PUT /v1/basket/{basketId}/ssrs
    Note over Web,RetailAPI: ssrSelections: [{passengerId,<br/>segmentId, ssrCode}]
    RetailAPI->>OrderMS: PUT /api/v1/basket/{basketId}/ssrs
    OrderMS-->>RetailAPI: 204 No Content
    RetailAPI-->>Web: 204 No Content
```

---

## Update basket — products

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS

    Web->>RetailAPI: PUT /v1/basket/{basketId}/products
    Note over Web,RetailAPI: productSelections: [{passengerId,<br/>segmentId, productId}]
    RetailAPI->>OrderMS: PUT /api/v1/basket/{basketId}/products
    OrderMS-->>RetailAPI: 204 No Content
    RetailAPI-->>Web: 204 No Content
```

---

## Get basket summary

Both summary endpoints retrieve the basket then reprice each offer to get the latest tax breakdown.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant OfferMS as Offer MS

    Web->>RetailAPI: GET /v1/basket/{basketId}/summary
    RetailAPI->>OrderMS: GET /api/v1/basket/{basketId}
    OrderMS-->>RetailAPI: BasketResponse (full basket data)

    loop For each offer in basket
        RetailAPI->>OfferMS: POST /api/v1/offers/{offerId}/reprice
        OfferMS-->>RetailAPI: Repriced offer (tax lines)
    end

    Note over RetailAPI: Computes summary totals<br/>(fare, seats, bags, products)
    RetailAPI-->>Web: BasketSummaryResponse

    Web->>RetailAPI: GET /v1/basket/{basketId}/payment-summary
    RetailAPI->>OrderMS: GET /api/v1/basket/{basketId}
    OrderMS-->>RetailAPI: BasketResponse

    loop For each offer in basket
        RetailAPI->>OfferMS: POST /api/v1/offers/{offerId}/reprice
        OfferMS-->>RetailAPI: Repriced offer (tax lines)
    end

    Note over RetailAPI: Computes payment breakdown<br/>by product type
    RetailAPI-->>Web: PaymentSummaryResponse
```

---

## Confirm basket (revenue booking)

The confirm flow is the most complex sequence in the system. It validates the basket, reprices offers, creates a draft order, takes payment, confirms the order, then issues tickets, settles ancillary payments, writes the manifest, and links the loyalty account — all in parallel after confirmation.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant OfferMS as Offer MS
    participant PaymentMS as Payment MS
    participant DeliveryMS as Delivery MS
    participant CustomerMS as Customer MS

    Web->>RetailAPI: POST /v1/basket/{basketId}/confirm
    Note over Web,RetailAPI: {paymentMethod, cardNumber,<br/>expiryDate, cvv, cardholderName}

    RetailAPI->>OrderMS: GET /api/v1/basket/{basketId}
    OrderMS-->>RetailAPI: Basket (status=Active, not expired)

    loop For each offerId in basket
        RetailAPI->>OfferMS: POST /api/v1/offers/{offerId}/reprice
        OfferMS-->>RetailAPI: Repriced offer (validated=true, tax lines locked)
    end

    RetailAPI->>OrderMS: POST /api/v1/orders
    Note over RetailAPI,OrderMS: basketId, channelCode, bookingType=Revenue
    OrderMS-->>RetailAPI: DraftOrder (orderId, status=Draft)

    RetailAPI->>PaymentMS: POST /api/v1/payment/initialise
    Note over RetailAPI,PaymentMS: method, currencyCode, totalAmount
    PaymentMS-->>RetailAPI: paymentId

    RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise
    Note over RetailAPI,PaymentMS: type=Fare, fareAmount, card details
    PaymentMS-->>RetailAPI: Authorised

    alt Auth fails
        RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/void
        RetailAPI->>OrderMS: DELETE /api/v1/orders/{orderId}
        RetailAPI-->>Web: 402 Payment Required
    end

    RetailAPI->>OrderMS: POST /api/v1/orders/{orderId}/confirm
    Note over RetailAPI,OrderMS: basketId, paymentRefs,<br/>enriched offer data (locked fares + tax lines)
    OrderMS-->>RetailAPI: ConfirmedOrder (bookingReference, basket deleted)

    Note over RetailAPI: Link payment to booking reference<br/>(async best-effort — does not block confirmation)
    RetailAPI-)PaymentMS: PATCH /api/v1/payment/{paymentId}/booking-reference

    par Post-confirm parallel operations
        Note over RetailAPI: Inventory hold + sell per segment
        loop Per segment (parallel)
            RetailAPI->>OfferMS: POST /api/v1/inventory/hold
            OfferMS-->>RetailAPI: Held
        end
        RetailAPI->>OfferMS: POST /api/v1/inventory/sell
        OfferMS-->>RetailAPI: Sold

    and
        RetailAPI->>DeliveryMS: POST /api/v1/tickets
        Note over RetailAPI,DeliveryMS: passengers, segments,<br/>bookingReference, fareBasisCodes
        DeliveryMS-->>RetailAPI: IssuedTickets (eTicketNumbers)
        RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/tickets
        Note over RetailAPI,OrderMS: Write e-ticket numbers back to order

    and
        RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
        Note over RetailAPI,PaymentMS: Settle fare amount

        opt Seat ancillaries present
            RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise (type=Seat)
            RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
            loop Per seat item
                RetailAPI->>DeliveryMS: POST /api/v1/documents (type=SeatAncillary EMD)
            end
        end

        opt Bag ancillaries present
            RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise (type=Bag)
            RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
            loop Per bag item
                RetailAPI->>DeliveryMS: POST /api/v1/documents (type=BagAncillary EMD)
            end
        end

        opt Product ancillaries present
            RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/authorise (type=Product)
            RetailAPI->>PaymentMS: POST /api/v1/payment/{paymentId}/settle
            loop Per product item
                RetailAPI->>DeliveryMS: POST /api/v1/documents (type=ProductAncillary EMD)
            end
        end

    and
        opt Loyalty number present
            RetailAPI->>CustomerMS: POST /api/v1/customers/{loyaltyNumber}/orders
            Note over RetailAPI,CustomerMS: Link confirmed order to loyalty account
            CustomerMS-->>RetailAPI: Linked
        end
    end

    Note over RetailAPI: Manifest write runs after ticket issuance<br/>(requires issued eTicketNumbers)
    loop Per segment
        RetailAPI->>DeliveryMS: POST /api/v1/manifest
        Note over RetailAPI,DeliveryMS: bookingReference, orderId,<br/>segment, issuedTickets[], seatAssignments
        DeliveryMS-->>RetailAPI: Manifest written
    end

    RetailAPI-->>Web: OrderResponse
    Note over RetailAPI,Web: bookingReference, eTicketNumbers,<br/>passengers, segments, totalAmount
```

---

## Confirm basket (reward booking)

Reward bookings follow the same structure as revenue bookings except no card payment is taken for the fare. The order is confirmed and linked to the loyalty account as with revenue bookings.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant OfferMS as Offer MS
    participant DeliveryMS as Delivery MS
    participant CustomerMS as Customer MS

    Web->>RetailAPI: POST /v1/basket/{basketId}/confirm
    Note over Web,RetailAPI: {bookingType=Reward,<br/>loyaltyPointsToRedeem, loyaltyNumber}

    RetailAPI->>OrderMS: GET /api/v1/basket/{basketId}
    OrderMS-->>RetailAPI: Basket (bookingType=Reward)

    loop For each offerId in basket
        RetailAPI->>OfferMS: POST /api/v1/offers/{offerId}/reprice
        OfferMS-->>RetailAPI: Repriced offer (validated=true)
    end

    RetailAPI->>OrderMS: POST /api/v1/orders
    Note over RetailAPI,OrderMS: basketId, bookingType=Reward,<br/>loyaltyPointsToRedeem
    OrderMS-->>RetailAPI: DraftOrder (orderId)

    Note over RetailAPI: No card payment for fare —<br/>no PaymentMS initialise/authorise/settle for fare amount

    RetailAPI->>OrderMS: POST /api/v1/orders/{orderId}/confirm
    OrderMS-->>RetailAPI: ConfirmedOrder (bookingReference)

    par Post-confirm parallel operations
        loop Per segment (parallel)
            RetailAPI->>OfferMS: POST /api/v1/inventory/hold
            OfferMS-->>RetailAPI: Held
        end
        RetailAPI->>OfferMS: POST /api/v1/inventory/sell
        OfferMS-->>RetailAPI: Sold

    and
        RetailAPI->>DeliveryMS: POST /api/v1/tickets
        DeliveryMS-->>RetailAPI: IssuedTickets (eTicketNumbers)
        RetailAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/tickets

    and
        RetailAPI->>CustomerMS: POST /api/v1/customers/{loyaltyNumber}/orders
        Note over RetailAPI,CustomerMS: Link confirmed order to loyalty account
        CustomerMS-->>RetailAPI: Linked
    end

    loop Per segment
        RetailAPI->>DeliveryMS: POST /api/v1/manifest
        DeliveryMS-->>RetailAPI: Manifest written
    end

    RetailAPI-->>Web: OrderResponse
```

---

## Validate order (manage-booking token)

Issues a short-lived JWT scoped to the booking reference, used to authorise subsequent manage-booking calls.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS

    Web->>RetailAPI: POST /v1/orders/validate
    Note over Web,RetailAPI: {bookingReference, givenName, surname}

    RetailAPI->>OrderMS: POST /api/v1/orders/retrieve
    Note over RetailAPI,OrderMS: bookingReference, surname — validates credentials
    OrderMS-->>RetailAPI: Order (confirms passenger identity)

    Note over RetailAPI: Generate JWT with bookingReference claim

    RetailAPI-->>Web: ValidateOrderResponse
    Note over RetailAPI,Web: {accessToken} — short-lived,<br/>scoped to this booking reference
```

---

## Get order

`GetOrderHandler` retrieves the order record then fetches both e-tickets and EMD documents from the Delivery MS in parallel.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS
    participant DeliveryMS as Delivery MS

    Web->>RetailAPI: GET /v1/orders/{bookingRef}
    Note over Web,RetailAPI: Requires valid manage-booking JWT<br/>or staff JWT

    par
        RetailAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
        OrderMS-->>RetailAPI: OrderRecord (passengers, segments, seats, ssrs)
    and
        RetailAPI->>DeliveryMS: GET /api/v1/tickets?bookingRef={bookingRef}
        DeliveryMS-->>RetailAPI: IssuedTickets[]
    and
        RetailAPI->>DeliveryMS: GET /api/v1/documents?bookingRef={bookingRef}
        DeliveryMS-->>RetailAPI: Documents[] (EMDs for seats, bags, products)
    end

    RetailAPI-->>Web: ManagedOrderResponse
    Note over RetailAPI,Web: passengers, segments, seats, bags,<br/>ssrs, eTickets, documents
```
