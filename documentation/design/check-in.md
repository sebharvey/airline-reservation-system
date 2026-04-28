# Check-in

There are two check-in paths. Both are owned by the **Operations API** — check-in is an airport operational capability, not a retail one. Seat and bag purchases made during the OLCI flow are an exception: those are retail transactions (payment, EMD issuance) and remain in the Retail API (`POST /v1/checkin/{bookingRef}/ancillaries`).

## Online check-in (OLCI)

Online check-in (OLCI) opens 24 hours before departure, allowing passengers to submit **Advance Passenger Information (API)** data and generate boarding passes.

- Completing OLCI moves each passenger to `checkedIn` status on the flight manifest, enabling boarding pass generation.
- The 24-hour window aligns with APIS submission cut-off times and prevents stale travel document data.
- Seat assignment (free of charge at OLCI) and bag additions are both available within the OLCI flow.

```mermaid
sequenceDiagram

    actor Traveller
    participant Web as Web (Angular)
    participant OperationsApi as Operations Api
    participant OrderMS as Order [MS]
    participant DeliveryMS as Delivery [MS]
    participant CustomerMS as Customer [MS]

    Traveller ->> Web: Opens online check in journey <br /> Enters lead PAX first name, last name and <br /> departure airport code (e.g. LHR)
 
    Note over Traveller, CustomerMS: Passport details

    Web ->> OperationsApi: POST /v1/oci/retrieve <br /> booking referece, lead PAX first name, last name, <br /> departure airport code (e.g. LHR)
    OperationsApi ->> OrderMS: POST /v1/orders/retrieve <br /> booking referece, lead PAX first name, last name <br /> [Existing endpoint]
    OrderMS ->> OrderMS: Look up order from order.Order
    OrderMS -->> OperationsApi: Array of PAX on booking and ticket numbers
    
    opt IsLoggedIn

        OperationsApi ->> CustomerMS: GET /v1/customers/{loyaltyNumber} <br /> [Existing endpoint]
        CustomerMS -->> OperationsApi: Customer profile data

        OperationsApi ->> OperationsApi: If lead PAX loyalty number matches profile <br /> Update PAX details array with passport <br /> information to pre-fill on the check in form

    end
    
    OperationsApi -->> Web: Return PAX details (and if appended, passport information)<br /> array on booking and ticket numbers
    Web ->> Web: Display PAX details (pre-fill passport details if suppled)
    
    Traveller ->> Web: Update passport information per PAX

    Web ->> OperationsApi: POST /v1/oci/pax <br /> Booking reference and <br />array of tickets for each PAX details with passport info
    OperationsApi ->> OperationsApi: Validate passport issue and expiry date
    OperationsApi ->> OrderMS: POST /v1/orders/retrieve <br /> booking referece, first name, last name <br /> [Existing endpoint]
    OrderMS -->> OperationsApi: Order details
    OperationsApi ->> OperationsApi: Update travel docs on order
    OperationsApi ->> OrderMS: POST /v1/orders <br /> Save order
    OrderMS -->> OperationsApi: Updated order

    OperationsApi -->> Web: Success / failure

    Note over Traveller, CustomerMS: Seat selection

    Traveller ->> Web: Clicks to continue to seat selection

    Web ->> OperationsApi: POST /v1/oci/seats <br /> Booking referece, departure airport code
    Note over OperationsApi: Not implemented at this time
    OperationsApi -->> Web: Return Success 

    Note over Traveller, CustomerMS: Baggage selection

    Traveller ->> Web: Clicks to continue to baggage selection

    Web ->> OperationsApi: POST /v1/oci/bags <br /> Booking referece, departure airport code
    Note over OperationsApi: Not implemented at this time
    OperationsApi -->> Web: Return Success 
 
    Note over Traveller, CustomerMS: Hazardous materials confirmation
    
    Traveller ->> Web: Clicks to continue to hazardous materials selection
    Web ->> Web: Display hazardous materials page

    Traveller ->> Web: Clicks to continue to submit check in information

    Web ->> OperationsApi: POST /v1/oci/checkin <br /> Booking reference and <br />array of tickets for each PAX details with passport info
    OperationsApi ->> OrderMS: POST /v1/orders/retrieve <br /> booking referece, lead pax first name, last name <br /> [Existing endpoint]
    OrderMS -->> OperationsApi: Order details

    OperationsApi ->> DeliveryMS: POST /v1/oci/checkin <br /> Departure airport code and array of ticket numbers successfully <br/> checked in with an object of the PAX details
    DeliveryMS ->> DeliveryMS: Update coupon status in ticket in DB (delivery.Ticket) on each ticket being checked in (status = C)
    DeliveryMS ->> DeliveryMS: For any coupon with no seat assigned, auto-assign a seat from the correct cabin. <br /> Passengers on the same booking are grouped so the allocator seats them in adjacent columns <br /> within the same row where possible. Seat assignment at OLCI is free of charge.
    DeliveryMS -->> OperationsApi: Array of checked in ticket nummbers

    OperationsApi -->> Web: Array of checked in ticket nummbers

    Note over Traveller, CustomerMS: Boarding pass generation

    Web ->> OperationsApi: POST /v1/oci/boarding-docs <br /> Ticket numbers and departure airport code

    OperationsApi ->> DeliveryMS: POST /v1/oci/boarding-docs <br /> Ticket numbers and departure airport code
    DeliveryMS ->> DeliveryMS: Retrieve ticket from DB (delivery.Ticket) <br /> and filter to the segments checked in for the airport code supplied <br /> and generate the BCBP string.
    DeliveryMS -->> OperationsApi: Array of boarding cards 

    OperationsApi -->> Web: Array of boarding cards with BCBP string.

    Web ->> Web: Render boarding cards
```

