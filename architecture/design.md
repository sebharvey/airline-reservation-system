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
    DELIVERY --> ORDER_DB
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
    - Uses Order DB
  - Customer
    - Customer DB
  - Accounting (orders and changes should be evented to this microservice from Order and Servicing microservices)
    - Accounting DB
  - Seat (manages seatmap definitions per aircraft type; provides seatmap views only — seat selection and inventory remain with Offer)
    - Seat DB


# Capability

## Offer

```mermaid

sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant OfferMS as Offer [MS]
    participant OrderMS as Order [MS]

    Traveller->>Web: Search for return flights (origin, destination, dates, pax)

    Web->>RetailAPI: POST /search (origin, destination, outbound date, inbound date, pax)

    RetailAPI->>OfferMS: Search availability (outbound)
    OfferMS-->>RetailAPI: Outbound flight options + fares

    RetailAPI->>OfferMS: Search availability (inbound)
    OfferMS-->>RetailAPI: Inbound flight options + fares

    RetailAPI-->>Web: Return combined outbound + inbound options

    Traveller->>Web: Select outbound flight + fare
    Traveller->>Web: Select inbound flight + fare

    Web->>RetailAPI: POST /basket (selected outbound + inbound flights, fares, pax details)

    RetailAPI->>OrderMS: Create basket (outbound, inbound, fares, pax)
    OrderMS-->>RetailAPI: Basket ID + basket summary

    RetailAPI-->>Web: Basket confirmed (Basket ID, itinerary, total price)

    Web-->>Traveller: Display basket summary — ready to proceed to booking

```

## Order

### Create

```mermaid

sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order [MS]
    participant PaymentMS as Payment [MS]
    participant OfferMS as Offer [MS]
    participant DeliveryMS as Delivery [MS]
    participant AccountingMS as Accounting [MS]

    Traveller->>Web: Enter passenger details

    Web->>RetailAPI: POST /order (basket ID, passenger details)
    RetailAPI->>OrderMS: Create order (basket, passenger details)
    OrderMS-->>RetailAPI: Order created (draft order ID)
    RetailAPI-->>Web: Order summary (draft order ID, itinerary, total price)

    Traveller->>Web: Enter payment details and confirm booking

    Web->>RetailAPI: POST /order/{id}/pay (payment details)
    RetailAPI->>PaymentMS: Authorise card (amount, card details)
    PaymentMS-->>RetailAPI: Authorisation confirmed (auth token)

    Note over RetailAPI, OfferMS: Ticketing process begins

    RetailAPI->>OfferMS: Remove seats from inventory
    OfferMS-->>RetailAPI: Inventory updated

    RetailAPI->>DeliveryMS: Create e-tickets (order ID, passenger details, flights)
    DeliveryMS-->>RetailAPI: E-ticket numbers issued

    RetailAPI->>PaymentMS: Settle payment (auth token, amount)
    PaymentMS-->>RetailAPI: Payment settled

    RetailAPI->>OrderMS: Confirm order (e-ticket numbers, booking reference)
    OrderMS-->>RetailAPI: Order confirmed (6-digit booking reference)

    Note over OrderMS, AccountingMS: Async event
    OrderMS-)AccountingMS: OrderConfirmed event (booking reference, amount, e-tickets)

    RetailAPI-->>Web: Booking confirmed (booking reference, e-ticket numbers)
    Web-->>Traveller: Display booking confirmation

```

## Servicing

### Manage booking - update PAX details

```mermaid

sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant ServicingMS as Servicing [MS]
    participant DeliveryMS as Delivery [MS]

    Traveller->>Web: Navigate to manage booking (booking reference)

    Web->>RetailAPI: GET /order/{bookingRef}
    RetailAPI->>ServicingMS: Retrieve order (booking reference)
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

    Traveller->>Web: Navigate to manage booking (booking reference)

    Web->>RetailAPI: GET /order/{bookingRef}
    RetailAPI->>ServicingMS: Retrieve order (booking reference)
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

    RetailAPI-->>Web: Seat selection confirmed (booking reference, updated e-tickets)
    Web-->>Traveller: Display updated booking confirmation with seat assignments

    Note over ServicingMS, AccountingMS: Async event
    ServicingMS-)AccountingMS: OrderServiced event (booking reference, seat change details)

```

## Delivery

### Online Check In

```mermaid

sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant ServicingMS as Servicing [MS]
    participant OfferMS as Offer [MS]
    participant DeliveryMS as Delivery [MS]

    Traveller->>Web: Navigate to online check-in (booking reference, surname)

    Web->>RetailAPI: GET /order/{bookingRef}/checkin
    RetailAPI->>ServicingMS: Retrieve order and eligibility (booking reference)
    ServicingMS-->>RetailAPI: Order details (PAX list, flights, seat assignments, e-tickets)
    RetailAPI-->>Web: Display PAX list and pre-flight details

    Traveller->>Web: Confirm / update travel document details for each PAX

    Web->>RetailAPI: POST /order/{bookingRef}/checkin (PAX IDs, travel document details)

    RetailAPI->>ServicingMS: Check in all PAX (booking reference, travel document details)
    ServicingMS-->>RetailAPI: PAX checked in, APIS data recorded

    RetailAPI->>OfferMS: Update seat inventory status to checked-in (flight ID, seat numbers)
    OfferMS-->>RetailAPI: Inventory updated

    RetailAPI->>DeliveryMS: Generate boarding cards (booking reference, PAX list, seats, flights)
    DeliveryMS-->>RetailAPI: Boarding cards generated (one per PAX per flight)

    RetailAPI-->>Web: Check-in confirmed (boarding cards)
    Web-->>Traveller: Display and offer download of boarding cards

```

## Seat

### Retrieve Seatmap

The Seat microservice is the system of record for aircraft seatmap definitions, organised by aircraft type (e.g. A350, B787). It provides the physical layout, seat attributes (class, position, extra legroom, etc.) and cabin configuration. It does **not** manage seat availability or inventory — that remains the responsibility of the Offer microservice.

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

# Technical Considerations

- Microservices built in C# as Azure Functions (isolated)
- Databases will be built in Microsoft SQL. Ideally these would be individual, isolated, database instances, but for this project, we will use one database with key domains separated logically using the domain names and the schema.
- Front end websites, app and contact centre apps (including others) will be built using the latest version of Angular, hosted as Static Web Apps on Azure.

# Glossary

- PAX - passenger
- NDC - New distribution capability (IATA standard)
- OLCI - Online Check In
- IROPS - Irregular Operations
- APIS - Advance Passenger Information System
