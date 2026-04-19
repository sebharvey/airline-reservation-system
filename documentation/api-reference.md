# API Endpoint Reference

> **HTTP verb convention for Retail API vs microservice endpoints:** The Retail API is the channel-facing orchestration layer. Where the Retail API and an internal microservice share the same URL path (e.g. `/v1/orders/{bookingRef}/cancel`), the Retail API endpoint uses `POST` (initiating the orchestration flow) while the corresponding internal microservice endpoint uses `PATCH` (applying a partial state update). These are distinct endpoints on distinct services; the verb difference is intentional and consistent throughout.

> **Microservice authentication:** All orchestration-to-microservice calls are authenticated using an Azure Function Host Key in the `x-functions-key` HTTP header. All microservices currently share the same key. See [Microservice Authentication — Host Keys](api.md#microservice-authentication--host-keys) in `api.md` for the full mechanism.

---

## Contents

- [Retail API](#retail-api--full-api-spec)
  - [Search & Basket](#search--basket)
  - [Orders](#orders)
  - [SSR](#ssr)
  - [Products](#products)
  - [Flights & Seatmaps](#flights--seatmaps)
  - [Check-in](#check-in)
  - [Admin Inventory Management](#admin-inventory-management)
- [Loyalty API](#loyalty-api--full-api-spec)
  - [Authentication](#authentication)
  - [Account & Profile](#account--profile)
  - [Admin Customer Management](#admin-customer-management)
- [Admin API](#admin-api--full-api-spec)
  - [Authentication](#authentication-3)
  - [User Management](#user-management)
- [Operations API](#operations-api--full-api-spec)
- [Schedule Microservice](#schedule-microservice--full-api-spec)
- [Offer Microservice](#offer-microservice--full-api-spec)
- [Order Microservice](#order-microservice--full-api-spec)
- [Payment Microservice](#payment-microservice--full-api-spec)
- [Delivery Microservice](#delivery-microservice--full-api-spec)
  - [Tickets](#tickets)
  - [Manifest](#manifest)
  - [Documents](#documents)
  - [Boarding Cards](#boarding-cards)
- [Ancillary Microservice](#ancillary-microservice--full-api-spec)
- [Identity Microservice](#identity-microservice--full-api-spec)
  - [Authentication](#authentication-1)
  - [Account Management](#account-management)
- [Customer Microservice](#customer-microservice--full-api-spec)
- [User Microservice](#user-microservice)
  - [Authentication](#authentication-2)
  - [User management](#user-management)
- [Airport API](#airport-api)
- [Finance API](#finance-api)
- [Accounting Microservice](#accounting-microservice)
- [Timatic Simulator](#timatic-simulator)

---

## Retail API — [Full API Spec](api-specs/retail-api.md)

### Search & Basket

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/search/slice` | Search for available direct flights for a single directional slice (outbound or inbound); returns all cabin classes per flight grouped as `flights[] → cabins[] → fareFamilies[] → offer`; each fare family carries one representative `offer` with an `offerId`; response includes a `sessionId` identifying the search session |
| `POST` | `/v1/search/connecting` | Search for connecting itinerary options via the LHR hub (e.g. DEL → JFK via LHR); assembles pairs of per-segment offers from the Offer MS, applies minimum connect time (60 min), and returns combined itinerary options each carrying two `OfferIds` — one per leg |
| `POST` | `/v1/basket` | Create a new basket with one or more flight offer IDs; accepts optional `sessionId` from the search response to scope Offer MS lookups to the indexed session; initiates the bookflow. For reward bookings, accepts `bookingType=Reward` and `loyaltyNumber`; verifies points balance via Customer MS before creating the basket |
| `PUT` | `/v1/basket/{basketId}/passengers` | Add or update passenger details on a basket |
| `PUT` | `/v1/basket/{basketId}/seats` | Add or update seat selections on a basket during the bookflow |
| `PUT` | `/v1/basket/{basketId}/bags` | Add or update bag selections on a basket during the bookflow; accepts bag offer IDs per passenger per segment; updates `TotalBagAmount` on the basket |
| `PUT` | `/v1/basket/{basketId}/ssrs` | Add or update Special Service Request selections on a basket during the bookflow; accepts SSR code, passenger reference, and segment reference per selection; no charge — basket total is unchanged |
| `GET` | `/v1/basket/{basketId}/summary` | Reprice all flight offers in the basket and return a pricing summary with per-flight tax line breakdowns |
| `GET` | `/v1/basket/{basketId}/payment-summary` | Return the complete payment-screen summary for a basket: flights, passengers, seat/bag/product/SSR selections, and all monetary totals pre-calculated server-side. The Angular client renders this response as-is with no business logic |
| `POST` | `/v1/basket/{basketId}/confirm` | Confirm a basket, triggering payment (fare + any seat/bag ancillaries as separate transactions), ticketing, and order creation. For reward baskets, orchestrates points authorisation (Customer MS), tax-only payment authorisation (Payment MS), ticketing, inventory settlement, points settlement, and order creation |

### Orders

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/orders/retrieve` | Retrieve a confirmed order by booking reference and passenger name |
| `PATCH` | `/v1/orders/{bookingRef}/passengers` | Correct or update passenger details on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/seats` | Add or change seat selection on a confirmed order (post-sale, charged) |
| `POST` | `/v1/orders/{bookingRef}/change` | Change a confirmed flight to a new itinerary; collects add-collect and change fee if applicable |
| `POST` | `/v1/orders/{bookingRef}/cancel` | Cancel a confirmed booking; initiates refund if fare conditions permit |
| `POST` | `/v1/orders/{bookingRef}/bags` | Add or update checked bag selection on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/ssrs` | Add, update, or remove Special Service Requests on a confirmed order (self-serve); rejected with `422` if within the SSR amendment cut-off window for the departure |
| `POST` | `/v1/reward/{redemptionReference}/reverse` | Reverse a points authorisation if a downstream step fails during reward booking confirmation |

### SSR

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/ssr/options` | Retrieve all active SSR codes, labels, and categories (Meal, Mobility, Accessibility) from `order.SsrCatalogue`; accepts optional `cabinCode` and `flightNumbers` query parameters |
| `POST` | `/v1/ssr/options` | Create a new SSR catalogue entry (`ssrCode`, `label`, `category`); admin endpoint — not channel-facing |
| `PUT` | `/v1/ssr/options/{ssrCode}` | Update an existing SSR entry (label or category); `ssrCode` is immutable; admin endpoint |
| `DELETE` | `/v1/ssr/options/{ssrCode}` | Deactivate an SSR code (`IsActive = 0`); existing order items referencing the code are unaffected; admin endpoint |

### Products

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/products` | Retrieve the active retail product catalogue from the Ancillary MS, pre-grouped as `productGroups[] → products[]`; each product carries all per-currency prices so the channel can filter by basket currency without further transformation |

### Flights & Seatmaps

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/flights/{flightId}/seatmap` | Retrieve seatmap with pricing and availability for a flight |
| `GET` | `/v1/flights/{flightId}/seat-availability` | Retrieve real-time seat availability overlay for a flight |

### Check-in

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/checkin/retrieve` | Retrieve booking details to begin the online check-in flow |
| `PATCH` | `/v1/checkin/{bookingRef}/seats` | Update seat assignment during check-in (no charge at OLCI) |
| `POST` | `/v1/checkin/{bookingRef}` | Submit check-in for all passengers, recording APIS data and generating boarding cards |

### Admin inventory management

Staff-only endpoints protected by a valid staff JWT token (`Authorization: Bearer <token>`). Requires `UserMs:JwtSecret` to be configured.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/admin/inventory` | Return flight inventory for a given departure date, grouped by flight (one row per flight with cabin F/J/W/Y aggregated as columns). Query param `departureDate=yyyy-MM-dd`; defaults to today. Each row includes total seats, seats available per cabin, overall load factor (percent of seats filled), and flight status. |

---

## Loyalty API — [Full API Spec](api-specs/loyalty-api.md)

### Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/auth/login` | Authenticate with email and password; returns a short-lived JWT access token, a refresh token, expiry, and the customer's `loyaltyNumber` |
| `POST` | `/v1/auth/refresh` | Exchange a valid refresh token for a new access token and rotated refresh token (single-use semantics) |
| `POST` | `/v1/auth/logout` | Revoke the current refresh token and invalidate the session |
| `POST` | `/v1/auth/password/reset-request` | Request a password reset link; dispatched to the registered email address if found — response is identical regardless to prevent account enumeration |
| `POST` | `/v1/auth/password/reset` | Submit a new password using a valid single-use reset token; invalidates all active refresh tokens on success |

### Account & Profile

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/register` | Register a new loyalty programme member, creating linked Identity and Customer records |
| `GET` | `/v1/customers/{loyaltyNumber}` | Retrieve a customer's profile, tier status, and points balance |
| `GET` | `/v1/customers/{loyaltyNumber}/transactions` | Retrieve paginated points transaction history |
| `PATCH` | `/v1/customers/{loyaltyNumber}/profile` | Update profile details (name, date of birth, nationality, phone, preferred language, passport number, passport issue date, passport issuing country, Known Traveller Number) |
| `POST` | `/v1/customers/{loyaltyNumber}/points/authorise` | Authorise a points redemption hold against the customer's balance for a reward booking; returns a `RedemptionReference`; verifies sufficient balance before placing hold |
| `POST` | `/v1/customers/{loyaltyNumber}/points/settle` | Settle a previously authorised points redemption; deducts points from balance and appends a `Redeem` transaction to the loyalty ledger |
| `POST` | `/v1/customers/{loyaltyNumber}/points/reverse` | Reverse a points authorisation hold, returning held points to the customer's available balance; used on booking failure rollback (e.g. ticketing failure after points authorisation) |
| `POST` | `/v1/customers/{loyaltyNumber}/points/reinstate` | Reinstate points to a customer's balance following a completed cancellation or flight change that results in a net reduction in points redeemed; appends a `Reinstate` transaction to the loyalty ledger; used by Retail API on voluntary cancellation (reward bookings) and by Retail API and Operations API when a flight change or IROPS rebooking reduces the points cost |
| `POST` | `/v1/customers/{loyaltyNumber}/points/add` | Add points directly to a customer's balance; caller supplies `transactionType` (must be one of `Earn`, `Redeem`, `Adjustment`, `Expiry`, `Reinstate`) and `description`; intended for manual adjustments and testing |
| `POST` | `/v1/customers/{loyaltyNumber}/points/transfer` | Transfer points from the authenticated member's account to another loyalty account; requires the recipient's loyalty number and email address — both are verified to match the same account before points move; each account receives an `Adjustment` transaction recording the counterpart's loyalty number |
| `GET` | `/v1/accounts/{userAccountId}/verify-email` | Verify ownership of an email address; called when the user follows the link in their registration confirmation email; delegates to Identity MS |
| `POST` | `/v1/customers/{loyaltyNumber}/email/change-request` | Initiate an email address change; sends verification link to the new address (step 1 of email change flow) |
| `POST` | `/v1/email/verify` | Verify a new email address using a time-limited token (step 2 of email change flow); delegates to Identity MS |

### Admin customer management

Staff-facing endpoints for managing loyalty customers. All routes require a valid staff JWT (issued by the Admin API) with a `role` claim of `User`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/admin/customers/search` | Search loyalty customers by name, loyalty number, or email address; returns a summary list of matching members; accepts optional `query` in the request body; when the query contains an `@` sign the Loyalty API also looks up the Identity microservice by email and resolves the linked customer |
| `GET` | `/v1/admin/customers/{loyaltyNumber}` | Retrieve full customer details including address, tier, points, activity timestamps, and linked identity account details (email, locked status, failed login attempts, last login, password changed date); password hash is never returned |
| `PATCH` | `/v1/admin/customers/{loyaltyNumber}` | Update customer profile fields (name, date of birth, nationality, phone, language, address, passport number, passport issue date, passport issuing country, Known Traveller Number) |
| `GET` | `/v1/admin/customers/{loyaltyNumber}/transactions` | Retrieve paginated loyalty transaction history for a customer |
| `POST` | `/v1/admin/customers/{loyaltyNumber}/points` | Assign adjustment points to a customer account; requires points and description |
| `DELETE` | `/v1/admin/customers/{loyaltyNumber}` | Delete a customer account and all its transactions permanently |
| `PATCH` | `/v1/admin/customers/{loyaltyNumber}/status` | Activate or deactivate a customer account; accepts `isActive` boolean |
| `PATCH` | `/v1/admin/customers/{loyaltyNumber}/identity` | Update identity account fields for a customer; accepts `email` (applied without verification) and `isLocked` (lock or unlock the account); at least one field must be provided; `409` if email already registered to another account |
| `GET` | `/v1/admin/customers/{loyaltyNumber}/notes` | Retrieve all contact-centre notes for a customer; ordered most-recent-first |
| `POST` | `/v1/admin/customers/{loyaltyNumber}/notes` | Add a contact-centre note to a customer account; `createdBy` is extracted from the staff JWT `unique_name` claim automatically |
| `PUT` | `/v1/admin/customers/{loyaltyNumber}/notes/{noteId}` | Update the text of an existing note; `404` if the note does not belong to the customer |
| `DELETE` | `/v1/admin/customers/{loyaltyNumber}/notes/{noteId}` | Delete a contact-centre note permanently; `404` if the note does not belong to the customer |

---

## Admin API — [Full API Spec](api-specs/admin-api.md)

The Admin API is the orchestration entry point for internal staff applications (Contact Centre, Airport, Operations, Finance). It delegates credential validation and JWT issuance to the User microservice and provides staff-facing user management endpoints. The Admin API owns no database tables.

### Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/auth/login` | Authenticate a staff member with username and password; returns a JWT access token (15-minute TTL), `userId`, `expiresAt`, and `tokenType`. Delegates to the User MS; returns `401` for invalid credentials and `403` for locked or inactive accounts |

### User management

Staff-facing endpoints for managing employee user accounts. All routes require a valid staff JWT with a `role` claim of `User`.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/admin/users` | Retrieve all employee user accounts (passwords excluded) |
| `GET` | `/v1/admin/users/{userId}` | Retrieve a single employee user account by ID |
| `POST` | `/v1/admin/users` | Create a new employee user account; returns `userId`; `409` if username or email already exists |
| `PATCH` | `/v1/admin/users/{userId}` | Update user profile fields (firstName, lastName, email); all optional; `409` if email already in use |
| `PATCH` | `/v1/admin/users/{userId}/status` | Activate or deactivate a user account |
| `POST` | `/v1/admin/users/{userId}/unlock` | Unlock a locked user account and reset failed login attempts |
| `POST` | `/v1/admin/users/{userId}/reset-password` | Reset a user's password; unlocks the account and clears failed attempts |
| `DELETE` | `/v1/admin/users/{userId}` | Permanently delete a user account; returns `404` if user does not exist |

---

## Operations API — [Full API Spec](api-specs/operations-api.md)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET`  | `/v1/schedule-groups` | Retrieve all schedule groups; returns `count` and per-group summary including `scheduleGroupId`, `name`, `seasonStart`, `seasonEnd`, `isActive`, `scheduleCount` |
| `POST` | `/v1/schedule-groups` | Create a new schedule group with `name`, `seasonStart`, `seasonEnd`, `isActive`, `createdBy`; returns the created group |
| `PUT`  | `/v1/schedule-groups/{scheduleGroupId}` | Update a schedule group's `name`, `seasonStart`, `seasonEnd`, `isActive` |
| `DELETE` | `/v1/schedule-groups/{scheduleGroupId}` | Delete a schedule group and all its flight schedules |
| `GET`  | `/v1/schedules` | Retrieve stored flight schedules; optional `?scheduleGroupId=` query parameter to filter by group; returns `count` and per-schedule summary including `scheduleId`, `scheduleGroupId`, route, times, `daysOfWeek`, aircraft type, validity window, `flightsCreated`, and `operatingDateCount` |
| `POST` | `/v1/schedules/ssim` | Import schedules from an IATA SSIM Chapter 7 plain-text file (`text/plain` body, required `?scheduleGroupId=` and optional `?createdBy=` query parameters); the Operations API parses all Type 3 scheduled-passenger leg records, converts them to the season schedule JSON payload format, and forwards to the Schedule MS `POST /v1/schedules`; returns `imported`, `deleted`, and per-schedule summary |
| `POST` | `/v1/schedules/import-inventory` | Generate `FlightInventory` and `Fare` records in the Offer MS from schedules stored in the Schedule MS; optional `?scheduleGroupId=` to limit to a specific group; request body supplies cabin seat counts for every aircraft type in the fleet as `aircraftConfigs[]{aircraftTypeCode, cabins[]{cabinCode, totalSeats}}`; the handler matches each schedule row to the correct aircraft config by `AircraftType`, enumerates operating dates, batch-creates inventory, then attaches fares from stored fare rules in the Offer MS; skips operating dates where inventory already exists; returns `schedulesProcessed`, `inventoriesCreated`, `inventoriesSkipped`, and `faresCreated` |
| `POST` | `/v1/admin/inventory/cancel` | Cancel all inventory for a flight; accepts `flightNumber` and `departureDate` (yyyy-MM-dd) in the request body; calls Offer MS `PATCH /v1/inventory/cancel` to set `SeatsAvailable = 0` and `Status = Cancelled` across all cabins; returns `204 No Content` on success; `404` if the flight is not found; `422` if already cancelled; staff JWT required |
| `POST` | `/v1/admin/disruption/cancel` | Cancel a flight and **synchronously** rebook all affected passengers (IROPS); closes inventory, then processes every booking in priority order (cabin F→J→W→Y, loyalty tier Platinum→Gold→Silver→Blue, booking date earliest first); for each booking: holds inventory on the best available replacement flight (direct then connecting via LHR, 72-hour window, 60-min MCT), adjusts reward points, rebooks the order, reissues e-tickets, updates the manifest; cancels with full IROPS refund if no replacement is available; returns per-passenger outcomes; staff JWT required |
| `POST` | `/v1/admin/disruption/change` | Handle aircraft type change disruption — future capability, not yet implemented; returns `501 Not Implemented`; staff JWT required |
| `POST` | `/v1/admin/disruption/time` | Handle flight time change disruption — future capability, not yet implemented; returns `501 Not Implemented`; staff JWT required |
| `GET`  | `/v1/flights/{flightNumber}/status` | Public endpoint — get real-time flight status derived from inventory state for today's date; calls the Offer MS to retrieve inventory for the flight, then maps inventory status and load factor into a `FlightStatus` response with scheduled/estimated times, gate, terminal, aircraft type, delay, and status message |
| `GET`  | `/v1/flight-numbers` | Return distinct flight numbers with route details (origin, destination, departure time, arrival time) from schedules active for the current date; calls the Schedule MS and filters to schedules where today falls within `ValidFrom`–`ValidTo`; intended for populating flight selection dropdowns |
| `POST` | `/v1/oci/retrieve` | Retrieve booking for online check-in by booking reference, lead PAX surname, and departure airport; optionally pre-fills passport data from loyalty profile when a loyalty number is supplied |
| `POST` | `/v1/oci/pax` | Submit or update travel document details for each passenger on the booking; validates passport expiry and persists documents to the order via the Order microservice |
| `POST` | `/v1/oci/seats` | Submit seat selection during check-in (not implemented — returns success) |
| `POST` | `/v1/oci/bags` | Submit baggage selection during check-in (not implemented — returns success) |
| `POST` | `/v1/oci/checkin` | Complete check-in for all passengers on a booking; retrieves the order to resolve ticket numbers, calls the Delivery microservice to update each ticket coupon status to `C`, and returns the list of checked-in ticket numbers |
| `POST` | `/v1/oci/boarding-docs` | Request boarding documents for a set of checked-in ticket numbers and departure airport; proxies to the Delivery microservice and returns an array of boarding cards with BCBP strings |

### Fare family management

Staff-only endpoints for managing the named fare family catalogue. Fare families are stored in `offer.FareFamily` and referenced by name in fare rules and search results. All routes require a valid staff JWT.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET`    | `/v1/admin/fare-families` | List all fare families ordered by `displayOrder` then `name` |
| `GET`    | `/v1/admin/fare-families/{fareFamilyId}` | Retrieve a single fare family by ID; `404` if not found |
| `POST`   | `/v1/admin/fare-families` | Create a new fare family; requires `name` (unique, max 50 chars); optional `description` and `displayOrder` |
| `PUT`    | `/v1/admin/fare-families/{fareFamilyId}` | Update name, description, and display order of an existing fare family; `404` if not found |
| `DELETE` | `/v1/admin/fare-families/{fareFamilyId}` | Permanently delete a fare family; `404` if not found |

### Fare rule management

Staff-only endpoints for managing fare pricing rules. All routes require a valid staff JWT. Responses include the `isPrivate` flag; when `true`, the fare is suppressed from the public Retail API and web channel — it is only visible via the admin Retail API (Contact Centre) and these Operations API endpoints.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/admin/fare-rules/search` | Search fare rules by optional free-text query; returns all matching fare rules including their `isPrivate` flag |
| `GET`  | `/v1/admin/fare-rules/{fareRuleId}` | Retrieve a single fare rule by ID; returns all fields including `isPrivate` |
| `POST` | `/v1/admin/fare-rules` | Create a new fare rule; accepts `isPrivate` boolean — when `true`, the fare is hidden from the public search channel and only available to Contact Centre agents |
| `PUT`  | `/v1/admin/fare-rules/{fareRuleId}` | Update an existing fare rule including its `isPrivate` flag; `404` if not found |
| `DELETE` | `/v1/admin/fare-rules/{fareRuleId}` | Permanently delete a fare rule; `404` if not found |

### Disruption handling

Endpoints called exclusively by the Flight Operations System (FOS). Authenticated via Azure Function Host Key (`x-functions-key`).

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/disruptions/delay` | Notify the system of a flight delay; updates all affected orders and manifests synchronously; returns `200 OK` when all records are updated |
| `POST` | `/v1/disruptions/cancellation` | Notify the system of a flight cancellation; **synchronously** closes flight inventory (Offer MS) and returns `202 Accepted`; per-passenger rebooking is processed **asynchronously** via Service Bus — each affected booking is published as an individual message and processed independently to prevent a single failure blocking the entire cohort; for the 72-hour no-rebooking window scenario, passengers are notified and bookings cancelled with full IROPS refund |

---

## Schedule Microservice — [Full API Spec](api-specs/schedule-microservice.md)

The Schedule microservice is the internal persistence layer for flight schedule definitions and schedule groups. It accepts a full season schedule payload with a target `scheduleGroupId`, atomically replaces schedule records within that group, and returns a per-schedule import summary. Called by the Operations API after parsing a SSIM file or loading a pre-built JSON schedule payload.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET`  | `/v1/schedule-groups` | Return all schedule groups ordered by active status and season start; returns `count` and per-group summary |
| `POST` | `/v1/schedule-groups` | Create a new schedule group; returns the created group |
| `PUT`  | `/v1/schedule-groups/{scheduleGroupId}` | Update a schedule group |
| `DELETE` | `/v1/schedule-groups/{scheduleGroupId}` | Delete a schedule group and all its flight schedules |
| `POST` | `/v1/schedules` | Replace all `FlightSchedule` records within the specified `scheduleGroupId` with the supplied season schedule payload; previous records for that group are deleted and the new set is inserted atomically; accepts the IATA-structured JSON format (`scheduleGroupId`, `header`, `carriers[]`, `schedules[]`); returns `imported`, `deleted`, and per-schedule summary (`scheduleId`, route, validity window, `operatingDateCount`) |
| `GET` | `/v1/schedules` | Return persisted flight schedule records; optional `?scheduleGroupId=` query parameter to filter by group; returns `count` and per-schedule summary including `scheduleId`, `scheduleGroupId`, route, times, `daysOfWeek`, validity window, `flightsCreated`, and `operatingDateCount` |

---

## Offer Microservice — [Full API Spec](api-specs/offer-microservice.md)

The Offer microservice operates on individual flight **segments** only. It has no concept of a multi-segment connecting itinerary; connecting itinerary assembly (pairing legs, enforcing minimum connect time, combining prices) is the responsibility of the Retail API orchestration layer.

> **Code share (future):** When code share flights are introduced, `POST /v1/search` responses will include optional `operatingCarrier` and `operatingFlightNumber` fields alongside the marketing flight details. These fields are intentionally omitted for own-metal flights in the initial release but must be handled by all consumers from launch.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/flights` | Create a new flight inventory record for a specific operating date and cabin; called by the Operations API during schedule generation; initialises `SeatsAvailable = TotalSeats`, `SeatsHeld = 0`, `SeatsSold = 0`; returns `inventoryId` |
| `POST` | `/v1/flights/batch` | Batch-create flight inventory records; for each item in the request, creates a new `FlightInventory` record if one does not already exist for that `flightNumber`/`departureDate`/`cabinCode` combination, otherwise skips it; returns `created`, `skipped`, and the list of newly created inventory records |
| `POST` | `/v1/flights/{inventoryId}/fares` | Add a fare definition to an existing flight inventory record; called by the Operations API to attribute pricing to inventory after creation; returns `fareId` |
| `POST` | `/v1/search` | Search flight inventory for a single segment (origin, destination, date, pax count) across all cabin classes and return priced, stored-offer-snapshotted offers; creates one `StoredOffer` row per flight containing all cabin fares in `FaresInfo` JSON; returns a `sessionId` shared across all rows produced by the search; called once per leg by the Retail API for both direct (`/v1/search/slice`) and connecting (`/v1/search/connecting`) searches; accepts optional `includePrivateFares` boolean (default `false`) — the public Retail API always sends `false`, the admin Retail API (Contact Centre) sends `true` to surface private fares |
| `GET` | `/v1/offers/{offerId}` | Retrieve a stored offer by the per-fare `offerId` (found inside `FaresInfo`); validates `ExpiresAt > now`; resolves flight details from `offer.FlightInventory` at read time; accepts optional `?sessionId=` query parameter to scope the lookup to the indexed session for efficiency |
| `GET` | `/v1/flights/{flightId}/seat-availability` | Retrieve current seat availability status for a flight — returns one entry per selectable seat with `SeatOfferId` (deterministic) and availability status (`available`, `held`, or `sold`) based on `offer.FlightInventory`; does **not** return pricing (pricing is owned by the Seat MS via `GET /v1/seat-offers?flightId=`); Retail API merges this availability data with the Seat MS offer response and the seatmap layout before returning to the channel |
| `POST` | `/v1/flights/{flightId}/seat-reservations` | Reserve seats against a basket or check-in |
| `PATCH` | `/v1/flights/{flightId}/seat-availability` | Update seat status on a flight (e.g. to checked-in) |
| `GET` | `/v1/flights/{inventoryId}` | Return flight details for a single inventory record by GUID; returns flight number, route, times, aircraft type, and status; called by the Retail API when resolving flight details for a confirmed order using the `inventoryId` stored on each flight order item |
| `GET` | `/v1/flights/{flightNumber}/inventory` | Return flight inventory for a specific flight number and departure date with cabin F/J/W/Y breakdown; query param `departureDate=yyyy-MM-dd` (defaults to today); called by the Operations API for flight status derivation |
| `GET` | `/v1/admin/inventory` | Return flight inventory for a given departure date, aggregated by flight — one row per flight with F/J/W/Y cabin counts as pivot columns; query param `departureDate=yyyy-MM-dd`; called by the Retail API admin endpoint |
| `POST` | `/v1/inventory/hold` | Hold seats against a new or replacement booking (increments SeatsHeld; decrements SeatsAvailable) |
| `POST` | `/v1/inventory/sell` | Convert held seats to sold at order confirmation (decrements SeatsHeld; increments SeatsSold; SeatsAvailable unchanged) |
| `POST` | `/v1/inventory/release` | Release held or sold seats back to available inventory (increments SeatsAvailable; decrements SeatsHeld or SeatsSold — used on voluntary cancel, flight change rollback, and basket expiry) |
| `PATCH` | `/v1/inventory/cancel` | Close a cancelled flight's inventory (sets SeatsAvailable = 0, status = Cancelled; used by Operations API on flight cancellation) |

**Fare family management (internal — called by Operations API)**

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET`    | `/v1/fare-families` | List all fare families ordered by `displayOrder` then `name` |
| `GET`    | `/v1/fare-families/{fareFamilyId}` | Retrieve a single fare family by ID; `404` if not found |
| `POST`   | `/v1/fare-families` | Create a new fare family; requires `name` (unique); optional `description` and `displayOrder` |
| `PUT`    | `/v1/fare-families/{fareFamilyId}` | Update an existing fare family; `404` if not found |
| `DELETE` | `/v1/fare-families/{fareFamilyId}` | Permanently delete a fare family; `404` if not found |

**Fare rule management (internal — called by Operations API)**

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/fare-rules/search` | Search fare rules by optional `query` string; returns all matching rules including `isPrivate` flag |
| `GET`  | `/v1/fare-rules/{fareRuleId}` | Retrieve a single fare rule by ID; returns all fields including `isPrivate` |
| `POST` | `/v1/fare-rules` | Create a new fare rule; accepts `isPrivate` boolean (default `false`); private fare rules are excluded from public `POST /v1/search` results unless `includePrivateFares = true` |
| `PUT`  | `/v1/fare-rules/{fareRuleId}` | Replace all fields of an existing fare rule including `isPrivate`; `404` if not found |
| `DELETE` | `/v1/fare-rules/{fareRuleId}` | Permanently delete a fare rule; `404` if not found |

---

## Order Microservice — [Full API Spec](api-specs/order-microservice.md)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/basket` | Create a new basket; expiry is fixed at 60 minutes from creation |
| `POST` | `/v1/basket/{basketId}/offers` | Add a validated stored-offer to a basket (called by Retail API after fetching and validating the offer from the Offer MS; validates `IsConsumed = 0` and `ExpiresAt > now`) |
| `PUT` | `/v1/basket/{basketId}/passengers` | Update passenger details on a basket |
| `PUT` | `/v1/basket/{basketId}/seats` | Update seat selections on a basket during the bookflow |
| `PUT` | `/v1/basket/{basketId}/bags` | Add or update bag selections on a basket during the bookflow; updates `TotalBagAmount` |
| `PUT` | `/v1/basket/{basketId}/ssrs` | Add or update SSR selections on a basket during the bookflow; no charge — basket total is unchanged |
| `POST` | `/v1/orders` | Create a draft order record from a basket; sets `OrderStatus = Draft`; basket remains active to allow PATCH operations before confirmation |
| `DELETE` | `/v1/orders/{orderId}` | Delete a draft order by ID; only orders in `Draft` status may be deleted; returns `204` on success, `404` if not found or not in Draft status |
| `POST` | `/v1/orders/confirm` | Confirm a draft order: validates required data (passengers, segments), writes payment references into OrderData, assigns a booking reference (PNR), transitions `OrderStatus` to `Confirmed`, and deletes the basket |
| `POST` | `/v1/orders/retrieve` | Retrieve a confirmed order by booking reference and passenger name |
| `GET` | `/v1/orders` | Query orders by flight number and departure date (used by Operations API for disruption handling) |
| `PATCH` | `/v1/orders/{bookingRef}/passengers` | Update passenger details on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/seats` | Update seat assignments on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/segments` | Update segment departure/arrival times (used by Operations API for flight delay handling) |
| `PATCH` | `/v1/orders/{bookingRef}/change` | Apply a confirmed flight change, recording new segment, add-collect, and payment reference |
| `PATCH` | `/v1/orders/{bookingRef}/cancel` | Mark an order as cancelled with reason and any cancellation fee |
| `PATCH` | `/v1/orders/{bookingRef}/rebook` | Rebook a passenger onto a replacement flight (used by Operations API for IROPS cancellation rebooking) |
| `PATCH` | `/v1/orders/{bookingRef}/bags` | Add or update bag order items on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/ssrs` | Add, update, or remove SSR items on a confirmed order; publishes `OrderChanged` event |
| `POST` | `/v1/orders/{bookingRef}/checkin` | Record check-in status and APIS data for passengers |
| `GET` | `/v1/ssr/options` | Retrieve all active SSR codes, labels, and categories from `order.SsrCatalogue`; called by the Retail API to serve the channel-facing SSR catalogue |

---

## Payment Microservice — [Full API Spec](api-specs/payment-microservice.md)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/payment/initialise` | Initialise a payment with order details; returns a `paymentId` (GUID) for use in subsequent operations |
| `POST` | `/v1/payment/{paymentId}/authorise` | Authorise an initialised payment with card details; creates an `Authorised` `PaymentEvent` row |
| `POST` | `/v1/payment/{paymentId}/settle` | Settle a previously authorised payment; creates a `Settled` or `PartialSettlement` `PaymentEvent` row |
| `POST` | `/v1/payment/{paymentId}/void` | Void a previously authorised payment, releasing held funds; updates the `PaymentEvent` row |
| `POST` | `/v1/payment/{paymentId}/refund` | Refund a settled payment in full or in part (used on voluntary cancellation) |
| `GET` | `/v1/payment/{paymentId}` | Retrieve a payment record by ID; returns the full payment including current status, authorised and settled amounts |
| `GET` | `/v1/payment/{paymentId}/events` | Retrieve all payment events for a payment in chronological order; reflects the full lifecycle history (Authorised, Settled, Voided, Refunded, etc.) |

---

## Delivery Microservice — [Full API Spec](api-specs/delivery-microservice.md)

The Delivery microservice manages three distinct record types: **Tickets** (financial/accounting documents — the airline's equivalent of an e-ticket or EMD), **Manifest** (the operational passenger manifest used by ground handling and crew), and **Documents** (ancillary EMD-equivalent records for post-sale purchases such as seats and bags).

### Tickets

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/tickets` | Retrieve all tickets for a booking reference; response includes per-ticket fare amounts, fare calculation string, structured tax breakdown with coupon attribution, and derived fare components |
| `POST` | `/v1/tickets` | Issue e-tickets — one per passenger covering all flight segments; request must include per-passenger fare construction with a valid IATA linear fare calculation string and tax breakdown; triggers a `TicketIssued` accounting event |
| `GET` | `/v1/tickets/{eTicketNumber}/coupons/{couponNumber}/value` | Return the derived attributed value for a single coupon (fareShare, taxShare, total); value is computed from the fare construction and tax breakdown, never stored |
| `PATCH` | `/v1/tickets/{eTicketNumber}/void` | Void an issued e-ticket (used on flight change, cancellation, or IROPS); triggers a `TicketVoided` accounting event |
| `POST` | `/v1/tickets/reissue` | Reissue e-tickets following a passenger detail update, seat change, or flight change; voids the original ticket and issues a replacement |

### Manifest

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/manifest` | Write operational manifest entries (`delivery.Manifest` records) at booking confirmation or after rebooking |
| `PUT` | `/v1/manifest` | Update manifest entries following a post-booking seat change |
| `PATCH` | `/v1/manifest/{bookingRef}` | Update manifest entries for a booking; used to record check-in status (OLCI) and to update SSR codes following a self-serve SSR change |
| `PATCH` | `/v1/manifest/{bookingRef}/flight` | Update departure/arrival times on manifest entries (used by Operations API for flight delay handling) |
| `DELETE` | `/v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}` | Remove all manifest entries for a specific flight and booking (used on change or cancellation) |
| `GET` | `/v1/manifest` | Retrieve the full passenger manifest for a flight (used by Operations API for IROPS cancellation rebooking and by DCS for check-in validation) |

### Documents

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/documents` | Issue an ancillary document (`delivery.Document` record) for a post-sale ancillary purchase (e.g. seat selection or excess baggage); triggers a `DocumentIssued` accounting event; called by Retail API after successful ancillary payment settlement |
| `PATCH` | `/v1/documents/{documentNumber}/void` | Void an ancillary document (used on voluntary cancellation or IROPS when ancillary charges are refunded); triggers a `DocumentVoided` accounting event |

### Boarding Cards

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/boarding-cards` | Generate boarding cards and BCBP barcode strings for checked-in passengers |

---

## Ancillary Microservice — [Full API Spec](api-specs/ancillary-microservice.md)

The Ancillary microservice owns seat ancillaries (seatmap definitions, fleet-wide seat pricing, and seat offer generation) and bag ancillaries (checked baggage policies, bag pricing, and bag offer generation). `SeatOfferId` and `BagOfferId` values are deterministic — generated on demand without offer storage.

**Seat — offer/query endpoints (called by Retail API during the booking path)**

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/seatmap/{aircraftType}` | Retrieve seatmap definition and cabin layout for an aircraft type (physical layout, seat attributes, cabin configuration only — no pricing or availability) |
| `GET` | `/v1/seat-offers?flightId={flightId}` | Generate and return priced seat offers for a specific flight; returns one `SeatOfferId` per selectable seat with current price and seat attributes; `SeatOfferId` is deterministic (stateless — no DB write required); used by Retail API to build the full seatmap response (layout + pricing + availability) for the channel |
| `GET` | `/v1/seat-offers/{seatOfferId}` | Retrieve and validate a specific seat offer by deterministic ID; confirms the pricing rule that generated the ID is still active and returns the current price; used by Retail API when adding a seat to a basket or confirming a seat purchase |

**Seat — admin endpoints (called from a future Contact Centre admin app — not channel-facing)**

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/aircraft-types` | List all aircraft types |
| `POST` | `/v1/aircraft-types` | Create a new aircraft type record |
| `GET` | `/v1/aircraft-types/{aircraftTypeCode}` | Retrieve an aircraft type by code |
| `PUT` | `/v1/aircraft-types/{aircraftTypeCode}` | Update an aircraft type record |
| `DELETE` | `/v1/aircraft-types/{aircraftTypeCode}` | Delete an aircraft type (only permitted if no active seatmaps reference it) |
| `GET` | `/v1/seatmaps` | List all seatmap definitions |
| `POST` | `/v1/seatmaps` | Create a new seatmap definition for an aircraft type |
| `GET` | `/v1/seatmaps/{seatmapId}` | Retrieve a seatmap definition by ID |
| `PUT` | `/v1/seatmaps/{seatmapId}` | Replace the cabin layout of an existing seatmap (increments `Version`) |
| `DELETE` | `/v1/seatmaps/{seatmapId}` | Delete a seatmap definition |
| `GET` | `/v1/seat-pricing` | List all seat pricing rules |
| `POST` | `/v1/seat-pricing` | Create a new seat pricing rule (`cabinCode`, `seatPosition`, `currencyCode`, `price`, `validFrom`, `validTo`) |
| `GET` | `/v1/seat-pricing/{seatPricingId}` | Retrieve a seat pricing rule by ID |
| `PUT` | `/v1/seat-pricing/{seatPricingId}` | Update a seat pricing rule |
| `DELETE` | `/v1/seat-pricing/{seatPricingId}` | Delete a seat pricing rule |

**Bag — offer/query endpoints (called by Retail API during the booking path)**

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/bags/offers?inventoryId={inventoryId}&cabinCode={cabinCode}` | Generate and return the free bag policy and priced bag offers for a flight and cabin; returns one `BagOfferId` per available bag tier; `BagOfferId` is deterministic (stateless — no DB write required) |
| `GET` | `/v1/bags/offers/{bagOfferId}` | Retrieve and validate a bag offer by deterministic ID; confirms the pricing rule that generated the ID is still active and returns the current price; used by Retail API when adding bags to a basket or confirming a bag purchase |

**Bag — admin endpoints (called from a future Contact Centre admin app — not channel-facing)**

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/bag-policies` | List all bag allowance policies |
| `POST` | `/v1/bag-policies` | Create a new bag allowance policy (`cabinCode`, `freeBagsIncluded`, `maxWeightKgPerBag`) |
| `GET` | `/v1/bag-policies/{policyId}` | Retrieve a bag policy by ID |
| `PUT` | `/v1/bag-policies/{policyId}` | Update a bag allowance policy |
| `DELETE` | `/v1/bag-policies/{policyId}` | Delete a bag allowance policy |
| `GET` | `/v1/bag-pricing` | List all bag pricing rules |
| `POST` | `/v1/bag-pricing` | Create a new bag pricing rule (`bagSequence`, `currencyCode`, `price`, `validFrom`, `validTo`) |
| `GET` | `/v1/bag-pricing/{pricingId}` | Retrieve a bag pricing rule by ID |
| `PUT` | `/v1/bag-pricing/{pricingId}` | Update a bag pricing rule |
| `DELETE` | `/v1/bag-pricing/{pricingId}` | Delete a bag pricing rule |

---

## Identity Microservice — [Full API Spec](api-specs/identity-microservice.md)

### Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/auth/login` | Validate credentials (email and Argon2id password hash); returns JWT access token, refresh token, and `identityReference` on success; increments failed attempt counter and locks account at threshold on failure |
| `POST` | `/v1/auth/refresh` | Validate and rotate a refresh token (single-use); returns new access token and replacement refresh token |
| `POST` | `/v1/auth/logout` | Revoke the active refresh token for a session |
| `POST` | `/v1/auth/password/reset-request` | Generate a time-limited single-use reset token (1-hour TTL) and dispatch to the registered email if found; always returns `202 Accepted` to prevent account enumeration |
| `POST` | `/v1/auth/password/reset` | Validate a reset token, update the password hash, unlock the account, and revoke all active refresh tokens |

### Account Management

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/accounts` | Create a new login account (called by Loyalty API during registration, after the Customer record is already created; accepts `customerId` to associate the identity with the existing customer) |
| `DELETE` | `/v1/accounts/{userAccountId}` | Delete a login account (used for registration rollback — called by Loyalty API if the post-identity `PATCH` to link the `identityReference` on the Customer record fails) |
| `GET` | `/v1/accounts/{userAccountId}` | Retrieve account summary (user account ID, email, and email verification status) by identity account ID; called by the Loyalty API during points transfer to verify the recipient's email address against their loyalty number |
| `GET` | `/v1/accounts/by-email/{email}` | Retrieve account summary by email address; returns `404` if no account exists for the given email; called by the Loyalty API during admin customer search to resolve a customer by email |
| `GET`, `POST` | `/v1/accounts/{userAccountId}/verify-email` | Mark an email address as verified; GET is used when the user follows the link in their confirmation email, POST is used for direct API calls |
| `POST` | `/v1/accounts/{identityReference}/email/change-request` | Initiate an email change; generates verification token and sends link to new address |
| `POST` | `/v1/email/verify` | Validate a change-request token; updates email and invalidates all active refresh tokens |

---

## Customer Microservice — [Full API Spec](api-specs/customer-microservice.md)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/customers` | Create a new loyalty account (called by Loyalty API as the **first** step of registration, before the Identity account is created; `identityReference` is initially `null` and linked in a subsequent `PATCH`) |
| `GET` | `/v1/customers/{loyaltyNumber}` | Retrieve a customer profile, tier status, and points balance |
| `GET` | `/v1/customers/by-identity/{identityId}` | Retrieve a customer profile by Identity MS account ID; used by the Loyalty API during login to resolve the loyalty number |
| `PATCH` | `/v1/customers/{loyaltyNumber}` | Update profile fields (name, date of birth, nationality, phone, preferred language, passport number, passport issue date, passport issuing country, Known Traveller Number, `identityReference`); also used by Loyalty API during registration to link the `identityReference` returned from the Identity MS after account creation |
| `DELETE` | `/v1/customers/{loyaltyNumber}` | Delete a customer record (used for registration rollback — called by Loyalty API if Identity account creation fails, or if the subsequent `PATCH` to link the `identityReference` fails) |
| `GET` | `/v1/customers/{loyaltyNumber}/transactions` | Retrieve paginated points transaction history |
| `POST` | `/v1/customers/{loyaltyNumber}/points/authorise` | Authorise a points redemption hold for a reward booking; verifies `PointsBalance >= requestedPoints`, places hold, returns `RedemptionReference` |
| `POST` | `/v1/customers/{loyaltyNumber}/points/settle` | Settle a held points redemption; decrements `PointsBalance`, appends `Redeem` transaction to ledger |
| `POST` | `/v1/customers/{loyaltyNumber}/points/reverse` | Reverse a held points redemption; releases held points back to available balance |
| `POST` | `/v1/customers/{loyaltyNumber}/points/reinstate` | Reinstate points to a customer's balance following a completed cancellation or flight change; appends a `Reinstate` transaction to the ledger |
| `POST` | `/v1/customers/{loyaltyNumber}/points/add` | Add points directly to a customer's balance using a caller-supplied transaction type and description; validates `transactionType` against the permitted set (`Earn`, `Redeem`, `Adjustment`, `Expiry`, `Reinstate`) |
| `POST` | `/v1/customers/{loyaltyNumber}/points/transfer` | Transfer points from the sender's account to a recipient account; debits sender and credits recipient atomically; appends an `Adjustment` transaction to each account with the counterpart's loyalty number in the description; called by the Loyalty API after email/loyalty-number verification |

---

## User Microservice

The User microservice owns employee user accounts for the Apex Air reservation system. It is the sole store of employee credentials and account state. It has no dependency on the Identity microservice (which manages loyalty member credentials) — the two domains are completely separate.

### Authentication

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/users/login` | Authenticate an employee with username and password; returns a JWT access token (15-minute TTL) on success; increments failed attempt counter and locks account at threshold (5 attempts) on failure |

### User management

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/users` | Create a new employee user account (`username`, `email`, `password`, `firstName`, `lastName`); returns `201 Created` with the new `userId`; rejects with `409 Conflict` if `username` or `email` is already registered |
| `GET` | `/v1/users` | Retrieve all employee user accounts; `passwordHash` is never returned |
| `DELETE` | `/v1/users/{userId}` | Permanently delete an employee user account; returns `204 No Content` on success; `404` if user does not exist |

---

## Airport API

> **Scope:** Future release. The Azure Functions project is scaffolded in this release with `/health` and hello-world endpoints only. No business logic is implemented.

The Airport API will serve the Airport App for non-OLCI check-in, gate management, seat assignment, and ground handling operations.

---

## Finance API

> **Scope:** Future release. The Azure Functions project is scaffolded in this release with `/health` and hello-world endpoints only. No business logic is implemented.

The Finance API will serve the Accounting System App, proxying read-only queries to the Accounting microservice for financial reporting (balance sheet, P&L views, audit trail).

---

## Accounting Microservice

> **Scope:** Future release. The Accounting microservice Azure Functions project is scaffolded in this release with event subscription stubs only. No financial reporting endpoints are implemented; the Finance API has no callable endpoints at this stage.

The Accounting microservice is a pure event consumer. It has no synchronous API surface. All input arrives via Azure Service Bus; it reacts to the following events:

| Event | Publisher | Action |
|-------|-----------|--------|
| `OrderConfirmed` | Order MS | Record fare revenue (or points liability for reward bookings) |
| `OrderChanged` | Order MS | Adjust revenue or points liability records |
| `OrderCancelled` | Order MS | Record refund and reverse points liability if applicable |
| `TicketIssued` | Delivery MS | Record ticket issuance for audit |
| `TicketVoided` | Delivery MS | Record ticket void |
| `DocumentIssued` | Delivery MS | Record ancillary (seat/bag) revenue |
| `DocumentVoided` | Delivery MS | Reverse ancillary revenue record |

---

## Timatic Simulator — [Full API Spec](api-specs/timatic-simulator.md)

> **Scope:** Simulator only — happy-path responses. No actual document, visa, or APIS validation is performed. Designed to be called by the `DocumentVerification` microservice during online check-in.

Mimics the IATA AutoCheck REST API. All endpoints use `Authorization: Bearer <token>` authentication. The expected token is stored in the `Timatic:ApiToken` Azure App Setting; incoming and stored tokens are compared as SHA-256 hashes (no plaintext comparison). Routes have no `/api` prefix — they are served at the paths below verbatim.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/autocheck/v1/documentcheck` | Document check — validates passport, visa, and health document requirements for a journey. Called at booking or OCI entry. Returns `status: OK`, no visa required, no health document required, and any applicable advisories (e.g. ESTA for US arrivals) |
| `POST` | `/autocheck/v1/apischeck` | APIS check — validates Advance Passenger Information when a passenger submits check-in data. Returns `apisStatus: ACCEPTED`, `fineRisk: LOW`, and a generated audit reference |
| `POST` | `/autocheck/v1/realtimecheck` | Realtime gate check — called when a gate agent scans the passenger MRZ. Parses the ICAO TD3 MRZ lines and returns a `decision: GO` with the extracted document fields |
