# API Endpoint Reference

---

## Retail API

### Search & Basket

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/search/slice` | Search for available direct flights for a single directional slice (outbound or inbound); returns one offer per available cabin class, each with a unique `OfferId` |
| `POST` | `/v1/search/connecting` | Search for connecting itinerary options via the LHR hub (e.g. DEL → JFK via LHR); assembles pairs of per-segment offers from the Offer MS, applies minimum connect time (60 min), and returns combined itinerary options each carrying two `OfferIds` — one per leg |
| `POST` | `/v1/basket` | Create a new basket with one or more flight offer IDs; initiates the bookflow |
| `PUT` | `/v1/basket/{basketId}/passengers` | Add or update passenger details on a basket |
| `PUT` | `/v1/basket/{basketId}/seats` | Add or update seat selections on a basket during the bookflow |
| `PUT` | `/v1/basket/{basketId}/bags` | Add or update bag selections on a basket during the bookflow; accepts bag offer IDs per passenger per segment; updates `TotalBagAmount` on the basket |
| `POST` | `/v1/basket/{basketId}/confirm` | Confirm a basket, triggering payment (fare + any seat/bag ancillaries as separate transactions), ticketing, and order creation |

### Orders

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/orders/retrieve` | Retrieve a confirmed order by booking reference and passenger name |
| `PATCH` | `/v1/orders/{bookingRef}/passengers` | Correct or update passenger details on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/seats` | Add or change seat selection on a confirmed order (post-sale, charged) |
| `POST` | `/v1/orders/{bookingRef}/change` | Change a confirmed flight to a new itinerary; collects add-collect and change fee if applicable |
| `POST` | `/v1/orders/{bookingRef}/cancel` | Cancel a confirmed booking; initiates refund if fare conditions permit |
| `POST` | `/v1/orders/{bookingRef}/bags` | Add or update checked bag selection on a confirmed order |

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

### Email Verification

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/email/verify` | Verify a new email address using a time-limited token (step 2 of email change flow) |

---

## Loyalty API

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/register` | Register a new loyalty programme member, creating linked Identity and Customer records |
| `GET` | `/v1/customers/{loyaltyNumber}` | Retrieve a customer's profile, tier status, and points balance |
| `GET` | `/v1/customers/{loyaltyNumber}/transactions` | Retrieve paginated points transaction history |
| `PATCH` | `/v1/customers/{loyaltyNumber}/profile` | Update profile details (name, date of birth, nationality, phone, preferred language) |
| `POST` | `/v1/customers/{loyaltyNumber}/email/change-request` | Initiate an email address change; sends verification link to the new address |

---

## Disruption API

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/disruptions/delay` | Notify the system of a flight delay; updates all affected orders and manifests |
| `POST` | `/v1/disruptions/cancellation` | Notify the system of a flight cancellation; takes flight off sale and rebooking all affected passengers |

---

## Offer Microservice

The Offer microservice operates on individual flight **segments** only. It has no concept of a multi-segment connecting itinerary; connecting itinerary assembly (pairing legs, enforcing minimum connect time, combining prices) is the responsibility of the Retail API orchestration layer.

