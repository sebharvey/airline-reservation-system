# Online check-in

Online check-in (OLCI) opens 24 hours before departure, allowing passengers to submit **Advance Passenger Information (API)** data and generate boarding passes.

- Completing OLCI moves each passenger to `checkedIn` status on the flight manifest, enabling boarding pass generation.
- The 24-hour window aligns with APIS submission cut-off times and prevents stale travel document data.
- Seat assignment (free of charge at OLCI) and bag additions are both available within the OLCI flow.

```mermaid
sequenceDiagram

    actor Traveller
    participant Web as Web (Angular)
    participant RetailApi as Retail API
    participant OrderMS as Order [MS]
    participant DeliveryMS as Delivery [MS]
    participant CustomerMS as Customer [MS]

    Traveller ->> Web: Opens online check-in journey <br /> Enters booking reference, given name and surname

    Note over Traveller, CustomerMS: Retrieve booking

    Web ->> RetailApi: POST /v1/checkin/retrieve <br /> bookingReference, givenName, surname
    RetailApi ->> OrderMS: POST /v1/orders/oci/retrieve <br /> bookingReference, surname
    OrderMS ->> OrderMS: Look up order from order.Order <br /> Validate surname against a PAX on the order
    OrderMS -->> RetailApi: Order details including PAX and ticket numbers

    opt IsLoggedIn

        RetailApi ->> CustomerMS: GET /v1/customers/{loyaltyNumber}
        CustomerMS -->> RetailApi: Customer profile data

        RetailApi ->> RetailApi: If lead PAX loyalty number matches profile <br /> Pre-fill passport information on the check-in form

    end

    RetailApi -->> Web: Order details with checkInEligible flag <br /> and per-PAX checkInStatus; passport pre-filled if logged in
    Web ->> Web: Display PAX list (pre-fill passport details if supplied)

    Traveller ->> Web: Reviews and completes passport/travel document per PAX

    Note over Traveller, CustomerMS: Seat selection

    Traveller ->> Web: Selects seats

    Web ->> RetailApi: PATCH /v1/checkin/{bookingRef}/seats <br /> seatSelections array (seatOfferId, passengerRef, inventoryId)
    RetailApi -->> Web: { "bookingReference": "AB1234", "updated": true }

    Note over Traveller, CustomerMS: Baggage selection

    Traveller ->> Web: Clicks to continue to baggage selection

    Web ->> RetailApi: POST /v1/checkin/{bookingRef}/bags <br /> Booking reference, departure airport code
    Note over RetailApi: Not implemented at this time
    RetailApi -->> Web: Return Success

    Note over Traveller, CustomerMS: Hazardous materials confirmation

    Traveller ->> Web: Clicks to continue to hazardous materials confirmation
    Web ->> Web: Display hazardous materials page

    Traveller ->> Web: Confirms and submits check-in

    Note over Traveller, CustomerMS: Submit check-in and generate boarding cards

    Web ->> RetailApi: POST /v1/checkin/{bookingRef} <br /> passengers array with passengerId and travelDocument per PAX
    RetailApi ->> OrderMS: POST /v1/orders/{bookingRef}/checkin <br /> checkins array with passengerId, travelDocument, segmentIds
    OrderMS ->> OrderMS: Write APIS data to OrderData <br /> Update passenger check-in status
    OrderMS -->> RetailApi: { "bookingReference": "AB1234", "checkedInPassengers": 1 }

    RetailApi ->> DeliveryMS: PATCH /v1/manifest/{bookingRef} <br /> updates array: inventoryId, passengerId, checkedIn=true, checkedInAt
    DeliveryMS ->> DeliveryMS: Set CheckedIn = 1, stamp CheckedInAt <br /> on delivery.Manifest rows
    DeliveryMS -->> RetailApi: { "updated": 1 }

    Note over Traveller, CustomerMS: Boarding card generation

    RetailApi ->> DeliveryMS: POST /v1/boarding-cards <br /> bookingReference, passengers with passengerId and inventoryIds
    DeliveryMS ->> DeliveryMS: Read delivery.Manifest rows for checked-in PAX <br /> Assemble IATA Resolution 792 BCBP string per segment
    DeliveryMS -->> RetailApi: boardingCards array with bcbpString per PAX per flight

    RetailApi -->> Web: boardingCards array with BCBP strings

    Web ->> Web: Render boarding cards
```

## APIs and microservices

The following APIs and microservices are involved in the online check-in flow.

