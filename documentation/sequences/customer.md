# Customer — sequence diagrams

Covers loyalty customer profile management, points transactions, preferences, and points redemption authorisation. All flows originate from the Angular web frontend through the Loyalty Orchestration API.

---

## Get customer profile

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS

    Web->>LoyaltyAPI: GET /v1/customers/{loyaltyNumber}/profile
    Note over Web,LoyaltyAPI: Bearer token in Authorization header
    LoyaltyAPI->>CustomerMS: GET /api/v1/customers/{loyaltyNumber}
    CustomerMS-->>LoyaltyAPI: LoyaltyCustomer
    LoyaltyAPI-->>Web: ProfileResponse
    Note over LoyaltyAPI,Web: {loyaltyNumber, givenName, surname,<br/>email, tier, pointsBalance,<br/>memberSince, preferences}
```

---

## Update customer profile

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS

    Web->>LoyaltyAPI: PATCH /v1/customers/{loyaltyNumber}/profile
    Note over Web,LoyaltyAPI: Partial update: {givenName?, surname?,<br/>phoneNumber?, dateOfBirth?,<br/>passportNumber?, nationality?}
    LoyaltyAPI->>CustomerMS: PATCH /api/v1/customers/{loyaltyNumber}
    CustomerMS-->>LoyaltyAPI: Updated profile
    LoyaltyAPI-->>Web: ProfileResponse
```

---

## Get preferences

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS

    Web->>LoyaltyAPI: GET /v1/customers/{loyaltyNumber}/preferences
    LoyaltyAPI->>CustomerMS: GET /api/v1/customers/{loyaltyNumber}/preferences
    CustomerMS-->>LoyaltyAPI: PreferencesResponse
    LoyaltyAPI-->>Web: PreferencesResponse
    Note over LoyaltyAPI,Web: {seatPreference, mealPreference,<br/>preferredLanguage, marketingConsent}
```

---

## Update preferences

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS

    Web->>LoyaltyAPI: PUT /v1/customers/{loyaltyNumber}/preferences
    Note over Web,LoyaltyAPI: {seatPreference, mealPreference,<br/>preferredLanguage, marketingConsent}
    LoyaltyAPI->>CustomerMS: PUT /api/v1/customers/{loyaltyNumber}/preferences
    CustomerMS-->>LoyaltyAPI: Updated
    LoyaltyAPI-->>Web: 204 No Content
```

---

## Get points transaction history

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS

    Web->>LoyaltyAPI: GET /v1/customers/{loyaltyNumber}/transactions
    LoyaltyAPI->>CustomerMS: GET /api/v1/customers/{loyaltyNumber}/transactions
    CustomerMS-->>LoyaltyAPI: TransactionsResponse
    LoyaltyAPI-->>Web: [LoyaltyTransaction]
    Note over LoyaltyAPI,Web: [{transactionId, type (Earn/Redeem/Expire),<br/>points, description, transactedAt}]
```

---

## Get customer orders

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS

    Web->>LoyaltyAPI: GET /v1/customers/{loyaltyNumber}/orders
    LoyaltyAPI->>CustomerMS: GET /api/v1/customers/{loyaltyNumber}/orders
    CustomerMS-->>LoyaltyAPI: OrdersResponse
    LoyaltyAPI-->>Web: [CustomerOrderSummary]
    Note over LoyaltyAPI,Web: [{bookingReference, flightNumber,<br/>route, departureDate, status, pointsEarned}]
```

---

## Transfer points

The recipient loyalty number and email are cross-validated before the transfer executes to prevent misdirected transfers.

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS
    participant IdentityMS as Identity MS

    Web->>LoyaltyAPI: POST /v1/customers/{loyaltyNumber}/points/transfer
    Note over Web,LoyaltyAPI: {recipientLoyaltyNumber,<br/>recipientEmail, points}

    LoyaltyAPI->>CustomerMS: GET /api/v1/customers/{recipientLoyaltyNumber}
    CustomerMS-->>LoyaltyAPI: RecipientCustomer (identityId)

    LoyaltyAPI->>IdentityMS: GET /api/v1/accounts/{identityId}
    Note over LoyaltyAPI,IdentityMS: Verify recipient email matches<br/>registered identity email
    IdentityMS-->>LoyaltyAPI: IdentityAccount (email)

    Note over LoyaltyAPI: Validate email match; throw if mismatch

    LoyaltyAPI->>CustomerMS: POST /api/v1/customers/points/transfer
    Note over LoyaltyAPI,CustomerMS: {senderLoyaltyNumber,<br/>recipientLoyaltyNumber, points}
    CustomerMS-->>LoyaltyAPI: TransferResult

    LoyaltyAPI-->>Web: TransferPointsResponse
    Note over LoyaltyAPI,Web: {senderLoyaltyNumber, recipientLoyaltyNumber,<br/>pointsTransferred, senderNewBalance,<br/>recipientNewBalance, transferredAt}
```

---

## Delete account

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS
    participant IdentityMS as Identity MS

    Web->>LoyaltyAPI: DELETE /v1/customers/{loyaltyNumber}/account
    Note over Web,LoyaltyAPI: Bearer token in Authorization header
    LoyaltyAPI->>CustomerMS: DELETE /api/v1/customers/{loyaltyNumber}
    CustomerMS-->>LoyaltyAPI: Customer record deleted
    LoyaltyAPI->>IdentityMS: DELETE /api/v1/accounts/{identityId}
    IdentityMS-->>LoyaltyAPI: Identity account deleted
    LoyaltyAPI-->>Web: 204 No Content
```

---

## Points accrual (post-booking, internal)

Called from within `ConfirmBasketHandler` for revenue bookings where the customer is loyalty-enrolled. Not triggered directly by the web frontend.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant CustomerMS as Customer MS

    RetailAPI->>CustomerMS: POST /api/v1/customers/{loyaltyNumber}/points
    Note over RetailAPI,CustomerMS: {bookingReference, points,<br/>transactionType=Earn,<br/>description=Revenue booking accrual}
    CustomerMS-->>RetailAPI: Points recorded
```

---

## Points reinstatement (post-cancellation, internal)

Called from within `CancelOrderHandler` for reward bookings.

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant CustomerMS as Customer MS

    RetailAPI->>CustomerMS: POST /api/v1/customers/{loyaltyNumber}/points/reinstate
    Note over RetailAPI,CustomerMS: {points, reason=VoluntaryCancellation}
    CustomerMS-->>RetailAPI: Points reinstated
```

---

## Admin — customer search and lookup

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS

    Terminal->>LoyaltyAPI: GET /v1/customers?search={query}
    LoyaltyAPI->>CustomerMS: GET /api/v1/customers?search={query}
    CustomerMS-->>LoyaltyAPI: CustomerSearchResponse
    LoyaltyAPI-->>Terminal: [CustomerSummary]
    Note over LoyaltyAPI,Terminal: [{loyaltyNumber, givenName,<br/>surname, email, tier}]
```
