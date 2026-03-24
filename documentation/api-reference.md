# API Endpoint Reference

> **HTTP verb convention for Retail API vs microservice endpoints:** The Retail API is the channel-facing orchestration layer. Where the Retail API and an internal microservice share the same URL path (e.g. `/v1/orders/{bookingRef}/cancel`), the Retail API endpoint uses `POST` (initiating the orchestration flow) while the corresponding internal microservice endpoint uses `PATCH` (applying a partial state update). These are distinct endpoints on distinct services; the verb difference is intentional and consistent throughout.

> **Microservice authentication:** All orchestration-to-microservice calls are authenticated using an Azure Function Host Key in the `x-functions-key` HTTP header. All microservices currently share the same key. See [Microservice Authentication — Host Keys](api.md#microservice-authentication--host-keys) in `api.md` for the full mechanism.

---

## Retail API — [Full API Spec](api-specs/retail-api.md)

### Search & Basket

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/search/slice` | Search for available direct flights for a single directional slice (outbound or inbound); returns one offer per available cabin class, each with a unique `OfferId` |
| `POST` | `/v1/search/connecting` | Search for connecting itinerary options via the LHR hub (e.g. DEL → JFK via LHR); assembles pairs of per-segment offers from the Offer MS, applies minimum connect time (60 min), and returns combined itinerary options each carrying two `OfferIds` — one per leg |
| `POST` | `/v1/basket` | Create a new basket with one or more flight offer IDs; initiates the bookflow. For reward bookings, accepts `bookingType=Reward` and `loyaltyNumber`; verifies points balance via Customer MS before creating the basket |
| `PUT` | `/v1/basket/{basketId}/passengers` | Add or update passenger details on a basket |
| `PUT` | `/v1/basket/{basketId}/seats` | Add or update seat selections on a basket during the bookflow |
| `PUT` | `/v1/basket/{basketId}/bags` | Add or update bag selections on a basket during the bookflow; accepts bag offer IDs per passenger per segment; updates `TotalBagAmount` on the basket |
| `PUT` | `/v1/basket/{basketId}/ssrs` | Add or update Special Service Request selections on a basket during the bookflow; accepts SSR code, passenger reference, and segment reference per selection; no charge — basket total is unchanged |
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
| `GET` | `/v1/ssr/options` | Retrieve all active SSR codes, labels, and categories (Meal, Mobility, Accessibility) from `retail.SsrCatalogue`; accepts optional `cabinCode` and `flightNumbers` query parameters |
| `POST` | `/v1/ssr/options` | Create a new SSR catalogue entry (`ssrCode`, `label`, `category`); admin endpoint — not channel-facing |
| `PUT` | `/v1/ssr/options/{ssrCode}` | Update an existing SSR entry (label or category); `ssrCode` is immutable; admin endpoint |
| `DELETE` | `/v1/ssr/options/{ssrCode}` | Deactivate an SSR code (`IsActive = 0`); existing order items referencing the code are unaffected; admin endpoint |

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
| `PATCH` | `/v1/customers/{loyaltyNumber}/profile` | Update profile details (name, date of birth, nationality, phone, preferred language) |
| `POST` | `/v1/customers/{loyaltyNumber}/points/authorise` | Authorise a points redemption hold against the customer's balance for a reward booking; returns a `RedemptionReference`; verifies sufficient balance before placing hold |
| `POST` | `/v1/customers/{loyaltyNumber}/points/settle` | Settle a previously authorised points redemption; deducts points from balance and appends a `Redeem` transaction to the loyalty ledger |
| `POST` | `/v1/customers/{loyaltyNumber}/points/reverse` | Reverse a points authorisation hold, returning held points to the customer's available balance; used on booking failure rollback (e.g. ticketing failure after points authorisation) |
| `POST` | `/v1/customers/{loyaltyNumber}/points/reinstate` | Reinstate points to a customer's balance following a completed cancellation or flight change that results in a net reduction in points redeemed; appends a `Reinstate` transaction to the loyalty ledger; used by Retail API on voluntary cancellation (reward bookings) and by Retail API and Disruption API when a flight change or IROPS rebooking reduces the points cost |
| `POST` | `/v1/customers/{loyaltyNumber}/points/add` | Add points directly to a customer's balance; caller supplies `transactionType` (must be one of `Earn`, `Redeem`, `Adjustment`, `Expiry`, `Reinstate`) and `description`; intended for manual adjustments and testing |
| `GET` | `/v1/accounts/{userAccountId}/verify-email` | Verify ownership of an email address; called when the user follows the link in their registration confirmation email; delegates to Identity MS |
| `POST` | `/v1/customers/{loyaltyNumber}/email/change-request` | Initiate an email address change; sends verification link to the new address (step 1 of email change flow) |
| `POST` | `/v1/email/verify` | Verify a new email address using a time-limited token (step 2 of email change flow); delegates to Identity MS |

---

## Disruption API — [Full API Spec](api-specs/disruption-api.md)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/disruptions/delay` | Notify the system of a flight delay; updates all affected orders and manifests synchronously; returns `200 OK` when all records are updated |
| `POST` | `/v1/disruptions/cancellation` | Notify the system of a flight cancellation; **synchronously** closes flight inventory (Offer MS) and returns `202 Accepted`; per-passenger rebooking is processed **asynchronously** via Service Bus — each affected booking is published as an individual message and processed independently to prevent a single failure blocking the entire cohort; for the 72-hour no-rebooking window scenario, passengers are notified and bookings cancelled with full IROPS refund |

---

## Operations API — [Full API Spec](api-specs/operations-api.md)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/schedules` | Create a flight schedule; orchestrates: (1) persists the schedule definition via Schedule MS, (2) generates `FlightInventory` and `Fare` records in the Offer domain via Offer MS for every operating date within the `ValidFrom`–`ValidTo` window that matches the `daysOfWeek` pattern, (3) updates the `FlightsCreated` count on the schedule record; returns `scheduleId` and the count of flights created |

---

## Schedule Microservice — [Full API Spec](api-specs/schedule-microservice.md)

The Schedule microservice is the internal persistence layer for flight schedule definitions. It is called by the Operations API during schedule creation to store the schedule record and to update the `FlightsCreated` count after the Operations API has generated inventory in the Offer domain.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/schedules` | Persist a validated flight schedule definition (`FlightSchedule`, `ScheduleCabin`, `ScheduleFare` records); enumerate operating dates in the `ValidFrom`–`ValidTo` window matching the `DaysOfWeek` bitmask; returns `scheduleId` and the list of operating dates |
| `PATCH` | `/v1/schedules/{scheduleId}` | Update the `FlightsCreated` count on a schedule record after the Operations API has completed bulk `FlightInventory` and `Fare` generation in the Offer domain |

---

## Offer Microservice — [Full API Spec](api-specs/offer-microservice.md)

The Offer microservice operates on individual flight **segments** only. It has no concept of a multi-segment connecting itinerary; connecting itinerary assembly (pairing legs, enforcing minimum connect time, combining prices) is the responsibility of the Retail API orchestration layer.

> **Code share (future):** When code share flights are introduced, `POST /v1/search` responses will include optional `operatingCarrier` and `operatingFlightNumber` fields alongside the marketing flight details. These fields are intentionally omitted for own-metal flights in the initial release but must be handled by all consumers from launch.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/flights` | Create a new flight inventory record for a specific operating date and cabin; called by the Schedule MS during schedule generation; initialises `SeatsAvailable = TotalSeats`, `SeatsHeld = 0`, `SeatsSold = 0`; returns `inventoryId` |
| `POST` | `/v1/flights/{inventoryId}/fares` | Add a fare definition to an existing flight inventory record; called by the Schedule MS once per fare per cabin per operating date during schedule generation; returns `fareId` |
| `POST` | `/v1/search` | Search flight inventory for a single segment (origin, destination, date, cabin, pax count) and return priced, stored-offer-snapshotted offers; called once per leg by the Retail API for both direct (`/v1/search/slice`) and connecting (`/v1/search/connecting`) searches |
| `GET` | `/v1/offers/{offerId}` | Retrieve a stored offer snapshot by ID (used by Retail API at basket creation; validates `IsConsumed = 0` and `ExpiresAt > now` before returning) |
| `GET` | `/v1/flights/{flightId}/seat-availability` | Retrieve current seat availability status for a flight — returns one entry per selectable seat with `SeatOfferId` (deterministic) and availability status (`available`, `held`, or `sold`) based on `offer.FlightInventory`; does **not** return pricing (pricing is owned by the Seat MS via `GET /v1/seat-offers?flightId=`); Retail API merges this availability data with the Seat MS offer response and the seatmap layout before returning to the channel |
| `POST` | `/v1/flights/{flightId}/seat-reservations` | Reserve seats against a basket or check-in |
| `PATCH` | `/v1/flights/{flightId}/seat-availability` | Update seat status on a flight (e.g. to checked-in) |
| `POST` | `/v1/inventory/hold` | Hold seats against a new or replacement booking (increments SeatsHeld; decrements SeatsAvailable) |
| `POST` | `/v1/inventory/sell` | Convert held seats to sold at order confirmation (decrements SeatsHeld; increments SeatsSold; SeatsAvailable unchanged) |
| `POST` | `/v1/inventory/release` | Release held or sold seats back to available inventory (increments SeatsAvailable; decrements SeatsHeld or SeatsSold — used on voluntary cancel, flight change rollback, and basket expiry) |
| `PATCH` | `/v1/inventory/cancel` | Close a cancelled flight's inventory (sets SeatsAvailable = 0, status = Cancelled; used by Disruption API on flight cancellation) |

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
| `POST` | `/v1/orders` | Confirm a basket and create a permanent order record; sets initial `OrderStatus = Confirmed` (or `OrderInit` for Contact Centre incremental booking flows) |
| `POST` | `/v1/orders/retrieve` | Retrieve a confirmed order by booking reference and passenger name |
| `GET` | `/v1/orders` | Query orders by flight number and departure date (used by Disruption API) |
| `PATCH` | `/v1/orders/{bookingRef}/passengers` | Update passenger details on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/seats` | Update seat assignments on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/segments` | Update segment departure/arrival times (used by Disruption API for delays) |
| `PATCH` | `/v1/orders/{bookingRef}/change` | Apply a confirmed flight change, recording new segment, add-collect, and payment reference |
| `PATCH` | `/v1/orders/{bookingRef}/cancel` | Mark an order as cancelled with reason and any cancellation fee |
| `PATCH` | `/v1/orders/{bookingRef}/rebook` | Rebook a passenger onto a replacement flight (used by Disruption API for cancellations) |
| `PATCH` | `/v1/orders/{bookingRef}/bags` | Add or update bag order items on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/ssrs` | Add, update, or remove SSR items on a confirmed order; publishes `OrderChanged` event |
| `POST` | `/v1/orders/{bookingRef}/checkin` | Record check-in status and APIS data for passengers |

---

## Payment Microservice — [Full API Spec](api-specs/payment-microservice.md)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/payment/authorise` | Authorise a card payment; returns a PaymentReference |
| `POST` | `/v1/payment/{paymentReference}/settle` | Settle a previously authorised payment |
| `POST` | `/v1/payment/{paymentReference}/refund` | Refund a settled payment in full or in part (used on voluntary cancellation) |

---

## Delivery Microservice — [Full API Spec](api-specs/delivery-microservice.md)

The Delivery microservice manages three distinct record types: **Tickets** (financial/accounting documents — the airline's equivalent of an e-ticket or EMD), **Manifest** (the operational passenger manifest used by ground handling and crew), and **Documents** (ancillary EMD-equivalent records for post-sale purchases such as seats and bags).

### Tickets

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/tickets` | Issue e-tickets (`delivery.Ticket` records) for all passengers and flight segments in a basket; each ticket carries the full fare snapshot and triggers a `TicketIssued` accounting event |
| `PATCH` | `/v1/tickets/{eTicketNumber}/void` | Void an issued e-ticket (used on flight change, cancellation, or IROPS); triggers a `TicketVoided` accounting event |
| `POST` | `/v1/tickets/reissue` | Reissue e-tickets following a passenger detail update, seat change, or flight change; voids the original ticket and issues a replacement |

### Manifest

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/manifest` | Write operational manifest entries (`delivery.Manifest` records) at booking confirmation or after rebooking |
| `PUT` | `/v1/manifest` | Update manifest entries following a post-booking seat change |
| `PATCH` | `/v1/manifest/{bookingRef}` | Update manifest entries for a booking; used to record check-in status (OLCI) and to update SSR codes following a self-serve SSR change |
| `PATCH` | `/v1/manifest/{bookingRef}/flight` | Update departure/arrival times on manifest entries (used by Disruption API for delays) |
| `DELETE` | `/v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}` | Remove all manifest entries for a specific flight and booking (used on change or cancellation) |
| `GET` | `/v1/manifest` | Retrieve the full passenger manifest for a flight (used by Disruption API for cancellation rebooking and by DCS for check-in validation) |

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

## Seat Microservice — [Full API Spec](api-specs/seat-microservice.md)

The Seat microservice owns seat offer generation. `SeatOfferId` values are deterministic (derived from `flightId`, `seatNumber`, and a pricing-rule hash) — no offer storage is required. Seat MS generates priced seat offers on demand; pricing rules are stored in `seat.SeatPricing`.

**Offer / query endpoints (called by Retail API during the booking path)**

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/seatmap/{aircraftType}` | Retrieve seatmap definition and cabin layout for an aircraft type (physical layout, seat attributes, cabin configuration only — no pricing or availability) |
| `GET` | `/v1/seat-offers?flightId={flightId}` | Generate and return priced seat offers for a specific flight; returns one `SeatOfferId` per selectable seat with current price and seat attributes; `SeatOfferId` is deterministic (stateless — no DB write required); used by Retail API to build the full seatmap response (layout + pricing + availability) for the channel |
| `GET` | `/v1/seat-offers/{seatOfferId}` | Retrieve and validate a specific seat offer by deterministic ID; confirms the pricing rule that generated the ID is still active and returns the current price; used by Retail API when adding a seat to a basket or confirming a seat purchase |

**Admin endpoints (called from a future Contact Centre admin app — not channel-facing)**

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

---

## Bag Microservice — [Full API Spec](api-specs/bags-microservice.md)

The Bag microservice owns bag pricing rules and bag offer generation. `BagOfferId` values are deterministic (derived from `inventoryId`, `cabinCode`, `bagCount`, and a pricing-rule hash) — no offer storage is required. Bag MS generates priced bag offers on demand from `bag.BagPricing` rules.

**Offer / query endpoints (called by Retail API during the booking path)**

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/bags/offers?inventoryId={inventoryId}&cabinCode={cabinCode}` | Generate and return the free bag policy and priced bag offers for a flight and cabin; returns one `BagOfferId` per available bag tier; `BagOfferId` is deterministic (stateless — no DB write required) |
| `GET` | `/v1/bags/offers/{bagOfferId}` | Retrieve and validate a bag offer by deterministic ID; confirms the pricing rule that generated the ID is still active and returns the current price; used by Retail API when adding bags to a basket or confirming a bag purchase |

**Admin endpoints (called from a future Contact Centre admin app — not channel-facing)**

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
| `PATCH` | `/v1/customers/{loyaltyNumber}` | Update profile fields (name, date of birth, nationality, phone, preferred language, `identityReference`); also used by Loyalty API during registration to link the `identityReference` returned from the Identity MS after account creation |
| `DELETE` | `/v1/customers/{loyaltyNumber}` | Delete a customer record (used for registration rollback — called by Loyalty API if Identity account creation fails, or if the subsequent `PATCH` to link the `identityReference` fails) |
| `GET` | `/v1/customers/{loyaltyNumber}/transactions` | Retrieve paginated points transaction history |
| `POST` | `/v1/customers/{loyaltyNumber}/points/authorise` | Authorise a points redemption hold for a reward booking; verifies `PointsBalance >= requestedPoints`, places hold, returns `RedemptionReference` |
| `POST` | `/v1/customers/{loyaltyNumber}/points/settle` | Settle a held points redemption; decrements `PointsBalance`, appends `Redeem` transaction to ledger |
| `POST` | `/v1/customers/{loyaltyNumber}/points/reverse` | Reverse a held points redemption; releases held points back to available balance |
| `POST` | `/v1/customers/{loyaltyNumber}/points/reinstate` | Reinstate points to a customer's balance following a completed cancellation or flight change; appends a `Reinstate` transaction to the ledger |
| `POST` | `/v1/customers/{loyaltyNumber}/points/add` | Add points directly to a customer's balance using a caller-supplied transaction type and description; validates `transactionType` against the permitted set (`Earn`, `Redeem`, `Adjustment`, `Expiry`, `Reinstate`) |

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
