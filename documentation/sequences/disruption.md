# Disruption — sequence diagrams

Covers IROPS (Irregular Operations) handling: admin-initiated flight cancellation with automatic rebooking, manual rebook of individual affected orders, and FOS (Flight Operations System) event processing. Flight time change is defined but not yet implemented.

---

## Admin — cancel flight and auto-rebook all passengers

The most complex disruption flow. Immediately closes inventory to prevent new bookings, retrieves the manifest to identify affected passengers, fetches replacement availability, then processes each booking in IROPS priority order (cabin class → loyalty tier → booking date).

```mermaid
sequenceDiagram
    participant Terminal as Admin UI
    participant OpsAPI as Operations API
    participant OfferMS as Offer MS
    participant DeliveryMS as Delivery MS
    participant OrderMS as Order MS

    Terminal->>OpsAPI: POST /v1/admin/disruption/cancel
    Note over Terminal,OpsAPI: {flightNumber, departureDate, reason?}

    OpsAPI->>OfferMS: PATCH /api/v1/inventory/cancel
    Note over OpsAPI,OfferMS: flightNumber, departureDate —<br/>immediately closes inventory,<br/>prevents new bookings on cancelled flight
    OfferMS-->>OpsAPI: Inventory closed

    OpsAPI->>OfferMS: GET /api/v1/flights/{flightNumber}/inventory?departureDate={date}
    Note over OpsAPI,OfferMS: Retrieve origin + destination for replacement search
    OfferMS-->>OpsAPI: FlightInventory (origin, destination)

    OpsAPI->>DeliveryMS: GET /api/v1/manifest?flightNumber={fn}&departureDate={date}
    Note over OpsAPI,DeliveryMS: Retrieve manifest for indexed orderId lookup
    DeliveryMS-->>OpsAPI: Manifest (entries[]: orderId, passengerId, eTicketNumber)

    OpsAPI->>OrderMS: POST /api/v1/orders/irops
    Note over OpsAPI,OrderMS: Batch fetch confirmed orders using<br/>orderIds from manifest (avoids full table scan)
    OrderMS-->>OpsAPI: AffectedOrders[]

    Note over OpsAPI: Sort by IROPS priority:<br/>cabin (F→J→W→Y), loyalty tier, booking date

    OpsAPI->>OfferMS: GET /api/v1/flights/availability?origin={o}&destination={d}&fromDate={date}&days=7
    Note over OpsAPI,OfferMS: Lightweight read — no fares, no stored offers
    OfferMS-->>OpsAPI: AvailabilityResponse (flights × cabins × seats)

    loop For each affected order (IROPS priority order)
        alt Replacement flight found with seats available in same cabin
            OpsAPI->>OfferMS: POST /api/v1/inventory/hold
            Note over OpsAPI,OfferMS: inventoryId, cabinCode,<br/>paxCount, orderId
            OfferMS-->>OpsAPI: Held

            OpsAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/rebook
            Note over OpsAPI,OrderMS: newFlightNumber, newDepartureDate,<br/>newInventoryId, reason=IropsRebooking
            OrderMS-->>OpsAPI: Order updated

            OpsAPI->>OfferMS: POST /api/v1/inventory/rebook
            Note over OpsAPI,OfferMS: Atomic swap — sell replacement,<br/>release original inventory
            OfferMS-->>OpsAPI: Inventory swapped

            OpsAPI->>DeliveryMS: GET /api/v1/tickets?bookingRef={bookingRef}
            DeliveryMS-->>OpsAPI: ExistingTickets[]

            OpsAPI->>DeliveryMS: POST /api/v1/tickets/reissue
            Note over OpsAPI,DeliveryMS: reason=IropsRebooking,<br/>existingTicketNumbers[], newSegments[]
            DeliveryMS-->>OpsAPI: NewTickets issued

            OpsAPI->>DeliveryMS: PATCH /api/v1/manifest/{bookingRef}/flight/{oldFlight}/{oldDate}
            Note over OpsAPI,DeliveryMS: Update manifest to replacement flight
            DeliveryMS-->>OpsAPI: Manifest updated

        else No replacement available
            Note over OpsAPI: Record outcome as NoFlightAvailable,<br/>manual handling required
        end
    end

    OpsAPI-->>Terminal: AdminDisruptionCancelResponse
    Note over OpsAPI,Terminal: {flightNumber, affectedPassengerCount,<br/>rebookedCount, failedCount,<br/>outcomes[]: {bookingRef, status,<br/>newFlightNumber?, noFlightReason?}}
```

---

## Admin — get affected orders for disrupted flight

Returns all confirmed orders on a flight in IROPS priority order (cabin class → loyalty tier → booking date). Runs two calls in parallel.

```mermaid
sequenceDiagram
    participant Terminal as Admin UI
    participant OpsAPI as Operations API
    participant OfferMS as Offer MS
    participant OrderMS as Order MS

    Terminal->>OpsAPI: GET /v1/admin/disruption/orders?flightNumber={fn}&departureDate={date}

    par
        OpsAPI->>OfferMS: GET /api/v1/flights/{flightNumber}/inventory?departureDate={date}
        OfferMS-->>OpsAPI: FlightInventory (origin, destination, status)
    and
        OpsAPI->>OrderMS: GET /api/v1/orders?flightNumber={fn}&departureDate={date}&status=Confirmed
        OrderMS-->>OpsAPI: ConfirmedOrders[]
    end

    Note over OpsAPI: Sort by IROPS priority:<br/>cabin (F→J→W→Y), loyalty tier, booking date

    OpsAPI-->>Terminal: AdminDisruptionOrdersResponse
    Note over OpsAPI,Terminal: orders[]: {bookingReference, passengers,<br/>cabinCode, loyaltyTier, bookingDate}
```

---

