# Order domain

The Order microservice manages the complete booking lifecycle — from basket creation through confirmation, post-sale changes, and cancellation — built on the **IATA One Order** standard.

- Bookings are represented as a single evolving `OrderData` JSON document, identified by a six-character **booking reference** (equivalent to the PNR in legacy systems).
- All state-changing operations publish an event to the event bus for downstream consumption (e.g. Accounting, Customer). Three event types are defined:
  - `OrderConfirmed` — published on initial order creation; consumed by Accounting (revenue recording) and Customer (points accrual if `loyaltyNumber` present). For reward bookings, the event includes `bookingType=Reward`, `totalPointsAmount`, and `redemptionReference` so Accounting can record the points liability separately from cash revenue.
  - `OrderChanged` — published on post-sale modifications (seat change, bag addition, flight change, SSR update, IROPS rebook); consumed by Accounting for revenue adjustment. For reward booking changes, includes `pointsAdjustment` (positive = additional points redeemed, negative = points reinstated) and updated `totalPointsAmount`.
  - `OrderCancelled` — published on voluntary cancellation; contains `refundableAmount` and `originalPaymentId` for Accounting to initiate refund processing. For reward bookings, additionally includes `bookingType=Reward`, `pointsReinstated` (total points restored to customer), and `redemptionReference` so Accounting can reverse the points liability entry.
- The Order microservice is the sole owner of order state; all changes — PAX updates, seat changes, flight changes, ancillary additions, cancellations — are orchestrated through the Retail API.
- An initial `OrderInit` status is supported for orders that are being assembled incrementally — particularly by Contact Centre agents using the Terminal app who build bookings in stages. An order in `OrderInit` cannot advance to `Confirmed` (and will not have a booking reference / PNR generated) until all mandatory fields are present: passenger names, itinerary, ticketing time limit, a contact field (email or phone), and the identity of the person creating the booking (sales agent reference, or `WEB` for web-originated orders). The `OrderInit` state is only relevant for Contact Centre channel bookings via the Terminal app; web, app, and kiosk bookings progress directly from basket creation to confirmation without entering `OrderInit`. **The Terminal app and the `OrderInit` flow are out of scope for the current release.** The `OrderStatus` field must include `OrderInit` as a valid value in the data model to support future implementation without a breaking schema change, but no endpoints or orchestration flows for `OrderInit` are built at this time.

### Create — Bookflow

The **bookflow** is the end-to-end initial purchase journey — from flight offer selection and basket creation through passenger details, ancillary selection, payment, and order confirmation — all within a single basket session bounded by the ticketing time limit.

- The `Basket` is a transient Order DB record accumulating flight offers, seat offers, bag offers, and passenger details as the booking is built.
- Hard-deleted on successful booking confirmation; expires automatically after **60 minutes** if abandoned — matching the `StoredOffer` expiry window so that all offer IDs referenced by an active basket remain valid throughout the basket lifetime.
- A ticketing time limit (TTL) is set at basket creation and stored on the `order.Order` record itself (not the basket); if elapsed, held inventory is released and the basket is marked expired.
- For each `OfferId` in the basket, the Order MS retrieves the stored offer snapshot from the Offer MS, guaranteeing the price and fare conditions match exactly what the customer was shown at search time.
- Order creation is a **two-step process**: `POST /v1/orders` creates a `Draft` order record from the basket (basket remains active); `POST /v1/orders/confirm` validates completeness (including that no flight departs within 1 hour — ticketing closed), authorises payment, assigns the booking reference (PNR), transitions the order to `Confirmed`, and deletes the basket. PATCH operations on the order record are permitted between creation and confirmation.

