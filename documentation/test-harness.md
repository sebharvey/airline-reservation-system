# Test harness

The test harness is a single-page web application that drives end-to-end API journey tests against deployed Apex Air services. It lives in `src/Services/TestHarness/`.

---

## File inventory

| File | Purpose |
|------|---------|
| `index.html` | Entry point — loads `harness.css` and `harness.js`. Contains the layout: header, journey selector, runtime data banner, step table, and modal overlay. |
| `harness.css` | Styling — dark theme, table layout, status badges, JSON syntax highlighting, modal. |
| `harness.js` | All application logic — journey loading, random test data generation, live API execution, data chaining, result rendering, and log export. |
| `*-journey.json` | Journey definition files — each defines a sequence of API steps for a specific test scenario. |

---

## Journey definition files

| File | Scenario |
|------|----------|
| `admin-journey.json` | Admin — user management |
| `admin-auth-guard-journey.json` | Admin — auth guard 401 checks |
| `flight-order-ax001-journey.json` | Bookflow — AX001 with seat selection |
| `flight-order-ax001-plus5days-journey.json` | Bookflow — AX001 with seat selection (+5 days) |
| `flight-order-ax001-no-seats-journey.json` | Bookflow — AX001 without seats |
| `multi-segment-oneway-journey.json` | Bookflow — one-way connecting DEL → LHR → JFK |
| `multi-segment-return-journey.json` | Bookflow — return connecting DEL → LHR → JFK |
| `bookflow-journey.json` | Bookflow — search, basket, seats, bags & SSR |
| `loyalty-journey.json` | Loyalty — registration, points & profile |
| `oci-1-pax-inbound-journey.json` | OCI — 1 pax inbound check-in |
| `oci-1-pax-outbound-journey.json` | OCI — 1 pax outbound check-in |
| `oci-2-pax-inbound-journey.json` | OCI — 2 pax inbound check-in |
| `oci-2-pax-outbound-journey.json` | OCI — 2 pax outbound check-in |
| `payment-journey.json` | Payment — authorise, settle, void & refund |
| `terminal-customer-journey.json` | Terminal — customer search & management |
| `user-journey.json` | User — account creation & authentication |
| `fare-rules-journey.json` | Fare rules — create, edit & remove |
| `timatic-journey.json` | Timatic Simulator — document check, APIS check, realtime gate check |
| `operations-journey.json` | Operations — SSIM import & inventory *(disabled)* |

---

## Journey JSON schema

Each file has this top-level shape:

```json
{
  "journey": {
    "name": "Human-readable title",
    "description": "What the journey tests",
    "domain": "Retail / Operations",
    "baseUrls": [
      { "id": "retail", "label": "Retail API", "url": "https://..." },
      { "id": "operations", "label": "Operations API", "url": "https://..." }
    ]
  },
  "steps": [ ... ]
}
```

### Step object

| Field | Type | Description |
|-------|------|-------------|
| `step` | number | Sequential step number (1-based). |
| `name` | string | Short human-readable name shown in the table. |
| `description` | string | Detailed explanation shown in the modal. |
| `type` | `"positive"` or `"negative"` | Whether the step expects success or failure. |
| `apiCall.method` | string | HTTP method (`GET`, `POST`, `PUT`, `DELETE`, `PATCH`). |
| `apiCall.endpoint` | string | URL path, may contain `{param}` placeholders. |
| `apiCall.baseUrlRef` | string | References a `baseUrls[].id` from the journey header. |
| `apiCall.pathParams` | object | Maps `{param}` names to values or `"from-step-N"` references. |
| `apiCall.note` | string | Optional note displayed below the endpoint in the table. |
| `request.headers` | object | HTTP headers to send. |
| `request.body` | any | JSON request body. May contain `__RAND_*__` or `__CHAIN_*__` placeholders. |
| `request.dataChain` | array | Declares which fields in the body come from previous step responses. |
| `request.diff` | object | Optional — documents field-level changes (from/to) for display only. |
| `response.statusCode` | number | Example status code. |
| `response.body` | any | Example response body (for documentation, not assertion). |
| `expected.statusCode` | number | Status code the test asserts against. |
| `expected.description` | string | Human-readable explanation of the expected outcome. |
| `expected.keyFields` | array | Field names highlighted as important in the response. |
| `expected.validationNotes` | string | Additional notes about validation rules. |
| `expected.assertions` | array | Optional — structured assertions (e.g. `{ "field": "passengers", "assertion": "count", "expected": 2 }`). |
| `chainsTo` | array | Declares which response fields feed into subsequent steps. |