## APIs and microservices

The following APIs and microservices are involved in the online check-in flow.

### Operations API

| Method | Path | Description | Request | Response |
|--------|------|-------------|---------|----------|
| `POST` | `/v1/oci/retrieve` | Retrieve booking for check-in by booking reference, lead PAX name, and departure airport code; optionally pre-fills passport data from loyalty profile | `{`<br>`  "bookingReference": "AB1234",`<br>`  "firstName": "Alex",`<br>`  "lastName": "Taylor",`<br>`  "departureAirport": "LHR"`<br>`}` | `{`<br>`  "bookingReference": "AB1234",`<br>`  "checkInEligible": true,`<br>`  "passengers": [{`<br>`    "passengerId": "PAX-1",`<br>`    "ticketNumber": "932-1234567890",`<br>`    "givenName": "Alex",`<br>`    "surname": "Taylor",`<br>`    "passengerTypeCode": "ADT",`<br>`    "travelDocument": null`<br>`  }]`<br>`}` |
| `POST` | `/v1/oci/pax` | Submit or update passport and travel document details for each PAX on the booking; validates passport dates and persists to order | `{`<br>`  "bookingReference": "AB1234",`<br>`  "passengers": [{`<br>`    "ticketNumber": "932-1234567890",`<br>`    "travelDocument": {`<br>`      "type": "PASSPORT",`<br>`      "number": "PA1234567",`<br>`      "issuingCountry": "GBR",`<br>`      "nationality": "GBR",`<br>`      "issueDate": "2019-06-01",`<br>`      "expiryDate": "2030-01-01"`<br>`    }`<br>`  }]`<br>`}` | `{`<br>`  "bookingReference": "AB1234",`<br>`  "success": true`<br>`}` |
| `POST` | `/v1/oci/seats` | Submit seat selection for the booking (not implemented) | — | — |
| `POST` | `/v1/oci/bags` | Submit baggage selection for the booking (not implemented) | — | — |
| `POST` | `/v1/oci/checkin` | Complete check-in for all passengers on a booking; retrieves the order to resolve ticket numbers, calls the Delivery microservice to update each ticket coupon status to `C`, and returns the list of checked-in ticket numbers | `{`<br>`  "bookingReference": "AB1234",`<br>`  "departureAirport": "LHR"`<br>`}` | `{`<br>`  "bookingReference": "AB1234",`<br>`  "checkedIn": [`<br>`    "932-1234567890"`<br>`  ]`<br>`}` |
| `POST` | `/v1/oci/boarding-docs` | Request boarding documents for a set of checked-in ticket numbers and departure airport; proxies to the Delivery microservice and returns an array of boarding cards with BCBP strings | `{`<br>`  "departureAirport": "LHR",`<br>`  "ticketNumbers": [`<br>`    "932-1234567890"`<br>`  ]`<br>`}` | `{`<br>`  "boardingCards": [{`<br>`    "ticketNumber": "932-1234567890",`<br>`    "passengerId": "PAX-1",`<br>`    "flightNumber": "AX003",`<br>`    "departureDate": "2026-08-15",`<br>`    "seatNumber": "1A",`<br>`    "cabinCode": "J",`<br>`    "sequenceNumber": "0001",`<br>`    "origin": "LHR",`<br>`    "destination": "JFK",`<br>`    "bcbpString": "M1TAYLOR/ALEX..."`<br>`  }]`<br>`}` |

---

### Order microservice

