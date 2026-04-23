# Offer — sequence diagrams

Covers flight search flows: direct (slice) search and connecting flight search. Both paths originate from the Angular web frontend, route through the Retail Orchestration API, and delegate to the Offer microservice.

---

## Direct flight search

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OfferMS as Offer MS

    Web->>RetailAPI: POST /v1/search/slice
    Note over Web,RetailAPI: origin, destination, departureDate,<br/>passengers (ADT/CHD/INF), cabinCode
    RetailAPI->>OfferMS: POST /api/v1/search
    Note over RetailAPI,OfferMS: Translates request; queries inventory<br/>and returns priced offers
    OfferMS-->>RetailAPI: FlightSearchResponse (offers with fares, availability)
    RetailAPI-->>Web: SliceSearchResponse
    Note over RetailAPI,Web: itineraries[], each with offerId,<br/>price, taxes, cabins, availability
```

---

## Connecting flight search

Two independent searches are executed in parallel against the Offer MS — one for each leg of the connection — then combined with a 60-minute minimum connection time (MCT) validation before returning results.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OfferMS as Offer MS

    Web->>RetailAPI: POST /v1/search/connecting
    Note over Web,RetailAPI: origin, hubAirport, destination,<br/>departureDate, passengers, cabinCode

    par Leg 1 search
        RetailAPI->>OfferMS: POST /api/v1/search (origin → hub)
        OfferMS-->>RetailAPI: Leg 1 offers
    and Leg 2 search
        RetailAPI->>OfferMS: POST /api/v1/search (hub → destination)
        OfferMS-->>RetailAPI: Leg 2 offers
    end

    Note over RetailAPI: Apply 60-minute MCT validation;<br/>discard incompatible pairings
    RetailAPI-->>Web: ConnectingSearchResponse
    Note over RetailAPI,Web: itineraryPairs[], each with<br/>leg1OfferId, leg2OfferId, combinedPrice
```