```mermaid
sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer [MS]
    participant OrderMS as Order [MS]
    participant OfferMS as Offer [MS]
    participant SeatMS as Seat [MS]
    participant BagMS as Bag [MS]
    participant PaymentMS as Payment [MS]
    participant DeliveryMS as Delivery [MS]
    participant AccountingMS as Accounting [MS]

    Note over Traveller, Web: Bookflow begins — flight selection already complete (see Offer / Search)

    opt Reward booking only — loyalty login before basket creation
        Traveller->>Web: Authenticate with loyalty credentials
        Web->>LoyaltyAPI: POST /v1/auth/login (email, password)
        LoyaltyAPI-->>Web: 200 OK — accessToken, customer profile (loyaltyNumber, pointsBalance)
        Web->>Web: Verify pointsBalance >= totalPointsRequired
        Note over Web: If insufficient points, display error and block booking
    end

    alt Revenue booking
        Web->>RetailAPI: POST /v1/basket (offerIds: [OfferId-Out, OfferId-In?], channel, currency)
        RetailAPI->>OrderMS: POST /v1/basket (offerIds, channel, currency)
        OrderMS-->>RetailAPI: 201 Created — basketId
    else Reward booking
        Web->>RetailAPI: POST /v1/basket (offerIds, channel, currency, bookingType=Reward, loyaltyNumber)
        RetailAPI->>CustomerMS: GET /v1/customers/{loyaltyNumber}
        CustomerMS-->>RetailAPI: 200 OK — pointsBalance verified
        RetailAPI->>OrderMS: POST /v1/basket (offerIds, channel, currency, bookingType=Reward, loyaltyNumber)
        OrderMS-->>RetailAPI: 201 Created — basketId
    end

    Note over OrderMS: Basket created with ExpiresAt = now + 60 min

    loop For each flight OfferId
        RetailAPI->>OfferMS: GET /v1/offers/{offerId}
        OfferMS-->>RetailAPI: 200 OK — stored offer snapshot (flight, fare, pricing, inventoryId)
        RetailAPI->>OrderMS: POST /v1/basket/{basketId}/offers (offerId, sessionId, totalAmount)
        OrderMS-->>RetailAPI: 200 OK — offer reference stored in basket
    end

    RetailAPI-->>Web: 201 Created — basket summary (basketId, itinerary, total fare price)

    Note over Web: Reward bookings: lead passenger auto-filled from loyalty profile
    Traveller->>Web: Enter passenger details
    Web->>RetailAPI: PUT /v1/basket/{basketId}/passengers (PAX details)
    RetailAPI->>OrderMS: PUT /v1/basket/{basketId}/passengers (PAX details)
    OrderMS-->>RetailAPI: 200 OK — basket updated

    opt Traveller selects seats during bookflow
        Web->>RetailAPI: GET /v1/flights/{flightId}/seatmap
        RetailAPI->>SeatMS: GET /v1/seatmap/{aircraftType}
        SeatMS-->>RetailAPI: 200 OK — seatmap layout (cabin configuration, seat positions, attributes)
        RetailAPI->>SeatMS: GET /v1/seat-offers?flightId={flightId}
        SeatMS-->>RetailAPI: 200 OK — priced seat offers (SeatOfferId, price, seat attributes per selectable seat)
        RetailAPI->>OfferMS: GET /v1/flights/{flightId}/seat-availability
        OfferMS-->>RetailAPI: 200 OK — seat availability status per seat (available|held|sold)
        RetailAPI-->>Web: 200 OK — seat map with pricing and availability (merged by Retail API)
        Traveller->>Web: Select seat(s) for each PAX
        Web->>RetailAPI: PUT /v1/basket/{basketId}/seats (seatOfferIds per PAX per flight)
        RetailAPI->>OrderMS: PUT /v1/basket/{basketId}/seats (seatOfferIds, PAX assignments)
        OrderMS-->>RetailAPI: 200 OK — basket updated (seat items added, revised total)
        RetailAPI-->>Web: 200 OK — seats reserved in basket, revised total
    end

    opt Traveller selects bags during bookflow
        loop For each flight segment
            RetailAPI->>BagMS: GET /v1/bags/offers?inventoryId={inventoryId}&cabinCode={cabinCode}
            BagMS-->>RetailAPI: 200 OK — bag policy (freeBagsIncluded, maxWeightKg) + bag offers (BagOfferId, price per additional bag)
        end
        RetailAPI-->>Web: 200 OK — free bag allowance and additional bag offers per segment
        Traveller->>Web: Select additional bag(s) per PAX per segment (if desired)
        Web->>RetailAPI: PUT /v1/basket/{basketId}/bags (bagOfferIds per PAX per segment)
        loop For each BagOfferId
            RetailAPI->>BagMS: GET /v1/bags/offers/{bagOfferId}
            BagMS-->>RetailAPI: 200 OK — validated (IsConsumed=0, unexpired, price locked)
        end
        RetailAPI->>OrderMS: PUT /v1/basket/{basketId}/bags (bagOfferIds, PAX assignments)
        OrderMS-->>RetailAPI: 200 OK — basket updated (bag items added, revised total)
        RetailAPI-->>Web: 200 OK — bags added to basket, revised total
    end

    Traveller->>Web: Enter payment details and confirm booking

    Web->>RetailAPI: POST /v1/basket/{basketId}/confirm (payment details)
    Note over RetailAPI: Validate basket not expired and within ticketingTimeLimit

    opt Reward booking — authorise points redemption
        RetailAPI->>CustomerMS: POST /v1/customers/{loyaltyNumber}/points/authorise (points=totalPointsAmount, basketId)
        CustomerMS-->>RetailAPI: 200 OK — redemptionReference, points held against balance
    end

    alt Revenue booking
        RetailAPI->>OrderMS: POST /v1/orders (basketId, bookingType=Revenue)
    else Reward booking
        RetailAPI->>OrderMS: POST /v1/orders (basketId, bookingType=Reward, redemptionReference)
    end
    OrderMS-->>RetailAPI: 201 Created — draft order (orderId, orderStatus=Draft)
    Note over OrderMS: Basket remains active; no booking reference assigned yet

    alt Revenue booking — authorise fare payment
        RetailAPI->>PaymentMS: POST /v1/payment/initialise (type=Fare, amount=totalFareAmount, currency)
        PaymentMS-->>RetailAPI: 200 OK — paymentId-1
        RetailAPI->>PaymentMS: POST /v1/payment/{paymentId-1}/authorise (amount=totalFareAmount, card details)
        PaymentMS-->>RetailAPI: 200 OK — fare authorisation confirmed (paymentId-1)
    else Reward booking — authorise tax payment only
        RetailAPI->>PaymentMS: POST /v1/payment/initialise (type=RewardTaxes, amount=totalTaxesAmount, currency)
        PaymentMS-->>RetailAPI: 200 OK — paymentId-1
        RetailAPI->>PaymentMS: POST /v1/payment/{paymentId-1}/authorise (amount=totalTaxesAmount, card details)
        PaymentMS-->>RetailAPI: 200 OK — taxes authorisation confirmed (paymentId-1)
    end

    opt Seats were selected during bookflow
        RetailAPI->>PaymentMS: POST /v1/payment/{paymentId-2}/authorise (amount=totalSeatAmount, card token from paymentId-1, description=SeatAncillary)
        PaymentMS-->>RetailAPI: 200 OK — seat authorisation confirmed (paymentId-2)
    end

    opt Bags were selected during bookflow
        RetailAPI->>PaymentMS: POST /v1/payment/{paymentId-3}/authorise (amount=totalBagAmount, card token from paymentId-1, description=BagAncillary)
        PaymentMS-->>RetailAPI: 200 OK — bag authorisation confirmed (paymentId-3)
    end

    RetailAPI->>OrderMS: POST /v1/orders/confirm (orderId, basketId, paymentReferences)
    Note over OrderMS: Validates passengers and segments present; rejects if any flight departs within 1 hour (ticketing closed);<br/>assigns booking reference; writes payment references into OrderData;<br/>transitions OrderStatus to Confirmed; deletes basket
    OrderMS-->>RetailAPI: 200 OK — order confirmed (bookingReference, orderStatus=Confirmed)

    RetailAPI->>DeliveryMS: POST /v1/tickets (basketId, bookingReference, passenger details, flight segments)
    DeliveryMS-->>RetailAPI: 201 Created — e-ticket numbers issued
    RetailAPI->>OfferMS: POST /v1/inventory/sell (inventoryIds, offerId, sessionId — mark seats as sold)
    OfferMS-->>RetailAPI: 200 OK — inventory updated (SeatsSold incremented, SeatsHeld decremented)

    RetailAPI->>OrderMS: PATCH /v1/orders/{bookingRef}/tickets (e-ticket numbers)
    OrderMS-->>RetailAPI: 200 OK — e-tickets written to OrderData

    RetailAPI->>DeliveryMS: POST /v1/manifest (inventoryId, seatNumber, bookingReference, eTicketNumber, passengerId — per PAX per segment)
    DeliveryMS-->>RetailAPI: 201 Created — manifest entries written

    opt Reward booking — settle points redemption
        RetailAPI->>CustomerMS: POST /v1/customers/{loyaltyNumber}/points/settle (redemptionReference)
        CustomerMS-->>RetailAPI: 200 OK — points deducted, Redeem transaction appended
        Note over CustomerMS: PointsBalance decremented, LoyaltyTransaction appended (type=Redeem)
    end

    RetailAPI->>PaymentMS: POST /v1/payment/{paymentId-1}/settle (settledAmount)
    PaymentMS-->>RetailAPI: 200 OK — payment settled

    Note over RetailAPI, PaymentMS: Settle ancillary payments after order confirmation - failure does not roll back the booking but must be flagged for manual reconciliation
    opt Seats were selected during bookflow
        RetailAPI->>PaymentMS: POST /v1/payment/{paymentId-2}/settle (settledAmount)
        PaymentMS-->>RetailAPI: 200 OK — seat payment settled
    end

    opt Bags were selected during bookflow
        RetailAPI->>PaymentMS: POST /v1/payment/{paymentId-3}/settle (settledAmount)
        PaymentMS-->>RetailAPI: 200 OK — bag payment settled
    end

    Note over OrderMS, AccountingMS: Async event
    alt Revenue booking
        OrderMS-)AccountingMS: OrderConfirmed event (bookingReference, bookingType=Revenue, amount, e-tickets, loyaltyNumber)
    else Reward booking
        OrderMS-)AccountingMS: OrderConfirmed event (bookingReference, bookingType=Reward, totalPointsAmount, redemptionReference, amount=taxesAndAncillaries, e-tickets, loyaltyNumber)
    end

    RetailAPI-->>Web: 201 Created — booking confirmed (bookingReference, e-ticket numbers)
    Web-->>Traveller: Display booking confirmation
```

