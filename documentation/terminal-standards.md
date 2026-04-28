# Terminal UX and UI standards

Design and interaction guardrails for the Apex Air contact centre Terminal Angular application (`src/Terminal`). Read alongside `system-overview.md` and `design/<domain>.md` before building any new pages or flows.

---

## Purpose and context

The Terminal is an **internal operator tool** used by Apex Air contact centre agents and supervisors. Its users are trained staff who perform the application repeatedly, under time pressure, on behalf of customers. The design priorities are therefore different from the customer-facing Web app:

- **Efficiency over hand-holding.** Experienced agents scan tables and act quickly. Minimise clicks and page loads.
- **Dense information display.** More data per screen is acceptable — agents need context to serve customers.
- **Precision and clarity.** Ambiguity in labels, amounts, or statuses is not acceptable in an operational tool.
- **Consistent patterns.** Agents build muscle memory. Every list, every form, and every detail page must behave the same way.

---

## Visual design tokens

All visual values are defined as CSS custom properties in `src/Terminal/src/styles.css`. Never hard-code a colour, radius, shadow, or transition in component CSS.

### Core tokens (shared with Web app)

| Token | Purpose |
|-------|---------|
| `--text` | Body text |
| `--bg-light` | Page and panel backgrounds |
| `--border` | Table borders, dividers, input borders |
| `--white` | Surface backgrounds |
| `--radius-sm` / `--radius-md` / `--radius-lg` | Corner radii |
| `--shadow-card` | Card and modal elevation |
| `--transition` | `0.25s ease` for all interactive transitions |

### Terminal-specific tokens

| Token | Purpose |
|-------|---------|
| `--sidebar-width` | Expanded sidebar width |
| `--sidebar-collapsed-width` | Collapsed sidebar width |
| `--topbar-height` | Top bar height (used for scroll-offset calculations) |
| `--term-bg` | Terminal dark background |
| `--term-text` | Terminal foreground text |
| `--term-prompt` | Prompt / accent colour |
| `--term-success` | Success state colour |
| `--term-error` | Error / alert colour |

### Dark mode

Dark mode is toggled by the `.dark` class on `<html>` via `ThemeService`. Component CSS must not hard-code light or dark values — use tokens.

---

## Application layout

### AppShell

All authenticated routes are rendered inside `AppShellComponent`, which provides the sidebar and top bar. Page components must not reproduce navigation chrome — they render only their own content inside the shell outlet.

```
┌────────────────────────────────────────────┐
│  Top bar (logo, user info, theme toggle)   │
├──────────┬─────────────────────────────────┤
│ Sidebar  │  <router-outlet>                │
│ (nav)    │  Page content                   │
│          │                                 │
└──────────┴─────────────────────────────────┘
```

### Sidebar navigation

- Groups: **Operations** (Inventory, Check-in, Flight Management, Watchlist, New Order), **Sales** (Orders, Customers), **Configuration** (Schedules, Fare Families, Fare Rules, Bag Policy, Bag Pricing, Seating, Products, SSR, Users).
- Each nav item has a Lucide icon, a label, and a one-line description shown in the expanded state.
- The sidebar collapses to icon-only on screens narrower than 901 px and can be manually toggled on wider screens.
- Active route is highlighted with the `.active` class on the nav item. Use `routerLinkActive`.
- Do not add nav items for incomplete features. Link only to fully working pages.
- New nav items must be placed in the correct group. Do not create a new group without discussing it first.

### Page width

- Page content fills the available area to the right of the sidebar.
- Constrain table and form content with a max-width where a full-bleed layout would harm readability (e.g. very wide single-column forms).

---

## Icons

The Terminal uses **Lucide Angular** (`lucide-angular`) exclusively. Do not import SVG icons inline or add another icon library.

- All icons used in the app are pre-registered in `app.config.ts`. Add new icons there before referencing them in a template.
- Use icon sizes consistent with their context: 16 px for inline text, 18–20 px for nav items and action buttons, 24 px for empty-state illustrations.
- Icons must always accompany a text label or have an `aria-label` / `title` — never an icon alone as the only affordance for an action.
- Status icons (e.g. check, alert, x-circle) must use the appropriate `--term-success` / `--term-error` token colour, not an inline colour value.

---

## Data tables (list views)

List pages are the primary navigation surface for agents. All list pages must follow this pattern.

### Table structure

