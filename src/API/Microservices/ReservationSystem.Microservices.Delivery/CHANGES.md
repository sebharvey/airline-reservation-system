# Ticketing Value Attribution — Change Summary

## Overview

This change implements IATA BSP/ARC-compliant ticketing value attribution. An e-ticket now covers one passenger across all flight segments in the booking (was: one passenger per segment). The ticket is the monetary unit; coupon-level value is always derived, never stored.

---

## Domain model

### New value objects

- **`FareComponent`** (`Domain/ValueObjects/FareComponent.cs`) — an immutable record representing one leg of an IATA fare calculation: `(Origin, Carrier, Destination, NucAmount, FareBasis?)`.
- **`CouponValue`** (`Domain/ValueObjects/CouponValue.cs`) — derived result of `Ticket.GetAttributedValue(couponNumber)`: `(CouponNumber, FareShare, TaxShare, Total, Currency)`. Never stored.
- **`FareCalculation`** (`Domain/ValueObjects/FareCalculation.cs`) — parses and validates an IATA linear fare calculation string. Exposes `Components`, `TotalNuc`, `Roe`, and `GetFareShareForCoupon(couponNumber, totalLocalFare)`. Validation: component NUC sum must equal declared `NUCxxx` total within 0.01.

### New entities

- **`TicketTax`** (`Domain/Entities/TicketTax.cs`) — one row per tax line per ticket. Holds `TaxCode`, `Amount`, `Currency`, and a collection of `TicketTaxCoupon` rows linking it to coupon numbers.
- **`TicketTaxCoupon`** — junction entity: `(TicketTaxId, CouponNumber)`. Unique per pair. `CouponNumber` constrained 1–4.

For split-per-coupon taxes (YQ/YR carrier surcharges), the total is divided evenly across N coupons and stored as N separate `TicketTax` rows, each with exactly one `TicketTaxCoupon`. This ensures `GetAttributedValue` can sum tax rows without double-counting.

### New service

- **`TaxAttributionService`** (`Domain/Services/TaxAttributionService.cs`) — data-driven rules engine that assigns tax codes to coupon numbers and computes attribution groups.
  - `GetCouponNumbers(taxCode, itinerary)` — returns which coupons a tax applies to based on departure-country, arrival-country, or all-coupons rules.
  - `AttributeTax(taxCode, totalAmount, itinerary)` — returns ready-to-store groups: for `SplitPerCoupon` taxes, returns N groups `(amount/N, [singleCoupon])`; for all others, one group `(totalAmount, matchingCoupons)`.
  - Default rules cover: GB, UB (UK APD/PSC — departure from GB), US, XY, YC, XA (US arrival taxes), YQ, YR (carrier surcharges — split per coupon).

### Refactored `Ticket` aggregate (`Domain/Entities/Ticket.cs`)

- Added stored financial columns: `TotalFareAmount`, `Currency`, `TotalTaxAmount`, `TotalAmount` (computed), `FareCalculation`.
- Added `TicketTaxes` collection (backing field `_ticketTaxes`; EF reads via field access).
- New `Create()` and `Reconstitute()` signatures include all financial parameters.
- `AddTax(TicketTax)` — attaches a tax row to the aggregate before persistence.
- `GetAttributedValue(couponNumber)` — parses `FareCalculation`, derives proportional NUC-weighted fare share, sums attributed taxes → returns `CouponValue?` (null if coupon out of range or fare calc unparseable).
- Coupon status constants consolidated into static class `CouponStatus` (Open, CheckedIn, Lifted, Flown, Refunded, Void, Exchanged, PrintExchange).

---

## Persistence

### SQL schema (`src/Database/Script.sql`)

- `delivery.Ticket` — added columns: `TotalFareAmount DECIMAL(10,2)`, `Currency CHAR(3)`, `TotalTaxAmount DECIMAL(10,2)`, `TotalAmount DECIMAL(10,2)`, `FareCalculation NVARCHAR(500)`. Added `CHK_Ticket_Ccy` constraint. Removed columns that were in the old spec but never in the real schema (ETicketNumber, InventoryId, FlightNumber, DepartureDate, GivenName, Surname, CabinCode, FareBasisCode).
- New `delivery.TicketTax` table: `(TicketTaxId, TicketId FK, TaxCode VARCHAR(4), Amount DECIMAL(10,2), Currency CHAR(3))`.
- New `delivery.TicketTaxCoupon` table: `(TicketTaxCouponId, TicketTaxId FK, CouponNumber TINYINT)` with `CHK_TicketTaxCoupon_Number` (1–4) and `UQ_TicketTaxCoupon` unique constraint.
- Seed data updated: reduced from 5 per-segment tickets to 3 per-passenger tickets (2 for AB1234, 1 for JC0005). Each seed ticket has TicketTax and TicketTaxCoupon rows. Order and manifest references updated accordingly.