| Method | Path | Description | Request | Response |
|--------|------|-------------|---------|----------|
| `POST` | `/v1/orders/retrieve` | [Existing endpoint] Retrieve an existing order by booking reference and surname; returns full order data | `{`<br>`  "bookingReference": "AB1234",`<br>`  "surname": "Taylor"`<br>`}` | `{`<br>`  "orderId": "a1b2c3d4-...",`<br>`  "bookingReference": "AB1234",`<br>`  "orderStatus": "Confirmed",`<br>`  "channelCode": "WEB",`<br>`  "currencyCode": "GBP",`<br>`  "ticketingTimeLimit": null,`<br>`  "totalAmount": 2950.00,`<br>`  "version": 3,`<br>`  "createdAt": "2026-08-01T10:00:00Z",`<br>`  "updatedAt": "2026-08-01T10:05:00Z",`<br>`  "orderData": { }`<br>`}` |
| `POST` | `/v1/orders` | Persist an updated order; used to save travel document changes back to the order record | Full order object with updated `orderData` | `{`<br>`  "orderId": "a1b2c3d4-...",`<br>`  "bookingReference": "AB1234",`<br>`  "version": 4`<br>`}` |

---

### Customer microservice

| Method | Path | Description | Request | Response |
|--------|------|-------------|---------|----------|
| `GET` | `/v1/customers/{loyaltyNumber}` | [Existing endpoint] Retrieve a customer profile by loyalty number; used to pre-fill passport information when the traveller is logged in | — | `{`<br>`  "customerId": "c1a2b3d4-...",`<br>`  "loyaltyNumber": "AX12345678",`<br>`  "givenName": "Alex",`<br>`  "surname": "Taylor",`<br>`  "dateOfBirth": "1985-03-12",`<br>`  "nationality": "GBR",`<br>`  "passportNumber": "PA1234567",`<br>`  "passportIssueDate": "2019-06-01",`<br>`  "passportIssuer": "GBR",`<br>`  "passportExpiryDate": "2030-01-01",`<br>`  "tierCode": "Silver",`<br>`  "pointsBalance": 12500,`<br>`  "isActive": true`<br>`}` |

---

### Delivery microservice

| Method | Path | Description | Request | Response |
|--------|------|-------------|---------|----------|
| `POST` | `/v1/oci/checkin` | Check in a set of tickets for a departure airport; updates coupon status to `C` on each ticket in `delivery.Ticket`; auto-assigns a seat from the correct cabin for any coupon that has no seat recorded — passengers on the same booking are grouped so the allocator seats them in adjacent columns within the same row where possible | `{`<br>`  "departureAirport": "LHR",`<br>`  "tickets": [{`<br>`    "ticketNumber": "932-1234567890",`<br>`    "passengerId": "PAX-1",`<br>`    "givenName": "Alex",`<br>`    "surname": "Taylor"`<br>`  }]`<br>`}` | `{`<br>`  "checkedIn": 1,`<br>`  "tickets": [{`<br>`    "ticketNumber": "932-1234567890",`<br>`    "status": "C"`<br>`  }]`<br>`}` |
| `POST` | `/v1/oci/boarding-docs` | Generate boarding documents (with BCBP) for a set of ticket numbers and departure airport; returns an array of boarding cards for the checked-in segments | `{`<br>`  "departureAirport": "LHR",`<br>`  "ticketNumbers": [`<br>`    "932-1234567890"`<br>`  ]`<br>`}` | `{`<br>`  "boardingCards": [{`<br>`    "ticketNumber": "932-1234567890",`<br>`    "passengerId": "PAX-1",`<br>`    "flightNumber": "AX003",`<br>`    "departureDate": "2026-08-15",`<br>`    "seatNumber": "1A",`<br>`    "cabinCode": "J",`<br>`    "sequenceNumber": "0001",`<br>`    "origin": "LHR",`<br>`    "destination": "JFK",`<br>`    "bcbpString": "M1TAYLOR/ALEX..."`<br>`  }]`<br>`}` |

## Boarding pass barcode string

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

The Delivery microservice is responsible for assembling this string at the point of boarding card generation, drawing on data from the `delivery.Manifest` row and the confirmed order. The barcode string is returned in the boarding card payload alongside human-readable fields; channels render it using their preferred barcode library (e.g. PDF417 for print, QR for mobile).

---

## Agent check-in

Agent check-in is performed by airport agents using the Terminal app. It is a single-step operation: the agent supplies travel documents for one or more passengers and the Operations API handles document persistence, Timatic validation, coupon update, and boarding card generation in one call.

Key differences from OLCI:
- **Single endpoint** — no multi-step form flow; all steps execute server-side in one request.
- **Timatic override** — agents can override a Timatic failure by setting `overrideTimatic: true` with a mandatory `overrideReason`; the override and all Timatic results are written as audit notes (`TIMATIC` / `TIMATIC_OVERRIDE`) on the order.
- **Lookup by booking reference or e-ticket** — the Terminal app looks up the order via the Retail API admin order endpoints (`GET /v1/admin/orders/{ref}` and `GET /v1/admin/orders/e-ticket/{number}`), then calls the Operations API for the check-in action itself.
- **Staff JWT required** — the `Admin` function prefix activates `TerminalAuthenticationMiddleware`.

