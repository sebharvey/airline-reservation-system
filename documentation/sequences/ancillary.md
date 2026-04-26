# Ancillary — sequence diagrams

Covers retrieval of ancillary catalogues used during the booking flow: seatmaps, bag offers, and ancillary products. All calls are read-only lookups from the web frontend through the Retail Orchestration API.

---

## Seatmap retrieval

Four calls run in parallel: two to the Seat MS for cabin layout and seat pricing, two to the Offer MS for held-seat availability and flight details. The Retail API merges the results into a single response.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant SeatMS as Seat MS
    participant OfferMS as Offer MS

    Web->>RetailAPI: GET /v1/flights/{flightId}/seatmap
    Note over Web,RetailAPI: aircraftType (required), cabinCode (optional filter)

    par Fetch layout
        RetailAPI->>SeatMS: GET /api/v1/seatmap/{aircraftType}
        SeatMS-->>RetailAPI: Cabin layout (rows, columns, seat attributes)
    and Fetch seat pricing
        RetailAPI->>SeatMS: GET /api/v1/seat-offers?flightId={flightId}&aircraftType={aircraftType}
        SeatMS-->>RetailAPI: Seat offers (seatNumber, price, tax per seat)
    and Fetch held seats
        RetailAPI->>OfferMS: GET /api/v1/inventory/{flightId}/holds
        OfferMS-->>RetailAPI: Held seat numbers (already booked)
    and Fetch flight details
        RetailAPI->>OfferMS: GET /api/v1/flights/{flightId}
        OfferMS-->>RetailAPI: Flight details (flightNumber, departureDate)
    end

    Note over RetailAPI: Merge layout and pricing<br/>Mark held seats as unavailable<br/>Generate SeatOfferId per available seat

    RetailAPI-->>Web: SeatmapResponse
    Note over RetailAPI,Web: cabins[]: seats with seatOfferId,<br/>price, tax, availability (available/held/sold),<br/>position (Window/Aisle/Middle), attributes
```

---

## Bag offers retrieval

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant BagMS as Bag MS

    Web->>RetailAPI: GET /v1/bags/offers?inventoryId={inventoryId}&cabinCode={cabinCode}
    Note over Web,RetailAPI: Both inventoryId and cabinCode are required
    RetailAPI->>BagMS: GET /api/v1/bags/offers?inventoryId={inventoryId}&cabinCode={cabinCode}
    BagMS-->>RetailAPI: BagOffersDto (policy and priced offers)
    RetailAPI-->>Web: BagOffersResponse
    Note over RetailAPI,Web: policy{freeBagsIncluded, maxWeightKgPerBag}<br/>additionalBagOffers[{bagOfferId, bagSequence,<br/>price, tax, currency, label}]
```

---

## Ancillary products retrieval

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant AncillaryMS as Ancillary MS

    Web->>RetailAPI: GET /v1/products?channel=WEB
    RetailAPI->>AncillaryMS: GET /api/v1/products
    AncillaryMS-->>RetailAPI: ProductsResponse
    RetailAPI-->>Web: ProductsResponse
    Note over RetailAPI,Web: products[] with productId,<br/>name, description, price, tax, currency
```
