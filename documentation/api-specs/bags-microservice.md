# Bag Microservice — API Specification

> **Service owner:** Bag domain
> **Base path:** `/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Bag microservice is the system of record for checked baggage policies and ancillary bag pricing. It owns two capability areas: the free bag allowance by cabin class (`bag.BagPolicy`) and the per-sequence pricing for additional bags beyond the free allowance (`bag.BagPricing`). It is also responsible for **bag offer generation** — producing priced `BagOffer` objects with deterministic `BagOfferId` values on demand.

`BagOfferId` values are computed deterministically from `inventoryId + cabinCode + bagSequence` and do not require a dedicated offer storage table. Because bag pricing is stable (not volatile), there is no risk of price drift between offer generation and purchase — the Retail API calls `GET /v1/bags/offers/{bagOfferId}` at order confirmation to validate the price stored in the basket matches the current Bag MS pricing.

> **Important:** The Bag microservice is an internal service. It is not called directly by channels (Web, App, NDC). All channel-facing requests are routed through the **Retail API** orchestration layer. Admin endpoints are called from a future Contact Centre admin application. See the [Security](#security) section for authentication details.

---

## Security

### Authentication

The Bag microservice sits behind the Retail API orchestration layer. Channels authenticate via the Retail API using OAuth 2.0 / OIDC; the Retail API validates JWTs before calling this service.

Calls from the Retail API to the Bag microservice are authenticated using an **Azure Function Host Key**, passed in the `x-functions-key` HTTP header. The Bag microservice does not validate JWTs; that responsibility belongs to the Retail API. See [Microservice Authentication — Host Keys](../api.md#microservice-authentication--host-keys) in `api.md` for the full mechanism, key storage, and retrieval details.

### Required Headers

| Header | Required | Description |
|--------|----------|-------------|
| `Content-Type` | Yes (for request bodies) | Must be `application/json` |
| `x-functions-key` | Yes (on all Retail API → Bag MS calls) | Azure Function Host Key authenticating the Retail API as an authorised caller |
| `X-Correlation-ID` | Yes | UUID generated at the channel boundary; propagated on every downstream call for distributed tracing and log correlation |

### Data Protection

- Bag pricing and policy data contains no PII. Standard logging practices apply.
- `BagOfferId` values are session-scoped and must not be cached by channels beyond the current booking session.

---

## Business Rules

### Free Bag Allowance

Free bag allowance is determined by cabin class and is uniform across all routes and aircraft types. The allowance is owned and enforced exclusively by the Bag microservice.

| Cabin | Free Bags Included | Max Weight per Bag |
|-------|--------------------|--------------------|
| First (F) | 2 bags | 32 kg |
| Business (J) | 2 bags | 32 kg |
| Premium Economy (W) | 2 bags | 23 kg |
| Economy (Y) | 1 bag | 23 kg |

### Additional Bag Pricing

Additional bag pricing is per bag, per segment, fleet-wide and route-agnostic. Pricing is determined by the sequence number of the additional bag beyond the free allowance.

| Additional Bag | Sequence | Price |
|----------------|----------|-------|
| 1st additional bag | 1 | £60.00 |
| 2nd additional bag | 2 | £80.00 |
| 3rd additional bag and beyond | 99 (catch-all) | £100.00 |

### Bag Offer Generation

The Bag MS generates priced `BagOffer` objects on demand. `BagOfferId` values are deterministic identifiers computed from `inventoryId + cabinCode + bagSequence`. No offer storage table is required.

- The Retail API calls `GET /v1/bags/offers?inventoryId={inventoryId}&cabinCode={cabinCode}` to retrieve the free allowance and all purchasable additional bag offers for a flight and cabin.
- The returned `BagOfferId` is stored in the basket during the bookflow.
- At order confirmation, the Retail API calls `GET /v1/bags/offers/{bagOfferId}` to validate the price stored in the basket matches the current Bag MS pricing. No consumption state is tracked.

### Where Bag Ancillaries Can Be Purchased

Bags may be purchased at three points in the customer journey:

1. **During the bookflow** — as an optional step within the basket before payment. Bags are settled as a separate payment transaction alongside the fare at confirmation.
2. **Post-sale (manage booking)** — after a confirmed booking, via the Retail API manage-booking flow.
3. **At online check-in (OLCI)** — additional bags may be added during the check-in flow with immediate payment.

### Ancillary Document Requirement

For every bag purchase, the Retail API must create a `delivery.Document` record (type `BagAncillary`) via the Delivery MS. This enables the Accounting system to account for bag ancillary revenue independently from the fare ticket. The Bag MS has no involvement in this step — it is the Retail API's responsibility.

---

## Data Schema

### `bag.BagPolicy`

Stores the free bag allowance rules by cabin class.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `PolicyId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `CabinCode` | CHAR(1) | No | | UK | `F` · `J` · `W` · `Y` |
| `FreeBagsIncluded` | TINYINT | No | | | Number of free checked bags included in the fare for this cabin |
| `MaxWeightKgPerBag` | TINYINT | No | | | Maximum weight per individual bag in kilograms |
| `IsActive` | BIT | No | `1` | | |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — generated by a SQL trigger on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — updated automatically by a SQL trigger on every row modification** |