## Admin — manual rebook of a single order

Allows a staff member to rebook one specific booking onto a chosen replacement flight. Fetches replacement availability, holds a seat, updates the order, performs an atomic inventory swap, reissues tickets, and updates the manifest.

```mermaid
sequenceDiagram
    participant Terminal as Admin UI
    participant OpsAPI as Operations API
    participant OfferMS as Offer MS
    participant OrderMS as Order MS
    participant DeliveryMS as Delivery MS

    Terminal->>OpsAPI: POST /v1/admin/disruption/rebook-order
    Note over Terminal,OpsAPI: {bookingReference,<br/>newFlightNumber, newDepartureDate, reason?}

    OpsAPI->>OfferMS: GET /api/v1/flights/{newFlightNumber}/inventory?departureDate={newDate}
    OfferMS-->>OpsAPI: FlightInventory (inventoryId, cabins)

    OpsAPI->>OrderMS: GET /api/v1/orders?flightNumber={origFlight}&departureDate={origDate}&status=Confirmed
    OrderMS-->>OpsAPI: AffectedOrders (including target bookingRef)

    OpsAPI->>OfferMS: GET /api/v1/flights/availability?origin={o}&destination={d}&fromDate={date}&days=7
    OfferMS-->>OpsAPI: AvailabilityResponse

    OpsAPI->>OfferMS: POST /api/v1/inventory/hold
    Note over OpsAPI,OfferMS: inventoryId, cabinCode, paxCount, orderId
    OfferMS-->>OpsAPI: Held

    OpsAPI->>OrderMS: PATCH /api/v1/orders/{bookingRef}/rebook
    Note over OpsAPI,OrderMS: reason=IropsRebooking,<br/>newFlightNumber, newDepartureDate
    OrderMS-->>OpsAPI: Updated

    OpsAPI->>OfferMS: POST /api/v1/inventory/rebook
    Note over OpsAPI,OfferMS: Atomic swap — sell replacement,<br/>release original inventory
    OfferMS-->>OpsAPI: Inventory swapped

    alt Hold failed for any leg
        OpsAPI->>OfferMS: POST /api/v1/inventory/release
        Note over OpsAPI,OfferMS: Release all held seats
    end

    OpsAPI->>DeliveryMS: GET /api/v1/tickets?bookingRef={bookingRef}
    DeliveryMS-->>OpsAPI: ExistingTickets[]

    OpsAPI->>DeliveryMS: POST /api/v1/tickets/reissue
    Note over OpsAPI,DeliveryMS: reason=IropsRebooking,<br/>existingTicketNumbers[], newSegments[]
    DeliveryMS-->>OpsAPI: New tickets issued

    OpsAPI->>DeliveryMS: PATCH /api/v1/manifest/{bookingRef}/flight/{origFlight}/{origDate}
    Note over OpsAPI,DeliveryMS: Update manifest to replacement flight
    DeliveryMS-->>OpsAPI: Manifest updated

    OpsAPI-->>Terminal: AdminDisruptionRebookOrderResponse
    Note over OpsAPI,Terminal: {bookingReference, status,<br/>newFlightNumber, newETicketNumbers}
```

---

## Admin — aircraft type change

Updates the aircraft type on all inventory for a given flight, used when equipment substitution is required.

```mermaid
sequenceDiagram
    participant Terminal as Admin UI
    participant OpsAPI as Operations API
    participant OfferMS as Offer MS

    Terminal->>OpsAPI: POST /v1/admin/disruption/change
    Note over Terminal,OpsAPI: {flightNumber, departureDate, newAircraftType}

    OpsAPI->>OfferMS: PATCH /api/v1/inventory/aircraft-type
    Note over OpsAPI,OfferMS: flightNumber, departureDate, newAircraftType
    OfferMS-->>OpsAPI: Updated (all inventory on flight updated)

    OpsAPI-->>Terminal: AdminDisruptionChangeResponse
```

---

## FOS — flight cancellation event (external system)

The FOS (Flight Operations System) triggers this endpoint. It delegates to the same `AdminDisruptionCancelHandler` used by staff.

```mermaid
sequenceDiagram
    participant FOS as Flight Ops System
    participant OpsAPI as Operations API

    FOS->>OpsAPI: POST /v1/disruptions/cancellation
    Note over FOS,OpsAPI: {flightNumber, scheduledDeparture,<br/>reason?, enableIropsRebooking}
    Note over OpsAPI: Delegates to AdminDisruptionCancelHandler<br/>(see cancel flight flow above)
    OpsAPI-->>FOS: DisruptionResponse
    Note over OpsAPI,FOS: {disruptionId, flightNumber,<br/>disruptionType=Cancellation,<br/>status, affectedBookings,<br/>rebookingsInitiated, processedAt}
```

---

## FOS — flight delay event (not yet implemented)

```mermaid
sequenceDiagram
    participant FOS as Flight Ops System
    participant OpsAPI as Operations API

    FOS->>OpsAPI: POST /v1/disruptions/delay
    Note over FOS,OpsAPI: {flightNumber, scheduledDeparture,<br/>delayMinutes, reason?}
    Note over OpsAPI: Handler not yet implemented —<br/>throws NotImplementedException
    OpsAPI-->>FOS: 500 Internal Server Error
```

---

## Admin — flight time change (not yet implemented)

```mermaid
sequenceDiagram
    participant Terminal as Admin UI
    participant OpsAPI as Operations API

    Terminal->>OpsAPI: POST /v1/admin/disruption/time
    Note over Terminal,OpsAPI: {flightNumber, departureDate,<br/>newDepartureTime, newArrivalTime, reason?}
    Note over OpsAPI: Handler not yet implemented —<br/>throws NotImplementedException
    OpsAPI-->>Terminal: 500 Internal Server Error
```
