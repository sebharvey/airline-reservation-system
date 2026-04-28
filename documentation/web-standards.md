# Web UX and UI standards

Design and interaction guardrails for the Apex Air customer-facing Angular web application (`src/Web`). Read alongside `system-overview.md` and `design/<domain>.md` before building any new pages or flows.

---

## Guiding principles

- **Clarity over density.** Customers should always know where they are, what they have selected, and what happens next.
- **Progressive disclosure.** Reveal detail when needed, not all at once. Multi-step flows show only the current step.
- **Consistent visual language.** All pages share the same colour tokens, typography, spacing scale, and component patterns — never introduce one-off styles.
- **Accessible by default.** Every interactive element is keyboard-reachable and labelled.

---

## Visual design tokens

All visual values are defined as CSS custom properties in `src/Web/src/styles.css`. Never hard-code a colour, radius, shadow, or transition in component CSS — always reference a token.

### Colour tokens

| Token | Light value | Purpose |
|-------|-------------|---------|
| `--text` | Near-black | Body text |
| `--bg-light` | Off-white | Page and card backgrounds |
| `--border` | Light grey | Dividers, input borders |
| `--white` | White | Surface backgrounds, reversed text |
| `--grad-start` / `--grad-end` | Purple → Blue | Brand gradient (hero, primary CTA) |

### Decoration tokens

| Token | Purpose |
|-------|---------|
| `--hero-gradient` | Full gradient value for hero sections and primary buttons |
| `--radius-sm` / `--radius-md` / `--radius-lg` | Corner radii — use the smallest that fits the element size |
| `--shadow-card` | Elevation shadow for cards and modals |
| `--transition` | `0.25s ease` — all interactive transitions |

### Dark mode

Dark mode is applied by adding the `.dark` class to `<html>` (toggled by `ThemeService`). Both `:root` and `html.dark` define the complete token set. Component CSS must never hard-code light or dark values — using tokens automatically supports both modes.

---

## Typography

- **Body text**: System sans-serif stack. Do not introduce web fonts.
- **Monospace**: Used only for booking references (PNRs), e-ticket numbers, and the NDC/XML debug view.
- **Hierarchy**: Use semantic headings (`<h1>` → `<h3>`) — do not simulate heading levels with bold spans or font-size overrides.
- **Numbers and amounts**: Tabular figures in all table columns so decimal points align vertically.

---

## Layout and spacing

- **Page max-width**: Constrained centrally — do not allow content to span the full viewport width on large screens.
- **Spacing scale**: Use multiples of 4 px via `rem` or the spacing tokens. Do not use arbitrary pixel values.
- **Cards**: Wrap distinct content units (flight results, passenger summaries, basket items) in a card with `--shadow-card` and `--radius-md`. Cards must not be nested inside other cards.
- **Section separation**: Use whitespace and subtle borders; avoid heavy dividers or box shadows within a section.

---

## Responsive design

The web app is customer-facing and must be fully usable on mobile.

- Design mobile-first. Width breakpoints are defined in `styles.css` — do not introduce new breakpoints without a clear reason.
- Multi-column layouts (e.g. search form, flight results) collapse to a single column below 768 px.
- Touch targets (buttons, links) are at minimum 44 × 44 px.
- The sticky header and any sticky CTA bars must not obscure scrollable content on small screens.
- Test every new page at 375 px width before marking it complete.

---

## Colour and brand usage

- **Primary gradient** (`--hero-gradient`): hero backgrounds, primary booking CTA buttons (one per page or flow step), progress indicators.
- **Secondary actions**: Outlined or ghost buttons — never a second gradient button alongside the primary CTA.
- **Destructive actions** (cancel, remove): Distinct red/danger colour. Must include a confirmation step — do not execute immediately on first click.
- **Status colours**: Use consistent semantic colours for flight statuses (on-time, delayed, cancelled), payment outcomes (success, failed), and form validation. Define new status colours as tokens in `styles.css`, never inline.

---

## Multi-step booking flows

All multi-step flows (Booking, Manage Booking, Check-in) follow these patterns.

### Step structure

- Each step is a full page component with a single primary action (`Continue`, `Pay`, `Confirm`).
- The primary CTA is always at the bottom of the form content, full-width on mobile.
- Secondary navigation (Back, Cancel) is present but visually subordinate to the primary CTA.
- Never present two CTAs of equal visual weight on the same step.

### Progress indication

- Show a step indicator at the top of each flow page. It must display the current step position (e.g. step 2 of 5) and completed steps.
- Do not skip or reorder steps in the indicator even if a step is conditionally bypassed in the route.

### State persistence across steps

- Flow state is held in the dedicated state service (e.g. `BookingStateService`) using Angular signals — not in route params, query strings, or `localStorage`.
- Each step reads its initial values from the state service and writes back on advance.
- If a user navigates directly to a mid-flow URL with no state, redirect to the flow entry page.
- Clear state when the flow completes or is abandoned.

### Pricing and totals

