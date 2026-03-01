# System Architecture - Design

## Overview

This outlines the design for an airline reservation system based on offer and order capability (Modern Airline Retailing).

The system will have the following core concepts.

- Offer - returns availability and pricing of the airlines flights
- Order - creates orders (bookings on the plane) based on the offer, with passenger information included, and takes payment
- Payment - payment orchestration, supporting at first credit card payments but in future other payment methods like PayPal and ApplePay.
- Servicing - change and cancel of orders
- Delivery - Akin to departure control, including online check in (OLCI), irregular operations (IROPS), seat allocation, gate management
- Customer - loyalty accounts for customers - with customer details, points balances, and transaction (historical and future orders)
- Accounting - accounting system - keeping a track of all orders, refunds, balance sheets, profit and loss.
- Seat - manages seatmap definitions per aircraft type; provides seatmap views to other services and channels (does not manage seat selection or inventory)

Please note (these one-name capability 'domain names' should be used for domain naming in the code)

## High level system architecture

```mermaid
graph TB
    subgraph Channels
        WEB[🌐 Web]
        APP[📱 App]
        NDC[✈️ NDC / GDS / OTA]
        KIOSK[🖥️ Kiosk]
        CC[🎧 Contact Centre App]
        AIRPORT[🛫 Airport App]
        ACCT_CH[📊 Accounting System App]
    end

    subgraph Orchestration["Orchestration APIs"]
        RETAIL_API[Retail API]
        LOYALTY_API[Loyalty API]
        AIRPORT_API[Airport API]
        ACCOUNTING_API[Accounting API]
    end

    subgraph Microservices
        subgraph OFFER_SVC["Offer Service"]
            OFFER[Offer]
            INV_DB[(Inventory DB)]
        end

        subgraph ORDER_SVC["Order Service"]
            ORDER[Order]
            ORDER_DB[(Order DB)]
        end

        subgraph PAYMENT_SVC["Payment Service"]
            PAYMENT[Payment]
            PAYMENT_DB[(Payment DB)]
        end

        subgraph SERVICING_SVC["Servicing Service"]
            SERVICING[Servicing]
        end

        subgraph DELIVERY_SVC["Delivery Service"]
            DELIVERY[Delivery]
            DELIVERY_DB[(Delivery DB)]
        end

        subgraph CUSTOMER_SVC["Customer Service"]
            CUSTOMER[Customer]
            CUSTOMER_DB[(Customer DB)]
        end

        subgraph ACCOUNTING_SVC["Accounting Service"]
            ACCOUNTING[Accounting]
            ACCOUNTING_DB[(Accounting DB)]
        end

        subgraph SEAT_SVC["Seat Service"]
            SEAT[Seat]
            SEAT_DB[(Seat DB)]
        end
    end

    subgraph Events["Event Bus"]
        EVT[Order & Servicing Events]
    end

    %% Channel → Orchestration
    WEB & APP & NDC & KIOSK & CC & AIRPORT --> RETAIL_API
    WEB & APP & CC --> LOYALTY_API
    AIRPORT --> AIRPORT_API
    ACCT_CH --> ACCOUNTING_API

    %% Orchestration → Microservices
    RETAIL_API --> OFFER & ORDER & PAYMENT & SERVICING & DELIVERY & CUSTOMER & SEAT
    LOYALTY_API --> CUSTOMER
    AIRPORT_API --> SERVICING & DELIVERY & CUSTOMER & SEAT
    ACCOUNTING_API --> ACCOUNTING

    %% Microservice → DB
    OFFER --> INV_DB
    ORDER --> ORDER_DB
    PAYMENT --> PAYMENT_DB
    SERVICING --> ORDER_DB
    DELIVERY --> DELIVERY_DB
    CUSTOMER --> CUSTOMER_DB
    ACCOUNTING --> ACCOUNTING_DB
    SEAT --> SEAT_DB

    %% Eventing to Accounting
    ORDER & SERVICING --> EVT
    EVT --> ACCOUNTING
```

Key components:

- Channels
  - Web
  - App
  - NDC (XML APIs based on IATA NDC standard for GDS and other airlines (OTAs) to connect to)
  - Kiosk (self service airport check in terminals)
  - Contact Centre App (for new bookings, IROPS management, customer account management)
  - Airport App (for airport staff to manage non-OLCI check in, and gate management, seat assignment, etc)
  - Accounting System App
- Orchestration APIs (these act as the APIs to connect the channels to the microservices)
  - Retail API (for web, app, NDC, kiosk, contact centre app, airport app)
  - Loyalty API (for web, app, contact centre)
  - Airport API (for Airport App)
  - Accounting API (for accounting system app)
- Microservices (and their data-bound databases)
  - Offer
    - Inventory DB
  - Order
    - Order DB
  - Payment
    - Payment DB
  - Servicing
    - Uses Order DB
  - Delivery
    - Delivery DB
  - Customer
    - Customer DB
  - Accounting (orders and changes should be evented to this microservice from Order and Servicing microservices)
    - Accounting DB
  - Seat (manages seatmap definitions per aircraft type; provides seatmap views only — seat selection and inventory remain with Offer)
    - Seat DB

# Capability

## Offer

### Search

The search flow is built around the concept of a **slice** — a single directional search (outbound or inbound). The customer searches for each slice independently. Each search returns a set of offers; those offers are persisted immediately to the `StoredOffer` table so that pricing is locked at the point of offer creation. The customer selects one offer per slice, and the resulting `OfferIds` are passed through to the basket and ultimately to the Order API.

This ensures price integrity: the Order API retrieves the stored offer by `OfferId` rather than re-pricing, so the fare the customer saw is guaranteed to be the fare charged — regardless of how much time elapses during payment.