*Ref: order bookflow - end-to-end revenue and reward booking flow covering flight selection, ancillary addition, payment (and points redemption for reward bookings), ticketing, and order confirmation; all cross-microservice orchestration is performed by the Retail API — microservices do not call each other*

### Reward Booking

A **reward booking** allows a loyalty programme member to redeem their accumulated points for flights, paying only applicable taxes and fees in cash. The reward booking flow shares the same basket and order infrastructure as revenue bookings but introduces an additional loyalty authentication step and a points redemption flow that mirrors the two-stage authorise-and-settle pattern used for card payments. The full sequence — including the reward-specific `alt`/`opt` branches — is shown in the unified bookflow diagram above.

#### Key Differences from Revenue Bookings

- **Points pricing:** Flight offers include both a revenue price and a points price (`PointsPrice` and `PointsTaxes`). The points price covers the base fare; taxes and fees remain payable in cash.
- **Loyalty login required:** After flight selection, the traveller must authenticate via the Loyalty API before proceeding to passenger details. This step is skipped for revenue bookings.
- **Points balance verification:** The Retail API verifies the customer's points balance against the total points required before creating the basket. If insufficient, the booking is rejected.
- **Lead passenger autofill:** On successful login, the lead passenger's details (name, date of birth, contact information, loyalty number) are pre-populated from the loyalty profile.
- **Points redemption:** The Retail API orchestrates a two-stage points redemption (authorise then settle) via the Customer microservice, analogous to the Payment MS authorise-and-settle flow for card payments.
- **Taxes still paid by card:** The credit card payment flow is still required for taxes, fees, and any ancillary charges (seats, bags). The `TotalAmount` on the basket and order reflects only the cash component.

#### Basket Structure for Reward Bookings

The basket for a reward booking carries additional fields:

- `BookingType`: `Reward` (vs `Revenue` for standard bookings)
- `TotalPointsAmount`: Total points to be redeemed for the fare component
- `TotalTaxesAmount`: Total taxes and fees payable in cash
- `LoyaltyNumber`: The authenticated member's loyalty number

