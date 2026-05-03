# Web Development Guide

This guide describes how to build the Apex Air Angular web application. Read `../CLAUDE.md` and `system-overview.md` first to understand the domain model and capabilities before writing any code.

---

## Technology Stack

| Layer | Technology |
|-------|-----------|
| Framework | Angular 21 (standalone components, no NgModules) |
| Language | TypeScript |
| Build | Angular CLI (`ng build`, `ng serve`) |
| Test | Vitest (`ng test`) |
| Styling | CSS (component-scoped via Angular's encapsulation) |
| HTTP | Angular `HttpClient` (injected via services) |
| Routing | Angular Router with lazy-loaded components |
| State | Angular signals (`signal`, `computed`, `asReadonly`) |

---

## Application Structure

```
src/Web/src/app/
├── app.ts                   ← root component
├── app.routes.ts            ← top-level route definitions
├── app.config.ts            ← application-level providers
├── components/              ← shared reusable UI components
│   └── airport-combobox/    ← typeahead airport picker (see Shared Components below)
├── pages/                   ← one directory per route group
│   ├── home/
│   ├── search-results/
│   ├── booking/             ← multi-step booking flow
│   │   ├── passengers/
│   │   ├── seats/
│   │   ├── bags/
│   │   ├── payment/
│   │   └── confirmation/
│   ├── manage-booking/      ← post-sale manage booking flow
│   │   ├── detail/
│   │   ├── seat/
│   │   ├── bags/
│   │   ├── bags-payment/
│   │   ├── bags-confirmation/
│   │   ├── change-flight/
│   │   └── cancel/
│   ├── check-in/            ← online check-in flow
│   │   ├── details/
│   │   ├── bags/
│   │   ├── seats/
│   │   ├── payment/
│   │   └── boarding-pass/
│   ├── loyalty/             ← loyalty programme flow
│   │   ├── register/
│   │   ├── account/
│   │   └── password-reset/
│   └── flight-status/
├── services/                ← API clients and state services
│   ├── retail-api.service.ts
│   ├── loyalty-api.service.ts
│   ├── booking-state.service.ts
│   ├── manage-booking-state.service.ts
│   ├── check-in-state.service.ts
│   ├── loyalty-state.service.ts
│   └── theme.service.ts
├── models/                  ← TypeScript interfaces matching API contracts
│   ├── flight.model.ts
│   ├── order.model.ts
│   └── loyalty.model.ts
└── data/
    └── airports.ts          ← static airport data
```

---

## Shared Components

Reusable UI components live in `components/`. Import and declare them in the `imports` array of any standalone page component that needs them — do not re-implement the same UI inline.

### `AirportComboboxComponent` — `components/airport-combobox/airport-combobox.ts`

**Use this component any time a form field needs an airport selection.** Do not build a bespoke airport input or dropdown.

The component implements `ControlValueAccessor` so it integrates with Angular's `ngModel` and reactive forms. The bound value is always the 3-letter IATA airport code (e.g. `'LHR'`). Filtering, dropdown state, and click-outside detection are all handled internally.

**Inputs**

| Input | Type | Default | Purpose |
|-------|------|---------|---------|
| `inputId` | `string` | `'airport'` | Forwarded to the inner `<input id>` — set this so a parent `<label for="...">` works correctly |
| `placeholder` | `string` | `'City or code'` | Placeholder text for the search input |

**Usage with a plain property**

```html
<div class="field">
  <label for="origin-input">From</label>
  <app-airport-combobox
    inputId="origin-input"
    placeholder="City or code"
    [(ngModel)]="origin"
    name="origin">
  </app-airport-combobox>
</div>
```

**Usage with an Angular signal**

```html
<div class="field">
  <label for="airport-input">Departure Airport</label>
  <app-airport-combobox
    inputId="airport-input"
    placeholder="City or airport code"
    [ngModel]="departureAirport()"
    (ngModelChange)="departureAirport.set($event)"
    name="departureAirport">
  </app-airport-combobox>
</div>
```

**Import**

```typescript
import { AirportComboboxComponent } from '../../components/airport-combobox/airport-combobox';

@Component({
  standalone: true,
  imports: [/* ... */, AirportComboboxComponent],
})
```

---

## Routing Conventions

Routes are defined in `app.routes.ts`. Follow these conventions:

- Top-level pages use eager-loaded components for routes that are always reachable from the nav bar (e.g. `HomeComponent`, `CheckInComponent`).
- Multi-step flow sub-pages use **lazy-loaded components** via `loadComponent`:

```typescript
{ path: 'step-name', loadComponent: () => import('./pages/flow/step/step').then(m => m.StepComponent) }
```

- Flow routes are grouped under a parent path with `children`:

```typescript
{
  path: 'flow-name',
  children: [
    { path: '', component: FlowEntryComponent },
    { path: 'step-one', loadComponent: () => ... },
    { path: 'step-two', loadComponent: () => ... },
  ]
}
```

- Unknown paths redirect to `''` via `{ path: '**', redirectTo: '' }`.
- New route groups must be added to `app.routes.ts` and must match the flow described in `design/<domain>.md` for that capability.

---

## API Services

### Retail API service (`retail-api.service.ts`)

The primary HTTP client for the Retail API (Booking, Search, Orders, Check-in, Manage Booking, SSR, Seatmaps). Inject it into page components that need to call these endpoints.

All methods map directly to endpoints defined in `api-reference.md`. Before adding a new method, check `api-reference.md` to confirm the endpoint exists and verify the correct HTTP verb.

### Loyalty API service (`loyalty-api.service.ts`)

HTTP client for the Loyalty API (authentication, registration, profile, points). Inject it into loyalty-flow page components.

### Adding a new API service method

1. Check `api-reference.md` — confirm the endpoint exists and note its verb, path, and request/response shape.
2. Check `models/` — add or extend a TypeScript interface that matches the API response exactly (camelCase field names).
3. Add the method to the relevant service, using `HttpClient` and the correct `Observable<T>` return type.
4. Inject the service into the component that needs it — do not call `HttpClient` directly from components.

---

## State Management

Multi-step flows use dedicated state services with Angular signals to share data across route boundaries without passing it through route params.

| Service | Flow |
|---------|------|
| `booking-state.service.ts` | Search → Basket → Passengers → Seats → Bags → Products → Payment → Confirmation |
| `manage-booking-state.service.ts` | Retrieve order → Detail → Seat/Bag/Change/Cancel actions |
| `check-in-state.service.ts` | Retrieve booking → Details → Bags → Seats → Payment → Boarding pass |
| `loyalty-state.service.ts` | Login → Account → Points |

Follow this pattern for new flows:

```typescript
@Injectable({ providedIn: 'root' })
export class NewFlowStateService {
  private readonly _data = signal<NewFlowState | null>(null);

  readonly data = this._data.asReadonly();
  readonly isReady = computed(() => this._data() !== null);

  setState(state: NewFlowState): void { this._data.set(state); }
  clearState(): void { this._data.set(null); }
}
```

State services should hold only the data needed to progress through the flow. Do not store sensitive card data in state. Do not use `BehaviorSubject` or other RxJS subjects for state — use signals throughout.

---

## Component Conventions

All components are **standalone** (Angular 21, no NgModules):

```typescript
@Component({
  selector: 'app-component-name',
  standalone: true,
  imports: [CommonModule, RouterModule, /* other standalone components */],
  templateUrl: './component-name.html',
  styleUrl: './component-name.css'
})
export class ComponentNameComponent { }
```

Naming:
- Component class: `PascalCaseComponent`
- Selector: `app-kebab-case`
- Files: `kebab-case.ts`, `kebab-case.html`, `kebab-case.css`
- Directory: `pages/<flow>/<step>/`

Do not create components that call API services directly — delegate API calls to a service method and state updates to a state service.

---

## UI conventions

### Numeric column alignment in tables

In all list-view tables in the Terminal Angular app (and by convention the Web app), any column that displays a monetary amount, price, tax, total, or loyalty points value must be **right-justified** — both the `<th>` column header and every `<td>` data cell in that column. This matches standard spreadsheet convention (e.g. Excel) and makes values easy to scan and compare vertically.

Apply `class="text-right"` to the `<th>` and `class="text-right"` (or a semantic equivalent such as `amount-cell`) to each corresponding `<td>`.

Applies to: fares, base prices, taxes, line totals, order amounts, payment amounts, bag pricing, seat pricing, loyalty points balances, points deltas, and any other monetary or points value rendered in a tabular list view.

### Booking reference copy-to-clipboard icon

Every place in the Terminal Angular app where a booking reference (PNR) is displayed to the user **must** include a copy-to-clipboard button immediately after the reference text. This covers: order list tables, order detail headers, new-order confirmation screens (main panel and basket sidebar), payment list tables and detail modals, disruption table and rebook result modals, and customer loyalty transaction tables.

**Pattern — HTML template**

Wrap the booking reference text and copy button together in a `pnr-copy-wrap` span. The `copy-btn` class and `pnr-copy-wrap` class are both defined in global `styles.css` and require no additional component CSS.

```html
<span class="pnr-copy-wrap">
  {{ bookingRef }}
  <button
    class="copy-btn"
    [class.copied]="copiedRef() === bookingRef"
    (click)="copyBookingRef(bookingRef, $event)"
    [title]="copiedRef() === bookingRef ? 'Copied!' : 'Copy booking reference'"
  >
    @if (copiedRef() === bookingRef) {
      <!-- checkmark icon -->
      <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>
    } @else {
      <!-- copy icon -->
      <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>
    }
  </button>
</span>
```

If the booking reference is optional and may be absent (e.g. loyalty transactions), guard with `@if (bookingRef) { ... } @else { — }`.

If the wrapping element has a click handler (e.g. a table row that opens a modal), pass `$event` to the method and call `event?.stopPropagation()` inside it so the click does not bubble.

**Pattern — TypeScript component**

```typescript
copiedRef = signal<string | null>(null);

copyBookingRef(text: string, event?: Event): void {
  event?.stopPropagation();
  navigator.clipboard.writeText(text).then(() => {
    this.copiedRef.set(text);
    setTimeout(() => this.copiedRef.set(null), 2000);
  });
}
```

Use a `string | null` signal (not `boolean`) so the icon state is tracked per-reference when multiple booking references are visible on the same page.

---

## TypeScript Models

Models in `models/` are TypeScript interfaces that mirror the API response contracts defined in `api-reference.md`. Keep them in sync:

- Use camelCase for all property names (matching the JSON responses).
- Use `number` for monetary amounts and always pair with a `currency: string` field.
- Use `string` for ISO 8601 timestamps — Angular templates and `Date` pipes handle conversion.
- Use `string` for IATA codes (airport codes, passenger types, aircraft types).
- Export every interface from its model file for import elsewhere.

---

## Adding New Capability to the Web Application

Follow this workflow when implementing a new feature in the web app:

1. **Read `system-overview.md` and `design/<domain>.md`** — identify the domain capability you are implementing and the user journey it supports.
2. **Read `api-reference.md`** — identify all API endpoints the new pages will consume. Confirm they exist before building the UI.
3. **Read `web.md` (this file)** — confirm which existing service, state service, or model you can reuse before creating new ones.
4. **Create the route group** in `app.routes.ts` following the conventions above.
5. **Add or extend a state service** if the capability is a multi-step flow.
6. **Add API service methods** for each endpoint the flow uses.
7. **Add TypeScript models** in `models/` for any new request/response shapes.
8. **Create page components** under `pages/<flow>/`, one per route step.
9. If the capability requires new backend endpoints that do not yet exist, design them in `design/<domain>.md` and `api-reference.md` first — see `../CLAUDE.md` for the full design workflow.

---

## Pre-completion checklist

Run through this checklist before every commit on an Angular change.

### Lucide icons (Terminal app)

Every Lucide icon referenced in any template must be registered in `src/Terminal/src/app/app.config.ts`. Two steps are required — both must be present:

1. Import the icon symbol from `lucide-angular` in the import list at the top of the file.
2. Add the same symbol to the `LucideIconProvider` object inside the `providers` array.

Omitting either step causes the icon to render as an empty box with no console error, making the omission invisible until the page is viewed in a browser. Before referencing any icon name in a template, search `app.config.ts` for it. If absent, add it.

```typescript
// app.config.ts — both import and registration are required
import { Pencil, Trash2 } from 'lucide-angular';   // ← 1. import

useValue: new LucideIconProvider({
  Pencil, Trash2,                                   // ← 2. register
})
```

### Angular `@for` track expressions

The `track` expression inside Angular `@for` must be a simple, guaranteed-truthy expression. The nullish coalescing operator (`??`) is unreliable in this position and can silently prevent the list from rendering entirely.

Use a ternary expression when a fallback value is needed:

```html
<!-- ✗ Unreliable — can silently break list rendering -->
@for (item of items; track item.id ?? item.name) { ... }

<!-- ✓ Correct -->
@for (item of items; track item.id ? item.id : item.name) { ... }
```

---

## Development Commands

```bash
# Serve locally
ng serve

# Build for production
ng build

# Run unit tests
ng test

# Run end-to-end tests
ng e2e

# Generate a new standalone component
ng generate component pages/flow/step/step-name
```

The development server runs at `http://localhost:4200/` and hot-reloads on file changes.

---

## Cross-References

- **Domain capability model** — `system-overview.md` and `design/<domain>.md`: which capabilities exist and what they do — drive the UI from this.
- **API endpoints** — `api-reference.md`: every endpoint the web app can call; verify verb, path, and shape before coding.
- **Backend implementation** — `api.md`: how backend APIs are built; useful when a needed endpoint does not exist yet.
- **Architecture rules** — `principles/architecture-principals.md`: the web app is a channel consumer — it talks to orchestration APIs only, never to microservices directly.
- **Integration conventions** — `principles/integration-principals.md`: error codes, correlation IDs, and response formats to handle in the UI.