```mermaid
sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant OfferMS as Offer [MS]
    participant OrderMS as Order [MS]

    Traveller->>Web: Search for outbound flight (origin, destination, date, pax)

    Web->>RetailAPI: POST /search/slice (origin, destination, date, pax, direction=outbound)
    RetailAPI->>OfferMS: Search availability (outbound slice)
    OfferMS->>OfferMS: Persist each result to StoredOffer table with unique OfferId
    OfferMS-->>RetailAPI: Outbound offer options (each with OfferId, flight details, fare, price)
    RetailAPI-->>Web: Display outbound options

    Traveller->>Web: Select preferred outbound offer

    opt Traveller wants a return flight
        Traveller->>Web: Search for inbound flight (origin, destination, date, pax)
        Web->>RetailAPI: POST /search/slice (origin, destination, date, pax, direction=inbound)
        RetailAPI->>OfferMS: Search availability (inbound slice)
        OfferMS->>OfferMS: Persist each result to StoredOffer table with unique OfferId
        OfferMS-->>RetailAPI: Inbound offer options (each with OfferId, flight details, fare, price)
        RetailAPI-->>Web: Display inbound options
        Traveller->>Web: Select preferred inbound offer
    end

    Note over Web, RetailAPI: Basket contains one or more OfferIds (outbound mandatory, inbound optional)

    Web->>RetailAPI: POST /basket (offerIds: [OfferId-Out, OfferId-In?], pax details)
    RetailAPI->>OrderMS: Create basket (offerIds, pax)
    OrderMS-->>RetailAPI: Basket ID + basket summary
    RetailAPI-->>Web: Basket confirmed (Basket ID, itinerary, total price)
    Web-->>Traveller: Display basket summary — ready to proceed to booking
```

### Data Schema — Offer

The Offer domain maintains three tables. `FlightInventory` tracks available seat capacity per flight and cabin. `Fare` records fare basis, pricing, and conditions per inventory record. `StoredOffer` persists the specific offer returned to a customer at search time, capturing the exact fare, flight, and pricing snapshot so that price integrity is maintained through to order creation.

```sql
-- offer.FlightInventory
-- One row per flight leg per cabin class
CREATE TABLE offer.FlightInventory (
    InventoryId       UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWID() PRIMARY KEY,
    FlightNumber      VARCHAR(10)       NOT NULL,   -- e.g. AX001
    DepartureDate     DATE              NOT NULL,
    Origin            CHAR(3)           NOT NULL,   -- IATA airport code
    Destination       CHAR(3)           NOT NULL,
    AircraftType      VARCHAR(4)        NOT NULL,   -- IATA-style 4-char code: manufacturer prefix + 3-digit variant, e.g. A351, B789
    CabinCode         CHAR(1)           NOT NULL,   -- F, J, W, Y
    TotalSeats        SMALLINT          NOT NULL,
    SeatsAvailable    SMALLINT          NOT NULL,
    SeatsSold         SMALLINT          NOT NULL DEFAULT 0,
    SeatsHeld         SMALLINT          NOT NULL DEFAULT 0,  -- seats held in baskets, not yet ticketed
    UpdatedAt         DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME()
);

CREATE INDEX IX_FlightInventory_Flight
    ON offer.FlightInventory (FlightNumber, DepartureDate, CabinCode);

-- offer.Fare
-- One row per fare offering, linked to a flight inventory record.
-- Pricing is broken into base fare, taxes, and total for accounting clarity.
CREATE TABLE offer.Fare (
    FareId            UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWID() PRIMARY KEY,
    InventoryId       UNIQUEIDENTIFIER  NOT NULL REFERENCES offer.FlightInventory(InventoryId),
    FareBasisCode     VARCHAR(20)       NOT NULL,   -- e.g. YLOWUK, JFLEXGB
    FareFamily        VARCHAR(50)       NULL,       -- e.g. Economy Light, Business Flex
    CabinCode         CHAR(1)           NOT NULL,
    BookingClass      CHAR(2)           NOT NULL,   -- revenue management booking class, e.g. Y, B, J
    CurrencyCode      CHAR(3)           NOT NULL DEFAULT 'GBP',
    BaseFareAmount    DECIMAL(10,2)     NOT NULL,
    TaxAmount         DECIMAL(10,2)     NOT NULL,
    TotalAmount       DECIMAL(10,2)     NOT NULL,   -- BaseFareAmount + TaxAmount
    IsRefundable      BIT               NOT NULL DEFAULT 0,
    IsChangeable      BIT               NOT NULL DEFAULT 0,
    ValidFrom         DATETIME2         NOT NULL,
    ValidTo           DATETIME2         NOT NULL
);

-- offer.StoredOffer
-- One row per offer presented to a customer during search. Captures a point-in-time
-- snapshot of the flight and fare so that price is honoured when the order is placed,
-- regardless of subsequent fare changes. OfferIds are passed into the basket and Order API.
CREATE TABLE offer.StoredOffer (
    OfferId           UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWID() PRIMARY KEY,
    InventoryId       UNIQUEIDENTIFIER  NOT NULL REFERENCES offer.FlightInventory(InventoryId),
    FareId            UNIQUEIDENTIFIER  NOT NULL REFERENCES offer.Fare(FareId),
    FlightNumber      VARCHAR(10)       NOT NULL,
    DepartureDate     DATE              NOT NULL,
    Origin            CHAR(3)           NOT NULL,
    Destination       CHAR(3)           NOT NULL,
    AircraftType      VARCHAR(4)        NOT NULL,
    CabinCode         CHAR(1)           NOT NULL,
    BookingClass      CHAR(2)           NOT NULL,
    FareBasisCode     VARCHAR(20)       NOT NULL,
    FareFamily        VARCHAR(50)       NULL,
    CurrencyCode      CHAR(3)           NOT NULL DEFAULT 'GBP',
    BaseFareAmount    DECIMAL(10,2)     NOT NULL,
    TaxAmount         DECIMAL(10,2)     NOT NULL,
    TotalAmount       DECIMAL(10,2)     NOT NULL,
    IsRefundable      BIT               NOT NULL DEFAULT 0,
    IsChangeable      BIT               NOT NULL DEFAULT 0,
    CreatedAt         DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
    ExpiresAt         DATETIME2         NOT NULL,   -- offer expiry; Order API should reject expired offers
    IsConsumed        BIT               NOT NULL DEFAULT 0  -- set to 1 once retrieved by Order API
);

CREATE INDEX IX_StoredOffer_Expiry
    ON offer.StoredOffer (ExpiresAt)
    WHERE IsConsumed = 0;
```