The `TotalFareAmount` is zero for reward bookings (the fare is covered by points). The `TotalAmount` equals `TotalTaxesAmount + TotalSeatAmount + TotalBagAmount` — only the cash-payable portion.

#### Points Redemption — Authorise and Settle

Points redemption follows a two-stage flow analogous to card payment authorisation and settlement, ensuring atomicity and allowing rollback if downstream steps fail:

1. **Authorise** (`POST /v1/customers/{loyaltyNumber}/points/authorise`): Places a hold on the required points against the customer's balance. The points are reserved but not yet deducted. Returns a `RedemptionReference` used to track the transaction.

2. **Settle** (`POST /v1/customers/{loyaltyNumber}/points/settle`): Deducts the held points from the customer's balance and appends a `Redeem` transaction to the loyalty transaction log. Called after e-ticket issuance and inventory settlement.

3. **Reverse** (`POST /v1/customers/{loyaltyNumber}/points/reverse`): Releases held points back to the customer's available balance. Called if any downstream step fails (e.g. ticketing failure, inventory settlement failure).

| Failure point | Behaviour |
|---|---|
| Points authorisation fails (insufficient balance) | Abort — do not proceed with card payment or order confirmation |
| Card payment authorisation fails | Reverse points hold, release inventory, return error |
| Ticketing fails | Reverse points hold, void card authorisation, release inventory, return error |
| Points settlement fails after order confirmation | Flag for manual reconciliation; order is confirmed but points deduction must be retried |

#### Order Data for Reward Bookings

The confirmed order for a reward booking includes:

- `BookingType`: `Reward`
- `TotalPointsAmount`: Points redeemed for the fare
- `TotalAmount`: Cash amount paid (taxes + ancillaries only)
- `PointsRedemption`: Object containing `RedemptionReference`, `LoyaltyNumber`, `PointsRedeemed`, and `Status`
- Standard `Payments` array for the cash/card transactions (taxes and ancillaries)

The `OrderConfirmed` event for reward bookings includes the `BookingType` and `RedemptionReference` fields, allowing the Accounting MS to distinguish between revenue and reward bookings for financial reporting.

### Ticketing

Ticketing converts a confirmed basket into a legally valid air travel contract — the final step of the bookflow, triggered immediately after payment authorisation within the same synchronous flow.

- The e-ticket number is the IATA-standard identifier for the travel contract and is required before manifest entries or boarding passes can be issued.

#### What is an E-Ticket?

- An e-ticket (electronic ticket) is the passenger's legal entitlement to travel, replacing the legacy paper ticket
- Each e-ticket covers **one passenger on one flight segment** — a return booking for two passengers generates four e-ticket numbers
- E-ticket numbers follow the IATA format: a **3-digit airline code prefix** followed by a **10-digit serial number**, e.g. `932-1234567890` (Apex Air prefix: `932`)
- E-ticket numbers are issued and owned by the **Delivery microservice**, which is the system of record for all issued tickets
- Once issued, an e-ticket number is immutable — post-booking changes (PAX updates, seat changes) trigger **reissuance** of a new e-ticket number against the same order item, not amendment of the existing one

#### Ticketing Flow

Ticketing occurs as part of the order confirmation sequence, orchestrated by the Retail API after fare payment has been authorised:

- **Pre-ticketing checks** (performed by the Retail API before calling Delivery):
  - Basket is in `Active` status
  - `now < TicketingTimeLimit` — if elapsed, basket must be marked `Expired` and inventory released
  - All stored offers referenced in the basket are unconsumed and not expired
  - Fare payment has been successfully authorised (`paymentId` held)
  - `departureDateTime > now + 1 hour` for every flight segment — if any flight departs within 1 hour, the Order MS rejects the confirmation with `422 Unprocessable Entity` (ticketing closed; no new sales once boarding is imminent)

- **E-ticket issuance** (Retail API → Delivery MS):
  - Retail API calls `POST /v1/tickets` on the Delivery microservice, passing: basket ID, passenger details, and flight segments
  - Delivery MS generates one e-ticket number per passenger per flight segment
  - E-ticket numbers are returned synchronously to the Retail API

- **Inventory settlement** (Retail API → Offer MS):
  - Retail API calls `POST /v1/inventory/sell` on the Offer microservice to convert held seats to sold: `SeatsHeld` is decremented and `SeatsSold` is incremented for each flight/cabin combination; `SeatsAvailable` is unchanged (it was already decremented when the basket hold was placed)
  - This step must complete before order confirmation is written

- **Fare payment settlement** (Retail API → Payment MS):
  - Retail API calls `POST /v1/payment/{paymentId}/settle` to move the authorised fare payment to `Settled`

- **Order confirmation** (Retail API → Order MS):
  - Retail API calls the Order microservice to convert the basket into a confirmed `order.Order` record
  - Payload includes: basket ID, all e-ticket numbers (per PAX per segment), and all payment IDs
  - Order MS writes the `order.Order` row with `OrderStatus = Confirmed` and a generated 6-character `BookingReference`
  - Order MS hard-deletes the basket row
  - Order MS publishes `OrderConfirmed` event to the event bus

- **Manifest population** (Retail API → Delivery MS):
  - Retail API validates each seat number against the active seatmap via the Seat MS before calling Delivery MS
  - Retail API calls the Delivery microservice to write one `delivery.Ticket` row per passenger per segment (and a corresponding `delivery.Manifest` row), passing seat numbers that have already been validated

