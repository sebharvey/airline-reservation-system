# Offer — sequence diagrams

Covers flight search flows: direct (slice) search, connecting flight search via LHR hub, and admin-facing search with private fares. Both paths originate from the Angular web frontend (or Terminal app for admin), route through the Retail Orchestration API, and delegate to the Offer microservice.

---

## Direct flight search

`SearchFlightsHandler` first attempts a direct search. If no direct results are found and neither endpoint is LHR, it falls back automatically to a connecting search via LHR (see below).

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OfferMS as Offer MS

    Web->>RetailAPI: POST /v1/search/slice
    Note over Web,RetailAPI: origin, destination, departureDate,<br/>passengers (ADT/CHD/INF), cabinCode

    RetailAPI->>OfferMS: POST /api/v1/search
    Note over RetailAPI,OfferMS: origin, destination, departureDate,<br/>paxCount, includePrivateFares=false
    OfferMS-->>RetailAPI: FlightSearchResponse (offers with fares, availability)

    RetailAPI-->>Web: SliceSearchResponse
    Note over RetailAPI,Web: itineraries[], each with offerId,<br/>price, taxes, cabins, availability
```

---

## Connecting flight search (via LHR hub)

When called explicitly at `/v1/search/connecting`, or triggered automatically by the slice handler when no direct service is found, a connecting search is performed. Leg 1 runs first; Leg 2 searches then run in parallel for each unique arrival date returned from Leg 1. A 60-minute minimum connection time (MCT) filter is applied before results are returned.

```mermaid
sequenceDiagram
    participant Web
    participant RetailAPI as Retail API
    participant OfferMS as Offer MS

    Web->>RetailAPI: POST /v1/search/connecting
    Note over Web,RetailAPI: origin, destination, departureDate,<br/>passengers, cabinCode

    RetailAPI->>OfferMS: POST /api/v1/search (origin → LHR)
    OfferMS-->>RetailAPI: Leg 1 offers (per unique arrival date)

    par Leg 2 searches — parallel per unique arrival date
        RetailAPI->>OfferMS: POST /api/v1/search (LHR → destination)
        OfferMS-->>RetailAPI: Leg 2 offers for date 1
    and
        RetailAPI->>OfferMS: POST /api/v1/search (LHR → destination)
        OfferMS-->>RetailAPI: Leg 2 offers for date 2
    end

    Note over RetailAPI: Apply 60-minute MCT validation -<br/>discard incompatible leg 1 / leg 2 pairings

    RetailAPI-->>Web: SliceSearchResponse
    Note over RetailAPI,Web: itineraries[] combining both legs,<br/>each with leg1OfferId, leg2OfferId,<br/>combinedPrice, cabins, availability
```

---

## Admin (staff) flight search

Staff search uses the same handler logic but sets `includePrivateFares=true` so that private fare tiers are included in results.

```mermaid
sequenceDiagram
    participant Terminal as Admin UI / Terminal
    participant RetailAPI as Retail API
    participant OfferMS as Offer MS

    Terminal->>RetailAPI: POST /v1/admin/search/slice
    Note over Terminal,RetailAPI: Same payload as /v1/search/slice

    RetailAPI->>OfferMS: POST /api/v1/search
    Note over RetailAPI,OfferMS: includePrivateFares=true
    OfferMS-->>RetailAPI: FlightSearchResponse (including private fares)

    RetailAPI-->>Terminal: SliceSearchResponse
```