-----

## Order

### Create

The Order API accepts an array of `OfferIds` from the basket (one per slice). For each `OfferId`, it calls the Offer microservice to retrieve the stored offer snapshot, using that data to populate the order items. This ensures the price and fare conditions recorded on the order exactly match what the customer was shown at search time.

```mermaid
sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order [MS]
    participant OfferMS as Offer [MS]
    participant PaymentMS as Payment [MS]
    participant DeliveryMS as Delivery [MS]
    participant AccountingMS as Accounting [MS]

    Traveller->>Web: Enter passenger details

    Web->>RetailAPI: POST /order (basket ID, offerIds: [OfferId-Out, OfferId-In?], passenger details)
    RetailAPI->>OrderMS: Create order (offerIds, passenger details)

    loop For each OfferId
        OrderMS->>OfferMS: GET /offer/{offerId} (retrieve stored offer)
        OfferMS-->>OrderMS: Stored offer snapshot (flight, fare, pricing)
    end

    OrderMS-->>RetailAPI: Order created (draft order ID, itinerary, total price)
    RetailAPI-->>Web: Order summary (draft order ID, itinerary, total price)

    Traveller->>Web: Enter payment details and confirm booking

    Web->>RetailAPI: POST /order/{id}/pay (payment details)
    RetailAPI->>PaymentMS: Authorise card (amount, card details)
    PaymentMS-->>RetailAPI: Authorisation confirmed (auth token)

    Note over RetailAPI, OfferMS: Ticketing process begins

    RetailAPI->>OfferMS: Remove seats from inventory (per stored offer)
    OfferMS-->>RetailAPI: Inventory updated

    RetailAPI->>DeliveryMS: Create e-tickets (order ID, passenger details, flights)
    DeliveryMS-->>RetailAPI: E-ticket numbers issued

    RetailAPI->>PaymentMS: Settle payment (auth token, amount)
    PaymentMS-->>RetailAPI: Payment settled

    RetailAPI->>OrderMS: Confirm order (e-ticket numbers, booking reference)
    OrderMS-->>RetailAPI: Order confirmed (6-digit booking reference)

    RetailAPI->>DeliveryMS: Write manifest entries (inventoryId, seatNumber, bookingReference, eTicketNumber, passengerId — per PAX per flight segment)
    DeliveryMS-->>RetailAPI: Manifest entries written

    Note over OrderMS, AccountingMS: Async event
    OrderMS-)AccountingMS: OrderConfirmed event (booking reference, amount, e-tickets)

    RetailAPI-->>Web: Booking confirmed (booking reference, e-ticket numbers)
    Web-->>Traveller: Display booking confirmation
```

### Data Schema — Order

The Order domain follows the IATA ONE Order model. The `Order` table holds scalar fields used for querying, routing, reporting, and event publishing. The full order detail — passengers, flight segments, order items, fares, seat assignments, e-tickets, payments, and audit history — is stored as a JSON document in the `OrderData` column. Fields that exist as typed columns on the table (such as `OrderId`, `BookingReference`, `OrderStatus`, `ChannelCode`, `CurrencyCode`, and `TotalAmount`) are intentionally excluded from the JSON document to avoid duplication.