- Display a running total or basket summary visible on every flow step after the passenger/product selection.
- Prices are locked at search time (stored offer). Never show a recalculated or estimated total — only show prices retrieved from the API.
- Currency and amount format: amount followed by ISO currency code, e.g. `£125.00 GBP`. Do not omit the currency symbol or code.

---

## Forms

- Every `<input>` and `<select>` has an associated `<label>` with a matching `for`/`id` pair. Never use `placeholder` as a substitute for a label.
- Validation errors appear inline beneath the relevant field, not in a top-level alert banner.
- Required fields are not marked with asterisks by default — instead, mark optional fields explicitly as `(optional)`.
- Date of birth and travel document expiry fields use separate day / month / year inputs, not a free-text date string.
- **Airport selection**: Always use `AirportComboboxComponent`. Never build a bespoke airport dropdown.
- **Payment**: Always use `PaymentFormComponent`. Never build a bespoke card input form.

### Form submit states

- Disable the submit button and show an inline loading indicator while an API call is in progress.
- On API error, re-enable the button and show the error message inline — do not clear the form.
- On success, navigate to the next step or confirmation page; do not flash a success toast and stay on the same page.

---

## Loading and skeleton states

- Show a loading spinner or skeleton placeholder whenever the page is waiting for an API response on first load.
- Do not show blank content areas while data is loading — use skeleton shapes that approximate the content dimensions.
- For in-place updates (e.g. repricing after a selection), show a localised spinner on the affected element, not a full-page overlay.

---

## Error states

- **API errors on page load**: Show a clear error message with a retry action. Do not show a blank page.
- **Not found (booking reference invalid)**: Show a specific "booking not found" message and a link back to the entry point.
- **Network timeout**: Inform the user and allow retry. Do not silently fail.
- **Validation errors from the API**: Display field-level messages where possible; fall back to a page-level message for non-field errors.
- Never expose raw error codes, stack traces, or internal error messages to the customer.

---

## Empty states

When a data list (e.g. flight results, loyalty transactions) returns with zero items, show a meaningful empty state message and a suggested action. Do not show an empty container or a raw "no results" string.

---

## Flight and booking data display

### Flight information

- **Route**: Origin → Destination using airport IATA codes and city names. Format: `London Heathrow (LHR) → New York JFK (JFK)`.
- **Departure and arrival times**: Local times in `HH:mm` with the date when the arrival date differs from the departure date.
- **Duration and stops**: Show total journey duration and the number of stops. For connecting flights, show the layover airport and duration.
- **Cabin class and fare family**: Display the commercial name (e.g. `Economy Lite`, `Business Flex`), not the internal fare code.

### Passenger names

- Display in title-case: `Mr John Smith`. Never all-caps or all-lowercase.

### Booking reference (PNR)

- Use a monospace font.
- Always render with a copy-to-clipboard button immediately after the reference — see the copy-to-clipboard pattern below.

---

## Copy-to-clipboard for booking references

Every booking reference displayed to the customer must include a copy icon button.

**HTML:**

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

**TypeScript:**

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

Use `string | null` (not `boolean`) so state is tracked per reference when multiple PNRs appear on the same page.

---

## Accessibility

- Semantic HTML: use `<nav>`, `<main>`, `<section>`, `<article>`, `<header>`, `<footer>` appropriately.
- Focus management: after a route transition, move focus to the page `<h1>` or the first interactive element.
- All images have descriptive `alt` text. Decorative images use `alt=""`.
- Colour contrast must meet WCAG AA (4.5:1 for body text, 3:1 for large text and UI components).
- Interactive elements must never rely solely on colour to convey state — use icons, labels, or patterns in addition.
- `AirportComboboxComponent` handles its own ARIA — do not add extra `aria-*` attributes to it externally.

---

## Boarding passes

- Display as a card with a horizontal divider styled as a perforation.
- QR code is generated client-side using the `qrcode` library. Render at minimum 200 × 200 px with a white quiet zone.
- Boarding pass cards must be print-friendly. Wrap them in a `@media print` style that removes the navigation, hides non-essential elements, and expands the card to full width.

---

## Numeric alignment in tables

Any column displaying a monetary amount, price, tax, total, or loyalty points value must be right-justified — both the `<th>` header and every `<td>` cell. Apply `class="text-right"` to both.

---

## Navigation

- The top navigation bar links are: Search, Manage Booking, Check-in, Flight Status, Loyalty (in that order).
- Active route is indicated in the nav — use `routerLinkActive` with the global `.active` class.
- Do not add navigation items for in-progress features. Link only to fully functional pages.

---

## Cross-references

| Document | When to consult |
|----------|----------------|
| `web.md` | Technical implementation guide — components, services, routing, state |
| `terminal-standards.md` | Agent-facing UI standards — shared patterns such as copy-to-clipboard and numeric alignment |
| `system-overview.md` | Domain model and bounded contexts |
| `design/<domain>.md` | User journey steps and business rules for each domain |
| `principles/security-principals.md` | XSS prevention, input sanitisation, token handling in the browser |
