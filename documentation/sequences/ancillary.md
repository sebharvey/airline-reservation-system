# Ancillary — sequence diagrams

Covers retrieval of ancillary catalogues used during the booking flow: seatmaps, bag offers, and ancillary products. All calls are read-only lookups from the web frontend through the Retail Orchestration API.

---

## Seatmap retrieval

Four calls run in parallel: cabin layout and seat pricing from the Seat MS, flight details from the Offer MS, and live seat occupancy from the Delivery MS manifest (the manifest is the authoritative source of truth for occupied seats).

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
        OfferMS-->>RetailAPI: FlightInventory (aircraftType, flightNumber, departureDate)
    and Fetch live occupancy
        RetailAPI->>DeliveryMS: GET /api/v1/manifest?flightNumber={fn}&departureDate={date}
        DeliveryMS-->>RetailAPI: Manifest entries (occupied seat numbers)
    end

    Note over RetailAPI: Merge layout, pricing, and occupancy<br/>Manifest seats → availability=held<br/>Priced seats not in manifest → availability=available<br/>Seats with no offer → availability=sold

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

---

## SSR options retrieval

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OrderMS as Order MS

    Web->>RetailAPI: GET /v1/ssr/options
    Note over Web,RetailAPI: Optional filters: cabinCode,<br/>flightNumbers (comma-separated)
    RetailAPI->>OrderMS: GET /api/v1/ssr/options
    OrderMS-->>RetailAPI: SsrOptionsResponse
    RetailAPI-->>Web: GetSsrOptionsResponse
    Note over RetailAPI,Web: ssrOptions[] grouped by category<br/>(meals, assistance, equipment, etc.)
```
