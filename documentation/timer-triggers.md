# Timer triggers

All timer triggers are Azure Functions (`[TimerTrigger]`) that run on a UTC cron schedule. They perform background housekeeping and are not reachable via HTTP.

---

## Midnight batch (00:00 UTC)

These four functions run concurrently at midnight UTC every day.

### `DeleteExpiredFlightInventory`

- **Service** — Offer microservice
- **Class** — `OfferCleanupFunction`
- **Schedule** — `0 0 0 * * *`
- **What it does** — Deletes flight inventory records (and their child `Fare`, `InventoryHold` rows) whose departure datetime is more than 48 hours in the past.
- **Deletion order** — `InventoryHold` → `Fare` → `FlightInventory` (single transaction, satisfies FK constraints).

### `DeleteExpiredStoredOffers`

- **Service** — Offer microservice
- **Class** — `OfferCleanupFunction`
- **Schedule** — `0 0 0 * * *`
- **What it does** — Deletes stored offer snapshots whose `ExpiresAt` timestamp is in the past. Stored offers are price-locked search results; once expired they can no longer be used to confirm an order.

### `DeleteExpiredDraftOrders`

- **Service** — Order microservice
- **Class** — `OrderCleanupFunction`
- **Schedule** — `0 0 0 * * *`
- **What it does** — Deletes orders in `Draft` status whose `UpdatedAt` is more than 24 hours in the past. Draft orders are created during the booking flow but never confirmed.

### `DeleteExpiredBaskets`

- **Service** — Order microservice
- **Class** — `BasketCleanupFunction`
- **Schedule** — `0 0 0 * * *`
- **What it does** — Deletes baskets whose `ExpiresAt` timestamp is in the past. Baskets are shopping sessions that hold flight and passenger selections before an order is placed.

---

## 01:00 UTC batch

### `RollingInventoryImport`

- **Service** — Offer microservice
- **Class** — `RollingInventoryImportFunction`
- **Schedule** — `0 0 1 * * *`
- **What it does** — Extends the rolling inventory window by importing the next day of flights at the 3-month boundary. Runs after the midnight cleanup to avoid importing inventory that would immediately be eligible for deletion.

---

## Every 20 minutes

### `Simulator`

- **Service** — Simulator microservice (`ReservationSystem.Microservices.Simulator`)
- **Class** — `SimulatorFunction`
- **Schedule** — `0 */20 * * * *`
- **What it does** — Creates 1–6 confirmed orders per run, simulating web bookings placed throughout the day. Each run picks a random order count, then for each order: selects a random route from seven daily long-haul routes (LHR→JFK, LHR→LAX, LHR→MIA, LHR→SFO, LHR→ORD, LHR→HKG, LHR→NRT), picks a random outbound departure within the next 48 hours (at least 1 hour from now), and adds 1–6 adult passengers. 70% of bookings include a return flight departing 1–7 days after the outbound. Cabin selection is weighted toward Economy (60%), then Premium Economy (25%), then Business (15%). 35% of bookings include SSR selections (meal or mobility codes). The full booking flow is executed: search, basket creation, basket summary repricing, passenger entry, seat selection, optional SSRs, and payment confirmation. Errors on individual orders are logged and skipped without aborting the run.
- **External dependency** — Retail API (`RetailApi:BaseUrl`). The Retail API base URL defaults to the live Azure deployment if the config key is absent.

### `SimulatorManualTrigger`

- **Service** — Simulator microservice (`ReservationSystem.Microservices.Simulator`)
- **Class** — `SimulatorFunction`
- **Trigger** — `GET /api/v1/simulator/run` (HTTP, anonymous)
- **What it does** — Runs the same logic as the `Simulator` timer trigger on demand. Opens a browser tab (or any HTTP client) to the endpoint URL to start one simulation run immediately without waiting for the next 20-minute interval. Returns `200 OK` with `{"message":"Simulator run completed."}` when all orders have been processed.

### `FlightOperationalDataSimulator`

- **Service** — Simulator microservice (`ReservationSystem.Microservices.Simulator`)
- **Class** — `SimulatorFunction`
- **Schedule** — `0 */20 * * * *`
- **What it does** — Simulates pre-departure operational updates to flight inventory. Each run authenticates with the Admin API using employee credentials (`User:Username` / `User:Password`), then fetches inventory for today and tomorrow via the Retail API admin endpoint. Two updates are applied:
  - **Aircraft registration** — flights departing 1–26 hours from now that have no registration are assigned one (format `G-X123`, e.g. `G-A456`). Primary assignment happens ~24 hours out; the wide lower bound catches any flight that was missed on a previous run.
  - **Departure gate** — flights departing 60–180 minutes from now that have no gate are assigned a random gate number between 1 and 50. The 60-minute floor guarantees the gate is always set before the last hour before departure.
  Each update calls the Operations API `PATCH /v1/admin/inventory/{inventoryId}/operational-data`. Flights that already have the value set are skipped. Individual failures are logged and do not abort the run.
- **External dependencies** — Admin API (`AdminApi:BaseUrl`), Retail API (`RetailApi:BaseUrl`), Operations API (`OperationsApi:BaseUrl`). Credentials read from `User:Username` and `User:Password`.

### `FlightOperationalDataSimulatorManualTrigger`

- **Service** — Simulator microservice (`ReservationSystem.Microservices.Simulator`)
- **Class** — `SimulatorFunction`
- **Trigger** — `GET /api/v1/simulator/flight-updates` (HTTP, anonymous)
- **What it does** — Runs the same logic as the `FlightOperationalDataSimulator` timer trigger on demand. Returns `200 OK` with `{"message":"Flight operational data update completed."}` when processing is complete.

---

## Execution order dependency

`RollingInventoryImport` is deliberately scheduled one hour after the midnight batch so that expired inventory is removed before new inventory is added.

```
00:00  DeleteExpiredFlightInventory
       DeleteExpiredStoredOffers
       DeleteExpiredDraftOrders
       DeleteExpiredBaskets

01:00  RollingInventoryImport
```