- Use a standard HTML `<table>` with `<thead>` and `<tbody>`.
- Column headers (`<th>`) are sentence-case labels, not abbreviations (except well-known codes such as PNR, IATA).
- Rows are clickable to open the detail view — apply a hover highlight and `cursor: pointer` to `<tr>`.
- Do not use nested tables.

### Column conventions

| Column type | Alignment | Notes |
|-------------|-----------|-------|
| Text (names, references) | Left | Default |
| Dates and times | Left | ISO date or `DD MMM YYYY` format |
| Status badges | Centre | |
| Monetary amounts, prices, taxes, totals | **Right** | Apply `class="text-right"` to both `<th>` and `<td>` |
| Loyalty points | **Right** | Apply `class="text-right"` to both `<th>` and `<td>` |
| Action buttons | Right | Align to right edge of table |

Numeric right-alignment applies to: fares, base prices, taxes, line totals, order amounts, payment amounts, bag pricing, seat pricing, points balances, and points deltas.

### Sorting and filtering

- Provide a search/filter input above the table for all list pages. The input filters client-side or triggers an API search depending on data volume.
- Use a `signal<string>` for the search query value, updated via `(input)` binding.
- Do not paginate — use a search-to-filter model instead. If a dataset is too large to return in full, display a search prompt before the table and require a query before showing results.

### Empty and loading states

- While loading, show a spinner centred in the table area — not a blank table.
- When the result set is empty, show a descriptive empty state within the table body area, not a blank table.
- When a search returns no matches, show "No results for [query]" with a clear link to reset the search.

---

## Detail pages

Detail pages (order detail, customer detail, user detail, flight management detail) follow a consistent structure.

### Header section

- Page title: entity type and identifier (e.g. `Order AA-123456`, `Customer: John Smith`).
- Key identifiers (booking reference, customer ID) displayed prominently with copy-to-clipboard.
- Status badge in the header.
- Primary action button(s) in the top-right of the header — maximum two primary actions per detail page.

### Content sections

- Divide detail content into logical sections with `<h2>` section headings and a subtle divider.
- Related data (e.g. passengers on an order, transactions on a payment) uses a compact inner table following the same column conventions as list tables.
- Section ordering: summary information first, then supporting data, then action history or audit trail last.

### Navigation back to list

- Provide a back link or breadcrumb in the page header so agents can return to the list without using the browser back button.

---

## Status badges

Status values (order status, payment status, flight status, booking status) must be rendered as badge chips, not plain text.

- Badge dimensions: `--radius-sm` pill shape, consistent padding.
- Each status has a defined badge colour mapping in `styles.css`. Do not introduce new status colours inline in component CSS — add them to the global token set.
- Badge text is always sentence-case (e.g. `Confirmed`, `Pending`, `Cancelled`).
- Accompany every status badge with a tooltip or label describing the status on hover, for new agents who are not yet familiar with all states.

---

## Forms

### General rules

- Every `<input>` and `<select>` has an associated `<label>` with a matching `for`/`id` pair.
- Group related fields with a `<fieldset>` and `<legend>` (e.g. passenger name, travel document, date range).
- Required fields are not marked with asterisks by default — mark optional fields as `(optional)`.
- Validation errors appear inline beneath the relevant field.

### Search and filter forms

- Search forms submit on Enter and also on explicit button click.
- After submission, focus the results table or the first result row so keyboard users can tab through results without re-clicking.

### Date inputs

- Use `<input type="date">` for date selection. The bound value is always an ISO date string (`YYYY-MM-DD`).
- For departure date + return date pairs, validate that return date is not before departure date before submitting.

### Saving and submitting

- Disable the submit button and show an inline spinner while the API call is in progress.
- On success, show a brief inline success message or navigate to the detail view of the saved entity.
- On API error, re-enable the button and display the error message inline — do not clear the form.

---

## Modals and dialogs

- Use modals for confirmations and quick-view overlays (e.g. payment detail, rebook confirmation) — not for multi-step flows.
- Every modal has a clearly labelled close button (×) in the top-right corner.
- Destructive confirm dialogs (cancel order, refund, delete) must have a clearly labelled Cancel action as the default-focused button, with the destructive action secondary.
- Modals must trap focus while open and restore focus to the trigger element when closed.
- Do not stack modals.

---

## Booking reference display

Every place a booking reference (PNR) appears in the Terminal must include a copy-to-clipboard button immediately after the reference text. This covers: order list, order detail, new-order confirmation, payment list, payment detail, disruption views, and customer loyalty transaction tables.