```sql
-- order.Order
-- Root order record. OrderData holds the full ONE Order document as JSON.
-- Scalar fields used for indexed lookups, routing, and eventing are stored as columns.
-- Fields present as columns are NOT duplicated inside OrderData.
CREATE TABLE order.Order (
    OrderId           UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWID() PRIMARY KEY,
    BookingReference  CHAR(6)           NULL,        -- populated on confirmation, e.g. AB1234
    OrderStatus       VARCHAR(20)       NOT NULL DEFAULT 'Draft',
                                                     -- Draft | Confirmed | Cancelled | Changed
    ChannelCode       VARCHAR(20)       NOT NULL,    -- WEB | APP | NDC | KIOSK | CC | AIRPORT
    CurrencyCode      CHAR(3)           NOT NULL DEFAULT 'GBP',
    TotalAmount       DECIMAL(10,2)     NULL,
    CreatedAt         DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
    OrderData         NVARCHAR(MAX)     NOT NULL     -- JSON: full ONE Order document (see below)

    CONSTRAINT CHK_OrderData CHECK (ISJSON(OrderData) = 1)
);

CREATE UNIQUE INDEX IX_Order_BookingReference
    ON order.Order (BookingReference)
    WHERE BookingReference IS NOT NULL;
```

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
      "eTickets": [
        { "passengerId": "PAX-1", "eTicketNumber": "932-1234567890" },
        { "passengerId": "PAX-2", "eTicketNumber": "932-1234567891" }
      ],
      "seatAssignments": [
        { "passengerId": "PAX-1", "seatNumber": "1A" },
        { "passengerId": "PAX-2", "seatNumber": "1B" }
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
      "eTickets": [
        { "passengerId": "PAX-1", "eTicketNumber": "932-1234567892" },
        { "passengerId": "PAX-2", "eTicketNumber": "932-1234567893" }
      ],
      "seatAssignments": [
        { "passengerId": "PAX-1", "seatNumber": "2A" },
        { "passengerId": "PAX-2", "seatNumber": "2B" }
      ]
    }
  ],
  "payments": [
    {
      "paymentId": "PMT-1",
      "method": "CreditCard",
      "amount": 874.50,
      "currency": "GBP",
      "status": "Settled",
      "cardLast4": "4242",
      "cardType": "Visa",
      "settledAt": "2025-06-01T10:32:00Z"
    }
  ],
  "history": [
    { "event": "OrderCreated",   "at": "2025-06-01T10:30:00Z", "by": "WEB" },
    { "event": "OrderConfirmed", "at": "2025-06-01T10:32:00Z", "by": "WEB" }
  ]
}
```

-----

## Servicing

### Manage booking - update PAX details

Allows a traveller to correct or update passenger information on a confirmed booking — such as a name correction, updated passport details, or a change of contact information — triggering e-ticket reissuance where required.

```mermaid
sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant ServicingMS as Servicing [MS]
    participant DeliveryMS as Delivery [MS]

    Traveller->>Web: Navigate to manage booking

    Web->>RetailAPI: POST /order/retrieve (bookingReference, givenName, surname)
    RetailAPI->>ServicingMS: Retrieve order (bookingReference, givenName, surname)
    ServicingMS-->>RetailAPI: Order details (PAX details, itinerary, e-tickets)
    RetailAPI-->>Web: Display current booking details

    Traveller->>Web: Update passenger details (e.g. name, passport, contact info)

    Web->>RetailAPI: PATCH /order/{bookingRef}/passengers (updated PAX details)
    RetailAPI->>ServicingMS: Update PAX details on order (booking reference, updated PAX)
    ServicingMS-->>RetailAPI: Order updated

    RetailAPI->>DeliveryMS: Reissue e-tickets (booking reference, updated PAX details)
    DeliveryMS-->>RetailAPI: Updated e-ticket numbers issued

    RetailAPI-->>Web: Update confirmed (booking reference, updated e-ticket numbers)
    Web-->>Traveller: Display updated booking confirmation
```

### Manage booking - select or update seat selection

Enables a traveller to choose or change their seat assignment after booking, presenting the live seatmap with real-time availability overlaid, and updating the manifest and e-tickets upon confirmation.

```mermaid
sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant ServicingMS as Servicing [MS]
    participant SeatMS as Seat [MS]
    participant OfferMS as Offer [MS]
    participant DeliveryMS as Delivery [MS]
    participant AccountingMS as Accounting [MS]

    Traveller->>Web: Navigate to manage booking

    Web->>RetailAPI: POST /order/retrieve (bookingReference, givenName, surname)
    RetailAPI->>ServicingMS: Retrieve order (bookingReference, givenName, surname)
    ServicingMS-->>RetailAPI: Order details (PAX list, current seat assignments, itinerary)
    RetailAPI-->>Web: Display current booking and seat map

    Web->>RetailAPI: GET /flights/{flightId}/seatmap
    RetailAPI->>SeatMS: Retrieve seatmap definition (aircraft type)
    SeatMS-->>RetailAPI: Seatmap layout and configuration
    RetailAPI->>OfferMS: Retrieve seat availability (flight ID)
    OfferMS-->>RetailAPI: Available and occupied seats
    RetailAPI-->>Web: Display seat map (layout from Seat MS, availability from Offer MS)

    Traveller->>Web: Select seat(s) for each PAX

    Web->>RetailAPI: PATCH /order/{bookingRef}/seats (PAX ID, selected seat per flight)

    RetailAPI->>OfferMS: Reserve selected seats in inventory (flight ID, seat numbers)
    OfferMS-->>RetailAPI: Seats reserved

    RetailAPI->>ServicingMS: Update seat assignment on order (booking reference, PAX seats)
    ServicingMS-->>RetailAPI: Order updated

    RetailAPI->>DeliveryMS: Reissue e-tickets (booking reference, updated seat assignments)
    DeliveryMS-->>RetailAPI: Updated e-ticket numbers issued

    RetailAPI->>DeliveryMS: Update manifest entries (inventoryId, seatNumber, bookingReference, eTicketNumber, passengerId — per affected PAX per flight segment)
    DeliveryMS-->>RetailAPI: Manifest entries updated

    RetailAPI-->>Web: Seat selection confirmed (booking reference, updated e-tickets)
    Web-->>Traveller: Display updated booking confirmation with seat assignments

    Note over ServicingMS, AccountingMS: Async event
    ServicingMS-)AccountingMS: OrderServiced event (booking reference, seat change details)
