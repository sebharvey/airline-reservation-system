# Online check-in

Online check-in (OLCI) opens 24 hours before departure, allowing passengers to submit **Advance Passenger Information (API)** data and generate boarding passes.

- Completing OLCI moves each passenger to `checkedIn` status on the flight manifest, enabling boarding pass generation.
- The 24-hour window aligns with APIS submission cut-off times and prevents stale travel document data.
- Seat assignment (free of charge at OLCI) and bag additions are both available within the OLCI flow.

```mermaid
sequenceDiagram
    actor Traveller
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order [MS]
    participant SeatMS as Seat [MS]
    participant BagMS as Bag [MS]
    participant OfferMS as Offer [MS]
    participant PaymentMS as Payment [MS]
    participant DeliveryMS as Delivery [MS]

    Traveller->>Web: Navigate to online check-in

    Web->>RetailAPI: POST /v1/checkin/retrieve (bookingReference, givenName, surname)
    RetailAPI->>OrderMS: POST /v1/orders/retrieve (bookingReference, givenName, surname)
    OrderMS-->>RetailAPI: 200 OK — order details (PAX list, flights, cabinCode, seat assignments, bag order items, e-tickets)
    RetailAPI-->>Web: 200 OK — PAX list, pre-flight details, existing ancillary summary

    opt Traveller has no seat assigned or wishes to change seat at check-in
        Note over Web, SeatMS: Seat selection at check-in is free of charge — no payment taken
        Web->>RetailAPI: GET /v1/flights/{flightId}/seatmap
        RetailAPI->>SeatMS: GET /v1/seatmap/{aircraftType}
        SeatMS-->>RetailAPI: 200 OK — seatmap layout (cabin configuration, seat positions, attributes)
        RetailAPI->>SeatMS: GET /v1/seat-offers?flightId={flightId}
        SeatMS-->>RetailAPI: 200 OK — priced seat offers (SeatOfferId, price, seat attributes — prices shown for info only at OLCI)
        RetailAPI->>OfferMS: GET /v1/flights/{flightId}/seat-availability
        OfferMS-->>RetailAPI: 200 OK — seat availability status per seat (available|held|sold)
        RetailAPI-->>Web: 200 OK — seat map with pricing for reference only (not charged at OLCI — merged by Retail API)
        Traveller->>Web: Select seat(s) for each PAX
        Web->>RetailAPI: PATCH /v1/checkin/{bookingRef}/seats (seatOfferIds per PAX)
        RetailAPI->>OfferMS: POST /v1/flights/{flightId}/seat-reservations (flightId, seatNumbers)
        OfferMS-->>RetailAPI: 200 OK — seats reserved
        RetailAPI->>OrderMS: PATCH /v1/orders/{bookingRef}/seats (PAX seat assignments)
        OrderMS-->>RetailAPI: 200 OK — order updated
    end

    opt Traveller wishes to add or purchase additional bags at check-in
        Note over Web, BagMS: Free allowance confirmed automatically- payment required for additional bags only
        RetailAPI->>BagMS: GET /v1/bags/offers?inventoryId={inventoryId}&cabinCode={cabinCode}
        BagMS-->>RetailAPI: 200 OK — bag policy (freeBagsIncluded, maxWeightKg) + bag offers for additional bags
        RetailAPI-->>Web: Free allowance and additional bag options
        Traveller->>Web: Select additional bag(s) if required and provide payment details
        Web->>RetailAPI: POST /v1/orders/{bookingRef}/bags (bagOfferIds per PAX per segment, paymentDetails)
        loop For each BagOfferId
            RetailAPI->>BagMS: GET /v1/bags/offers/{bagOfferId}
            BagMS-->>RetailAPI: 200 OK — validated (IsConsumed=0, unexpired, price locked)
        end
        RetailAPI->>PaymentMS: POST /v1/payment/authorise (amount, cardDetails, description=BagAncillary)
        PaymentMS-->>RetailAPI: 200 OK — paymentReference
        RetailAPI->>PaymentMS: POST /v1/payment/{paymentReference}/settle (settledAmount)
        PaymentMS-->>RetailAPI: 200 OK — bag payment settled
        RetailAPI->>OrderMS: PATCH /v1/orders/{bookingRef}/bags (bagOfferIds, passengerRefs, segmentRefs, paymentReference)
        OrderMS-->>RetailAPI: 200 OK — order updated with Bag order items
    end

    Traveller->>Web: Confirm / update travel document details for each PAX

    Web->>RetailAPI: POST /v1/checkin/{bookingRef} (PAX IDs, travel document details)

    RetailAPI->>OrderMS: POST /v1/orders/{bookingRef}/checkin (travel document details)
    OrderMS-->>RetailAPI: 200 OK — PAX checked in, APIS data recorded

    RetailAPI->>OfferMS: PATCH /v1/flights/{flightId}/seat-availability (flightId, seatNumbers, status=checked-in)
    OfferMS-->>RetailAPI: 200 OK — inventory updated

    RetailAPI->>DeliveryMS: PATCH /v1/manifest/{bookingRef} (PAX IDs, checkedIn=true, checkedInAt=now)
    DeliveryMS-->>RetailAPI: 200 OK — manifest entries updated

    RetailAPI->>DeliveryMS: POST /v1/boarding-cards (bookingReference, PAX list, seats, flights)
    DeliveryMS-->>RetailAPI: 201 Created — boarding cards (one per PAX per flight) including BCBP barcode string

    RetailAPI-->>Web: 200 OK — check-in confirmed (boarding cards)
    Web-->>Traveller: Display and offer download of boarding cards
```

*Ref: delivery - online check-in flow including seat selection, bag addition, and boarding card generation*

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