- **Ancillary settlement** (if seats or bags were selected during the bookflow):
  - Ancillary payments are authorised during the bookflow basket-confirm step, **before** order confirmation is written, so that all payment authorisations are in place when the order is created
  - Settlement of each ancillary payment occurs **after** order confirmation: `POST /v1/payment/{paymentId}/settle` is called once for seat ancillary and once for bag ancillary, each with their own `PaymentId`
  - Failure of an ancillary settlement does not roll back the confirmed booking (the order is already confirmed and e-tickets issued), but must be flagged for manual reconciliation via the Payment audit trail

#### Reissuance

E-tickets must be reissued (new number generated, old number voided) in the following scenarios:

- **PAX name correction** — name changes invalidate the existing ticket as the passenger name is encoded in the BCBP barcode string
- **Seat change post-booking** — seat number is encoded on the boarding pass; if the e-ticket record references a specific seat, reissuance ensures consistency
- **Schedule change by the airline** — if the operating flight details change materially (departure time, routing), affected tickets are reissued

Reissuance is always performed by the Delivery microservice. The Order microservice is updated with the new e-ticket numbers via the Retail API orchestration layer, and new manifest entries replace the previous ones.

#### Failure Handling

Ticketing involves multiple sequential calls; partial failures must be handled explicitly:

| Failure point | Behaviour |
|---|---|
| Delivery MS fails to issue tickets | Abort — do not settle payment, do not confirm order; return error to channel |
| Offer MS fails to settle inventory (convert held to sold) | Retry up to 3 times; if still failing, void payment authorisation and return error |
| Payment settlement fails after inventory removed | Flag order for manual reconciliation; order is not confirmed until settlement succeeds |
| Order MS fails to confirm | Attempt compensation: void payment, reinstate inventory, void e-tickets; alert ops team if compensation also fails |

> All state-changing steps should be logged with sufficient detail to support manual reconciliation in the event of a partial failure that cannot be automatically compensated.

### Data Schema — Order

The Order domain owns three structures in the Order DB: the `Basket` tables (transient pre-sale state), the `Order` table (confirmed post-sale state), and the `BasketConfig` table (system configuration for expiry and ticketing time limits).

#### Basket

The basket is the transient in-progress state for a purchase journey, accumulating flight offers, seat offers, passenger details, and payment intent until payment completes.

- Contains no booking reference, PNR, or e-ticket numbers — these don't exist until the sale completes.
- Hard-deleted on successful order confirmation; if abandoned or the ticketing time limit elapses, marked `Expired` and held inventory released by a background cleanup job.
- `BasketData` holds the full basket state as a JSON document; scalar fields used for indexed lookups and lifecycle management are stored as typed columns.

#### `order.Basket`

Basket expiry is fixed at **60 minutes** from creation, matching the `StoredOffer` lifetime. This ensures that all offer IDs referenced by a basket remain valid for the duration of the basket lifecycle. There is no separate configuration table for basket or ticketing time limits; the ticketing time limit is stored on the `order.Order` record itself when the order is first created.

| Column | Type | Nullable | Default | Key | Notes |
|---|---|---|---|---|---|
| BasketId | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| ChannelCode | VARCHAR(20) | No | | | `WEB` · `APP` · `NDC` · `KIOSK` · `CC` · `AIRPORT` |
| CurrencyCode | CHAR(3) | No | `'GBP'` | | ISO 4217 currency code |
| BasketStatus | VARCHAR(20) | No | `'Active'` | | `Active` · `Expired` · `Abandoned` · `Confirmed` |
| TotalFareAmount | DECIMAL(10,2) | Yes | | | Sum of flight offer prices; updated as basket is built |
| TotalSeatAmount | DECIMAL(10,2) | Yes | `0.00` | | Sum of seat offer prices; updated as seats are added during bookflow |
| TotalBagAmount | DECIMAL(10,2) | Yes | `0.00` | | Sum of bag offer prices; updated as bags are added during bookflow |
| TotalAmount | DECIMAL(10,2) | Yes | | | TotalFareAmount + TotalSeatAmount + TotalBagAmount |
| ExpiresAt | DATETIME2 | No | | | Basket hard expiry: `CreatedAt + 60 minutes` |
| ConfirmedOrderId | UNIQUEIDENTIFIER | Yes | | FK → `order.Order(OrderId)` | Set on successful confirmation; null until then |
| CreatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| UpdatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| Version | INT | No | `1` | | Optimistic concurrency version counter; incremented on every write |
| BasketData | NVARCHAR(MAX) | No | | | JSON document containing the full basket state (see example below) |

