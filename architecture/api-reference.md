# API Endpoint Reference

---

## Retail API

### Search & Basket

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/search/slice` | Search for available flights for a single directional slice |
| `POST` | `/v1/basket` | Create a new basket with one or more offer IDs |
| `PUT` | `/v1/basket/{basketId}/passengers` | Add or update passenger details on a basket |
| `PUT` | `/v1/basket/{basketId}/seats` | Add or update seat selections on a basket |
| `POST` | `/v1/basket/{basketId}/confirm` | Confirm a basket, triggering payment, ticketing, and order creation |

### Orders

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/orders/retrieve` | Retrieve a confirmed order by booking reference and passenger name |
| `PATCH` | `/v1/orders/{bookingRef}/passengers` | Correct or update passenger details on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/seats` | Add or change seat selection on a confirmed order |

### Flights & Seatmaps

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/flights/{flightId}/seatmap` | Retrieve seatmap with pricing and availability for a flight |

### Check-in

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/checkin/retrieve` | Retrieve booking details to begin the check-in flow |
| `PATCH` | `/v1/checkin/{bookingRef}/seats` | Update seat assignment during check-in (no charge) |
| `POST` | `/v1/checkin/{bookingRef}` | Submit check-in for all passengers, recording APIS data |

---

## Loyalty API

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/register` | Register a new loyalty programme member |
| `GET` | `/v1/customers/{loyaltyNumber}` | Retrieve a customer's profile and points balance |
| `GET` | `/v1/customers/{loyaltyNumber}/transactions` | Retrieve paginated points transaction history |

---

## Offer Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/search` | Search flight inventory and return priced offers |
| `GET` | `/v1/offers/{offerId}` | Retrieve a stored offer snapshot by ID |
| `GET` | `/v1/flights/{flightId}/seat-availability` | Retrieve current seat availability for a flight |
| `POST` | `/v1/flights/{flightId}/seat-reservations` | Reserve seats against a basket or check-in |
| `POST` | `/v1/inventory/release` | Decrement available inventory and mark seats as sold |
| `PATCH` | `/v1/flights/{flightId}/seat-availability` | Update seat status (e.g. to checked-in) |

---

## Order Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/basket` | Create a new basket |
| `PUT` | `/v1/basket/{basketId}/passengers` | Update passenger details on a basket |
| `PUT` | `/v1/basket/{basketId}/seats` | Update seat selections on a basket |
| `POST` | `/v1/orders` | Confirm a basket and create a permanent order record |
| `POST` | `/v1/orders/retrieve` | Retrieve a confirmed order by booking reference and passenger name |
| `PATCH` | `/v1/orders/{bookingRef}/passengers` | Update passenger details on a confirmed order |
| `PATCH` | `/v1/orders/{bookingRef}/seats` | Update seat assignments on a confirmed order |
| `POST` | `/v1/orders/{bookingRef}/checkin` | Record check-in status and APIS data for passengers |

---

## Payment Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/payment/authorise` | Authorise a card payment |
| `POST` | `/v1/payment/{paymentReference}/settle` | Settle a previously authorised payment |

---

## Delivery Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/tickets` | Issue e-tickets for all passengers and flight segments in a basket |
| `POST` | `/v1/tickets/reissue` | Reissue e-tickets following a name correction or seat change |
| `POST` | `/v1/manifest` | Write flight manifest entries at booking confirmation |
| `PUT` | `/v1/manifest` | Update manifest entries following a post-booking seat change |
| `PATCH` | `/v1/manifest/{bookingRef}` | Update check-in status on manifest entries |
| `POST` | `/v1/boarding-cards` | Generate boarding cards and BCBP barcode strings |

---

## Seat Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/v1/seatmap/{aircraftType}` | Retrieve seatmap definition and seat offers for an aircraft type |

---

## Identity Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/accounts` | Create a new login account |
| `DELETE` | `/v1/accounts/{userAccountId}` | Delete a login account (used for registration rollback) |
| `POST` | `/v1/accounts/{userAccountId}/verify-email` | Mark an email address as verified |

---

## Customer Microservice

| Method | Endpoint | Description |
|--------|----------|-------------|
| `POST` | `/v1/customers` | Create a new loyalty account |
| `GET` | `/v1/customers/{loyaltyNumber}` | Retrieve a customer profile and points balance |
| `GET` | `/v1/customers/{loyaltyNumber}/transactions` | Retrieve paginated points transaction history |