### Data chaining

Steps pass data forward via the `chainsTo` and `request.dataChain` mechanism:

- **`chainsTo`** on a step's response declares output variables:
  - `field` — the response field name to capture.
  - `as` — the chain variable name (alias).
  - `usedInSteps` — which subsequent steps consume it.
  - `path` — dot-separated path for nested extraction (e.g. `passengers[0].ticketNumber`).
  - `randomArrayPath` — wildcard path (e.g. `flights[*].cabins[*].fareFamilies[*].offer.offerId`) to collect all matching values and pick one at random.
  - `randomAvailableSeatFrom` — special mode for seat selection: picks a random available seat from a cabin array.

- **`request.dataChain`** on a consuming step declares inputs:
  - `field` — the request body field to populate.
  - `from` — the chain variable name to read from.
  - `source` — human-readable label (e.g. `"Step 3 → response.basketId"`).

- **`__CHAIN_varName__`** placeholders in request body strings are resolved at runtime from the chain.

---

## Runtime variable placeholders

Journey JSON files use `__RAND_*__` placeholders that are replaced at runtime with randomly generated test data. The harness auto-detects whether a journey uses these by scanning for `__RAND_` in the serialised steps.

### PAX-1 (primary passenger) placeholders

| Placeholder | Source | Example |
|-------------|--------|---------|
| `__RAND_GIVEN_NAME__` | Random first name from pool | `Amara` |
| `__RAND_SURNAME__` | Random surname from pool | `Patel` |
| `__RAND_EMAIL__` | `givenname.surname.NNNNNN@testmail.example.com` | `amara.patel.482910@testmail.example.com` |
| `__RAND_PASSWORD__` | Fixed: `Apex@ir2026!` | `Apex@ir2026!` |
| `__RAND_GENDER__` | `Male` or `Female` | `Female` |
| `__RAND_DOB__` | Random date 1950–2005 | `1987-04-12` |
| `__RAND_PHONE__` | `07` + 9 random digits | `07482910384` |
| `__RAND_LOYALTY_NUMBER__` | `AX` + 7 random digits | `AX4829103` |
| `__RAND_GENDER_CODE__` | ICAO gender code: `M` or `F` (derived from `__RAND_GENDER__`) | `F` |
| `__RAND_DOC_NUMBER__` | 9 random digits (passport/travel document number) | `482910384` |
| `__RAND_DOC_EXPIRY__` | Random date 5–10 years ahead | `2031-09-14` |
| `__RAND_MRZ_LINE1__` | ICAO 9303 TD3 MRZ line 1 built from PAX-1 name | `P<GBRPATEL<<AMARA<<<...` |
| `__RAND_MRZ_LINE2__` | ICAO 9303 TD3 MRZ line 2 built from PAX-1 doc data | `4829103847GBR870412...` |

### PAX-2 (second passenger) placeholders

Used in 2-pax journey files. Guaranteed to differ from PAX-1 values.

| Placeholder | Source | Example |
|-------------|--------|---------|
| `__RAND_GIVEN_NAME_2__` | Random first name (different from PAX-1) | `Liam` |
| `__RAND_SURNAME_2__` | Random surname (different from PAX-1) | `Garcia` |
| `__RAND_EMAIL_2__` | `givenname.surname.NNNNNN@testmail.example.com` | `liam.garcia.719204@testmail.example.com` |
| `__RAND_GENDER_2__` | `Male` or `Female` (independent of PAX-1) | `Male` |
| `__RAND_DOB_2__` | Random date 1950–2005 (independent of PAX-1) | `1992-11-03` |
| `__RAND_PHONE_2__` | `07` + 9 random digits | `07193847562` |

### Recipient placeholders (loyalty transfer)

| Placeholder | Source |
|-------------|--------|
| `__RAND_RECIPIENT_GIVEN_NAME__` | Random first name |
| `__RAND_RECIPIENT_SURNAME__` | Random surname |
| `__RAND_RECIPIENT_EMAIL__` | Formatted email |
| `__RAND_RECIPIENT_PASSWORD__` | Fixed: `Apex@ir2026!` |

### Route and date placeholders

| Placeholder | Source |
|-------------|--------|
| `__RAND_OUTBOUND_ORIGIN__` | Route origin (e.g. `LHR`) |
| `__RAND_OUTBOUND_DEST__` | Route destination (e.g. `JFK`) |
| `__RAND_RETURN_ORIGIN__` | Return origin (same as outbound dest) |
| `__RAND_RETURN_DEST__` | Return destination (same as outbound origin) |
| `__RAND_DEPART_DATE__` | Tomorrow (today + 1 day) |
| `__RAND_RETURN_DATE__` | Depart date + 7 days |
| `__IN_5_DAYS_DATE__` | Today + 5 days |