> **Indexes:** `IX_Basket_Status_Expiry` on `(BasketStatus, ExpiresAt)` WHERE `BasketStatus = 'Active'` — used by background expiry job.
> **Constraints:** `CHK_BasketData` — `ISJSON(BasketData) = 1`; `BasketData` must be a valid JSON document.
> **Concurrency:** `Version` is used for optimistic concurrency control — see [Optimistic Concurrency Control](#optimistic-concurrency-control).
> **Basket lifecycle:** A basket is hard-deleted immediately when an order is confirmed. Expired and abandoned baskets are retained for 7 days for diagnostics before being purged.

**Example `BasketData` JSON document**

The JSON captures the full in-progress state. It mirrors the eventual shape of `OrderData` for passengers and flight segments, but uses `offerSnapshots` rather than confirmed order items, and has no `eTickets`, booking reference, or payment settlement data.

```json
{
  "channel": "WEB",
  "currency": "GBP",
  "ticketingTimeLimit": "2025-06-02T10:30:00Z",
  "passengers": [
    {
      "passengerId": "PAX-1",
      "type": "ADT",
      "givenName": "Alex",
      "surname": "Taylor",
      "dateOfBirth": "1985-03-12",
      "gender": "Male",
      "loyaltyNumber": "AX9876543",
      "contacts": {
        "email": "alex.taylor@example.com",
        "phone": "+447700900100"
      },
      "travelDocument": {
        "type": "PASSPORT",
        "number": "PA1234567",
        "issuingCountry": "GBR",
        "expiryDate": "2030-01-01",
        "nationality": "GBR"
      }
    },
    {
      "passengerId": "PAX-2",
      "type": "ADT",
      "givenName": "Jordan",
      "surname": "Taylor",
      "dateOfBirth": "1987-07-22",
      "gender": "Female",
      "loyaltyNumber": null,
      "contacts": null,
      "travelDocument": null
    }
  ],
  "flightOffers": [
    {
      "basketItemId": "BI-1",
      "offerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "flightNumber": "AX003",
      "origin": "LHR",
      "destination": "JFK",
      "departureDateTime": "2025-08-15T11:00:00Z",
      "arrivalDateTime": "2025-08-15T14:10:00Z",
      "aircraftType": "A351",
      "cabinCode": "J",
      "bookingClass": "J",
      "fareBasisCode": "JFLEXGB",
      "fareFamily": "Business Flex",
      "passengerRefs": ["PAX-1", "PAX-2"],
      "unitPrice": 350.00,
      "taxes": 87.25,
      "totalPrice": 437.25,
      "isRefundable": true,
      "isChangeable": true,
      "offerExpiresAt": "2025-06-01T11:00:00Z"
    },
    {
      "basketItemId": "BI-2",
      "offerId": "7cb87a21-1234-4abc-9def-1a2b3c4d5e6f",
      "flightNumber": "AX004",
      "origin": "JFK",
      "destination": "LHR",
      "departureDateTime": "2025-08-25T22:00:00Z",
      "arrivalDateTime": "2025-08-26T10:15:00Z",
      "aircraftType": "A351",
      "cabinCode": "J",
      "bookingClass": "J",
      "fareBasisCode": "JFLEXGB",
      "fareFamily": "Business Flex",
      "passengerRefs": ["PAX-1", "PAX-2"],
      "unitPrice": 350.00,
      "taxes": 87.25,
      "totalPrice": 437.25,
      "isRefundable": true,
      "isChangeable": true,
      "offerExpiresAt": "2025-06-01T11:00:00Z"
    }
  ],
  "seatOffers": [
    {
      "basketItemId": "BI-3",
      "seatOfferId": "so-a351-1A-v1",
      "basketItemRef": "BI-1",
      "passengerRef": "PAX-1",
      "seatNumber": "1A",
      "seatPosition": "Window",
      "cabinCode": "J",
      "price": 0.00,
      "currency": "GBP",
      "note": "Business Class — no charge"
    },
    {
      "basketItemId": "BI-4",
      "seatOfferId": "so-a351-1K-v1",
      "basketItemRef": "BI-1",
      "passengerRef": "PAX-2",
      "seatNumber": "1K",
      "seatPosition": "Window",
      "cabinCode": "J",
      "price": 0.00,
      "currency": "GBP",
      "note": "Business Class — no charge"
    }
  ],
  "bagOffers": [
    {
      "basketItemId": "BI-5",
      "bagOfferId": "bo-business-bag1-v1",
      "basketItemRef": "BI-1",
      "passengerRef": "PAX-1",
      "bagSequence": 1,
      "freeBagsIncluded": 2,
      "additionalBags": 1,
      "price": 60.00,
      "currency": "GBP",
      "note": "1st additional bag — LHR→JFK segment"
    }
  ],
  "paymentIntent": {
    "method": "CreditCard",
    "cardType": "Visa",
    "cardLast4": "4242",
    "totalFareAmount": 1749.00,
    "totalSeatAmount": 0.00,
    "totalBagAmount": 60.00,
    "grandTotal": 1809.00,
    "currency": "GBP",
    "status": "PendingAuthorisation"
  },
  "history": [
    { "event": "BasketCreated",          "at": "2025-06-01T10:30:00Z", "by": "WEB" },
    { "event": "PassengersAdded",        "at": "2025-06-01T10:31:00Z", "by": "WEB" },
    { "event": "SeatsAdded",             "at": "2025-06-01T10:32:00Z", "by": "WEB" },
    { "event": "BagsAdded",              "at": "2025-06-01T10:33:00Z", "by": "WEB" },
    { "event": "PaymentIntentRecorded",  "at": "2025-06-01T10:34:00Z", "by": "WEB" }
  ]
}
```

> **Ticketing time limit:** The `TicketingTimeLimit` is set at basket creation from the active `BasketConfig` row and is included in the basket summary returned to the channel so it can display a countdown to the traveller. The Retail API must validate that `now < TicketingTimeLimit` before attempting authorisation. If the limit has elapsed, the basket must be marked `Expired`, inventory released, and the traveller directed to start a new search.

> **Basket expiry job:** A background process runs on a schedule (e.g. every 5 minutes) and queries `order.Basket WHERE BasketStatus = 'Active' AND ExpiresAt <= now`. For each expired basket it sets `BasketStatus = 'Expired'` and fires a compensating call to the Offer microservice to release any held inventory. Expired baskets are retained for a short period (e.g. 7 days) for diagnostic purposes before being purged.

> **Basket deletion on sale:** The Order microservice hard-deletes the basket row as part of the order confirmation transaction (`POST /v1/orders`). The confirmed `OrderData` JSON is the authoritative post-sale record; the basket is no longer needed. The Retail API does not issue a separate delete call.

#### Order

The `Order` table is written once the basket is confirmed — payment taken, inventory settled, and e-tickets issued — following the IATA ONE Order model.

- Scalar fields (`BookingReference`, `OrderStatus`, `ChannelCode`, `TotalAmount`, etc.) stored as typed columns for querying, routing, and event publishing.
- Full order detail (passengers, segments, order items, seat assignments, e-tickets, payments, audit history) stored in the `OrderData` JSON document.
- Fields present as typed columns are intentionally excluded from `OrderData` to avoid duplication; the columns are the single source of truth for those values.

#### `order.Order`

| Column | Type | Nullable | Default | Key | Notes |
|---|---|---|---|---|---|
| OrderId | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| BookingReference | CHAR(6) | Yes | | UK | Populated on confirmation, e.g. `AB1234`; null before confirmation — multiple unconfirmed orders may have `NULL` simultaneously |
| OrderStatus | VARCHAR(20) | No | `'Draft'` | | `OrderInit` · `Draft` · `Confirmed` · `Changed` · `Cancelled` |
| ChannelCode | VARCHAR(20) | No | | | `WEB` · `APP` · `NDC` · `KIOSK` · `CC` · `AIRPORT` |
| CurrencyCode | CHAR(3) | No | `'GBP'` | | ISO 4217 currency code |
| TicketingTimeLimit | DATETIME2 | Yes | | | Latest time at which payment must complete; set at order creation; null until the order is confirmed |
| TotalAmount | DECIMAL(10,2) | Yes | | | Total order value including all order items; null until confirmed |
| CreatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| UpdatedAt | DATETIME2 | No | SYSUTCDATETIME() | | |
| Version | INT | No | `1` | | Optimistic concurrency version counter; incremented on every write |
| OrderData | NVARCHAR(MAX) | No | | | JSON document containing the full ONE Order detail (see example below) |

> **Indexes:** `IX_Order_BookingReference` (unique, filtered) on `(BookingReference)` WHERE `BookingReference IS NOT NULL` — enforces uniqueness of booking references whilst permitting multiple rows with a `NULL` booking reference (unconfirmed orders in `OrderInit` or `Draft` status).
> **Constraints:** `CHK_OrderData` — `ISJSON(OrderData) = 1`; `OrderData` must be a valid JSON document.
> **Column duplication:** Fields present as typed columns (`OrderId`, `BookingReference`, `OrderStatus`, `ChannelCode`, `CurrencyCode`, `TotalAmount`, `CreatedAt`) are NOT duplicated inside `OrderData`. The table columns are the single source of truth for those values; `OrderData` carries the relational detail only.
> **Concurrency:** `Version` is used for optimistic concurrency control — see [Optimistic Concurrency Control](#optimistic-concurrency-control).

**Example `OrderData` JSON document**

The JSON structure is aligned to IATA ONE Order concepts. Scalar identifiers and status fields that exist as typed columns on the `order.Order` table (`orderId`, `bookingReference`, `orderStatus`, `channel`, `currency`, `totalAmount`, `createdAt`) are excluded from the JSON document — the table columns are the single source of truth for those values. The JSON carries the relational detail: passengers, flight segments, order items, payments, and audit history.

```json
{
  "dataLists": {
    "passengers": [
      {
        "passengerId": "PAX-1",
        "type": "ADT",
        "givenName": "Alex",
        "surname": "Taylor",
        "dateOfBirth": "1985-03-12",
        "gender": "Male",
        "loyaltyNumber": "AX9876543",
        "contacts": {
          "email": "alex.taylor@example.com",
          "phone": "+447700900100"
        },
        "travelDocument": {
          "type": "PASSPORT",
          "number": "PA1234567",
          "issuingCountry": "GBR",
          "expiryDate": "2030-01-01",
          "nationality": "GBR"
        }
      },
      {
        "passengerId": "PAX-2",
        "type": "ADT",
        "givenName": "Jordan",
        "surname": "Taylor",
        "dateOfBirth": "1987-07-22",
        "gender": "Female",
        "loyaltyNumber": null,
        "contacts": null,
        "travelDocument": {
          "type": "PASSPORT",
          "number": "PA7654321",
          "issuingCountry": "GBR",
          "expiryDate": "2028-06-30",
          "nationality": "GBR"
        }
      }
    ],
    "flightSegments": [
      {
        "segmentId": "SEG-1",
        "flightNumber": "AX003",
        "origin": "LHR",
        "destination": "JFK",
        "departureDateTime": "2025-08-15T11:00:00Z",
        "arrivalDateTime": "2025-08-15T14:10:00Z",
        "aircraftType": "A351",
        "operatingCarrier": "AX",
        "marketingCarrier": "AX",
        "cabinCode": "J",
        "bookingClass": "J"
      },
      {
        "segmentId": "SEG-2",
        "flightNumber": "AX004",
        "origin": "JFK",
        "destination": "LHR",
        "departureDateTime": "2025-08-25T22:00:00Z",
        "arrivalDateTime": "2025-08-26T10:15:00Z",
        "aircraftType": "A351",
        "operatingCarrier": "AX",
        "marketingCarrier": "AX",
        "cabinCode": "J",
        "bookingClass": "J"
      }
    ]
  },
  "orderItems": [
    {
      "orderItemId": "OI-1",
      "type": "Flight",
      "segmentRef": "SEG-1",
      "passengerRefs": ["PAX-1", "PAX-2"],
      "offerId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "fareBasisCode": "JFLEXGB",
      "fareFamily": "Business Flex",
      "unitPrice": 350.00,
      "taxes": 87.25,
      "totalPrice": 437.25,
      "isRefundable": true,
      "isChangeable": true,
      "paymentId": "d4f5a6b7-1111-4c8d-9e0f-1a2b3c4d5e01",
      "eTickets": [
        { "passengerId": "PAX-1", "eTicketNumber": "932-1234567890" },
        { "passengerId": "PAX-2", "eTicketNumber": "932-1234567891" }
      ],
      "seatAssignments": [
        { "passengerId": "PAX-1", "seatNumber": "1A" },
        { "passengerId": "PAX-2", "seatNumber": "1K" }
      ]
    },
    {
      "orderItemId": "OI-2",
      "type": "Flight",
      "segmentRef": "SEG-2",
      "passengerRefs": ["PAX-1", "PAX-2"],
      "offerId": "7cb87a21-1234-4abc-9def-1a2b3c4d5e6f",
      "fareBasisCode": "JFLEXGB",
      "fareFamily": "Business Flex",
      "unitPrice": 350.00,
      "taxes": 87.25,
      "totalPrice": 437.25,
      "isRefundable": true,
      "isChangeable": true,
      "paymentId": "d4f5a6b7-1111-4c8d-9e0f-1a2b3c4d5e01",
      "eTickets": [
        { "passengerId": "PAX-1", "eTicketNumber": "932-1234567892" },
        { "passengerId": "PAX-2", "eTicketNumber": "932-1234567893" }
      ],
      "seatAssignments": [
        { "passengerId": "PAX-1", "seatNumber": "2A" },
        { "passengerId": "PAX-2", "seatNumber": "2K" }
      ]
    },
    {
      "orderItemId": "OI-3",
      "type": "Seat",
      "segmentRef": "SEG-1",
      "passengerRefs": ["PAX-1"],
      "offerId": "a1b2c3d4-seat-4562-b3fc-000000000001",
      "seatNumber": "1A",
      "seatPosition": "Window",
      "unitPrice": 0.00,
      "taxes": 0.00,
      "totalPrice": 0.00,
      "note": "Business Class — seat selection included in fare"
    },
    {
      "orderItemId": "OI-4",
      "type": "Seat",
      "segmentRef": "SEG-1",
      "passengerRefs": ["PAX-2"],
      "offerId": "a1b2c3d4-seat-4562-b3fc-000000000002",
      "seatNumber": "1K",
      "seatPosition": "Window",
      "unitPrice": 0.00,
      "taxes": 0.00,
      "totalPrice": 0.00,
      "note": "Business Class — seat selection included in fare"
    },
    {
      "orderItemId": "OI-5",
      "type": "Bag",
      "segmentRef": "SEG-1",
      "passengerRefs": ["PAX-1"],
      "bagOfferId": "bo-business-bag1-v1",
      "freeBagsIncluded": 2,
      "additionalBags": 1,
      "bagSequence": 1,
      "unitPrice": 60.00,
      "taxes": 0.00,
      "totalPrice": 60.00,
      "paymentId": "e5a6b7c8-2222-4d9e-af01-2b3c4d5e6f02"
    }
  ],
  "payments": [
    {
      "paymentId": "d4f5a6b7-1111-4c8d-9e0f-1a2b3c4d5e01",
      "description": "Fare — LHR-JFK-LHR, 2 PAX",
      "method": "CreditCard",
      "cardLast4": "4242",
      "cardType": "Visa",
      "authorisedAmount": 1749.00,
      "settledAmount": 1749.00,
      "currency": "GBP",
      "status": "Settled",
      "authorisedAt": "2025-06-01T10:31:00Z",
      "settledAt": "2025-06-01T10:32:00Z"
    },
    {
      "paymentId": "e5a6b7c8-2222-4d9e-af01-2b3c4d5e6f02",
      "description": "Bag ancillary — SEG-1, PAX-1, 1 additional bag",
      "method": "CreditCard",
      "cardLast4": "4242",
      "cardType": "Visa",
      "authorisedAmount": 60.00,
      "settledAmount": 60.00,
      "currency": "GBP",
      "status": "Settled",
      "authorisedAt": "2025-06-01T10:45:00Z",
      "settledAt": "2025-06-01T10:45:10Z"
    }
  ],
  "history": [
    { "event": "OrderCreated",   "at": "2025-06-01T10:30:00Z", "by": "WEB" },
    { "event": "OrderConfirmed", "at": "2025-06-01T10:32:00Z", "by": "WEB" },
    { "event": "BagAncillaryAdded", "at": "2025-06-01T10:45:00Z", "by": "WEB" }
  ]
}
```

---

## Timer triggers

### `DeleteExpiredBaskets`

- **Function name:** `DeleteExpiredBaskets`
- **Schedule:** `0 0 * * * *` — at the top of every hour
- **Microservice:** `ReservationSystem.Microservices.Order`
- **Handler:** `DeleteExpiredBasketsHandler`

Deletes all `order.Basket` rows whose `ExpiresAt` timestamp is in the past. Baskets expire 60 minutes after creation, matching the `StoredOffer` expiry window. This ensures that abandoned bookings do not accumulate in the database and that any inventory held against expired baskets is not blocked indefinitely.

The function logs a start message and a completion message containing the count of deleted basket rows. It is invoked by the Azure Functions runtime on its cron schedule and receives a `CancellationToken` for graceful shutdown.
