# Check-in — sequence diagrams

Covers the online check-in (OCI) journey: retrieve booking, submit travel documents, select seats, complete check-in, and retrieve boarding passes.

---

## Retrieve booking for check-in

```mermaid
sequenceDiagram
    participant Web
    participant OpsAPI as Operations API
    participant OrderMS as Order MS
    participant CustomerMS as Customer MS

    Web->>OpsAPI: POST /v1/oci/retrieve
    Note over Web,OpsAPI: {bookingReference, firstName,<br/>lastName, departureAirport, loyaltyNumber?}

    OpsAPI->>OrderMS: POST /api/v1/orders/retrieve
    Note over OpsAPI,OrderMS: bookingReference, lastName
    OrderMS-->>OpsAPI: Order (passengers, eTickets, segments, bookingType)

    opt Loyalty number supplied
        OpsAPI->>CustomerMS: GET /api/v1/customers/{loyaltyNumber}
        Note over OpsAPI,CustomerMS: Pre-fill passport data from loyalty profile
        CustomerMS-->>OpsAPI: CustomerProfile (passportNumber, nationality, etc.)
    end

    Note over OpsAPI: Build OciRetrieveResult — map<br/>passengerId → eTicketNumber;<br/>pre-fill travel docs if available

    OpsAPI-->>Web: OciRetrieveResult
    Note over OpsAPI,Web: {bookingReference, checkInEligible,<br/>isStandby, passengers[{passengerId,<br/>ticketNumber, travelDocument?}]}
```

---

## Submit passenger travel documents (APIS)

```mermaid
sequenceDiagram
    participant Web
    participant OpsAPI as Operations API
    participant OrderMS as Order MS

    Web->>OpsAPI: POST /v1/oci/pax
    Note over Web,OpsAPI: {bookingReference, departureAirport,<br/>passengers: [{ticketNumber,<br/>travelDocument{type, number,<br/>issuingCountry, nationality,<br/>issueDate, expiryDate}}]}

    Note over OpsAPI: Validate ticket number format (NNN-NNNNNNNNNN)<br/>Validate passport not expired

    OpsAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
    Note over OpsAPI,OrderMS: Guard: verify all passengers<br/>on order have docs submitted
    OrderMS-->>OpsAPI: Order

    OpsAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/travel-docs
    Note over OpsAPI,OrderMS: Save APIS documents per passenger
    OrderMS-->>OpsAPI: 204 No Content

    OpsAPI-->>Web: {bookingReference, success: true}
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
    RetailAPI->>SeatMS: GET /api/v1/seatmap/{aircraftType}
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
    participant OrderMS as Order MS

    Web->>OpsAPI: POST /v1/oci/seats
    Note over Web,OpsAPI: {bookingReference, departureAirport}

    OpsAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
    Note over OpsAPI,OrderMS: Guard: verify travel documents present
    OrderMS-->>OpsAPI: Order

    Note over OpsAPI: Seat selection not implemented —<br/>returns success immediately
    OpsAPI-->>Web: {bookingReference, success: true}
```

---

## OCI bag selection (stub)

OCI bag selection is not yet implemented — the endpoint accepts the request and returns success without calling any downstream services.

```mermaid
sequenceDiagram
    participant Web
    participant OpsAPI as Operations API
    participant OrderMS as Order MS

    Web->>OpsAPI: POST /v1/oci/bags
    Note over Web,OpsAPI: {bookingReference, departureAirport}

    OpsAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
    Note over OpsAPI,OrderMS: Guard: verify travel documents present
    OrderMS-->>OpsAPI: Order

    Note over OpsAPI: Bag selection not implemented —<br/>returns success immediately
    OpsAPI-->>Web: {bookingReference, success: true}
```

---

## Complete check-in

```mermaid
sequenceDiagram
    participant Web
    participant OpsAPI as Operations API
    participant OrderMS as Order MS
    participant DeliveryMS as Delivery MS

    Web->>OpsAPI: POST /v1/oci/checkin
    Note over Web,OpsAPI: {bookingReference, departureAirport}

    OpsAPI->>OrderMS: GET /api/v1/orders/{bookingRef}
    Note over OpsAPI,OrderMS: Guard: verify all passengers<br/>have travel documents
    OrderMS-->>OpsAPI: Order (passengers with docs verified)

    OpsAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/checkin
    Note over OpsAPI,OrderMS: departureAirport
    OrderMS-->>OpsAPI: Check-in confirmed

    OpsAPI->>DeliveryMS: PATCH /api/v1/tickets/coupon-status
    Note over OpsAPI,DeliveryMS: Update coupon status to 'C'<br/>(checked in) at departure airport
    DeliveryMS-->>OpsAPI: Updated

    OpsAPI-->>Web: {bookingReference, checkedIn: true}
```

---

## Retrieve boarding passes

```mermaid
sequenceDiagram
    participant Web
    participant OpsAPI as Operations API
    participant DeliveryMS as Delivery MS

    Web->>OpsAPI: POST /v1/oci/boarding-docs
    Note over Web,OpsAPI: {departureAirport,<br/>ticketNumbers: ["932-..."]}

    OpsAPI->>DeliveryMS: GET /api/v1/boarding-docs
    Note over OpsAPI,DeliveryMS: departureAirport, ticketNumbers[]
    DeliveryMS-->>OpsAPI: BoardingDocsResponse

    OpsAPI-->>Web: BoardingCardsResponse
    Note over OpsAPI,Web: boardingCards[]: ticketNumber,<br/>flightNumber, seatNumber, cabinCode,<br/>sequenceNumber, origin, destination,<br/>bcbpString (IATA BCBP barcode)
```