### Retail API

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/checkin/retrieve` | Retrieve booking for check-in by booking reference and lead PAX name; validates the 24-hour OLCI window; optionally pre-fills passport data from loyalty profile |
| `PATCH` | `/v1/checkin/{bookingRef}/seats` | Update seat assignment during check-in (no charge at OLCI) |
| `POST` | `/v1/checkin/{bookingRef}` | Submit check-in for all passengers: record APIS data, update manifest, and generate boarding cards |
| `POST` | `/v1/checkin/{bookingRef}/bags` | Submit baggage selection for the booking (not implemented) |

#### POST /v1/checkin/retrieve

```json
// Request
{
  "bookingReference": "AB1234",
  "givenName": "Alex",
  "surname": "Taylor"
}
```

```json
// Response 200 OK — order detail plus check-in eligibility
{
  "checkInEligible": true,
  "passengers": [
    {
      "passengerId": "PAX-1",
      "checkInStatus": "NotCheckedIn"
    }
  ],
  "orderId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "bookingReference": "AB1234",
  "orderStatus": "Confirmed",
  "orderData": { }
}
```

#### PATCH /v1/checkin/{bookingRef}/seats

```json
// Request
{
  "seatSelections": [
    {
      "seatOfferId": "so-3fa85f64-5A-v1",
      "passengerRef": "PAX-1",
      "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ]
}
```

```json
// Response 200 OK
{
  "bookingReference": "AB1234",
  "updated": true
}
```

#### POST /v1/checkin/{bookingRef}

```json
// Request
{
  "passengers": [
    {
      "passengerId": "PAX-1",
      "travelDocument": {
        "type": "PASSPORT",
        "number": "PA1234567",
        "issuingCountry": "GBR",
        "expiryDate": "2030-01-01",
        "nationality": "GBR",
        "dateOfBirth": "1985-03-12",
        "gender": "Male",
        "residenceCountry": "GBR"
      }
    }
  ]
}
```

```json
// Response 200 OK
{
  "bookingReference": "AB1234",
  "boardingCards": [
    {
      "passengerId": "PAX-1",
      "flightNumber": "AX001",
      "departureDate": "2026-08-15",
      "seatNumber": "1A",
      "cabinCode": "J",
      "sequenceNumber": "0001",
      "bcbpString": "M1TAYLOR/ALEX        EAB1234 LHRJFKAX 0001 228J001A0001 156>518 W6042 AX 2A00000012345678 JAX7KLP2NZR901A",
      "passengerName": "TAYLOR/ALEX",
      "origin": "LHR",
      "destination": "JFK",
      "eTicketNumber": "932-1234567890"
    }
  ]
}
```

---

### Order microservice

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/v1/orders/oci/retrieve` | Retrieve an existing order by booking reference and surname for the OLCI flow; validates surname against a PAX on the order |
| `POST` | `/v1/orders/{bookingRef}/checkin` | Record check-in status and APIS travel document data for each passenger; writes APIS fields into `OrderData` |

#### POST /v1/orders/oci/retrieve

```json
// Request
{
  "bookingReference": "AB1234",
  "surname": "Taylor"
}
```

```json
// Response 200 OK
{
  "orderId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "bookingReference": "AB1234",
  "orderStatus": "Confirmed",
  "channelCode": "WEB",
  "currencyCode": "GBP",
  "totalAmount": 2950.00,
  "version": 3,
  "createdAt": "2026-08-01T10:00:00Z",
  "updatedAt": "2026-08-01T10:05:00Z",
  "orderData": { }
}
```

#### POST /v1/orders/{bookingRef}/checkin

```json
// Request
{
  "checkins": [
    {
      "passengerId": "PAX-1",
      "travelDocument": {
        "type": "PASSPORT",
        "number": "PA1234567",
        "issuingCountry": "GBR",
        "expiryDate": "2030-01-01",
        "nationality": "GBR",
        "dateOfBirth": "1985-03-12",
        "gender": "Male",
        "residenceCountry": "GBR"
      },
      "segmentIds": ["SEG-1"]
    }
  ]
}
```

```json
// Response 200 OK
{
  "bookingReference": "AB1234",
  "checkedInPassengers": 1
}
```

---

### Customer microservice

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/v1/customers/{loyaltyNumber}` | Retrieve a customer profile by loyalty number; used to pre-fill passport information when the traveller is logged in |

#### GET /v1/customers/{loyaltyNumber}

```json
// Response 200 OK
{
  "customerId": "c1a2b3d4-e5f6-7890-abcd-ef1234567890",
  "loyaltyNumber": "AX12345678",
  "givenName": "Alex",
  "surname": "Taylor",
  "dateOfBirth": "1985-03-12",
  "nationality": "GBR",
  "passportNumber": "PA1234567",
  "passportIssueDate": "2019-06-01",
  "passportIssuer": "GBR",
  "passportExpiryDate": "2030-01-01",
  "tierCode": "Silver",
  "pointsBalance": 12500,
  "isActive": true
}
```

---

### Delivery microservice

| Method | Path | Description |
|--------|------|-------------|
| `PATCH` | `/v1/manifest/{bookingRef}` | Update manifest entries for a booking; sets `CheckedIn = 1` and stamps `CheckedInAt` on each passenger's `delivery.Manifest` row |
| `POST` | `/v1/boarding-cards` | Generate boarding cards and BCBP barcode strings for all checked-in passengers; assembles the IATA Resolution 792 string from `delivery.Manifest` data |

#### PATCH /v1/manifest/{bookingRef}

```json
// Request
{
  "updates": [
    {
      "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "passengerId": "PAX-1",
      "checkedIn": true,
      "checkedInAt": "2026-08-14T09:30:00Z",
      "ssrCodes": ["VGML"],
      "version": 1
    }
  ]
}
```

```json
// Response 200 OK
{
  "updated": 1
}
```

#### POST /v1/boarding-cards

```json
// Request
{
  "bookingReference": "AB1234",
  "passengers": [
    {
      "passengerId": "PAX-1",
      "inventoryIds": ["3fa85f64-5717-4562-b3fc-2c963f66afa6"]
    }
  ]
}
```

```json
// Response 201 Created
{
  "boardingCards": [
    {
      "passengerId": "PAX-1",
      "flightNumber": "AX001",
      "departureDate": "2026-08-15",
      "seatNumber": "1A",
      "cabinCode": "J",
      "sequenceNumber": "0001",
      "bcbpString": "M1TAYLOR/ALEX        EAB1234 LHRJFKAX 0001 228J001A0001 156>518 W6042 AX 2A00000012345678 JAX7KLP2NZR901A",
      "passengerName": "TAYLOR/ALEX",
      "origin": "LHR",
      "destination": "JFK",
      "eTicketNumber": "932-1234567890"
    }
  ]
}
```

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
