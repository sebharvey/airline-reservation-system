# Service dashboard

> Quick-reference overview of all Apex Air platform services — owners, base paths, key capabilities, and health endpoint.
> Updated: 2026-03-28

---

## Orchestration APIs

Orchestration APIs are channel-facing (or staff-facing) entry points. They coordinate microservices but own no database tables themselves.

| Service | Base path | Key capabilities | Health |
|---------|-----------|-----------------|--------|
| **Retail API** | `/v1` | Search flights, create baskets, confirm bookings, manage orders, SSR catalogue | `GET /health` |
| **Loyalty API** | `/v1` | Member authentication, registration, profile management, points authorisation/settlement | `GET /health` |
| **Disruption API** | `/v1` | Flight delay and cancellation orchestration; async passenger rebooking via Service Bus | `GET /health` |
| **Operations API** | `/v1` | SSIM schedule import, schedule-to-inventory import, staff-facing schedule management | `GET /health` |
| **Admin API** | `/v1` | Staff authentication (delegates to User MS) | `GET /health` |

---

## Microservices

Microservices own a single bounded context and its database schema. They are not called directly by channels — all calls route through an orchestration API.

| Service | Schema | Key capabilities | Health |
|---------|--------|-----------------|--------|
| **Schedule MS** | `schedule` | Persist/replace season schedule definitions; retrieve all schedules for inventory import | `GET /health` |
| **Offer MS** | `offer` | Flight inventory, fares, stored offer snapshots, seat availability, inventory hold/sell/release/cancel | `GET /health` |
| **Order MS** | `order` | Baskets, confirmed orders, post-sale changes, check-in, SSR management | `GET /health` |
| **Payment MS** | `payment` | Card authorisation, settlement, void, refund, payment event audit trail | `GET /health` |
| **Delivery MS** | `delivery` | E-ticket issuance/voiding/reissuance, passenger manifests, ancillary documents, boarding cards | `GET /health` |
| **Customer MS** | `customer` | Loyalty member profiles, tier configuration, points ledger, preferences | `GET /health` |
| **Identity MS** | `identity` | Member credential management, session tokens, refresh tokens | `GET /health` |
| **Seat MS** | `seat` | Seatmap definitions by aircraft type, seat-offer pricing, seat reservation status | `GET /health` |
| **Bag MS** | `bag` | Bag allowance policies by cabin, excess baggage pricing | `GET /health` |
| **User MS** | `user` | Internal staff user accounts, login, account management | `GET /health` |

---

## Operations API — endpoint summary

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/schedules/ssim` | Parse IATA SSIM Chapter 7 file and persist schedule definitions to Schedule MS |
| `POST` | `/v1/schedules/import-inventory` | Generate `FlightInventory` and `Fare` records in Offer MS from all stored schedules; skips existing records |

### Typical schedule activation workflow

```
1.  POST /v1/schedules/ssim          →  store schedule definitions (Schedule MS)
2.  POST /v1/schedules/import-inventory  →  generate inventory + fares (Offer MS)
```

After step 2, flights are immediately live for offer search.

---

## Schedule MS — endpoint summary

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/schedules` | Atomically replace all `FlightSchedule` records with a new season payload |
| `GET` | `/v1/schedules` | Return all persisted flight schedule records with operating date counts |

---

## Offer MS — endpoint summary

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/flights` | Create a single flight inventory record |
| `POST` | `/v1/flights/batch` | Batch-create inventory records; skips existing combinations |
| `POST` | `/v1/flights/{inventoryId}/fares` | Add a fare to an inventory record |
| `POST` | `/v1/search` | Search inventory and return stored-offer snapshots |
| `GET` | `/v1/offers/{offerId}` | Retrieve a stored offer by ID |
| `GET` | `/v1/flights/{flightId}/seat-availability` | Get per-seat availability status |
| `POST` | `/v1/flights/{flightId}/seat-reservations` | Reserve specific seats |
| `PATCH` | `/v1/flights/{flightId}/seat-availability` | Update seat status (e.g. to checked-in) |
| `POST` | `/v1/inventory/hold` | Hold seats for a basket |
| `POST` | `/v1/inventory/sell` | Convert held seats to sold at order confirmation |
| `POST` | `/v1/inventory/release` | Release held or sold seats back to available |
| `PATCH` | `/v1/inventory/cancel` | Cancel all inventory for a flight (Disruption API) |

---

## Data ownership

| Domain | Tables | Owner |
|--------|--------|-------|
| `schedule` | `FlightSchedule` | Schedule MS |
| `offer` | `FlightInventory`, `Fare`, `StoredOffer`, `InventoryHold`, `SeatReservation` | Offer MS |
| `order` | `Basket`, `Order`, `SsrCatalogue` | Order MS |
| `payment` | `Payment`, `PaymentEvent` | Payment MS |
| `delivery` | `Ticket`, `Manifest`, `Document` | Delivery MS |
| `seat` | `AircraftType`, `Seatmap`, `SeatPricing` | Seat MS |
| `bag` | `BagPolicy`, `BagPricing` | Bag MS |
| `customer` | `Customer`, `TierConfig`, `LoyaltyTransaction`, `Preferences` | Customer MS |
| `identity` | `UserAccount`, `RefreshToken` | Identity MS |
| `user` | `User` | User MS |
| `disruption` | `DisruptionEvent` | Disruption API (event log only) |

---

## Key architectural rules

- **No microservice-to-microservice calls.** All cross-domain synchronous calls route through an orchestration API.
- **Stored Offer pattern.** Prices locked at search time via `offer.StoredOffer` snapshot. Orders always retrieve by `OfferId`.
- **Inventory invariant.** `SeatsAvailable + SeatsHeld + SeatsSold = TotalSeats` is maintained by the Offer MS application layer.
- **Monetary amounts.** `DECIMAL(18,2)` in SQL, `decimal` in C#. Never floating-point.
- **Timestamps.** ISO 8601 UTC throughout. `createdAt`/`updatedAt` are database-generated — never written by application code.
- **IATA codes.** Airport codes `CHAR(3)` uppercase; aircraft codes 4-char (e.g. `A351`); passenger types `ADT`/`CHD`/`INF`/`YTH`.

---

## Related documentation

| Document | Purpose |
|----------|---------|
| [System overview](system-overview.md) | Architecture, domain model, airline context, glossary |
| [API reference](api-reference.md) | All endpoints with verb, path, and description |
| [Operations API spec](api-specs/operations-api.md) | Schedule management orchestration |
| [Schedule MS spec](api-specs/schedule-microservice.md) | Schedule persistence and retrieval |
| [Offer MS spec](api-specs/offer-microservice.md) | Inventory, fares, stored offers |
| [Design — Schedule](design/schedule.md) | SSIM import sequence, schedule domain design |