### EF Core (`Infrastructure/Persistence/`)

- `DeliveryDbContext` — added `DbSet<TicketTax>`, `DbSet<TicketTaxCoupon>`. Ticket entity configured with new column mappings and cascade-delete navigation to taxes. Backing-field access mode set for both `TicketTaxes` and `AppliedToCoupons`.
- `EfTicketRepository` — `GetByETicketNumberWithTaxesAsync` added (loads ticket with `Include(TicketTaxes).ThenInclude(AppliedToCoupons)`). `GetByBookingReferenceAsync` also includes taxes.
- `ITicketRepository` — added `GetByETicketNumberWithTaxesAsync`.

---

## Application layer

### `IssueTicketsHandler` (rewritten)

- Now requires `fareConstruction` per passenger in the request.
- Validates via `IssueTicketsRequestValidator` (FluentValidation) — throws `ValidationException` on invalid input.
- Builds one `Ticket` per passenger (not per segment) using `Ticket.Create(bookingRef, passengerId, baseFare, currency, totalTaxes, fareCalcLine, ticketData)`.
- For each tax, calls `TaxAttributionService.AttributeTax()` to get per-coupon groups, then creates and attaches `TicketTax` rows with precise split amounts.
- `BuildTicketData` embeds `attributedTaxCodes` per coupon (indicative only) and uses `CouponStatus.Open` on all new coupons.

### `IssueTicketsRequestValidator` (new)

- Validates: `bookingReference` required + length 6, ≥1 passenger, 1–4 segments.
- Per passenger: `fareConstruction` required, `fareCalculationLine` must parse, `collectingCurrency` must be 3 chars, `baseFare > 0`, `totalTaxes ≥ 0`, tax sum must match `totalTaxes` within ±0.02.

### `GetCouponValueHandler` (new)

- Loads ticket via `GetByETicketNumberWithTaxesAsync`.
- Calls `ticket.GetAttributedValue(couponNumber)` and maps result to `GetCouponValueResponse`.
- Returns `null` if not found or coupon out of range.

---

## API contract

### Request — `POST /v1/tickets`

- **Removed** per-segment `fareComponent` field (pricing was per-segment before).
- **Added** `fareConstruction` per passenger (required): `baseFare`, `collectingCurrency`, `totalTaxes`, `fareCalculationLine`, `taxes[]`.
- Segments retain: `segmentId`, `flightNumber`, `operatingFlightNumber`, `departureDate`, `departureTime`, `origin`, `destination`, `cabinCode`, `cabinName`, `fareBasisCode`, `stopoverIndicator`, `baggageAllowance`, `seatAssignments`, `ssrCodes`.

### Response — `POST /v1/tickets`

- Returns **one ticket per passenger** (not one per segment). Response shape: `{ ticketId, eTicketNumber, passengerId, segmentIds[] }`.

### Response — `GET /v1/tickets/{eTicketNumber}`

- Added financial fields: `totalFareAmount`, `currency`, `totalTaxAmount`, `totalAmount`, `fareCalculation`.
- Added derived `fareComponents[]` (parsed from `fareCalculation`; `null` if unparseable).
- Added stored `taxBreakdown[]` (one entry per `TicketTax` row, with `appliesToCouponNumbers`).

### New endpoint — `GET /v1/tickets/{eTicketNumber}/coupons/{couponNumber}/value`

Returns derived coupon monetary value: `{ eTicketNumber, couponNumber, fareShare, taxShare, total, currency, valueSource: "derived" }`.

---

## Package dependency

- Added `FluentValidation` 11.9.2 to `ReservationSystem.Microservices.Delivery.csproj`.

---

## Documentation

- `documentation/api-specs/delivery-microservice.md` — updated throughout: business rules, `delivery.Ticket` schema, new `delivery.TicketTax`/`delivery.TicketTaxCoupon` schemas, TicketData JSON structure, POST /v1/tickets request and response examples, new GET endpoints.
- `documentation/api-reference.md` — added GET ticket and GET coupon value endpoint rows.