```

## Delivery

### Online Check In

Allows a traveller to check in for their flight from 24 hours before departure, confirming or updating travel document details for each passenger and receiving boarding cards upon completion.

```mermaid
sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant ServicingMS as Servicing [MS]
    participant OfferMS as Offer [MS]
    participant DeliveryMS as Delivery [MS]

    Traveller->>Web: Navigate to online check-in

    Web->>RetailAPI: POST /order/checkin/retrieve (bookingReference, givenName, surname)
    RetailAPI->>ServicingMS: Retrieve order and eligibility (bookingReference, givenName, surname)
    ServicingMS-->>RetailAPI: Order details (PAX list, flights, seat assignments, e-tickets)
    RetailAPI-->>Web: Display PAX list and pre-flight details

    Traveller->>Web: Confirm / update travel document details for each PAX

    Web->>RetailAPI: POST /order/{bookingRef}/checkin (PAX IDs, travel document details)

    RetailAPI->>ServicingMS: Check in all PAX (booking reference, travel document details)
    ServicingMS-->>RetailAPI: PAX checked in, APIS data recorded

    RetailAPI->>OfferMS: Update seat inventory status to checked-in (flight ID, seat numbers)
    OfferMS-->>RetailAPI: Inventory updated

    RetailAPI->>DeliveryMS: Update manifest check-in status (bookingReference, PAX IDs, checkedIn=true, checkedInAt=now)
    DeliveryMS-->>RetailAPI: Manifest entries updated

    RetailAPI->>DeliveryMS: Generate boarding cards (booking reference, PAX list, seats, flights)
    DeliveryMS-->>RetailAPI: Boarding cards generated (one per PAX per flight) including boarding pass barcode string

    RetailAPI-->>Web: Check-in confirmed (boarding cards)
    Web-->>Traveller: Display and offer download of boarding cards
```

### Boarding Pass Barcode String

Each boarding card issued by the Delivery microservice includes a barcode string compliant with **IATA Resolution 792** (Bar Coded Boarding Pass — BCBP). This string is used directly to generate the physical barcode on printed boarding passes and the QR code displayed in the mobile app. Both formats encode identical data; the presentation layer determines the rendering.

The format is a structured plaintext string with fixed-width and positional fields. An example for a single-leg boarding pass:

```
M1TAYLOR/ALEX        EAB1234 LHRJFKAX 0003 042J001A0001 156>518 W6042 AX 2A00000012345678 JAX7KLP2NZR901A
```

The fields break down as follows:

| Segment | Value in example | Description |
|---|---|---|
| `M1` | `M1` | Format code (`M`) + number of legs encoded (`1`) |
| `TAYLOR/ALEX` | `TAYLOR/ALEX` | Passenger name — surname / given name, padded to 20 chars |
| `EAB1234` | `EAB1234` | Electronic ticket indicator (`E`) + PNR / booking reference |
| `LHR` | `LHR` | Origin IATA airport code |
| `JFK` | `JFK` | Destination IATA airport code |
| `AX` | `AX` | Operating carrier IATA code (Apex Air) |
| `0003` | `0003` | Flight number, padded to 4 chars |
| `042` | `042` | Julian date of flight departure |
| `J` | `J` | Cabin / booking class code |
| `001A` | `001A` | Seat number, padded to 4 chars |
| `0001` | `0001` | Sequence / check-in number |
| `1` | `1` | Passenger status code (`1` = checked in) |
| `56>518` | `56>518` | Conditional item size indicator and version number (BCBP version 6) |
| `W6042` | `W6042` | Julian date of issue + ticket issuer code |
| `AX` | `AX` | Operating carrier for this leg (repeated in conditional section) |
| `2A00000012345678` | `2A00000012345678` | Frequent flyer / loyalty number |
| `JAX7KLP2NZR901A` | `JAX7KLP2NZR901A` | Airline-specific free-text data (selectee indicator, document verification, etc.) |

The Delivery microservice is responsible for assembling this string at the point of boarding card generation, drawing on data from the `FlightManifest` row and the confirmed order. The barcode string is returned in the boarding card payload alongside human-readable fields; channels render it using their preferred barcode library (e.g. PDF417 for print, QR for mobile).

### Data Schema — Delivery

The Delivery domain owns its own `Delivery DB` and is the system of record for who is on each flight and where they are sitting. The `FlightManifest` table holds one row per passenger per flight segment, populated at the point of booking confirmation and updated whenever a seat is changed post-purchase. It provides a clean, queryable view of the passenger load for a given flight — used for gate management, check-in verification, IROPS, and regulatory APIS submissions.

Seat number integrity is enforced at the application layer: before any insert or update, the Delivery microservice calls the Seat microservice to validate that the given `SeatNumber` exists on the active seatmap for the relevant aircraft type. Rows may not be written with a seat number that does not appear in the seatmap definition. This prevents manifest corruption from downstream data entry errors or stale seat references.

```sql
-- delivery.FlightManifest
-- One row per passenger per flight segment. Written at booking confirmation;
-- updated on any post-purchase seat change. SeatNumber must be a valid seat
-- from the active seatmap for the aircraft type — validated at application layer
-- before insert or update.
CREATE TABLE delivery.FlightManifest (
    ManifestId        UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWID() PRIMARY KEY,
    InventoryId       UNIQUEIDENTIFIER  NOT NULL,               -- FK ref to offer.FlightInventory (cross-schema; not enforced as DB constraint)
    FlightNumber      VARCHAR(10)       NOT NULL,               -- denormalised for query convenience, e.g. AX003
    DepartureDate     DATE              NOT NULL,               -- denormalised for query convenience
    AircraftType      CHAR(4)           NOT NULL,               -- used for seatmap validation at write time
    SeatNumber        VARCHAR(5)        NOT NULL,               -- e.g. 1A, 22K — must exist on active seatmap for AircraftType
    CabinCode         CHAR(1)           NOT NULL,               -- F, J, W, Y
    BookingReference  CHAR(6)           NOT NULL,               -- e.g. AB1234
    ETicketNumber     VARCHAR(20)       NOT NULL,               -- e.g. 932-1234567890
    PassengerId       VARCHAR(20)       NOT NULL,               -- PAX reference from the order, e.g. PAX-1
    GivenName         VARCHAR(100)      NOT NULL,               -- denormalised for manifest readability
    Surname           VARCHAR(100)      NOT NULL,               -- denormalised for manifest readability
    CheckedIn         BIT               NOT NULL DEFAULT 0,
    CheckedInAt       DATETIME2         NULL,
    CreatedAt         DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt         DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME()
);

