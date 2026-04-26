# Timatic Simulator — API Specification

> **Service owner:** Simulator
> **Base path:** `/autocheck/v1`
> **Transport:** HTTPS (TLS 1.2 minimum)
> **Content type:** `application/json`

The Timatic Simulator mimics the IATA AutoCheck REST API for testing. It is called by the `DocumentVerification` service during online check-in (OCI) to verify passport, visa, and health document requirements, to validate Advance Passenger Information (APIS), and to run realtime gate checks via MRZ scan.

The simulator supports both happy-path and non-successful responses. Non-successful states are triggered by specific test values in the request body — see [Non-successful states](#non-successful-states) for details.

> **Important:** This is a simulator. No actual document, visa, or APIS validation is performed. It is not suitable for production use.

> **Path convention:** Routes are served without an `/api` prefix. The three endpoints are reachable directly at `/autocheck/v1/documentcheck`, `/autocheck/v1/apischeck`, and `/autocheck/v1/realtimecheck`.

---

## Security

### Authentication

All endpoints require a Bearer token in the `Authorization` header. The expected token value is stored as plain text in the `Timatic:ApiToken` Azure App Setting. Incoming and stored tokens are compared as SHA-256 hashes — no plain-text comparison is performed.

**Authentication flow:**

1. The caller includes `Authorization: Bearer <token>` on every request.
2. The simulator hashes the incoming token with SHA-256.
3. The simulator hashes the configured `Timatic:ApiToken` value with SHA-256.
4. If the two hashes match, the request proceeds. Otherwise `401 Unauthorized` is returned immediately.

### Required headers

| Header | Required | Description |
|--------|----------|-------------|
| `Authorization` | Yes | `Bearer <token>` — must match the SHA-256 hash of `Timatic:ApiToken` in Azure App Settings |
| `Content-Type` | Yes | Must be `application/json` |

### Error responses

| Status | Condition |
|--------|-----------|
| `400 Bad Request` | Request body is missing, unparseable, or `null` |
| `401 Unauthorized` | `Authorization` header absent, malformed, or token hash mismatch |

---

## Endpoints

### POST /autocheck/v1/documentcheck

Validates passport, visa, and health document requirements for a passenger journey. Called at booking confirmation or OCI entry. Returns `status: OK` under normal conditions. Returns `status: FAILED` with a visa requirement when the document number starts with `FAIL` (see [Non-successful states](#non-successful-states)). Appends an ESTA advisory for journeys arriving at US airports.

#### Request

```json
{
  "transactionIdentifier": "TXN-2026-DOC-001",
  "airlineCode": "AX",
  "journeyType": "OW",
  "paxInfo": {
    "documentType": "P",
    "nationality": "GBR",
    "documentIssuerCountry": "GBR",
    "documentNumber": "123456789",
    "documentExpiryDate": "2031-01-01",
    "dateOfBirth": "1980-01-01",
    "gender": "M",
    "residentCountry": "GBR"
  },
  "itinerary": [
    {
      "departureAirport": "LHR",
      "arrivalAirport": "JFK",
      "airline": "AX",
      "flightNumber": "AX001",
      "departureDate": "2026-05-01"
    }
  ]
}
```

#### Request fields

| Field | Type | Description |
|-------|------|-------------|
| `transactionIdentifier` | string | Caller-generated unique reference; echoed back in the response |
| `airlineCode` | string | IATA airline designator, e.g. `AX` |
| `journeyType` | string | `OW` (one-way) or `RT` (return) |
| `paxInfo.documentType` | string | `P` (passport), `V` (visa), `I` (identity card) |
| `paxInfo.nationality` | string | ISO 3166-1 alpha-3 country code, e.g. `GBR` |
| `paxInfo.documentIssuerCountry` | string | ISO 3166-1 alpha-3 country code of issuing state |
| `paxInfo.documentNumber` | string | Document identifier |
| `paxInfo.documentExpiryDate` | string | ISO 8601 date `YYYY-MM-DD` |
| `paxInfo.dateOfBirth` | string | ISO 8601 date `YYYY-MM-DD` |
| `paxInfo.gender` | string | `M`, `F`, or `X` |
| `paxInfo.residentCountry` | string | ISO 3166-1 alpha-3 country of residence |
| `itinerary[]` | array | One or more flight segments in travel order |
| `itinerary[].departureAirport` | string | IATA airport code |
| `itinerary[].arrivalAirport` | string | IATA airport code |
| `itinerary[].airline` | string | IATA airline designator |
| `itinerary[].flightNumber` | string | Full flight number, e.g. `AX001` |
| `itinerary[].departureDate` | string | ISO 8601 date `YYYY-MM-DD` |

#### Response — `200 OK` (standard route, no advisory)

```json
{
  "transactionIdentifier": "TXN-2026-DOC-001",
  "status": "OK",
  "passportRequired": true,
  "visaRequired": false,
  "healthDocRequired": false,
  "transitVisaRequired": false,
  "requirements": [],
  "advisories": [],
  "dataAsOf": "2026-04-16T08:00:00.0000000Z"
}
```

#### Response — `200 OK` (US arrival — ESTA advisory appended)

When the final segment's `arrivalAirport` is a US gateway (JFK, LAX, ORD, ATL, DFW, MIA, SFO, SEA, BOS, EWR, IAD, IAH, MCO, MSP, DTW, PHL, LGA, CLT, DEN, PHX, LAS) the simulator appends an ESTA advisory:

```json
{
  "transactionIdentifier": "TXN-2026-DOC-001",
  "status": "OK",
  "passportRequired": true,
  "visaRequired": false,
  "healthDocRequired": false,
  "transitVisaRequired": false,
  "requirements": [],
  "advisories": [
    {
      "type": "ESTA",
      "description": "ESTA registration required prior to travel.",
      "url": "https://esta.cbp.dhs.gov",
      "mandatory": true
    }
  ],
  "dataAsOf": "2026-04-16T08:00:00.0000000Z"
}
```

#### Response fields

| Field | Type | Description |
|-------|------|-------------|
| `transactionIdentifier` | string | Echoed from the request |
| `status` | string | Always `OK` |
| `passportRequired` | boolean | Always `true` |
| `visaRequired` | boolean | Always `false` |
| `healthDocRequired` | boolean | Always `false` |
| `transitVisaRequired` | boolean | Always `false` |
| `requirements` | array | Always empty `[]` |
| `advisories` | array | Zero or one ESTA advisory object for US arrivals |
| `advisories[].type` | string | Advisory type — `ESTA` for US destinations |
| `advisories[].description` | string | Human-readable advisory text |
| `advisories[].url` | string | Reference URL |
| `advisories[].mandatory` | boolean | Whether the advisory is mandatory |
| `dataAsOf` | string | ISO 8601 UTC timestamp — today at 08:00 UTC |

---

### POST /autocheck/v1/apischeck

Validates Advance Passenger Information when a passenger submits check-in data. Called during OCI submission. Returns `apisStatus: ACCEPTED` under normal conditions. Returns `apisStatus: REJECTED` when the passenger's given name contains `ALAN` (see [Non-successful states](#non-successful-states)).

#### Request

```json
{
  "transactionIdentifier": "TXN-2026-APIS-001",
  "airlineCode": "AX",
  "flightNumber": "AX001",
  "departureDate": "2026-05-01",
  "departureAirport": "LHR",
  "arrivalAirport": "JFK",
  "paxInfo": {
    "surname": "SMITH",
    "givenNames": "JOHN",
    "dateOfBirth": "1980-01-01",
    "gender": "M",
    "nationality": "GBR",
    "documentType": "P",
    "documentNumber": "123456789",
    "documentIssuerCountry": "GBR",
    "documentExpiryDate": "2031-01-01"
  }
}
```

#### Request fields

| Field | Type | Description |
|-------|------|-------------|
| `transactionIdentifier` | string | Caller-generated unique reference; echoed back in the response |
| `airlineCode` | string | IATA airline designator |
| `flightNumber` | string | Full flight number, e.g. `AX001` |
| `departureDate` | string | ISO 8601 date `YYYY-MM-DD` |
| `departureAirport` | string | IATA airport code |
| `arrivalAirport` | string | IATA airport code |
| `paxInfo.surname` | string | Passenger surname in uppercase |
| `paxInfo.givenNames` | string | Given name(s) in uppercase |
| `paxInfo.dateOfBirth` | string | ISO 8601 date `YYYY-MM-DD` |
| `paxInfo.gender` | string | `M`, `F`, or `X` |
| `paxInfo.nationality` | string | ISO 3166-1 alpha-3 country code |
| `paxInfo.documentType` | string | `P` (passport) |
| `paxInfo.documentNumber` | string | Document identifier |
| `paxInfo.documentIssuerCountry` | string | ISO 3166-1 alpha-3 country code of issuing state |
| `paxInfo.documentExpiryDate` | string | ISO 8601 date `YYYY-MM-DD` |

#### Response — `200 OK`

```json
{
  "transactionIdentifier": "TXN-2026-APIS-001",
  "apisStatus": "ACCEPTED",
  "carrierLiabilityConfirmed": true,
  "fineRisk": "LOW",
  "warnings": [],
  "auditRef": "TMC-2026-04-16-A1B2C3D4",
  "processedAt": "2026-04-16T09:30:00.0000000Z"
}
```

#### Response fields

| Field | Type | Description |
|-------|------|-------------|
| `transactionIdentifier` | string | Echoed from the request |
| `apisStatus` | string | Always `ACCEPTED` |
| `carrierLiabilityConfirmed` | boolean | Always `true` |
| `fineRisk` | string | Always `LOW` |
| `warnings` | array | Always empty `[]` |
| `auditRef` | string | Generated audit reference — format `TMC-YYYY-MM-DD-XXXXXXXX` (8 uppercase hex chars) |
| `processedAt` | string | ISO 8601 UTC timestamp of when the simulator processed the request |

---

### POST /autocheck/v1/realtimecheck

Runs a realtime gate check when an agent scans the passenger's MRZ at the boarding gate. Parses ICAO 9303 TD3 Machine Readable Passport (MRP) lines and returns `decision: GO` under normal conditions. Returns `decision: NO_GO` when the MRZ surname parses to `BLOCKED` (see [Non-successful states](#non-successful-states)).

#### MRZ format (ICAO 9303 TD3)

Each line is exactly 44 characters. `<` is the filler character.

**Line 1:**

| Positions | Length | Field |
|-----------|--------|-------|
| 0 | 1 | Document type (`P` for passport) |
| 1 | 1 | Subtype / filler |
| 2–4 | 3 | Issuing state (ISO 3166-1 alpha-3) |
| 5–43 | 39 | Name field — `SURNAME<<GIVENNAME1<GIVENNAME2` |

**Line 2:**

| Positions | Length | Field |
|-----------|--------|-------|
| 0–8 | 9 | Document number |
| 9 | 1 | Check digit |
| 10–12 | 3 | Nationality (ISO 3166-1 alpha-3) |
| 13–18 | 6 | Date of birth `YYMMDD` |
| 19 | 1 | Check digit |
| 20 | 1 | Sex (`M`/`F`/`<`) |
| 21–26 | 6 | Expiry date `YYMMDD` |
| 27 | 1 | Check digit |
| 28–43 | 16 | Optional data + overall check digit |

Date interpretation: year values ≥ 60 are treated as 1900s; values < 60 are treated as 2000s.

#### Request

```json
{
  "transactionIdentifier": "TXN-2026-GATE-001",
  "airlineCode": "AX",
  "flightNumber": "AX001",
  "departureAirport": "LHR",
  "arrivalAirport": "JFK",
  "mrzData": {
    "line1": "P<GBRSMITH<<JOHN<<<<<<<<<<<<<<<<<<<<<<<<<<<<",
    "line2": "1234567897GBR8001011M3101015<<<<<<<<<<<<<<<9"
  },
  "agentId": "AGENT-LHR-001",
  "checkTimestamp": "2026-05-01T06:30:00Z"
}
```

#### Request fields

| Field | Type | Description |
|-------|------|-------------|
| `transactionIdentifier` | string | Caller-generated unique reference; echoed back in the response |
| `airlineCode` | string | IATA airline designator |
| `flightNumber` | string | Full flight number, e.g. `AX001` |
| `departureAirport` | string | IATA airport code |
| `arrivalAirport` | string | IATA airport code |
| `mrzData.line1` | string | ICAO TD3 MRZ line 1 — exactly 44 characters |
| `mrzData.line2` | string | ICAO TD3 MRZ line 2 — exactly 44 characters |
| `agentId` | string | Gate agent identifier for audit purposes |
| `checkTimestamp` | string | ISO 8601 UTC timestamp of when the MRZ was scanned |

#### Response — `200 OK`

```json
{
  "transactionIdentifier": "TXN-2026-GATE-001",
  "decision": "GO",
  "conditionsMet": true,
  "carrierLiabilityConfirmed": true,
  "parsedDocument": {
    "surname": "SMITH",
    "givenNames": "JOHN",
    "nationality": "GBR",
    "dateOfBirth": "1980-01-01",
    "documentExpiryDate": "2031-01-01",
    "documentNumber": "123456789"
  },
  "auditRef": "TMC-2026-04-16-GATE-A1B2C3",
  "processedAt": "2026-05-01T06:30:00.1234567Z"
}
```

#### Response fields

| Field | Type | Description |
|-------|------|-------------|
| `transactionIdentifier` | string | Echoed from the request |
| `decision` | string | Always `GO` |
| `conditionsMet` | boolean | Always `true` |
| `carrierLiabilityConfirmed` | boolean | Always `true` |
| `parsedDocument.surname` | string | Surname parsed from MRZ line 1 name field (double-`<` delimiter) |
| `parsedDocument.givenNames` | string | Given names parsed from MRZ line 1, single-`<` replaced with space |
| `parsedDocument.nationality` | string | ISO 3166-1 alpha-3 code from MRZ line 2 positions 10–12 |
| `parsedDocument.dateOfBirth` | string | ISO 8601 date derived from MRZ line 2 positions 13–18 |
| `parsedDocument.documentExpiryDate` | string | ISO 8601 date derived from MRZ line 2 positions 21–26 |
| `parsedDocument.documentNumber` | string | Document number from MRZ line 2 positions 0–8 (trailing `<` stripped) |
| `auditRef` | string | Generated audit reference — format `TMC-YYYY-MM-DD-GATE-XXXXXX` (6 uppercase hex chars) |
| `processedAt` | string | ISO 8601 UTC timestamp of when the simulator processed the request |

---

## Non-successful states

The simulator returns non-successful responses when specific sentinel values appear in the request. These allow callers to test rejection and failure handling without modifying authentication or infrastructure configuration.

### Trigger summary

| Endpoint | Trigger field | Trigger value | Non-successful response |
|----------|--------------|---------------|------------------------|
| `/autocheck/v1/documentcheck` | `paxInfo.documentNumber` | Starts with `FAIL` (case-insensitive) | `status: FAILED`, `visaRequired: true`, requirements populated |
| `/autocheck/v1/apischeck` | `paxInfo.givenNames` | Contains `ALAN` (case-insensitive) | `apisStatus: REJECTED`, `fineRisk: HIGH`, warnings populated |
| `/autocheck/v1/realtimecheck` | MRZ line 1 surname field | Parses to `BLOCKED` | `decision: NO_GO`, `conditionsMet: false` |

### Document check — FAILED (`status: FAILED`)

**Trigger:** `paxInfo.documentNumber` starts with `FAIL` (e.g. `FAIL123456`, `FAIL`).

**Response — `200 OK`**

```json
{
  "transactionIdentifier": "TXN-2026-DOC-003",
  "status": "FAILED",
  "passportRequired": true,
  "visaRequired": true,
  "healthDocRequired": false,
  "transitVisaRequired": false,
  "requirements": [
    {
      "type": "VISA",
      "description": "Visa required. Travel document number is not accepted.",
      "mandatory": true
    }
  ],
  "advisories": [],
  "dataAsOf": "2026-04-22T08:00:00.0000000Z"
}
```

**Changed fields vs happy path:**

| Field | Happy-path value | FAILED value |
|-------|-----------------|--------------|
| `status` | `OK` | `FAILED` |
| `visaRequired` | `false` | `true` |
| `requirements` | `[]` | One requirement object with `type: VISA` |

### APIS check — REJECTED (`apisStatus: REJECTED`)

**Trigger:** `paxInfo.givenNames` contains `ALAN` (case-insensitive), e.g. `ALAN`, `Alan`, `ALAN JAMES`.

This simulates a watchlist match that blocks the passenger from completing online check-in.

**Response — `200 OK`**

```json
{
  "transactionIdentifier": "TXN-2026-APIS-002",
  "apisStatus": "REJECTED",
  "carrierLiabilityConfirmed": false,
  "fineRisk": "HIGH",
  "warnings": [
    {
      "code": "WATCHLIST_MATCH",
      "description": "Passenger name matches a watchlist entry. Online check-in is not permitted."
    }
  ],
  "auditRef": "TMC-2026-04-22-A1B2C3D4",
  "processedAt": "2026-04-22T09:30:00.0000000Z"
}
```

**Changed fields vs happy path:**

| Field | Happy-path value | REJECTED value |
|-------|-----------------|----------------|
| `apisStatus` | `ACCEPTED` | `REJECTED` |
| `carrierLiabilityConfirmed` | `true` | `false` |
| `fineRisk` | `LOW` | `HIGH` |
| `warnings` | `[]` | One warning object with `code: WATCHLIST_MATCH` |

### Realtime gate check — NO_GO (`decision: NO_GO`)

**Trigger:** MRZ line 1 surname field parses to `BLOCKED` (double-`<` delimiter). Example MRZ:

```
line1: "P<GBRBLOCKED<<TEST<<<<<<<<<<<<<<<<<<<<<<<<<<<<"
line2: "1234567897GBR8001011M3101015<<<<<<<<<<<<<<<9"
```

This simulates a gate-level denial where the passenger's document has been revoked or boarding is prohibited.

**Response — `200 OK`**

```json
{
  "transactionIdentifier": "TXN-2026-GATE-002",
  "decision": "NO_GO",
  "conditionsMet": false,
  "carrierLiabilityConfirmed": false,
  "parsedDocument": {
    "surname": "BLOCKED",
    "givenNames": "TEST",
    "nationality": "GBR",
    "dateOfBirth": "1980-01-01",
    "documentExpiryDate": "2031-01-01",
    "documentNumber": "123456789"
  },
  "auditRef": "TMC-2026-04-22-GATE-A1B2C3",
  "processedAt": "2026-04-22T06:30:00.0000000Z"
}
```

**Changed fields vs happy path:**

| Field | Happy-path value | NO_GO value |
|-------|-----------------|-------------|
| `decision` | `GO` | `NO_GO` |
| `conditionsMet` | `true` | `false` |
| `carrierLiabilityConfirmed` | `true` | `false` |

---

## Data conventions

- All timestamps are ISO 8601 UTC (`O` format — e.g. `2026-04-16T09:30:00.0000000Z`).
- All date fields use `YYYY-MM-DD`.
- Airport codes are IATA `CHAR(3)` uppercase.
- Country codes are ISO 3166-1 alpha-3 uppercase.
- JSON field names are camelCase.

---

## Invocation examples

### Document check (curl)

```bash
curl -s -X POST \
  https://reservation-system-simulator-timatic-h0guaxfvgaengdeh.uksouth-01.azurewebsites.net/autocheck/v1/documentcheck \
  -H "Authorization: Bearer <timatic-api-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "transactionIdentifier": "TXN-2026-DOC-001",
    "airlineCode": "AX",
    "journeyType": "OW",
    "paxInfo": {
      "documentType": "P",
      "nationality": "GBR",
      "documentIssuerCountry": "GBR",
      "documentNumber": "123456789",
      "documentExpiryDate": "2031-01-01",
      "dateOfBirth": "1980-01-01",
      "gender": "M",
      "residentCountry": "GBR"
    },
    "itinerary": [
      {
        "departureAirport": "LHR",
        "arrivalAirport": "JFK",
        "airline": "AX",
        "flightNumber": "AX001",
        "departureDate": "2026-05-01"
      }
    ]
  }'
```

### APIS check (curl)

```bash
curl -s -X POST \
  https://reservation-system-simulator-timatic-h0guaxfvgaengdeh.uksouth-01.azurewebsites.net/autocheck/v1/apischeck \
  -H "Authorization: Bearer <timatic-api-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "transactionIdentifier": "TXN-2026-APIS-001",
    "airlineCode": "AX",
    "flightNumber": "AX001",
    "departureDate": "2026-05-01",
    "departureAirport": "LHR",
    "arrivalAirport": "JFK",
    "paxInfo": {
      "surname": "SMITH",
      "givenNames": "JOHN",
      "dateOfBirth": "1980-01-01",
      "gender": "M",
      "nationality": "GBR",
      "documentType": "P",
      "documentNumber": "123456789",
      "documentIssuerCountry": "GBR",
      "documentExpiryDate": "2031-01-01"
    }
  }'
```

### Realtime gate check (curl)

```bash
curl -s -X POST \
  https://reservation-system-simulator-timatic-h0guaxfvgaengdeh.uksouth-01.azurewebsites.net/autocheck/v1/realtimecheck \
  -H "Authorization: Bearer <timatic-api-token>" \
  -H "Content-Type: application/json" \
  -d '{
    "transactionIdentifier": "TXN-2026-GATE-001",
    "airlineCode": "AX",
    "flightNumber": "AX001",
    "departureAirport": "LHR",
    "arrivalAirport": "JFK",
    "mrzData": {
      "line1": "P<GBRSMITH<<JOHN<<<<<<<<<<<<<<<<<<<<<<<<<<<<",
      "line2": "1234567897GBR8001011M3101015<<<<<<<<<<<<<<<9"
    },
    "agentId": "AGENT-LHR-001",
    "checkTimestamp": "2026-05-01T06:30:00Z"
  }'
```

---

## OCI check-in test scenarios

The Timatic simulator is called by the Delivery microservice during `POST /v1/oci/checkin`. Both `documentcheck` and `apischeck` run in Phase 1 of the check-in handler before any coupon status is updated. A failure from either endpoint causes the entire check-in to be rejected with `422 Unprocessable Entity`; no passenger is checked in.

The scenarios below cover the full check-in journey via the Operations API (`POST /api/v1/oci/*`). All assume a valid booking reference and that the 24-hour check-in window is open.

---

### Scenario 1 — Standard check-in, non-US route (happy path)

**Trigger values:** Any passport number that does not start with `FAIL`; given name that does not contain `ALAN`.

**Journey steps:**

| Step | Endpoint | Key request values |
|------|----------|--------------------|
| 1 | `POST /api/v1/oci/retrieve` | Valid booking reference, lead PAX name, departure airport |
| 2 | `POST /api/v1/oci/pax` | `"number": "PA1234567"`, `"nationality": "GBR"` |
| 3 | `POST /api/v1/oci/seats` | — (not implemented, returns success) |
| 4 | `POST /api/v1/oci/bags` | — (not implemented, returns success) |
| 5 | `POST /api/v1/oci/checkin` | Valid booking reference and departure airport |
| 6 | `POST /api/v1/oci/boarding-docs` | Ticket numbers from step 5 |

**Expected outcomes:**
- `documentcheck` → `status: OK`, `requirements: []`
- `apischeck` → `apisStatus: ACCEPTED`, `fineRisk: LOW`
- Step 5 returns `200 OK` with `checkedIn` ticket numbers
- Step 6 returns boarding cards with BCBP strings

---

### Scenario 2 — US destination, ESTA advisory (happy path)

**Trigger values:** Valid passport number; given name without `ALAN`; route arriving at a US gateway (JFK, LAX, ORD, ATL, DFW, MIA, SFO, SEA, BOS, EWR, IAD, IAH, MCO, MSP, DTW, PHL, LGA, CLT, DEN, PHX, LAS).

**Journey steps:** Identical to Scenario 1.

**Expected outcomes:**
- `documentcheck` → `status: OK`, `advisories` contains one ESTA entry (`type: "ESTA"`, `mandatory: true`)
- `apischeck` → `apisStatus: ACCEPTED`
- Advisory is logged internally and is non-blocking
- Check-in and boarding pass generation succeed as normal

---

### Scenario 3 — Passenger already checked in (happy path)

**Trigger values:** `POST /api/v1/oci/checkin` called a second time for the same booking reference and departure airport.

**Expected outcomes:**
- Timatic checks run again (both phases execute regardless of prior check-in state)
- Response returns `alreadyCheckedIn: true` with the original ticket numbers
- No coupon status change; boarding pass retrieval still succeeds via `POST /api/v1/oci/boarding-docs`

---

### Scenario 4 — Document check failure (`FAIL` passport prefix)

**Trigger value:** `paxInfo.documentNumber` starts with `FAIL` (e.g. `FAIL123456`). Submit this passport number at step 2.

**Journey steps:**

| Step | Endpoint | Key request values | Expected result |
|------|----------|--------------------|-----------------|
| 1 | `POST /api/v1/oci/retrieve` | — | 200 OK |
| 2 | `POST /api/v1/oci/pax` | `"number": "FAIL123456"` | **200 OK** — documents are stored; Timatic is not called here |
| 3 | `POST /api/v1/oci/seats` | — | 200 OK |
| 4 | `POST /api/v1/oci/bags` | — | 200 OK |
| 5 | `POST /api/v1/oci/checkin` | — | **422 Unprocessable Entity** |

**Timatic response at step 5:**
- `documentcheck` → `status: FAILED`, `visaRequired: true`, `requirements[0].type: "VISA"`
- `apischeck` → **not called** (execution stops on document check failure)

**Error message returned to caller:**
```
Travel document check failed for passenger [GivenName] [Surname]: Visa required. Travel document number is not accepted.
```

---

### Scenario 5 — APIS check failure (`ALAN` in given name)

**Trigger value:** `paxInfo.givenNames` contains `ALAN` (case-insensitive, e.g. `ALAN`, `Alan`, `ALAN JAMES`). Use a valid passport number (no `FAIL` prefix).

**Journey steps:**

| Step | Endpoint | Key request values | Expected result |
|------|----------|--------------------|-----------------|
| 1 | `POST /api/v1/oci/retrieve` | — | 200 OK |
| 2 | `POST /api/v1/oci/pax` | `"number": "PA1234567"` | 200 OK |
| 3 | `POST /api/v1/oci/seats` | — | 200 OK |
| 4 | `POST /api/v1/oci/bags` | — | 200 OK |
| 5 | `POST /api/v1/oci/checkin` | Booking has passenger with given name `ALAN JAMES` | **422 Unprocessable Entity** |

**Timatic response at step 5:**
- `documentcheck` → `status: OK` (passes)
- `apischeck` → `apisStatus: REJECTED`, `fineRisk: HIGH`, `warnings[0].code: "WATCHLIST_MATCH"`

**Error message returned to caller:**
```
APIS check failed for passenger ALAN JAMES [Surname]: Passenger name matches a watchlist entry. Online check-in is not permitted.
```

---

### Scenario 6 — Both triggers active (document check fires first)

**Trigger values:** Passport number starting with `FAIL` **and** given name containing `ALAN`.

**Expected outcomes:**
- `documentcheck` runs first and returns `status: FAILED`
- Execution halts; `apischeck` is **never called**
- `422` is returned with the document failure message, not a watchlist message

---

### Scenario 7 — Multi-passenger booking, one passenger blocked

**Trigger values:** Two passengers on the same booking. Passenger 1 has a valid passport and name. Passenger 2 has a given name containing `ALAN` and a valid passport number.

**Expected outcomes:**
- Phase 1 iterates passengers in order
- Passenger 1 passes both Timatic checks
- Passenger 2 fails the APIS check (`WATCHLIST_MATCH`)
- `InvalidOperationException` is thrown; Phase 2 (coupon status updates) is **never reached**
- **Neither** passenger is checked in
- `422` is returned referencing Passenger 2's name

---

### Scenario 8 — Missing travel documents (pre-Timatic guard)

**Trigger:** Call `POST /api/v1/oci/checkin` without having first submitted travel documents via `POST /api/v1/oci/pax`.

**Expected outcomes:**
- The Operations API travel document guard fires before the Delivery MS is called
- Timatic is never reached
- `400 Bad Request`: `"Passenger travel documents have not been submitted."`

---

### Test scenario summary

| Scenario | Passport number | Given name | Doc check result | APIS check result | Final outcome |
|----------|----------------|------------|------------------|--------------------|---------------|
| 1 — Standard (non-US) | `PA1234567` | `JOHN` | OK | ACCEPTED | ✅ 200 — checked in |
| 2 — US route (ESTA) | `PA1234567` | `JOHN` | OK + ESTA advisory | ACCEPTED | ✅ 200 — checked in |
| 3 — Already checked in | any valid | any valid | OK | ACCEPTED | ✅ 200 — `alreadyCheckedIn: true` |
| 4 — Doc failure | `FAIL123456` | `JOHN` | **FAILED** | Not called | ❌ 422 — visa required |
| 5 — APIS failure | `PA1234567` | `ALAN JAMES` | OK | **REJECTED** | ❌ 422 — watchlist match |
| 6 — Both triggers | `FAIL123456` | `ALAN` | **FAILED** | Not called | ❌ 422 — doc failure (first) |
| 7 — Multi-pax, one blocked | mixed | one has `ALAN` | OK / OK | ACCEPTED / **REJECTED** | ❌ 422 — no one checked in |
| 8 — No travel docs | n/a | n/a | Not called | Not called | ❌ 400 — docs missing |

---

## Related documentation

- [Service URLs](../service-urls.md) — live base URL and configuration key
- [API Reference](../api-reference.md#timatic-simulator) — endpoint summary
- [Test Harness](../test-harness.md) — `timatic-journey.json` for end-to-end simulator tests
