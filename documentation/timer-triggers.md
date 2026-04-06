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

## Execution order dependency

`RollingInventoryImport` is deliberately scheduled one hour after the midnight batch so that expired inventory is removed before new inventory is added.

```
00:00  DeleteExpiredFlightInventory
       DeleteExpiredStoredOffers
       DeleteExpiredDraftOrders
       DeleteExpiredBaskets

01:00  RollingInventoryImport
```