-- Unique constraint: one seat per flight per manifest (prevents double-assignment)
CREATE UNIQUE INDEX IX_FlightManifest_Seat
    ON delivery.FlightManifest (InventoryId, SeatNumber);

-- Unique constraint: one manifest entry per PAX per flight
CREATE UNIQUE INDEX IX_FlightManifest_Pax
    ON delivery.FlightManifest (InventoryId, ETicketNumber);

-- Index to support fast flight-level manifest retrieval (gate staff, IROPS)
CREATE INDEX IX_FlightManifest_Flight
    ON delivery.FlightManifest (FlightNumber, DepartureDate);

-- Index to support lookup by booking reference (customer servicing, check-in)
CREATE INDEX IX_FlightManifest_BookingReference
    ON delivery.FlightManifest (BookingReference);
```

> **Cross-schema integrity:** `InventoryId` references `offer.FlightInventory` but is not declared as a foreign key, as the Delivery and Offer domains are logically separated (and would be physically separated in a fully isolated deployment). Referential integrity between these schemas is the responsibility of the Retail API orchestration layer, which controls the write sequence.

> **Seatmap validation:** The Delivery microservice must call `GET /seatmap/{aircraftType}` on the Seat microservice and confirm the `SeatNumber` exists in the returned cabin layout before writing any `FlightManifest` row. If the seat is not present on the active seatmap, the write must be rejected with an appropriate error. This check applies to both initial inserts (at booking confirmation) and updates (at seat changes).

## Seat

### Retrieve Seatmap

The Seat microservice is the system of record for aircraft seatmap definitions, organised by aircraft type (e.g. A351, B789). It provides the physical layout, seat attributes (class, position, extra legroom, etc.) and cabin configuration. It does **not** manage seat availability or inventory — that remains the responsibility of the Offer microservice.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant SeatMS as Seat [MS]
    participant SeatDB as Seat DB

    RetailAPI->>SeatMS: GET /seatmap/{aircraftType}
    SeatMS->>SeatDB: Retrieve seatmap definition (aircraft type)
    SeatDB-->>SeatMS: Seatmap rows, seats, cabin zones, seat attributes
    SeatMS-->>RetailAPI: Seatmap definition (layout, cabin config, seat metadata)
```

### Data Schema — Seat

The Seat domain uses two relational tables: `AircraftType` as the root reference record, and `Seatmap` which holds one row per active aircraft configuration. The cabin layout and all seat definitions are stored as a JSON document in the `CabinLayout` column. This is well-suited to JSON storage as the layout is a hierarchical, read-heavy document that varies significantly by aircraft type and is consumed whole by the front-end seat picker rather than queried row-by-row. Aircraft type codes follow the 4-character convention defined in Technical Considerations (e.g. `A351`, `B789`).

```sql
-- seat.AircraftType
-- Reference table of aircraft types operated by the airline
CREATE TABLE seat.AircraftType (
    AircraftTypeCode  CHAR(4)           NOT NULL PRIMARY KEY,  -- 4-char code: manufacturer prefix + 3-digit variant, e.g. A351 (A350-1000), B789 (B787-900)
    Manufacturer      VARCHAR(50)       NOT NULL,              -- e.g. Airbus, Boeing
    FriendlyName      VARCHAR(100)      NULL,                  -- e.g. Airbus A350-1000, Boeing 787-900
    TotalSeats        SMALLINT          NOT NULL,
    IsActive          BIT               NOT NULL DEFAULT 1
);

-- seat.Seatmap
-- One row per active aircraft configuration. CabinLayout holds the full seatmap as JSON.
CREATE TABLE seat.Seatmap (
    SeatmapId         UNIQUEIDENTIFIER  NOT NULL DEFAULT NEWID() PRIMARY KEY,
    AircraftTypeCode  CHAR(4)           NOT NULL REFERENCES seat.AircraftType(AircraftTypeCode),
    Version           INT               NOT NULL DEFAULT 1,
    IsActive          BIT               NOT NULL DEFAULT 1,
    UpdatedAt         DATETIME2         NOT NULL DEFAULT SYSUTCDATETIME(),
    CabinLayout       NVARCHAR(MAX)     NOT NULL   -- JSON: full cabin and seat definitions (see below)

    CONSTRAINT CHK_CabinLayout CHECK (ISJSON(CabinLayout) = 1)
);

CREATE INDEX IX_Seatmap_AircraftType
    ON seat.Seatmap (AircraftTypeCode)
    WHERE IsActive = 1;
```

**Example `CabinLayout` JSON document**

The JSON is structured as an ordered array of cabins, each containing a column configuration and an array of rows. Each seat carries its label, position, and a set of physical attributes. This structure is consumed directly by the front-end seat picker UI, which overlays real-time availability from the Offer microservice at query time.