> **Constraints:** `UNIQUE` on `(CabinCode)` — enforces a single active policy per cabin code.
> **Policy changes** must be made by updating the existing row, not inserting a new one.
> **Seed data:** `('F', 2, 32)` · `('J', 2, 32)` · `('W', 2, 23)` · `('Y', 1, 23)`.

### `bag.BagPricing`

Stores the per-sequence pricing for additional bags.

| Column | Type | Nullable | Default | Key | Notes |
|--------|------|----------|---------|-----|-------|
| `PricingId` | UNIQUEIDENTIFIER | No | NEWID() | PK | |
| `BagSequence` | TINYINT | No | | UK (with CurrencyCode) | `1` = 1st additional bag · `2` = 2nd additional · `99` = 3rd and beyond (catch-all) |
| `CurrencyCode` | CHAR(3) | No | `'GBP'` | UK (with BagSequence) | ISO 4217 currency code |
| `Price` | DECIMAL(10,2) | No | | | |
| `IsActive` | BIT | No | `1` | | |
| `ValidFrom` | DATETIME2 | No | | | Effective start of this pricing rule |
| `ValidTo` | DATETIME2 | Yes | | | Null = open-ended / currently active |
| `CreatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — generated by a SQL trigger on insert** |
| `UpdatedAt` | DATETIME2 | No | SYSUTCDATETIME() | | **Read-only — updated automatically by a SQL trigger on every row modification** |

> **Constraints:** `UQ_BagPricing_Sequence` (unique) on `(BagSequence, CurrencyCode)` — enforces one active price per bag sequence/currency combination.
> **Seed data:** `(1, 'GBP', 60.00)` · `(2, 'GBP', 80.00)` · `(99, 'GBP', 100.00)`.

> **`createdAt` / `updatedAt` are database-generated fields and must never be written by the application layer.** Both are set and maintained exclusively by SQL triggers. The application layer must re-read the persisted row after any INSERT or UPDATE and use the returned values in the API response. In-memory timestamps set before persistence are provisional only. These fields are always present in responses but are not valid in request bodies.

---

## Endpoints

---

### GET /v1/bags/offers?inventoryId={inventoryId}&cabinCode={cabinCode}

Generate and return the free bag policy and priced bag offers for a specific flight and cabin. Returns the free allowance for the cabin and one `BagOfferId` per purchasable additional bag tier. `BagOfferId` values are deterministic — no database write is required.

**When to use:** Called by the Retail API during the bookflow (when displaying bag options to the passenger) and during the OLCI check-in flow. Also called during the post-sale manage-booking flow when a customer wishes to add bags to a confirmed booking.

> **Offer generation:** The Bag MS reads the active `BagPolicy` for the given `cabinCode` and all active `BagPricing` rows, then generates one `BagOffer` per purchasable additional bag slot. The `BagOfferId` is derived deterministically from `inventoryId + cabinCode + bagSequence`. No offer record is stored.

#### Query Parameters

| Parameter | Type | Required | Description |
|-----------|------|----------|-------------|
| `inventoryId` | string (UUID) | Yes | The `InventoryId` from `offer.FlightInventory` identifying the specific flight |
| `cabinCode` | string | Yes | Cabin class code: `F`, `J`, `W`, or `Y` |

#### Request

```
GET /v1/bags/offers?inventoryId=3fa85f64-5717-4562-b3fc-2c963f66afa6&cabinCode=Y
```

#### Response — `200 OK`

```json
{
  "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cabinCode": "Y",
  "policy": {
    "freeBagsIncluded": 1,
    "maxWeightKgPerBag": 23
  },
  "bagOffers": [
    {
      "bagOfferId": "bo-3fa85f64-Y-1-v1",
      "bagSequence": 1,
      "description": "1st additional checked bag",
      "price": 60.00,
      "currencyCode": "GBP"
    },
    {
      "bagOfferId": "bo-3fa85f64-Y-2-v1",
      "bagSequence": 2,
      "description": "2nd additional checked bag",
      "price": 80.00,
      "currencyCode": "GBP"
    },
    {
      "bagOfferId": "bo-3fa85f64-Y-99-v1",
      "bagSequence": 99,
      "description": "3rd additional checked bag and beyond",
      "price": 100.00,
      "currencyCode": "GBP"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `inventoryId` | string (UUID) | The flight inventory identifier, echoed back |
| `cabinCode` | string | The cabin class, echoed back |
| `policy.freeBagsIncluded` | integer | Number of free checked bags included in the fare for this cabin |
| `policy.maxWeightKgPerBag` | integer | Maximum weight per bag in kilograms |
| `bagOffers` | array | One entry per purchasable additional bag tier |
| `bagOffers[].bagOfferId` | string | Deterministic offer identifier derived from `inventoryId + cabinCode + bagSequence` |
| `bagOffers[].bagSequence` | integer | The sequence number of this additional bag relative to the free allowance (`1` = first additional, `2` = second, `99` = third and beyond) |
| `bagOffers[].description` | string | Human-readable description of this bag tier |
| `bagOffers[].price` | number | Price for this additional bag in the stated currency. Decimal, 2 places |
| `bagOffers[].currencyCode` | string | ISO 4217 currency code |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing or invalid query parameters (e.g. malformed UUID, invalid cabin code) |
| `404 Not Found` | No active `BagPolicy` found for the given `cabinCode`, or `inventoryId` is not recognised |

---

### GET /v1/bags/offers/{bagOfferId}

Retrieve and validate a bag offer by its deterministic ID. Confirms the pricing rule that generated the ID is still active and returns the current price. Used by the Retail API when adding bags to a basket or at order confirmation to validate the price stored in the basket.

**When to use:** Called by the Retail API when a passenger selects bags during the bookflow (to validate each `BagOfferId` before adding to the basket) and again at basket confirmation to verify prices have not changed since the offer was generated.

> **No consumption state:** This endpoint does not track whether an offer has been used. Unlike flight `StoredOffer` records, bag offers are generated on demand and validated on retrieval — there is no `IsConsumed` flag. The Retail API calls this endpoint to confirm the underlying pricing rule is still active.

> **Validation:** The Bag MS resolves the `bagOfferId` to its constituent parts (`inventoryId`, `cabinCode`, `bagSequence`), retrieves the current active `BagPricing` row for the identified sequence, and confirms it is still active. If the pricing rule has been deactivated since the offer was generated, the endpoint returns `404 Not Found` and the booking flow must be aborted.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `bagOfferId` | string | The deterministic bag offer identifier returned from `GET /v1/bags/offers` |

#### Request

```
GET /v1/bags/offers/bo-3fa85f64-Y-1-v1
```

#### Response — `200 OK`

```json
{
  "bagOfferId": "bo-3fa85f64-Y-1-v1",
  "inventoryId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cabinCode": "Y",
  "bagSequence": 1,
  "description": "1st additional checked bag",
  "price": 60.00,
  "currencyCode": "GBP",
  "isValid": true
}
```

| Field | Type | Description |
|-------|------|-------------|
| `bagOfferId` | string | The bag offer identifier, echoed back |
| `inventoryId` | string (UUID) | The flight inventory identifier resolved from the offer ID |
| `cabinCode` | string | The cabin class resolved from the offer ID |
| `bagSequence` | integer | The additional bag sequence number resolved from the offer ID |
| `description` | string | Human-readable description of this bag tier |
| `price` | number | Current price for this additional bag. Decimal, 2 places |
| `currencyCode` | string | ISO 4217 currency code |
| `isValid` | boolean | `true` if the underlying pricing rule is still active and the offer remains valid |

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Malformed `bagOfferId` — cannot be resolved to constituent parts |
| `404 Not Found` | The pricing rule underlying this offer is no longer active, or the `bagOfferId` cannot be resolved |

---

### GET /v1/bag-policies

List all bag allowance policies. Returns all records including inactive ones to support admin audit. Admin endpoint — not channel-facing.

**When to use:** Called from the Contact Centre admin application to view current and historical bag allowance policies.

#### Response — `200 OK`

```json
{
  "policies": [
    {
      "policyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
      "cabinCode": "Y",
      "freeBagsIncluded": 1,
      "maxWeightKgPerBag": 23,
      "isActive": true,
      "createdAt": "2025-01-01T00:00:00Z",
      "updatedAt": "2025-01-01T00:00:00Z"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `policyId` | string (UUID) | Unique policy identifier |
| `cabinCode` | string | Cabin class code: `F`, `J`, `W`, or `Y` |
| `freeBagsIncluded` | integer | Number of free checked bags included in the fare for this cabin |
| `maxWeightKgPerBag` | integer | Maximum weight per bag in kilograms |
| `isActive` | boolean | Whether this policy is currently active |
| `createdAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated on insert |
| `updatedAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated on every update |

---

### POST /v1/bag-policies

Create a new bag allowance policy. Admin endpoint — not channel-facing.

**When to use:** Called from the Contact Centre admin application when introducing a new cabin code or allowance rule.

> **Note:** A `UNIQUE` constraint on `CabinCode` enforces a single active policy per cabin. If a policy for the given cabin already exists, this endpoint returns `409 Conflict`. To change an existing policy, use `PUT /v1/bag-policies/{policyId}`.

#### Request

```json
{
  "cabinCode": "Y",
  "freeBagsIncluded": 1,
  "maxWeightKgPerBag": 23
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `cabinCode` | string | Yes | Cabin class code: `F`, `J`, `W`, or `Y` |
| `freeBagsIncluded` | integer | Yes | Number of free bags included. Must be ≥ 0 |
| `maxWeightKgPerBag` | integer | Yes | Maximum weight per bag in kg. Must be > 0 |

#### Response — `201 Created`

```json
{
  "policyId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "cabinCode": "Y",
  "freeBagsIncluded": 1,
  "maxWeightKgPerBag": 23,
  "isActive": true,
  "createdAt": "2026-03-20T10:00:00Z",
  "updatedAt": "2026-03-20T10:00:00Z"
}
```

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid values |
| `409 Conflict` | A policy already exists for the given `cabinCode` |

---

### GET /v1/bag-policies/{policyId}

Retrieve a bag policy by ID. Admin endpoint — not channel-facing.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `policyId` | string (UUID) | The unique policy identifier |

#### Response — `200 OK`

Returns a single policy object in the same schema as the items in `GET /v1/bag-policies`.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No policy found for the given `policyId` |

---

### PUT /v1/bag-policies/{policyId}

Update an existing bag allowance policy. Replaces all mutable fields. Admin endpoint — not channel-facing.

**When to use:** Called from the Contact Centre admin application when changing the free bag allowance or weight limit for a cabin.

> **Impact on active bookings:** Updating a policy takes effect immediately for new offer generation. Existing `BagOfferId` values stored in active baskets reference the pricing at the time of offer generation and are validated at confirmation via `GET /v1/bags/offers/{bagOfferId}`. If the policy change causes an existing offer to become invalid, the basket must be rebuilt.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `policyId` | string (UUID) | The unique policy identifier |

#### Request

```json
{
  "freeBagsIncluded": 2,
  "maxWeightKgPerBag": 23,
  "isActive": true
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `freeBagsIncluded` | integer | Yes | Number of free bags included. Must be ≥ 0 |
| `maxWeightKgPerBag` | integer | Yes | Maximum weight per bag in kg. Must be > 0 |
| `isActive` | boolean | Yes | Whether this policy should be active |

#### Response — `200 OK`

Returns the full updated policy object in the same schema as `GET /v1/bag-policies/{policyId}`.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields or invalid values |
| `404 Not Found` | No policy found for the given `policyId` |

---

### DELETE /v1/bag-policies/{policyId}

Delete a bag allowance policy. Admin endpoint — not channel-facing.

> **Warning:** Deleting a policy will cause `GET /v1/bags/offers` to return `404 Not Found` for the affected cabin code. This should only be used for policies that were created in error. To disable a policy without deleting it, use `PUT /v1/bag-policies/{policyId}` with `isActive: false`.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `policyId` | string (UUID) | The unique policy identifier |

#### Response — `204 No Content`

No response body.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No policy found for the given `policyId` |

---

### GET /v1/bag-pricing

List all bag pricing rules. Returns all records including inactive ones. Admin endpoint — not channel-facing.

#### Response — `200 OK`

```json
{
  "pricing": [
    {
      "pricingId": "b2c3d4e5-f6a7-8901-bcde-f12345678901",
      "bagSequence": 1,
      "currencyCode": "GBP",
      "price": 60.00,
      "isActive": true,
      "validFrom": "2026-01-01T00:00:00Z",
      "validTo": null,
      "createdAt": "2025-12-01T00:00:00Z",
      "updatedAt": "2025-12-01T00:00:00Z"
    }
  ]
}
```

| Field | Type | Description |
|-------|------|-------------|
| `pricingId` | string (UUID) | Unique pricing rule identifier |
| `bagSequence` | integer | Sequence number: `1` = first additional bag, `2` = second, `99` = third and beyond |
| `currencyCode` | string | ISO 4217 currency code |
| `price` | number | Price for this bag tier. Decimal, 2 places |
| `isActive` | boolean | Whether this rule is currently active |
| `validFrom` | string (datetime) | ISO 8601 UTC effective start of this rule |
| `validTo` | string (datetime) | ISO 8601 UTC end of this rule. `null` = open-ended / currently active |
| `createdAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated on insert |
| `updatedAt` | string (datetime) | ISO 8601 UTC. Read-only — SQL trigger-generated on every update |

---

### POST /v1/bag-pricing

Create a new bag pricing rule. Admin endpoint — not channel-facing.

> **Uniqueness:** A `UNIQUE` constraint on `(BagSequence, CurrencyCode)` enforces one active price per sequence/currency combination. Creating a rule for an existing active sequence/currency combination returns `409 Conflict`. To change a price, use `PUT /v1/bag-pricing/{pricingId}`.

#### Request

```json
{
  "bagSequence": 1,
  "currencyCode": "GBP",
  "price": 60.00,
  "validFrom": "2026-01-01T00:00:00Z",
  "validTo": null
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `bagSequence` | integer | Yes | Sequence number: `1`, `2`, or `99` (catch-all for 3rd bag and beyond) |
| `currencyCode` | string | Yes | ISO 4217 currency code, e.g. `"GBP"` |
| `price` | number | Yes | Price for this bag tier. Decimal, 2 places. Must be > 0 |
| `validFrom` | string (datetime) | Yes | ISO 8601 UTC effective start |
| `validTo` | string (datetime) | No | ISO 8601 UTC end. `null` = open-ended |

#### Response — `201 Created`

Returns the full created pricing rule in the same schema as items in `GET /v1/bag-pricing`.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid values, or `validFrom` after `validTo` |
| `409 Conflict` | An active pricing rule already exists for the given `bagSequence` and `currencyCode` combination |

---

### GET /v1/bag-pricing/{pricingId}

Retrieve a bag pricing rule by ID. Admin endpoint — not channel-facing.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `pricingId` | string (UUID) | The unique pricing rule identifier |

#### Response — `200 OK`

Returns a single pricing rule in the same schema as items in `GET /v1/bag-pricing`.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No pricing rule found for the given `pricingId` |

---

### PUT /v1/bag-pricing/{pricingId}

Update a bag pricing rule. Replaces all mutable fields. Admin endpoint — not channel-facing.

**When to use:** Called from the Contact Centre admin application when changing the price for a specific bag tier.

> **Impact on active bookings:** Price changes take effect immediately for new offer generation. Existing `BagOfferId` values in active baskets will be validated at confirmation via `GET /v1/bags/offers/{bagOfferId}`. If the price has changed, the Retail API will detect the discrepancy and must re-present the updated price to the customer before proceeding.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `pricingId` | string (UUID) | The unique pricing rule identifier |

#### Request

```json
{
  "price": 65.00,
  "validFrom": "2026-04-01T00:00:00Z",
  "validTo": null,
  "isActive": true
}
```

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `price` | number | Yes | Updated price. Decimal, 2 places. Must be > 0 |
| `validFrom` | string (datetime) | Yes | ISO 8601 UTC effective start |
| `validTo` | string (datetime) | No | ISO 8601 UTC end. `null` = open-ended |
| `isActive` | boolean | Yes | Whether this rule is active |

#### Response — `200 OK`

Returns the full updated pricing rule in the same schema as `GET /v1/bag-pricing/{pricingId}`.

#### Error Responses

| Status | Reason |
|--------|--------|
| `400 Bad Request` | Missing required fields, invalid values, or `validFrom` after `validTo` |
| `404 Not Found` | No pricing rule found for the given `pricingId` |

---

### DELETE /v1/bag-pricing/{pricingId}

Delete a bag pricing rule. Admin endpoint — not channel-facing.

> **Warning:** Deleting a pricing rule will invalidate any `BagOfferId` that references it. Any basket containing such an offer will fail validation at order confirmation. To disable a rule without deleting it, use `PUT /v1/bag-pricing/{pricingId}` with `isActive: false`.

#### Path Parameters

| Parameter | Type | Description |
|-----------|------|-------------|
| `pricingId` | string (UUID) | The unique pricing rule identifier |

#### Response — `204 No Content`

No response body.

#### Error Responses

| Status | Reason |
|--------|--------|
| `404 Not Found` | No pricing rule found for the given `pricingId` |

---

## Retail API Integration Flows

### Bookflow — Bag Selection

During the bookflow, the Retail API calls the Bag MS once per flight segment to retrieve the free allowance and purchasable bag offers. The full sequence is:

1. **Retail API → Bag MS:** `GET /v1/bags/offers?inventoryId={inventoryId}&cabinCode={cabinCode}` — called once per segment to retrieve the free bag policy and priced bag offers.
2. **Retail API → channel:** Returns the merged free allowance and bag options per segment per passenger.
3. **Passenger selects additional bags.**
4. **Retail API → Bag MS:** `GET /v1/bags/offers/{bagOfferId}` — called once per selected `BagOfferId` to validate the offer is still active and lock the current price.
5. **Retail API → Order MS:** `PUT /v1/basket/{basketId}/bags` — bag offer IDs and passenger assignments written to the basket.
6. **At basket confirmation:**
   - Retail API → Payment MS: authorise bag payment (`description=BagAncillary`).
   - Retail API → Order MS: create order with bag items.
   - Retail API → Delivery MS: issue `delivery.Document` (type `BagAncillary`) — Bag MS not involved.
   - Retail API → Payment MS: settle bag payment after order confirmation.

### Post-Sale Bag Addition (Manage Booking)

1. **Retail API → Bag MS:** `GET /v1/bags/offers?inventoryId={inventoryId}&cabinCode={cabinCode}` — retrieve current allowance and additional bag offers.
2. **Passenger selects additional bags.**
3. **Retail API → Bag MS:** `GET /v1/bags/offers/{bagOfferId}` — validate each selected offer.
4. **Retail API → Payment MS:** Authorise and settle bag payment.
5. **Retail API → Order MS:** `PATCH /v1/orders/{bookingRef}/bags` — add bag order items with `paymentId`.
6. **Retail API → Delivery MS:** Issue `delivery.Document` (type `BagAncillary`).

### OLCI (Online Check-In) Bag Addition

Same flow as post-sale bag addition, triggered within the check-in flow. Bags added at OLCI require immediate payment.

---

## Data Conventions

| Convention | Format | Example |
|------------|--------|---------|
| Timestamps | ISO 8601 UTC | `"2026-03-20T10:00:00Z"` |
| Currency codes | ISO 4217 | `"GBP"` |
| Currency amounts | Decimal, 2 places | `60.00` |
| JSON field names | camelCase | `bagSequence` |
| UUIDs | RFC 4122 lowercase | `"a1b2c3d4-e5f6-7890-abcd-ef1234567890"` |
| Cabin codes | Single character | `"Y"`, `"W"`, `"J"`, `"F"` |

---

## Invocation Examples

### Retrieve bag offers for a flight (Retail API → Bag MS, bookflow)

```bash
curl -X GET "https://{bag-ms-host}/v1/bags/offers?inventoryId=3fa85f64-5717-4562-b3fc-2c963f66afa6&cabinCode=Y" \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

### Validate a bag offer at basket confirmation (Retail API → Bag MS)

```bash
curl -X GET https://{bag-ms-host}/v1/bags/offers/bo-3fa85f64-Y-1-v1 \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

### List all bag policies (admin)

```bash
curl -X GET https://{bag-ms-host}/v1/bag-policies \
  -H "x-functions-key: {host-key}" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000"
```

### Create a bag pricing rule (admin)

```bash
curl -X POST https://{bag-ms-host}/v1/bag-pricing \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "bagSequence": 1,
    "currencyCode": "GBP",
    "price": 60.00,
    "validFrom": "2026-01-01T00:00:00Z",
    "validTo": null
  }'
```

### Update a bag pricing rule (admin)

```bash
curl -X PUT https://{bag-ms-host}/v1/bag-pricing/b2c3d4e5-f6a7-8901-bcde-f12345678901 \
  -H "x-functions-key: {host-key}" \
  -H "Content-Type: application/json" \
  -H "X-Correlation-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -d '{
    "price": 65.00,
    "validFrom": "2026-04-01T00:00:00Z",
    "validTo": null,
    "isActive": true
  }'
```

> **Note:** All calls to the Bag microservice are authenticated using the `x-functions-key` header. The Bag MS never receives or validates end-user JWTs. See [`api.md` — Microservice Authentication](../api.md#microservice-authentication--host-keys) for the full host key mechanism.

---

## Related Documentation

- [API Endpoint Reference](../api-reference.md) — Summary of all microservice endpoints
- [System Design](../system-overview.md) — Full domain design including ancillary sequence diagrams for bookflow bag selection, post-sale bag addition, and OLCI bag purchase flows
