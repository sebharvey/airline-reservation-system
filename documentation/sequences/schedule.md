# Schedule — sequence diagrams

Covers flight schedule management: SSIM file import, schedule-to-inventory import, and schedule retrieval. Also covers the operational flight status query used by the admin/operations layer.

---

## SSIM schedule import (admin)

```mermaid
sequenceDiagram
    participant Terminal as Admin UI
    participant OpsAPI as Operations API
    participant ScheduleMS as Schedule MS

    Terminal->>OpsAPI: POST /v1/schedules/ssim?scheduleGroupId={id}&createdBy={user}
    Note over Terminal,OpsAPI: Body: SSIM Chapter 7 plain-text file
    Note over OpsAPI: Parse SSIM file into structured schedule records<br/>(flightNumber, origin, destination,<br/>departureTime, daysOfWeek, validFrom/To, aircraftType)
    OpsAPI->>ScheduleMS: POST /api/v1/schedules
    Note over OpsAPI,ScheduleMS: Structured schedule payload,<br/>scheduleGroupId scopes the import
    ScheduleMS-->>OpsAPI: ImportResult (imported, deleted counts)
    OpsAPI-->>Terminal: ImportSsimResponse
    Note over OpsAPI,Terminal: {imported, deleted,<br/>scheduleGroupId, perScheduleSummary[]}
```

---

## Schedule retrieval (admin)

```mermaid
sequenceDiagram
    participant Terminal as Admin UI
    participant OpsAPI as Operations API
    participant ScheduleMS as Schedule MS

    Terminal->>OpsAPI: GET /v1/schedules?scheduleGroupId={id}
    OpsAPI->>ScheduleMS: GET /api/v1/schedules?scheduleGroupId={id}
    ScheduleMS-->>OpsAPI: ScheduleListResponse
    OpsAPI-->>Terminal: GetSchedulesResponse
    Note over OpsAPI,Terminal: count, schedules[]: {scheduleId,<br/>flightNumber, origin, destination,<br/>departureTime, daysOfWeek, aircraftType,<br/>validFrom, validTo, flightsCreated}
```

---

## Import schedules to inventory (admin)

Converts stored schedule records into offer inventory. Fetches aircraft configurations from the Seat MS and fare rules from the Offer MS, then batch-creates flight inventory and applies fare rules — all via the Offer MS.

```mermaid
sequenceDiagram
    participant Terminal as Admin UI
    participant OpsAPI as Operations API
    participant ScheduleMS as Schedule MS
    participant SeatMS as Seat MS
    participant OfferMS as Offer MS

    Terminal->>OpsAPI: POST /v1/schedules/import-inventory
    Note over Terminal,OpsAPI: {scheduleGroupId?, toDate?}

    OpsAPI->>ScheduleMS: GET /api/v1/schedules?scheduleGroupId={id}
    ScheduleMS-->>OpsAPI: Schedules (validFrom, validTo, daysOfWeek, aircraftType)

    OpsAPI->>SeatMS: GET /api/v1/aircraft-types
    Note over OpsAPI,SeatMS: Resolve cabin seat counts per aircraft type
    SeatMS-->>OpsAPI: AircraftTypesResponse [{aircraftTypeCode, cabinCounts}]

    OpsAPI->>OfferMS: POST /api/v1/fare-rules/search
    Note over OpsAPI,OfferMS: Fetch all active fare rules grouped by cabin<br/>(fare rules live in the Offer MS)
    OfferMS-->>OpsAPI: FareRules[]

    Note over OpsAPI: For each schedule: enumerate operating dates<br/>(ValidFrom → min(ValidTo, toDate)),<br/>skip dates before today,<br/>skip schedules with no aircraft config

    OpsAPI->>OfferMS: POST /api/v1/flights/batch
    Note over OpsAPI,OfferMS: flights[]: {flightNumber, departureDate,<br/>departureTime, arrivalTime, origin,<br/>destination, aircraftType, cabins[]} -<br/>existing records skipped automatically
    OfferMS-->>OpsAPI: BatchCreateResult {created, skipped, inventories[]}

    loop For each newly created inventory × cabin × matching fare rule
        OpsAPI->>OfferMS: POST /api/v1/flights/{inventoryId}/fares
        Note over OpsAPI,OfferMS: fareBasisCode, fareFamily, bookingClass,<br/>baseFareAmount, taxAmount, isRefundable,<br/>isChangeable, pointsPrice (if Reward rule)
        OfferMS-->>OpsAPI: Fare created
    end

    OpsAPI-->>Terminal: ImportSchedulesToInventoryResponse
    Note over OpsAPI,Terminal: {schedulesProcessed, inventoriesCreated,<br/>inventoriesSkipped, faresCreated}
```

---

## Real-time flight status query

Flight status is derived from Offer MS inventory data (load factor, capacity, operational flags), not from the Schedule MS.

```mermaid
sequenceDiagram
    participant Web
    participant OpsAPI as Operations API
    participant OfferMS as Offer MS

    Web->>OpsAPI: GET /v1/flights/{flightNumber}/status
    Note over Web,OpsAPI: Optional query: departureDate

    OpsAPI->>OfferMS: GET /api/v1/flights/{flightNumber}/inventory?departureDate={date}
    Note over OpsAPI,OfferMS: Returns inventory details including<br/>seat counts, holds, and operational data
    OfferMS-->>OpsAPI: FlightInventoryResponse

    Note over OpsAPI: Derive status from inventory<br/>(load factor, available seats, gate, aircraft registration)

    OpsAPI-->>Web: FlightStatusResponse
    Note over OpsAPI,Web: {flightNumber, status, loadFactor,<br/>gate, aircraftRegistration,<br/>scheduledDeparture, capacity}
```

---

## Schedule group management

```mermaid
sequenceDiagram
    participant Terminal as Admin UI
    participant OpsAPI as Operations API
    participant ScheduleMS as Schedule MS

    Terminal->>OpsAPI: GET /v1/schedule-groups
    OpsAPI->>ScheduleMS: GET /api/v1/schedule-groups
    ScheduleMS-->>OpsAPI: ScheduleGroups[]
    OpsAPI-->>Terminal: GetScheduleGroupsResponse

    Terminal->>OpsAPI: POST /v1/schedule-groups
    Note over Terminal,OpsAPI: {name, seasonStart, seasonEnd, createdBy}
    OpsAPI->>ScheduleMS: POST /api/v1/schedule-groups
    ScheduleMS-->>OpsAPI: ScheduleGroup (scheduleGroupId)
    OpsAPI-->>Terminal: 201 Created

    Terminal->>OpsAPI: DELETE /v1/schedule-groups/{scheduleGroupId}
    Note over Terminal,OpsAPI: Deletes group and all its schedules
    OpsAPI->>ScheduleMS: DELETE /api/v1/schedule-groups/{scheduleGroupId}
    ScheduleMS-->>OpsAPI: 204 No Content
    OpsAPI-->>Terminal: 204 No Content
```