```json
{
  "aircraftType": "A351",
  "version": 1,
  "totalSeats": 258,
  "cabins": [
    {
      "cabinCode": "J",
      "cabinName": "Upper Class",
      "deckLevel": "Main",
      "startRow": 1,
      "endRow": 8,
      "columns": ["A", "D", "G", "K"],
      "layout": "1-1-1-1",
      "rows": [
        {
          "rowNumber": 1,
          "seats": [
            {
              "seatNumber": "1A",
              "column": "A",
              "type": "Suite",
              "position": "Window",
              "attributes": ["ExtraLegroom", "BlockedForCrew"],
              "isSelectable": false
            },
            {
              "seatNumber": "1D",
              "column": "D",
              "type": "Suite",
              "position": "Middle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "1G",
              "column": "G",
              "type": "Suite",
              "position": "Middle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "1K",
              "column": "K",
              "type": "Suite",
              "position": "Window",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            }
          ]
        },
        {
          "rowNumber": 2,
          "seats": [
            {
              "seatNumber": "2A",
              "column": "A",
              "type": "Suite",
              "position": "Window",
              "attributes": [],
              "isSelectable": true
            },
            {
              "seatNumber": "2D",
              "column": "D",
              "type": "Suite",
              "position": "Middle",
              "attributes": [],
              "isSelectable": true
            },
            {
              "seatNumber": "2G",
              "column": "G",
              "type": "Suite",
              "position": "Middle",
              "attributes": [],
              "isSelectable": true
            },
            {
              "seatNumber": "2K",
              "column": "K",
              "type": "Suite",
              "position": "Window",
              "attributes": [],
              "isSelectable": true
            }
          ]
        }
      ]
    },
    {
      "cabinCode": "W",
      "cabinName": "Premium Economy",
      "deckLevel": "Main",
      "startRow": 11,
      "endRow": 18,
      "columns": ["A", "B", "C", "D", "E", "F", "G", "H", "K"],
      "layout": "3-3-3",
      "rows": [
        {
          "rowNumber": 11,
          "seats": [
            {
              "seatNumber": "11A",
              "column": "A",
              "type": "Standard",
              "position": "Window",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "11B",
              "column": "B",
              "type": "Standard",
              "position": "Middle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "11C",
              "column": "C",
              "type": "Standard",
              "position": "Aisle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "11D",
              "column": "D",
              "type": "Standard",
              "position": "Aisle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "11E",
              "column": "E",
              "type": "Standard",
              "position": "Middle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "11F",
              "column": "F",
              "type": "Standard",
              "position": "Aisle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "11G",
              "column": "G",
              "type": "Standard",
              "position": "Aisle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "11H",
              "column": "H",
              "type": "Standard",
              "position": "Middle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "11K",
              "column": "K",
              "type": "Standard",
              "position": "Window",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            }
          ]
        }
      ]
    },
    {
      "cabinCode": "Y",
      "cabinName": "Economy",
      "deckLevel": "Main",
      "startRow": 22,
      "endRow": 54,
      "columns": ["A", "B", "C", "D", "E", "F", "G", "H", "K"],
      "layout": "3-3-3",
      "rows": [
        {
          "rowNumber": 22,
          "seats": [
            {
              "seatNumber": "22A",
              "column": "A",
              "type": "Standard",
              "position": "Window",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "22B",
              "column": "B",
              "type": "Standard",
              "position": "Middle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "22C",
              "column": "C",
              "type": "Standard",
              "position": "Aisle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "22D",
              "column": "D",
              "type": "Standard",
              "position": "Aisle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "22E",
              "column": "E",
              "type": "Standard",
              "position": "Middle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "22F",
              "column": "F",
              "type": "Standard",
              "position": "Aisle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "22G",
              "column": "G",
              "type": "Standard",
              "position": "Aisle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "22H",
              "column": "H",
              "type": "Standard",
              "position": "Middle",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            },
            {
              "seatNumber": "22K",
              "column": "K",
              "type": "Standard",
              "position": "Window",
              "attributes": ["ExtraLegroom"],
              "isSelectable": true
            }
          ]
        }
      ]
    }
  ]
}
```

> **Note:** `isSelectable` reflects whether a seat is physically available for selection (i.e. not a structural no-fly zone, crew seat, or permanently blocked position). Real-time occupancy — whether a seat has been sold or held on a specific flight — is overlaid at query time from `offer.FlightInventory` and is never stored here.

-----

# Technical Considerations

- Microservices built in C# as Azure Functions (isolated)
- Databases will be built in Microsoft SQL. Ideally these would be individual, isolated, database instances, but for this project, we will use one database with key domains separated logically using the domain names and the schema.
- Front end websites, app and contact centre apps (including others) will be built using the latest version of Angular, hosted as Static Web Apps on Azure.
- **Aircraft type codes** are represented as a 4-character code consisting of the manufacturer prefix followed by a 3-digit variant number. The third digit encodes the specific variant. For example: A350-1000 → `A351`, A350-900 → `A359`, B787-900 → `B789`, B787-10 → `B781`. This convention is consistent with IATA SSIM aircraft designator standards and must be used uniformly across all services, databases, and API contracts.
- JSON columns (`OrderData`, `CabinLayout`) use SQL Server's native `NVARCHAR(MAX)` with `ISJSON` check constraints to enforce structural validity. Where query performance requires filtering or sorting on JSON properties, SQL Server computed columns with JSON path expressions should be used to create targeted indexes.
- **StoredOffer expiry:** The `offer.StoredOffer` table includes an `ExpiresAt` column. The Order API must validate that an offer has not expired before consuming it. A background job should periodically purge or archive expired, unconsumed offers to keep the table lean.
- **Offer consumption:** Once an `OfferId` is successfully retrieved by the Order API during order creation, `IsConsumed` is set to `1` on the `StoredOffer` row to prevent the same offer being used on multiple orders.
- **Delivery DB:** The Delivery microservice owns its own `Delivery DB` schema (`delivery.*`). It no longer reads from or writes to `order.Order`. Order data required for manifest population (e-ticket numbers, passenger names, seat assignments) is passed explicitly by the Retail API orchestration layer at the point of booking confirmation and subsequent seat changes.
- **FlightManifest seatmap validation:** Before writing any row to `delivery.FlightManifest`, the Delivery microservice must validate the `SeatNumber` against the active seatmap for the relevant `AircraftType` by calling the Seat microservice. Any seat number not present on the seatmap must be rejected. This validation applies to both initial writes (booking confirmation) and updates (post-purchase seat changes).