---

## harness.js architecture

The file is an immediately-invoked async function with these major sections:

| Section | Lines (approx) | Responsibility |
|---------|----------------|----------------|
| Config dropdown | Top | Populates the journey selector from a `CONFIGS` array. |
| Name pools | ~41–81 | `FIRST_NAMES` and `SURNAMES` arrays for random data generation. |
| `generateRuntimeVars()` | ~93–150 | Builds the `runtimeVars` object with all random values for the current run. |
| `applyRuntimeVars()` | ~152–171 | Recursive string replacement — walks any object/array and replaces `__RAND_*__` tokens. PAX-2 placeholders (`_2` suffix) are replaced **before** PAX-1 placeholders to avoid partial matches. |
| API interaction log | ~177–260 | Stores per-step request/response data. Supports per-step and full-journey log export. |
| Copy Logs button | ~296–350 | Copies the full API log to clipboard. |
| `loadJourney()` | ~359–364 | Fetches `<config>-journey.json` and calls `initJourney()`. |
| `initJourney()` | ~383–420 | Sets page title, detects runtime vars, renders base URL inputs, health-checks endpoints, builds the table. |
| Base URL inputs | ~410–463 | Renders editable base URL fields. Runs health checks (`GET /api/health`) on load. |
| Table setup | ~464–507 | Builds `<tr>` rows from steps, wires up click-to-expand modal. |
| Modal | ~508–627 | Shows step detail on row click — request/response bodies, live results, diff, chain info. |
| Runtime data banner | ~628–650 | Shows the current random values (name, email, route, dates) in a banner above the table. |
| `buildStepsWithVars()` | ~656–663 | Deep-clones steps and applies runtime variable substitution. |
| Run All / Next Step | ~665–744 | Orchestrates sequential step execution. "Run All" executes every step. "Next" executes one step at a time. |
| `runStep()` | ~840–1011 | Core execution — builds URL with path params, substitutes chained data into body and headers, calls `fetch()`, logs the interaction, extracts chained values from the response, evaluates assertions. |
| `collectAllValues()` | ~789–803 | Walks wildcard paths like `flights[*].cabins[*].fareFamilies[*].offer.offerId`. |
| `getPath()` / `setPath()` | ~806–838 | Navigates dot-separated paths with array index support. |
| `evaluateAssertions()` | ~1013–1024 | Runs structured assertions (currently supports `count`). |
| `resolvePathParamChains()` | ~1026–1049 | Resolves `from-step-N` references in path parameters after a step completes. |
| Cell builders | ~1051–1178 | `buildApiCallCell`, `buildRequestCell`, `buildResponseCell`, `buildExpectedCell` — construct table cell HTML. |
| Helpers | ~1180–1228 | `esc()` (HTML escaping), `statusLabel()`, `syntaxHighlight()` (JSON with colour). |

---

## index.html structure

The page layout consists of:

1. **Header** — `<h1>` with journey title (populated by JS).
2. **Journey selector** — `<select id="configSelect">` dropdown.
3. **Base URL list** — `<div id="baseUrlList">` — editable service URLs with health-check indicators.
4. **Control buttons** — Run, Next, Copy Logs.
5. **Runtime data banner** — `<div id="runtimeDataBanner">` — shows random test values for the current run.
6. **Step table** — `<table class="harness-table">` with columns: Step, Name, API Call, Expected, Time.
7. **Modal overlay** — `<div id="stepModal">` — click any row to see full request/response detail.

---

## How to add a new journey

1. Create `src/Services/TestHarness/<name>-journey.json` following the schema above.
2. Add an entry to the `CONFIGS` array at the top of `harness.js`:
   ```js
   { value: '<name>', label: 'Human-Readable Label' }
   ```
3. Define steps with appropriate `chainsTo` and `dataChain` entries to thread data between steps.

## How to add a new runtime variable

1. Generate the value in `generateRuntimeVars()` in `harness.js`.
2. Add it to the `runtimeVars` object.
3. Add a `.replace(/__RAND_NEW_VAR__/g, runtimeVars.newVar)` line in `applyRuntimeVars()`. Place more-specific patterns (e.g. `_2` suffix variants) **before** their shorter counterparts to avoid partial matches.
4. Use `__RAND_NEW_VAR__` in journey JSON files.