```mermaid
sequenceDiagram

    actor Agent
    participant Terminal as Terminal (Angular)
    participant RetailApi as Retail API
    participant OperationsApi as Operations API
    participant OrderMS as Order [MS]
    participant DeliveryMS as Delivery [MS]

    Agent ->> Terminal: Enters booking reference or e-ticket number

    Terminal ->> RetailApi: GET /v1/admin/orders/{ref} (or /e-ticket/{number})
    RetailApi -->> Terminal: Full order detail (passengers, segments, e-tickets)

    Agent ->> Terminal: Selects departure airport and enters/confirms travel documents per PAX

    Terminal ->> OperationsApi: POST /v1/admin/checkin/{bookingRef} <br /> departureAirport, passengers[{ ticketNumber, travelDocument }]

    OperationsApi ->> OrderMS: GET /v1/orders/{bookingRef}
    OrderMS -->> OperationsApi: Order detail

    OperationsApi ->> OrderMS: PATCH /v1/orders/{bookingRef}/passengers <br /> Persist travel documents

    OperationsApi ->> DeliveryMS: POST /v1/oci/checkin <br /> Tickets with doc details; Timatic validation runs here

    alt Timatic pass
        DeliveryMS -->> OperationsApi: Checked-in ticket list + Timatic PASS notes
        OperationsApi ->> OrderMS: PATCH /v1/orders/{bookingRef}/notes <br /> Write OCI notes (type: OCI) with Timatic PASS results
    else Timatic fail, no override
        DeliveryMS -->> OperationsApi: 422 with timaticNotes
        OperationsApi ->> OrderMS: PATCH /v1/orders/{bookingRef}/notes <br /> Write OCI notes (type: OCI) with Timatic FAIL results
        OperationsApi -->> Terminal: 400 with timaticNotes — agent must review
    else Timatic fail, agent override
        OperationsApi ->> OrderMS: PATCH /v1/orders/{bookingRef}/notes <br /> Write TIMATIC_OVERRIDE audit note with reason
        OperationsApi ->> DeliveryMS: POST /v1/oci/checkin (bypassTimatic: true)
        DeliveryMS -->> OperationsApi: Checked-in ticket list
    end

    OperationsApi ->> DeliveryMS: POST /v1/oci/boarding-docs <br /> Checked-in ticket numbers + departure airport
    DeliveryMS -->> OperationsApi: Array of boarding cards with BCBP strings

    OperationsApi -->> Terminal: { bookingReference, timaticNotes, boardingCards }

    Terminal ->> Terminal: Render boarding card per passenger
```

### Operations API

| Method | Path | Description | Request | Response |
|--------|------|-------------|---------|----------|
| `POST` | `/v1/admin/checkin/{bookingRef}` | Agent check-in for one or more passengers; persists travel docs, runs Timatic, updates coupon status to `C`, returns boarding cards with BCBP strings; supports `overrideTimatic` with `overrideReason`; writes Timatic and override audit notes to the order; staff JWT required | `{`<br>`  "departureAirport": "LHR",`<br>`  "passengers": [{`<br>`    "ticketNumber": "932-1234567890",`<br>`    "travelDocument": {`<br>`      "type": "PASSPORT",`<br>`      "number": "PA1234567",`<br>`      "issuingCountry": "GBR",`<br>`      "nationality": "GBR",`<br>`      "issueDate": "2019-06-01",`<br>`      "expiryDate": "2030-01-01"`<br>`    }`<br>`  }],`<br>`  "overrideTimatic": false,`<br>`  "overrideReason": null`<br>`}` | `{`<br>`  "bookingReference": "AB1234",`<br>`  "timaticNotes": [{`<br>`    "checkType": "APIS",`<br>`    "ticketNumber": "932-1234567890",`<br>`    "status": "PASS",`<br>`    "detail": ""`<br>`  }],`<br>`  "boardingCards": [{`<br>`    "ticketNumber": "932-1234567890",`<br>`    "passengerId": "PAX-1",`<br>`    "flightNumber": "AX003",`<br>`    "departureDate": "2026-08-15",`<br>`    "seatNumber": "1A",`<br>`    "cabinCode": "J",`<br>`    "sequenceNumber": "0001",`<br>`    "origin": "LHR",`<br>`    "destination": "JFK",`<br>`    "bcbpString": "M1TAYLOR/ALEX..."`<br>`  }]`<br>`}` |

### Retail API (ancillaries only)

Seat and bag purchases during OLCI are a retail transaction. They remain in the Retail API.

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/checkin/{bookingRef}/ancillaries` | Purchase seat and/or bag ancillaries during online check-in; processes payment and issues EMDs |
