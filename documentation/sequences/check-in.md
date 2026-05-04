# Check-in — sequence diagrams

Covers the online check-in (OLCI) journey: retrieve booking, submit travel documents, complete check-in with watchlist and Timatic validation, and retrieve boarding passes. Also covers agent-assisted check-in with override capability.

---

## Retrieve booking for check-in

```mermaid
sequenceDiagram
    actor Traveller
    participant Web
    participant OpsAPI as Operations API
    participant OrderMS as Order MS
    participant CustomerMS as Customer MS

    Traveller->>Web: Opens check-in journey
    Note over Traveller,Web: Enters booking reference,<br/>last name and departure airport code

    Web->>OpsAPI: POST /v1/oci/retrieve
    Note over Web,OpsAPI: bookingReference, lastName,<br/>departureAirport, loyaltyNumber (optional)

    OpsAPI->>OrderMS: POST /api/v1/orders/retrieve
    Note over OpsAPI,OrderMS: bookingReference, lastName
    OrderMS-->>OpsAPI: Order (passengers, eTickets, segments, bookingType)

    opt Logged in with loyalty number
        OpsAPI->>CustomerMS: GET /api/v1/customers/{loyaltyNumber}
        Note over OpsAPI,CustomerMS: Pre-fill passport data from loyalty profile
        CustomerMS-->>OpsAPI: CustomerProfile (passportNumber, nationality, etc.)
    end

    Note over OpsAPI: Build OciRetrieveResult<br/>Map passengerId to eTicketNumber<br/>Pre-fill travel docs if loyalty profile supplied

    OpsAPI-->>Web: OciRetrieveResult
    Note over OpsAPI,Web: bookingReference, checkInEligible,<br/>passengers with ticketNumber and travelDocument

    Web->>Web: Display PAX details<br/>Pre-fill passport details if supplied
```

---

## Submit passenger travel documents (APIS)

```mermaid
sequenceDiagram
    actor Traveller
    participant Web
    participant OpsAPI as Operations API
    participant OrderMS as Order MS

    Traveller->>Web: Enters passport details per passenger

    Web->>OpsAPI: POST /v1/oci/pax
    Note over Web,OpsAPI: bookingReference, departureAirport,<br/>passengers [{ticketNumber,<br/>travelDocument{type, number,<br/>issuingCountry, nationality,<br/>issueDate, expiryDate}}]

    Note over OpsAPI: Validate ticket number format (NNN-NNNNNNNNNN)<br/>Validate passport not expired

    OpsAPI->>OrderMS: GET /api/v1/orders/{bookingReference}
    OrderMS-->>OpsAPI: Order details

    OpsAPI->>OrderMS: PATCH /api/v1/orders/{bookingReference}/passengers
    Note over OpsAPI,OrderMS: Updated travel document data per passenger
    OrderMS-->>OpsAPI: Updated order

    OpsAPI-->>Web: bookingReference, success true
```

---

## Seatmap retrieval during check-in

Check-in seatmap uses the same endpoint as the booking flow. Four calls run in parallel: cabin layout and seat pricing from Seat MS, flight details from Offer MS, and live occupancy from Delivery MS manifest.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant SeatMS as Seat MS
    participant OfferMS as Offer MS
    participant DeliveryMS as Delivery MS

    Web->>RetailAPI: GET /v1/flights/{flightId}/seatmap
    Note over Web,RetailAPI: flightId (inventoryId), cabinCode (optional filter)

    par Fetch cabin layout
        RetailAPI->>SeatMS: GET /api/v1/seatmap/{aircraftType}
        SeatMS-->>RetailAPI: Cabin layout (rows, columns, seat attributes)
    and Fetch seat pricing
        RetailAPI->>SeatMS: GET /api/v1/seat-offers?flightId={flightId}
        SeatMS-->>RetailAPI: Seat offers (seatNumber, price, tax per seat)
    and Fetch flight details
        RetailAPI->>OfferMS: GET /api/v1/flights/{flightId}
        OfferMS-->>RetailAPI: FlightInventory (aircraftType, flightNumber)
    and Fetch live occupancy
        RetailAPI->>DeliveryMS: GET /api/v1/manifest?flightNumber={fn}&departureDate={date}
        DeliveryMS-->>RetailAPI: Manifest (occupied seat numbers — source of truth)
    end

    Note over RetailAPI: Merge layout, pricing, occupancy<br/>Manifest seats → held, priced seats → available,<br/>no offer seats → sold

    RetailAPI-->>Web: SeatmapResponse
    Note over RetailAPI,Web: cabins[]: seats with seatOfferId,<br/>price, tax, availability (available/held/sold),<br/>position (Window/Aisle/Middle), attributes
```

---

## Hazardous materials confirmation

Passengers confirm they are not carrying prohibited hazardous materials. This is a UI-only step with no API call.

```mermaid
sequenceDiagram
    actor Traveller
    participant Web

    Traveller->>Web: Reviews hazardous materials restrictions
    Web->>Web: Display hazardous materials page
    Traveller->>Web: Confirms and continues to submit check-in