**HTML pattern:**

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
      <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><polyline points="20 6 9 17 4 12"></polyline></svg>
    } @else {
      <svg xmlns="http://www.w3.org/2000/svg" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path></svg>
    }
  </button>
</span>
```

**TypeScript pattern:**

```typescript
readonly copiedRef = signal<string | null>(null);

copyBookingRef(text: string, event?: Event): void {
  event?.stopPropagation();
  navigator.clipboard.writeText(text).then(() => {
    this.copiedRef.set(text);
    setTimeout(() => this.copiedRef.set(null), 2000);
  });
}
```

Use `string | null` (not `boolean`) so state is tracked per reference when multiple PNRs appear on the same page. The `pnr-copy-wrap` and `copy-btn` classes are defined in global `styles.css` and need no component-level CSS.

If the booking reference may be absent (e.g. loyalty transactions), guard with `@if (bookingRef) { ... } @else { — }`.

If the containing element has a click handler (e.g. a row that opens a modal), pass `$event` and call `event?.stopPropagation()` to prevent bubbling.

---

## Action buttons and destructive operations

- **Primary action**: Solid brand-colour button. One per page or section at most.
- **Secondary action**: Outlined or ghost button.
- **Destructive action** (cancel order, delete record, refund): Danger-coloured button. Always requires a confirmation step — never executes on first click. Confirmation must name the specific entity being affected (e.g. "Cancel order AA-123456?").
- **Icon buttons**: Always include a visible label or a `title` attribute for tooltip text.

---

## Error states

- API errors on page load: show an inline error message with a retry button. Do not show a blank page.
- Not found (order/customer/flight not found): show a specific "not found" message and a link back to the list.
- Validation errors from the API: display field-level messages where possible; fall back to a page-level message.
- Never expose raw error codes, stack traces, or internal error messages in the UI.
- For operations that partially fail (e.g. multi-passenger disruption rebook with some failures), clearly distinguish which records succeeded and which failed.

---

## Responsive behaviour

- The sidebar collapses to icon-only on screens narrower than 901 px. The main content area expands to fill the space.
- All tables are horizontally scrollable on narrow screens — do not truncate or hide columns by default.
- The app is primarily designed for desktop use (1280 px+). Mobile layout is a fallback, not a primary target. Tables and complex forms do not need to be optimised for touch.

---

## Authentication and session UI

- The login page (`/login`) is the only public route. All other routes require authentication.
- If an unauthenticated user navigates to a protected route, redirect to `/login` with a clear "Please log in to continue" message.
- On 401 from any API call, the interceptor automatically logs out and redirects to `/login` — the UI will handle this without component-level code.
- Logged-in user identity (name, role) is visible in the top bar.
- Session data uses `sessionStorage` — it is cleared when the browser tab is closed.

---

## New order flow

The new order flow (`/new-order`) is the Terminal equivalent of the customer booking flow. Apply the same step-by-step pattern as the Web app multi-step flow, with these additions:

- An agent basket sidebar is visible on the right throughout the flow showing the in-progress booking, passenger count, and running total.
- The basket sidebar refreshes after each selection. The total displayed is always from the API, never client-calculated.
- The confirmation page displays the new booking reference with a prominent copy-to-clipboard button.

---

## Aircraft swap and disruption

These are operational screens under time pressure. Apply these additional conventions:

- Affected passengers are listed in a table with checkboxes for bulk selection.
- Actions (rebook, cancel, downgrade) are clearly labelled with their consequences.
- Results (succeeded / failed) are shown inline in the table, row by row — not in a separate results page.
- The flight identifier (flight number + departure date) is always visible in the page header so agents do not lose context.

---

## Accessibility

- Semantic HTML: use `<nav>`, `<main>`, `<table>`, `<th scope="col">` and `<th scope="row">` appropriately.
- All form inputs have associated labels.
- All icons used as standalone actions have an `aria-label` or `title`.
- Keyboard navigation: all interactive elements are reachable and operable via keyboard. Tab order follows visual reading order.
- Modal focus trapping: implemented for all modals.

---

## Cross-references

| Document | When to consult |
|----------|----------------|
| `web.md` | Technical implementation guide for both Web and Terminal Angular apps |
| `web-standards.md` | Customer-facing Web app UX standards — shared patterns (copy-to-clipboard, numeric alignment) |
| `system-overview.md` | Domain model and bounded contexts |
| `design/<domain>.md` | Business rules and data schemas for each domain |
| `authentication.md` | JWT and session handling — Terminal uses Admin API login |
| `principles/security-principals.md` | Input validation, XSS prevention, session security |
