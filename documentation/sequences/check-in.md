# Check-in — sequence diagrams

Covers the online check-in (OLCI) journey: retrieve booking, submit travel documents, select seats, complete check-in with Timatic validation, and retrieve boarding passes.

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
    Note over Traveller,Web: Enters booking reference, first name,<br/>last name and departure airport code

    Web->>OpsAPI: POST /v1/oci/retrieve
    Note over Web,OpsAPI: bookingReference, firstName, lastName,<br/>departureAirport, loyaltyNumber (optional)

    OpsAPI->>OrderMS: POST /v1/orders/retrieve
    Note over OpsAPI,OrderMS: bookingReference, lastName
    OrderMS-->>OpsAPI: Order (passengers, eTickets, segments, bookingType)

    opt Logged in with loyalty number
        OpsAPI->>CustomerMS: GET /v1/customers/{loyaltyNumber}
        Note over OpsAPI,CustomerMS: Pre-fill passport data from loyalty profile
        CustomerMS-->>OpsAPI: CustomerProfile (passportNumber, nationality, etc.)
    end

    Note over OpsAPI: Build OciRetrieveResult<br/>Map passengerId to eTicketNumber<br/>Pre-fill travel docs if available

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
    Note over Web,OpsAPI: bookingReference, departureAirport<br/>passengers [{ticketNumber,<br/>travelDocument{type, number,<br/>issuingCountry, nationality,<br/>issueDate, expiryDate}}]

    Note over OpsAPI: Validate ticket number format (NNN-NNNNNNNNNN)<br/>Validate passport not expired

    OpsAPI->>OrderMS: POST /v1/orders/retrieve
    Note over OpsAPI,OrderMS: bookingReference, lastName
    OrderMS-->>OpsAPI: Order details

    OpsAPI->>OpsAPI: Update travel docs on order

    OpsAPI->>OrderMS: POST /v1/orders
    Note over OpsAPI,OrderMS: Save updated order with APIS documents per passenger
    OrderMS-->>OpsAPI: Updated order

    OpsAPI-->>Web: bookingReference, success true
```

---

## Seatmap retrieval during check-in

Check-in seatmap retrieval uses the same endpoint as the booking flow.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant SeatMS as Seat MS

    Web->>RetailAPI: GET /v1/flights/{flightId}/seatmap
    RetailAPI->>SeatMS: GET /v1/seatmap/{aircraftType}
    SeatMS-->>RetailAPI: SeatmapResponse
    RetailAPI-->>Web: SeatmapResponse
```

---

## OCI seat selection (stub)

OCI seat selection is not yet implemented — the endpoint accepts the request and returns success without calling any downstream services.

```mermaid
sequenceDiagram
    participant Web
    participant OpsAPI as Operations API

    Web->>OpsAPI: POST /v1/oci/seats
    Note over Web,OpsAPI: bookingReference, departureAirport

    Note over OpsAPI: Seat selection not implemented<br/>Returns success immediately

    OpsAPI-->>Web: bookingReference, success true
```

---

## OCI bag selection (stub)

OCI bag selection is not yet implemented — the endpoint accepts the request and returns success without calling any downstream services.

```mermaid
sequenceDiagram
    participant Web
    participant OpsAPI as Operations API

    Web->>OpsAPI: POST /v1/oci/bags
    Note over Web,OpsAPI: bookingReference, departureAirport

    Note over OpsAPI: Bag selection not implemented<br/>Returns success immediately

    OpsAPI-->>Web: bookingReference, success true
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

## Complete check-in

Timatic validation runs inside the Delivery microservice before any coupon status is updated. Both `documentcheck` and `apischeck` run per passenger in Phase 1. A failure from either check rejects the entire check-in — no passenger is checked in.

```mermaid
sequenceDiagram
    participant Web
    participant OpsAPI as Operations API
    participant OrderMS as Order MS
    participant DeliveryMS as Delivery MS
    participant Timatic as Timatic Simulator

    Web->>OpsAPI: POST /v1/oci/checkin
    Note over Web,OpsAPI: bookingReference, departureAirport

    OpsAPI->>OrderMS: POST /v1/orders/retrieve
    Note over OpsAPI,OrderMS: Retrieve order to resolve ticket numbers<br/>Verify all passengers have travel documents
    OrderMS-->>OpsAPI: Order (passengers with travel docs)

    OpsAPI->>DeliveryMS: POST /v1/oci/checkin
    Note over OpsAPI,DeliveryMS: departureAirport and array of tickets<br/>with passengerId, givenName, surname

    loop Phase 1 — Timatic validation per passenger
        DeliveryMS->>Timatic: POST /autocheck/v1/documentcheck
        Note over DeliveryMS,Timatic: documentType, nationality, documentNumber,<br/>documentExpiryDate, itinerary (origin and destination)
        Timatic-->>DeliveryMS: status OK or FAILED

        DeliveryMS->>Timatic: POST /autocheck/v1/apischeck
        Note over DeliveryMS,Timatic: flightNumber, departureDate,<br/>departureAirport, arrivalAirport, paxInfo
        Timatic-->>DeliveryMS: apisStatus ACCEPTED or REJECTED
    end

    alt Any Timatic check failed
        DeliveryMS-->>OpsAPI: 422 — document or APIS failure details
        OpsAPI-->>Web: 422 Unprocessable Entity
    else All passengers passed Phase 1
        Note over DeliveryMS: Phase 2 — update coupon status
        DeliveryMS->>DeliveryMS: Update coupon status to C (checked in)<br/>on each ticket in delivery.Ticket
        DeliveryMS->>DeliveryMS: Auto-assign seat from correct cabin<br/>for any coupon with no seat recorded<br/>Passengers on same booking grouped<br/>in adjacent columns where possible
        DeliveryMS-->>OpsAPI: Array of checked-in ticket numbers
        OpsAPI-->>Web: Array of checked-in ticket numbers
    end
```

---

## Retrieve boarding passes

```mermaid
sequenceDiagram
    participant Web
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS

    Web->>OpsAPI: POST /v1/oci/boarding-docs
    Note over Web,OpsAPI: departureAirport<br/>ticketNumbers[]

    OpsAPI->>DeliveryMS: POST /v1/oci/boarding-docs
    Note over OpsAPI,DeliveryMS: departureAirport, ticketNumbers[]
    DeliveryMS->>DeliveryMS: Retrieve ticket from delivery.Ticket<br/>Filter to segments checked in for airport code<br/>Generate BCBP string per segment
    DeliveryMS-->>OpsAPI: Array of boarding cards

    OpsAPI-->>Web: BoardingCardsResponse
    Note over OpsAPI,Web: boardingCards[]: ticketNumber,<br/>flightNumber, seatNumber, cabinCode,<br/>sequenceNumber, origin, destination,<br/>bcbpString (IATA BCBP barcode)

    Web->>Web: Render boarding cards
```
