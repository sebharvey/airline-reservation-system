# Customer — sequence diagrams

Covers loyalty customer profile management, points transactions, preferences, and account operations. All flows originate from the Angular web frontend through the Loyalty Orchestration API.

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
    Note over Web,LoyaltyAPI: Optional: page, pageSize
    LoyaltyAPI->>CustomerMS: GET /api/v1/customers/{loyaltyNumber}/transactions?page={p}&pageSize={ps}
    CustomerMS-->>LoyaltyAPI: TransactionsResponse (paginated)
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

The recipient loyalty number and email are cross-validated against the Identity MS before the transfer executes, preventing misdirected transfers.

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

    Note over LoyaltyAPI: Validate email match — throw if mismatch

    LoyaltyAPI->>CustomerMS: POST /api/v1/customers/{loyaltyNumber}/points/transfer
    Note over LoyaltyAPI,CustomerMS: {recipientLoyaltyNumber, points}
    CustomerMS-->>LoyaltyAPI: TransferResult

    LoyaltyAPI-->>Web: TransferPointsResponse
    Note over LoyaltyAPI,Web: {senderLoyaltyNumber, recipientLoyaltyNumber,<br/>pointsTransferred, senderNewBalance,<br/>recipientNewBalance, transferredAt}
```

---

## Delete account

Deletes the customer loyalty record only. The associated Identity MS account is not deleted by this flow.

```mermaid
sequenceDiagram
    participant Web
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS

    Web->>LoyaltyAPI: DELETE /v1/customers/{loyaltyNumber}/account
    Note over Web,LoyaltyAPI: Bearer token in Authorization header
    LoyaltyAPI->>CustomerMS: DELETE /api/v1/customers/{loyaltyNumber}
    CustomerMS-->>LoyaltyAPI: Customer record deleted
    LoyaltyAPI-->>Web: 204 No Content
```

---

## Link order to loyalty account (post-booking, internal)

Called from within `ConfirmBasketHandler` for any booking where a loyalty number is present (both revenue and reward bookings).

```mermaid
sequenceDiagram
    participant RetailAPI as Retail API
    participant CustomerMS as Customer MS

    RetailAPI->>CustomerMS: POST /api/v1/customers/{loyaltyNumber}/orders
    Note over RetailAPI,CustomerMS: {bookingReference, orderId}
    CustomerMS-->>RetailAPI: Order linked to loyalty account
```

---

## Sign-up bonus points award (registration, internal)

Called from within `RegisterHandler` after account creation.

```mermaid
sequenceDiagram
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS

    LoyaltyAPI->>CustomerMS: POST /api/v1/customers/{loyaltyNumber}/points/add
    Note over LoyaltyAPI,CustomerMS: {points=1500,<br/>transactionType=Earn,<br/>description=Sign up bonus}
    CustomerMS-->>LoyaltyAPI: Points awarded
```

---

## Admin — customer search

Staff search uses both a name/loyalty-number lookup and, if the search term contains `@`, a parallel email lookup via the Identity MS. Email results are merged with name results and prioritised.

```mermaid
sequenceDiagram
    participant Terminal as Contact Centre / Admin UI
    participant LoyaltyAPI as Loyalty API
    participant CustomerMS as Customer MS
    participant IdentityMS as Identity MS

    Terminal->>LoyaltyAPI: POST /v1/customers/search
    Note over Terminal,LoyaltyAPI: {searchTerm}

    par Name and loyalty number search
        LoyaltyAPI->>CustomerMS: POST /api/v1/customers/search
        Note over LoyaltyAPI,CustomerMS: Contains match on name,<br/>exact match on loyalty number
        CustomerMS-->>LoyaltyAPI: CustomerSearchResponse (up to 50 results)
    and Email search (if searchTerm contains @)
        LoyaltyAPI->>IdentityMS: GET /api/v1/accounts/by-email/{email}
        IdentityMS-->>LoyaltyAPI: IdentityAccount (userAccountId)
        LoyaltyAPI->>CustomerMS: GET /api/v1/customers/by-identity/{userAccountId}
        CustomerMS-->>LoyaltyAPI: CustomerProfile
    end

    Note over LoyaltyAPI: Merge results — email result prioritised

    LoyaltyAPI-->>Terminal: [CustomerSummaryResponse]
    Note over LoyaltyAPI,Terminal: [{loyaltyNumber, givenName,<br/>surname, tier, pointsBalance, isActive}]
```