# Security Principles

All services and data stores in this system must adhere to the following baseline security standards. These apply across all environments (development, staging, production) unless explicitly noted.

## Transport Security

All communication between clients and the platform, and between internal services, must use TLS 1.2 or higher. Unencrypted HTTP must not be accepted on any endpoint in any environment. API Gateway and orchestration layer endpoints must enforce HTTPS and reject downgrade attempts. Internal service-to-service communication within the Azure private network must also use TLS.

## Encryption at Rest

All databases and storage accounts must have encryption at rest enabled using platform-managed keys as a minimum, with customer-managed keys (CMK) via Azure Key Vault required for any store holding personally identifiable information (PII) or payment data. This includes all SQL databases in the shared schema, the Delivery DB, and any blob storage used for boarding card generation or document storage.

## Authentication and Authorisation

Customer-facing APIs (Retail API, Loyalty API) must authenticate travellers using OAuth 2.0 / OpenID Connect with short-lived access tokens. Booking retrieval endpoints (manage booking, check-in) require the traveller to supply booking reference, given name, and surname — these three factors together form the access credential for unauthenticated (guest) flows and must all be validated server-side before any order data is returned. Internal service-to-service calls (orchestration layer to microservices) must use managed identities or scoped API keys; no service should be reachable without authentication. Role-based access control (RBAC) must be applied to internal tooling and airport/contact centre apps.

## PII Handling

Passenger personal data (names, dates of birth, passport numbers, contact details) must be treated as PII and handled in accordance with UK GDPR and any applicable destination-country regulations. PII must not be logged in plain text in application logs or telemetry. Log entries referencing passengers should use anonymised identifiers (e.g. `PassengerId`, `BookingReference`) rather than names or document numbers. Data retention policies must be defined per domain and enforced via automated purge jobs.

## Payment Data

Card data must never be stored or logged by any service other than the Payment microservice and its downstream payment processor. The platform must achieve and maintain PCI DSS compliance for the payment flow. Only the last four digits of a card number and the card type may be stored on the order (as currently defined in `OrderData`). Full card numbers, CVV codes, and raw authorisation tokens must not persist beyond the payment transaction.

## Input Validation and API Hardening

All API inputs must be validated at the orchestration layer before being forwarded to microservices. SQL injection and injection attacks must be mitigated through parameterised queries — no dynamic SQL construction from user input. API rate limiting must be applied to all public-facing endpoints. CORS policies must be explicitly configured and restricted to known channel origins. API contracts must reject unexpected fields (strict schema validation) to prevent mass assignment vulnerabilities.

## Secrets Management

Connection strings, API keys, and credentials must not be stored in source code, configuration files, or environment variables in plain text. All secrets must be stored in Azure Key Vault and accessed at runtime via managed identity. Secret rotation must be supported without requiring redeployment.

## Audit Logging

All state-changing operations (order creation, payment, check-in, manifest writes, PAX updates) must produce an audit log entry including the actor, timestamp, and a summary of the change. Audit logs must be immutable and retained for a minimum of 7 years in line with airline regulatory requirements. Audit logs must be stored separately from application logs and must not be accessible to application-layer services for modification.

## Vulnerability and Dependency Management

All service dependencies (NuGet packages, npm packages) must be kept up to date and scanned for known vulnerabilities as part of the CI/CD pipeline. Critical and high-severity vulnerabilities must block deployment. Penetration testing must be conducted at least annually and after significant architectural changes.

---

# Airline Context — Apex Air

This document describes the reservation system for **Apex Air**, IATA carrier code **AX**. All examples, flight numbers, carrier codes, and loyalty references throughout this document use the `AX` designator.

Apex Air is a premium transatlantic and long-haul carrier operating a fleet of approximately 50 aircraft across three types:

- **Boeing 787-9** (`B789`) — primary long-haul workhorse, used on transatlantic and Asia-Pacific routes
- **Airbus A330-900** (`A339`) — medium-to-long-haul, used on Caribbean and secondary transatlantic routes
- **Airbus A350-1000** (`A351`) — flagship widebody, used on high-demand transatlantic and key Asia routes

Apex Air's network is focused on the following key markets:

- **North America** — major gateway cities including New York (JFK), Los Angeles (LAX), Miami (MIA), Chicago (ORD), and Boston (BOS)
- **Caribbean** — leisure and VFR routes to destinations including Barbados (BGI), Jamaica (KIN), and the Bahamas (NAS)
- **East Asia** — Hong Kong (HKG), Tokyo (NRT), Shanghai (PVG), and Beijing (PEK)
- **South-East Asia** — Singapore (SIN)
- **South Asia** — key Indian cities including Mumbai (BOM), Delhi (DEL), and Bangalore (BLR)

All flights operate from a single UK hub. Apex Air participates in the IATA ONE Order standard and operates a modern retailing architecture as described in this document.

---

# Glossary

- **APIS** — Advance Passenger Information System
- **BCBP** — Bar Coded Boarding Pass (IATA Resolution 792 standard for boarding pass barcode encoding)
- **CMK** — Customer-Managed Key
- **CORS** — Cross-Origin Resource Sharing
- **GDS** — Global Distribution System
- **IATA** — International Air Transport Association
- **IROPS** — Irregular Operations
- **NDC** — New Distribution Capability (IATA standard)
- **OLCI** — Online Check In
- **OTA** — Online Travel Agent
- **PAX** — Passenger
- **PCI DSS** — Payment Card Industry Data Security Standard
- **PII** — Personally Identifiable Information
- **PNR** — Passenger Name Record
- **RBAC** — Role-Based Access Control
- **TLS** — Transport Layer Security
- **UK GDPR** — United Kingdom General Data Protection Regulation