```

---

## Complete check-in (online — passenger self-service)

Timatic validation runs inside the Delivery MS. Both `documentcheck` and `apischeck` run per passenger. A watchlist check runs in the Operations API before the Timatic check. A failure from either check rejects the entire check-in. On success, the Order MS is updated with check-in status.

```mermaid
sequenceDiagram
    participant Web
    participant OpsAPI as Operations API
    participant OrderMS as Order MS
    participant DeliveryMS as Delivery MS
    participant Timatic as Timatic Simulator

    Web->>OpsAPI: POST /v1/oci/checkin
    Note over Web,OpsAPI: bookingReference, departureAirport

    OpsAPI->>OrderMS: GET /api/v1/orders/{bookingReference}
    Note over OpsAPI,OrderMS: Retrieve order to resolve ticket numbers<br/>Verify all passengers have travel documents
    OrderMS-->>OpsAPI: Order (passengers with travel docs, eTickets)

    Note over OpsAPI: Watchlist check
    OpsAPI->>OpsAPI: WatchlistService.CheckAsync(passengers)

    alt Watchlist match found
        Note over OpsAPI: Save watchlist block note to order<br/>Return blocked — no check-in proceeds
        OpsAPI-->>Web: 422 Watchlist block
    end

    OpsAPI->>OfferMS: GET /api/v1/flights/{inventoryId}
    Note over OpsAPI,OfferMS: Retrieve aircraft type for cabin config

    OpsAPI->>SeatMS: GET /api/v1/aircraft-types/{aircraftType}/cabin-configs
    Note over OpsAPI,SeatMS: Non-fatal — cabin configs used for seat auto-assign

    OpsAPI->>DeliveryMS: POST /api/v1/oci/checkin
    Note over OpsAPI,DeliveryMS: departureAirport, tickets[]{ticketNumber,<br/>passengerId, givenName, surname,<br/>travelDocument}, cabinConfigs

    Note over DeliveryMS: Timatic validation per passenger<br/>(documentcheck + apischeck)

    alt Any Timatic check failed
        DeliveryMS-->>OpsAPI: 422 — document or APIS failure details
        Note over OpsAPI: Save Timatic block note to order
        OpsAPI-->>Web: 422 Unprocessable Entity
    else All passengers passed
        DeliveryMS-->>OpsAPI: checkedInTicketNumbers[]

        OpsAPI->>OrderMS: PATCH /api/v1/orders/{bookingReference}/checkin
        Note over OpsAPI,OrderMS: departureAirport, checkedInAt,<br/>paxCheckIn per passenger
        OrderMS-->>OpsAPI: Updated

        OpsAPI-->>Web: checkedInTicketNumbers[]
    end
```

---

## Complete check-in (agent-assisted — with override capability)

Agent check-in supports watchlist override and Timatic bypass for exceptional cases (e.g., staff-verified documents). When a Timatic block occurs and `bypassTimatic=true` is set, the Delivery MS check-in is retried with the bypass flag.

```mermaid
sequenceDiagram
    participant Terminal as Admin UI / Terminal
    participant OpsAPI as Operations API
    participant OrderMS as Order MS
    participant OfferMS as Offer MS
    participant SeatMS as Seat MS
    participant DeliveryMS as Delivery MS

    Terminal->>OpsAPI: POST /v1/admin/checkin
    Note over Terminal,OpsAPI: bookingReference, departureAirport,<br/>overrideWatchlist?, bypassTimatic?

    OpsAPI->>OrderMS: GET /api/v1/orders/{bookingReference}
    OrderMS-->>OpsAPI: Order (passengers, travel docs, eTickets)

    OpsAPI->>OrderMS: PATCH /api/v1/orders/{bookingReference}/passengers
    Note over OpsAPI,OrderMS: Persist latest travel document data

    Note over OpsAPI: Watchlist check
    OpsAPI->>OpsAPI: WatchlistService.CheckAsync(passengers)

    alt Watchlist match and no override
        Note over OpsAPI: Save watchlist note to order
        OpsAPI-->>Terminal: 422 Watchlist block
    end

    OpsAPI->>OfferMS: GET /api/v1/flights/{inventoryId}
    OfferMS-->>OpsAPI: FlightInventory (aircraftType)

    OpsAPI->>SeatMS: GET /api/v1/aircraft-types/{aircraftType}/cabin-configs
    Note over OpsAPI,SeatMS: Non-fatal if fails

    OpsAPI->>DeliveryMS: POST /api/v1/oci/checkin
    Note over OpsAPI,DeliveryMS: departureAirport, tickets[], cabinConfigs

    alt Timatic block and bypassTimatic=true
        Note over OpsAPI: Save Timatic block note to order
        OpsAPI->>DeliveryMS: POST /api/v1/oci/checkin (bypassTimatic=true)
        DeliveryMS-->>OpsAPI: checkedInTicketNumbers[]
    else Timatic block and no bypass
        Note over OpsAPI: Save Timatic block note
        OpsAPI-->>Terminal: 422 Timatic block
    else All passed
        DeliveryMS-->>OpsAPI: checkedInTicketNumbers[]
    end

    OpsAPI->>DeliveryMS: POST /api/v1/oci/boarding-docs
    Note over OpsAPI,DeliveryMS: departureAirport, checkedInTicketNumbers
    DeliveryMS-->>OpsAPI: BoardingCards[]

    OpsAPI-->>Terminal: AdminCheckInResult (boardingCards, timaticNotes)
```

---

## Retrieve boarding passes

```mermaid
sequenceDiagram
    participant Web
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS

    Web->>OpsAPI: POST /v1/oci/boarding-docs
    Note over Web,OpsAPI: departureAirport, ticketNumbers[]

    OpsAPI->>DeliveryMS: POST /api/v1/oci/boarding-docs
    Note over OpsAPI,DeliveryMS: departureAirport, ticketNumbers[]
    DeliveryMS->>DeliveryMS: Retrieve tickets checked in for airport code<br/>Generate BCBP string per segment
    DeliveryMS-->>OpsAPI: Array of boarding cards

    OpsAPI-->>Web: BoardingCardsResponse
    Note over OpsAPI,Web: boardingCards[]: ticketNumber,<br/>flightNumber, seatNumber, cabinCode,<br/>sequenceNumber, origin, destination,<br/>bcbpString (IATA BCBP barcode)

    Web->>Web: Render boarding cards
```