> **Code share (future):** When code share flights are introduced, `POST /v1/search` responses will include optional `operatingCarrier` and `operatingFlightNumber` fields alongside the marketing flight details. These fields are intentionally omitted for own-metal flights in the initial release but must be handled by all consumers from launch.

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/search` | Search flight inventory for a single segment (origin, destination, date, cabin, pax count) and return priced, stored-offer-snapshotted offers; called once per leg by the Retail API for both direct (`/v1/search/slice`) and connecting (`/v1/search/connecting`) searches |
| `GET` | `/v1/offers/{offerId}` | Retrieve a stored offer snapshot by ID (used by Order MS at basket creation; validates `IsConsumed = 0` and `ExpiresAt > now` before returning) |
| `GET` | `/v1/flights/{flightId}/seat-availability` | Retrieve current seat availability for a flight |
| `POST` | `/v1/flights/{flightId}/seat-reservations` | Reserve seats against a basket or check-in |
| `PATCH` | `/v1/flights/{flightId}/seat-availability` | Update seat status on a flight (e.g. to checked-in) |
| `POST` | `/v1/inventory/hold` | Hold seats against a new or replacement booking (increments SeatsHeld) |
| `POST` | `/v1/inventory/release` | Release held or sold seats back to available inventory (used on cancel or change) |
| `PATCH` | `/v1/inventory/cancel` | Close a cancelled flight's inventory (sets SeatsAvailable = 0, status = Cancelled) |

---

## Order Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/basket` | Create a new basket |
| `PUT` | `/v1/basket/{basketId}/passengers` | Update passenger details on a basket |
| `PUT` | `/v1/basket/{basketId}/seats` | Update seat selections on a basket during the bookflow |
| `PUT` | `/v1/basket/{basketId}/bags` | Add or update bag selections on a basket during the bookflow; updates `TotalBagAmount` |
| `POST` | `/v1/orders` | Confirm a basket and create a permanent order record |
| `POST` | `/v1/orders/retrieve` | Retrieve a confirmed order by booking reference and passenger name |
| `GET` | `/v1/orders` | Query orders by flight number and departure date (used by Disruption API) |
| `PATCH` | `/v1/orders/{bookingRef}/passengers` | Update passenger details on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/seats` | Update seat assignments on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/segments` | Update segment departure/arrival times (used by Disruption API for delays) |
| `PATCH` | `/v1/orders/{bookingRef}/change` | Apply a confirmed flight change, recording new segment, add-collect, and payment reference |
| `PATCH` | `/v1/orders/{bookingRef}/cancel` | Mark an order as cancelled with reason and any cancellation fee |
| `PATCH` | `/v1/orders/{bookingRef}/rebook` | Rebook a passenger onto a replacement flight (used by Disruption API for cancellations) |
| `PATCH` | `/v1/orders/{bookingRef}/bags` | Add or update bag order items on a confirmed order |
| `POST` | `/v1/orders/{bookingRef}/checkin` | Record check-in status and APIS data for passengers |

---

## Payment Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/payment/authorise` | Authorise a card payment; returns a PaymentReference |
| `POST` | `/v1/payment/{paymentReference}/settle` | Settle a previously authorised payment |
| `POST` | `/v1/payment/{paymentReference}/refund` | Refund a settled payment in full or in part (used on voluntary cancellation) |

---

## Delivery Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/tickets` | Issue e-tickets for all passengers and flight segments in a basket |
| `PATCH` | `/v1/tickets/{eTicketNumber}/void` | Void an issued e-ticket (used on flight change, cancellation, or IROPS) |
| `POST` | `/v1/tickets/reissue` | Reissue e-tickets following a passenger detail update, seat change, or flight change |
| `POST` | `/v1/manifest` | Write flight manifest entries at booking confirmation or after rebooking |
| `PUT` | `/v1/manifest` | Update manifest entries following a post-booking seat change |
| `PATCH` | `/v1/manifest/{bookingRef}` | Update check-in status on manifest entries |
| `PATCH` | `/v1/manifest/{bookingRef}/flight` | Update departure/arrival times on manifest entries (used by Disruption API for delays) |
| `DELETE` | `/v1/manifest/{bookingRef}/flight/{flightNumber}/{departureDate}` | Remove all manifest entries for a specific flight and booking (used on change or cancellation) |
| `GET` | `/v1/manifest` | Retrieve the full passenger manifest for a flight (used by Disruption API for cancellation rebooking) |
| `POST` | `/v1/boarding-cards` | Generate boarding cards and BCBP barcode strings for checked-in passengers |

---

## Seat Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/seatmap/{aircraftType}` | Retrieve seatmap definition, cabin layout, and seat offers for an aircraft type |

---

## Bag Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/bags/offers` | Retrieve the free bag policy and priced bag offers for a flight and cabin (`?inventoryId=&cabinCode=`) |

---

## Identity Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/accounts` | Create a new login account (called by Loyalty API during registration) |
| `DELETE` | `/v1/accounts/{userAccountId}` | Delete a login account (used for registration rollback on Customer MS failure) |
| `POST` | `/v1/accounts/{userAccountId}/verify-email` | Mark an email address as verified |
| `POST` | `/v1/accounts/{identityReference}/email/change-request` | Initiate an email change; generates verification token and sends link to new address |
| `POST` | `/v1/email/verify` | Validate a change-request token; updates email and invalidates all active refresh tokens |

---

## Customer Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/customers` | Create a new loyalty account (called by Loyalty API during registration) |
| `GET` | `/v1/customers/{loyaltyNumber}` | Retrieve a customer profile, tier status, and points balance |
| `PATCH` | `/v1/customers/{loyaltyNumber}` | Update profile fields (name, date of birth, nationality, phone, preferred language) |
| `GET` | `/v1/customers/{loyaltyNumber}/transactions` | Retrieve paginated points transaction history |
