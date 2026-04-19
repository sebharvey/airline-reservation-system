import { Component, inject, signal, computed, effect } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AirportComboboxComponent } from '../../components/airport-combobox/airport-combobox';
import { Router } from '@angular/router';
import {
  NewOrderService,
  SearchOffer,
  BasketSummary,
  BasketFlight,
  ConfirmResponse,
  BasketPassenger,
  SeatOffer,
  CabinSeatmap,
  Seatmap,
  BasketSeatSelection,
  Product,
  ProductPrice,
  ProductCatalogue,
  BasketProductSelection,
} from '../../services/new-order.service';

interface PassengerForm {
  passengerId: string;
  type: 'ADT' | 'CHD' | 'INF';
  givenName: string;
  surname: string;
  dob: string;
  gender: string;
  email: string;
  phone: string;
  loyaltyNumber: string;
}

interface FlightGroup {
  flightNumber: string;
  origin: string;
  destination: string;
  departureTime: string;
  arrivalTime: string;
  arrivalDayOffset: number;
  durationMinutes: number;
  aircraftType: string;
  offers: SearchOffer[];
}

interface SeatmapEntry {
  flight: BasketFlight;
  seatmap: Seatmap | null;
  loading: boolean;
  error: string;
}

interface SelectedSeat {
  passengerId: string;
  segmentId: string;
  basketItemId: string;
  seatOffer: SeatOffer;
}

interface SelectedProduct {
  productId: string;
  offerId: string;
  name: string;
  passengerId: string | null;
  segmentId: string | null;
  unitPrice: number;
  tax: number;
  currencyCode: string;
}

type Step = 'search' | 'outbound-results' | 'inbound-results' | 'passengers' | 'seats' | 'products' | 'payment' | 'confirmed';

const CABIN_ORDER: Record<string, number> = { F: 0, J: 1, W: 2, Y: 3 };

@Component({
  selector: 'app-new-order',
  imports: [FormsModule, AirportComboboxComponent],
  templateUrl: './new-order.html',
  styleUrl: './new-order.css',
})
export class NewOrderComponent {
  #svc = inject(NewOrderService);
  #router = inject(Router);

  constructor() {
    // Auto-set return date to +14 days when outbound date changes, if return date is not yet set.
    effect(() => {
      const outbound = this.outboundDate();
      if (outbound && !this.returnDate()) {
        const d = new Date(outbound + 'T00:00:00');
        d.setDate(d.getDate() + 14);
        this.returnDate.set(d.toISOString().slice(0, 10));
      }
    });
  }

  // ── Search form ──────────────────────────────────────────────────────────
  tripType = signal<'one-way' | 'return'>('one-way');
  origin = signal('LHR');
  destination = signal('JFK');
  outboundDate = signal((() => { const d = new Date(); d.setDate(d.getDate() + 1); return d.toISOString().slice(0, 10); })());
  returnDate = signal('');
  adults = signal(1);
  children = signal(0);
  infants = signal(0);
  isStandby = signal(false);

  totalPax = computed(() => this.adults() + this.children() + this.infants());

  // ── Flow state ───────────────────────────────────────────────────────────
  step = signal<Step>('search');
  loading = signal(false);
  error = signal('');

  // ── Search results ───────────────────────────────────────────────────────
  outboundOffers = signal<SearchOffer[]>([]);
  inboundOffers = signal<SearchOffer[]>([]);
  selectedOutboundOffer = signal<SearchOffer | null>(null);
  selectedInboundOffer = signal<SearchOffer | null>(null);

  outboundFlightGroups = computed<FlightGroup[]>(() =>
    this.#groupOffersByFlight(this.outboundOffers())
  );

  inboundFlightGroups = computed<FlightGroup[]>(() =>
    this.#groupOffersByFlight(this.inboundOffers())
  );

  // ── Basket ───────────────────────────────────────────────────────────────
  basket = signal<BasketSummary | null>(null);

  // ── Passengers ───────────────────────────────────────────────────────────
  passengerForms = signal<PassengerForm[]>([]);

  passengersValid = computed(() =>
    this.passengerForms().length > 0 &&
    this.passengerForms().every(p => p.givenName.trim() && p.surname.trim())
  );

  // ── Accordion (booking panel) ─────────────────────────────────────────────
  accordionSection = signal<'passengers' | 'seats' | 'products' | 'payment'>('passengers');

  // ── Payment ──────────────────────────────────────────────────────────────
  payMethod = signal<'CreditCard' | 'Cash'>('CreditCard');
  cardNumber = signal('');
  expiryMonth = signal('');
  expiryYear = signal('');
  cardCvv = signal('');
  cardName = signal('');
  paymentSubmitted = signal(false);

  readonly cardDisplayNumber = computed(() => {
    const raw = this.cardNumber().replace(/\D/g, '').substring(0, 16);
    return raw.replace(/(.{4})/g, '$1 ').trim();
  });

  readonly expiryMonths = computed(() => [
    { value: '01', label: '01 — Jan' }, { value: '02', label: '02 — Feb' },
    { value: '03', label: '03 — Mar' }, { value: '04', label: '04 — Apr' },
    { value: '05', label: '05 — May' }, { value: '06', label: '06 — Jun' },
    { value: '07', label: '07 — Jul' }, { value: '08', label: '08 — Aug' },
    { value: '09', label: '09 — Sep' }, { value: '10', label: '10 — Oct' },
    { value: '11', label: '11 — Nov' }, { value: '12', label: '12 — Dec' },
  ]);

  readonly expiryYears = computed(() => {
    const cur = new Date().getFullYear();
    return Array.from({ length: 12 }, (_, i) => cur + i);
  });

  detectCardType(): string {
    const num = this.cardNumber().replace(/\D/g, '');
    if (num.startsWith('4')) return 'Visa';
    if (/^5[1-5]/.test(num) || /^2[2-7]/.test(num)) return 'Mastercard';
    if (/^3[47]/.test(num)) return 'Amex';
    return 'Card';
  }

  onCardNumberInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const digits = input.value.replace(/\D/g, '').substring(0, 16);
    this.cardNumber.set(digits);
    input.value = digits.replace(/(.{4})/g, '$1 ').trim();
  }

  fillTestCard(): void {
    const nextYear = (new Date().getFullYear() + 3).toString();
    this.cardName.set('Test Agent');
    this.cardNumber.set('4111111111111111');
    this.expiryMonth.set('12');
    this.expiryYear.set(nextYear);
    this.cardCvv.set('123');
  }

  // ── Seats ─────────────────────────────────────────────────────────────────
  seatmapEntries = signal<SeatmapEntry[]>([]);
  activeSeatPaxIdx = signal(0);
  activeSeatFlightIdx = signal(0);
  seatSelections = signal<Map<string, SelectedSeat>>(new Map());
  seatsSaving = signal(false);
  seatsError = signal('');

  readonly basketFareTotals = computed(() => {
    const flights = this.basket()?.flights ?? [];
    return {
      fare: flights.reduce((s, f) => s + f.fareAmount, 0),
      tax: flights.reduce((s, f) => s + f.taxAmount, 0),
    };
  });

  readonly activeSeatEntry = computed(() => this.seatmapEntries()[this.activeSeatFlightIdx()] ?? null);
  readonly activeSeatPax = computed(() => this.passengerForms()[this.activeSeatPaxIdx()] ?? null);
  readonly activeSeatCabin = computed((): CabinSeatmap | null => {
    const entry = this.activeSeatEntry();
    if (!entry?.seatmap) return null;
    return entry.seatmap.cabins.find(c => c.cabinCode === entry.flight.cabinCode)
      ?? entry.seatmap.cabins[0]
      ?? null;
  });

  // ── Products ─────────────────────────────────────────────────────────────
  productCatalogue = signal<ProductCatalogue | null>(null);
  productSelections = signal<Map<string, SelectedProduct>>(new Map());
  productsLoading = signal(false);
  productsError = signal('');

  readonly productTotal = computed(() => {
    let total = 0;
    for (const sel of this.productSelections().values()) {
      total += sel.unitPrice + sel.tax;
    }
    return total;
  });

  // ── Confirmation ─────────────────────────────────────────────────────────
  confirmed = signal<ConfirmResponse | null>(null);

  // ── Search ───────────────────────────────────────────────────────────────

  async search(): Promise<void> {
    if (!this.origin() || !this.destination() || !this.outboundDate()) {
      this.error.set('Please fill in origin, destination, and outbound date.');
      return;
    }
    if (this.tripType() === 'return' && !this.returnDate()) {
      this.error.set('Please fill in the return date.');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    this.outboundOffers.set([]);
    this.selectedOutboundOffer.set(null);
    this.selectedInboundOffer.set(null);
    this.basket.set(null);
    this.confirmed.set(null);
    try {
      const bookingType = this.isStandby() ? 'Standby' : 'Revenue';
      const res = await this.#svc.searchSlice(
        this.origin(),
        this.destination(),
        this.outboundDate(),
        this.totalPax(),
        bookingType,
      );
      this.outboundOffers.set(res.offers);
      this.step.set('outbound-results');
    } catch (err: any) {
      this.error.set(err?.error?.message ?? 'Search failed. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  async selectOutbound(offer: SearchOffer): Promise<void> {
    this.selectedOutboundOffer.set(offer);
    this.error.set('');

    if (this.tripType() === 'return') {
      this.loading.set(true);
      try {
        const res = await this.#svc.searchSlice(
          this.destination(),
          this.origin(),
          this.returnDate(),
          this.totalPax(),
          this.isStandby() ? 'Standby' : 'Revenue',
        );
        this.inboundOffers.set(res.offers);
        this.step.set('inbound-results');
      } catch (err: any) {
        this.error.set(err?.error?.message ?? 'Return search failed. Please try again.');
        this.selectedOutboundOffer.set(null);
      } finally {
        this.loading.set(false);
      }
    } else {
      await this.#createBasketAndContinue([{ offerId: offer.offerId, sessionId: offer.sessionId }], this.totalPax());
    }
  }

  async selectInbound(offer: SearchOffer): Promise<void> {
    this.selectedInboundOffer.set(offer);
    this.error.set('');
    const outbound = this.selectedOutboundOffer();
    if (!outbound) return;
    await this.#createBasketAndContinue([
      { offerId: outbound.offerId, sessionId: outbound.sessionId },
      { offerId: offer.offerId, sessionId: offer.sessionId },
    ], this.totalPax());
  }

  // ── Passengers ───────────────────────────────────────────────────────────

  updatePassengerField(index: number, field: keyof PassengerForm, value: string): void {
    this.passengerForms.update(forms => {
      const updated = [...forms];
      updated[index] = { ...updated[index], [field]: value };
      return updated;
    });
  }

  async savePassengers(): Promise<void> {
    if (!this.passengersValid()) {
      this.error.set('Please fill in first and last name for all passengers.');
      return;
    }
    const basketSummary = this.basket();
    if (!basketSummary) return;

    const passengers: BasketPassenger[] = this.passengerForms().map((f, idx) => ({
      passengerId: f.passengerId,
      type: f.type,
      givenName: f.givenName.trim(),
      surname: f.surname.trim(),
      dob: f.dob || null,
      gender: f.gender || null,
      loyaltyNumber: f.loyaltyNumber.trim() || null,
      contacts:
        idx === 0
          ? { email: f.email.trim() || null, phone: f.phone.trim() || null }
          : null,
      docs: [],
    }));

    this.loading.set(true);
    this.error.set('');
    try {
      await this.#svc.updatePassengers(basketSummary.basketId, passengers);
      if (this.isStandby()) {
        this.step.set('payment');
        this.accordionSection.set('payment');
      } else {
        this.step.set('seats');
        this.accordionSection.set('seats');
        this.activeSeatPaxIdx.set(0);
        this.activeSeatFlightIdx.set(0);
        this.#loadSeatmaps();
      }
    } catch (err: any) {
      this.error.set(err?.error?.message ?? 'Failed to save passengers. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  // ── Seats ─────────────────────────────────────────────────────────────────

  #loadSeatmaps(): void {
    const basket = this.basket();
    if (!basket?.flights?.length) return;
    const entries: SeatmapEntry[] = basket.flights.map(f => ({ flight: f, seatmap: null, loading: true, error: '' }));
    this.seatmapEntries.set(entries);
    basket.flights.forEach((f, idx) => {
      if (!f.inventoryId) {
        this.seatmapEntries.update(list => {
          const updated = [...list];
          updated[idx] = { ...updated[idx], loading: false, error: 'No inventory ID for this flight' };
          return updated;
        });
        return;
      }
      this.#svc.getSeatmap(f.inventoryId, f.flightNumber, f.aircraftType ?? '', f.cabinCode)
        .then(seatmap => {
          this.seatmapEntries.update(list => {
            const updated = [...list];
            updated[idx] = { ...updated[idx], seatmap, loading: false };
            return updated;
          });
        })
        .catch(() => {
          this.seatmapEntries.update(list => {
            const updated = [...list];
            updated[idx] = { ...updated[idx], loading: false, error: 'Failed to load seatmap' };
            return updated;
          });
        });
    });
  }

  selectSeat(seatOffer: SeatOffer, entry: SeatmapEntry): void {
    if (seatOffer.availability !== 'available') return;
    const pax = this.activeSeatPax();
    if (!pax) return;
    if (this.isSeatTakenByOther(seatOffer, entry.flight.basketItemId)) return;
    const key = `${pax.passengerId}__${entry.flight.basketItemId}`;
    const newMap = new Map<string, SelectedSeat>(this.seatSelections());
    if (newMap.get(key)?.seatOffer.seatOfferId === seatOffer.seatOfferId) {
      newMap.delete(key);
    } else {
      newMap.set(key, { passengerId: pax.passengerId, segmentId: entry.flight.inventoryId ?? '', basketItemId: entry.flight.basketItemId, seatOffer });
    }
    this.seatSelections.set(newMap);
  }

  isSeatSelected(seatOffer: SeatOffer, basketItemId: string): boolean {
    const pax = this.activeSeatPax();
    if (!pax) return false;
    const sel = this.seatSelections().get(`${pax.passengerId}__${basketItemId}`);
    return sel?.seatOffer.seatOfferId === seatOffer.seatOfferId;
  }

  isSeatTakenByOther(seatOffer: SeatOffer, basketItemId: string): boolean {
    const pax = this.activeSeatPax();
    if (!pax) return false;
    for (const [key, sel] of this.seatSelections()) {
      if (key.startsWith(pax.passengerId + '__')) continue;
      if (sel.seatOffer.seatOfferId === seatOffer.seatOfferId && sel.basketItemId === basketItemId) return true;
    }
    return false;
  }

  getSeatClass(seatOffer: SeatOffer | null, basketItemId: string): string {
    if (!seatOffer) return 'sm-seat sm-seat-gap';
    if (seatOffer.availability === 'sold' || seatOffer.availability === 'held') return 'sm-seat sm-seat-sold';
    if (this.isSeatSelected(seatOffer, basketItemId)) return 'sm-seat sm-seat-selected';
    if (this.isSeatTakenByOther(seatOffer, basketItemId)) return 'sm-seat sm-seat-other';
    return 'sm-seat sm-seat-available';
  }

  getSeatRows(cabin: CabinSeatmap): number[] {
    const rows: number[] = [];
    for (let r = cabin.startRow; r <= cabin.endRow; r++) rows.push(r);
    return rows;
  }

  getSeatForRowCol(cabin: CabinSeatmap, row: number, col: string): SeatOffer | null {
    return cabin.seats.find(s => s.rowNumber === row && s.column === col) ?? null;
  }

  hasAisle(layout: string, colIndex: number): boolean {
    const groups = layout.split('-').map(Number);
    let count = 0;
    for (let g = 0; g < groups.length - 1; g++) {
      count += groups[g];
      if (colIndex === count - 1) return true;
    }
    return false;
  }

  getSeatPrice(seat: SeatOffer, cabinCode: string): string {
    if (cabinCode === 'J' || cabinCode === 'F') return 'Incl.';
    if (seat.price === 0) return 'Free';
    return `${seat.currency} ${seat.price.toFixed(0)}`;
  }

  getSelectedSeatForPaxFlight(passengerId: string, basketItemId: string): SelectedSeat | undefined {
    return this.seatSelections().get(`${passengerId}__${basketItemId}`);
  }

  async skipSeats(): Promise<void> {
    const basket = this.basket();
    if (!basket) return;
    this.seatsSaving.set(true);
    this.seatsError.set('');
    try {
      await this.#svc.updateSeats(basket.basketId, []);
      this.seatSelections.set(new Map());
      this.step.set('products');
      this.accordionSection.set('products');
      this.#loadProductCatalogue();
    } catch (err: any) {
      this.seatsError.set(err?.error?.message ?? 'Failed to proceed. Please try again.');
    } finally {
      this.seatsSaving.set(false);
    }
  }

  async saveSeats(): Promise<void> {
    const basket = this.basket();
    if (!basket) return;
    const selectionsList: BasketSeatSelection[] = [];
    for (const sel of this.seatSelections().values()) {
      selectionsList.push({
        passengerId: sel.passengerId,
        segmentId: sel.segmentId,
        basketItemRef: sel.basketItemId,
        seatOfferId: sel.seatOffer.seatOfferId,
        seatNumber: sel.seatOffer.seatNumber,
        seatPosition: sel.seatOffer.position,
        price: sel.seatOffer.price,
        tax: sel.seatOffer.tax ?? 0,
        currency: sel.seatOffer.currency,
      });
    }
    this.seatsSaving.set(true);
    this.seatsError.set('');
    try {
      await this.#svc.updateSeats(basket.basketId, selectionsList);
      this.step.set('products');
      this.accordionSection.set('products');
      this.#loadProductCatalogue();
    } catch (err: any) {
      this.seatsError.set(err?.error?.message ?? 'Failed to save seats. Please try again.');
    } finally {
      this.seatsSaving.set(false);
    }
  }

  // ── Products ─────────────────────────────────────────────────────────────

  #loadProductCatalogue(): void {
    if (this.productCatalogue()) return;
    this.productsLoading.set(true);
    this.#svc.getProducts()
      .then(catalogue => {
        this.productCatalogue.set(catalogue);
        this.productsLoading.set(false);
      })
      .catch(() => {
        this.productsLoading.set(false);
        this.productsError.set('Failed to load products.');
      });
  }

  getProductPrice(product: Product): ProductPrice | null {
    const currency = this.basket()?.currency ?? 'GBP';
    return product.prices.find(p => p.currencyCode === currency)
      ?? product.prices[0]
      ?? null;
  }

  toggleProduct(product: Product, passengerId: string, segmentId: string | null): void {
    const price = this.getProductPrice(product);
    if (!price) return;
    const key = `${product.productId}__${passengerId}__${segmentId ?? ''}`;
    const newMap = new Map<string, SelectedProduct>(this.productSelections());
    if (newMap.has(key)) {
      newMap.delete(key);
    } else {
      newMap.set(key, {
        productId: product.productId,
        offerId: price.offerId,
        name: product.name,
        passengerId,
        segmentId,
        unitPrice: price.price,
        tax: price.tax,
        currencyCode: price.currencyCode,
      });
    }
    this.productSelections.set(newMap);
  }

  isProductSelected(productId: string, passengerId: string, segmentId: string | null): boolean {
    return this.productSelections().has(`${productId}__${passengerId}__${segmentId ?? ''}`);
  }

  async skipProducts(): Promise<void> {
    const basket = this.basket();
    if (!basket) return;
    this.productsLoading.set(true);
    this.productsError.set('');
    try {
      const updated = await this.#svc.updateProducts(basket.basketId, []);
      this.basket.set(updated);
      this.productSelections.set(new Map());
      this.step.set('payment');
      this.accordionSection.set('payment');
    } catch (err: any) {
      this.productsError.set(err?.error?.message ?? 'Failed to proceed. Please try again.');
    } finally {
      this.productsLoading.set(false);
    }
  }

  async saveProducts(): Promise<void> {
    const basket = this.basket();
    if (!basket) return;
    const selectionsList: BasketProductSelection[] = [];
    for (const sel of this.productSelections().values()) {
      selectionsList.push({
        productId: sel.productId,
        offerId: sel.offerId,
        name: sel.name,
        passengerId: sel.passengerId,
        segmentId: sel.segmentId,
        quantity: 1,
        unitPrice: sel.unitPrice,
        tax: sel.tax,
        price: sel.unitPrice + sel.tax,
        currencyCode: sel.currencyCode,
      });
    }
    this.productsLoading.set(true);
    this.productsError.set('');
    try {
      const updated = await this.#svc.updateProducts(basket.basketId, selectionsList);
      this.basket.set(updated);
      this.step.set('payment');
      this.accordionSection.set('payment');
    } catch (err: any) {
      this.productsError.set(err?.error?.message ?? 'Failed to save products. Please try again.');
    } finally {
      this.productsLoading.set(false);
    }
  }

  // ── Payment ──────────────────────────────────────────────────────────────

  async confirmBooking(): Promise<void> {
    const basketSummary = this.basket();
    if (!basketSummary) return;

    if (this.payMethod() === 'CreditCard') {
      this.paymentSubmitted.set(true);
      const digits = this.cardNumber().replace(/\D/g, '');
      if (!this.cardName().trim() || digits.length < 16 || !this.expiryMonth() || !this.expiryYear() || this.cardCvv().trim().length < 3) {
        return;
      }
    }

    this.loading.set(true);
    this.error.set('');
    try {
      const expiryDate = this.payMethod() === 'CreditCard'
        ? `${this.expiryMonth()}/${this.expiryYear().toString().slice(-2)}`
        : '';
      const result = await this.#svc.confirmBasket(basketSummary.basketId, {
        method: this.payMethod(),
        cardNumber: this.cardNumber().replace(/\D/g, ''),
        expiryDate,
        cvv: this.cardCvv(),
        cardholderName: this.cardName(),
      }, this.isStandby() ? 'Standby' : 'Revenue');
      this.confirmed.set(result);
      this.step.set('confirmed');
    } catch (err: any) {
      this.error.set(err?.error?.message ?? 'Booking failed. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  // ── Navigation ───────────────────────────────────────────────────────────

  backToSearch(): void {
    this.step.set('search');
  }

  backToOutboundResults(): void {
    this.step.set('outbound-results');
    this.selectedInboundOffer.set(null);
    this.basket.set(null);
  }

  backToResults(): void {
    if (this.tripType() === 'return' && this.inboundOffers().length > 0) {
      this.step.set('inbound-results');
    } else {
      this.step.set('outbound-results');
    }
    this.basket.set(null);
  }

  backToPassengers(): void {
    this.step.set('passengers');
    this.accordionSection.set('passengers');
  }

  viewOrder(): void {
    const ref = this.confirmed()?.bookingReference;
    if (ref) this.#router.navigate(['/order', ref]);
  }

  startOver(): void {
    this.step.set('search');
    this.isStandby.set(false);
    this.outboundOffers.set([]);
    this.inboundOffers.set([]);
    this.selectedOutboundOffer.set(null);
    this.selectedInboundOffer.set(null);
    this.basket.set(null);
    this.passengerForms.set([]);
    this.confirmed.set(null);
    this.error.set('');
    this.accordionSection.set('passengers');
    this.cardNumber.set('');
    this.expiryMonth.set('');
    this.expiryYear.set('');
    this.cardCvv.set('');
    this.cardName.set('');
    this.paymentSubmitted.set(false);
    this.payMethod.set('CreditCard');
    this.seatmapEntries.set([]);
    this.activeSeatPaxIdx.set(0);
    this.activeSeatFlightIdx.set(0);
    this.seatSelections.set(new Map());
    this.seatsSaving.set(false);
    this.seatsError.set('');
    this.productCatalogue.set(null);
    this.productSelections.set(new Map());
    this.productsLoading.set(false);
    this.productsError.set('');
  }

  // ── Pax counters ─────────────────────────────────────────────────────────

  swapAirports(): void {
    const temp = this.origin();
    this.origin.set(this.destination());
    this.destination.set(temp);
  }

  incrementPax(type: 'adults' | 'children' | 'infants'): void {
    if (type === 'adults') this.adults.update(v => Math.min(v + 1, 9));
    else if (type === 'children') this.children.update(v => Math.min(v + 1, 9));
    else this.infants.update(v => Math.min(v + 1, this.adults()));
  }

  decrementPax(type: 'adults' | 'children' | 'infants'): void {
    if (type === 'adults') this.adults.update(v => Math.max(v - 1, 1));
    else if (type === 'children') this.children.update(v => Math.max(v - 1, 0));
    else this.infants.update(v => Math.max(v - 1, 0));
  }

  // ── Formatting helpers ───────────────────────────────────────────────────

  formatAmount(amount: number, currency = 'GBP'): string {
    return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(amount);
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '';
    const [y, m, d] = dateStr.split('-').map(Number);
    const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    return `${d} ${months[m - 1]} ${y}`;
  }

  formatExpiry(iso: string): string {
    return new Date(iso).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  }

  formatDuration(minutes: number): string {
    const h = Math.floor(minutes / 60);
    const m = minutes % 60;
    return m > 0 ? `${h}h ${m}m` : `${h}h`;
  }

  cabinLabel(code: string): string {
    return (
      { F: 'First', J: 'Business', W: 'Prem. Eco.', Y: 'Economy' }[code] ?? code
    );
  }

  cabinClass(code: string): string {
    return (
      { F: 'cabin-first', J: 'cabin-business', W: 'cabin-premium', Y: 'cabin-economy' }[code] ?? ''
    );
  }

  paxLabel(type: string): string {
    return { ADT: 'Adult', CHD: 'Child', INF: 'Infant' }[type] ?? type;
  }

  isOutboundSelected(group: FlightGroup): boolean {
    const sel = this.selectedOutboundOffer();
    if (!sel) return false;
    return (
      sel.flightNumber === group.flightNumber &&
      sel.departureTime === group.departureTime
    );
  }

  isInboundSelected(group: FlightGroup): boolean {
    const sel = this.selectedInboundOffer();
    if (!sel) return false;
    return (
      sel.flightNumber === group.flightNumber &&
      sel.departureTime === group.departureTime
    );
  }

  // #region TEMP: Random pax test data — remove before production
  fillRandomPaxData(): void {
    const pick = <T>(arr: T[]): T => arr[Math.floor(Math.random() * arr.length)];
    const randInt = (min: number, max: number) => Math.floor(Math.random() * (max - min + 1)) + min;
    const randomDob = (minAge: number, maxAge: number): string => {
      const now = new Date();
      const year = now.getFullYear() - randInt(minAge, maxAge);
      const month = randInt(1, 12).toString().padStart(2, '0');
      const day = randInt(1, 28).toString().padStart(2, '0');
      return `${year}-${month}-${day}`;
    };

    const maleNames   = ['James', 'Thomas', 'Daniel', 'William', 'Mohammed', 'Liam', 'Carlos', 'Arjun', 'Stefan', 'Oluwaseun', 'Marcus', 'Henrik', 'Rajan', 'Dmitri', 'Kwame', 'Tariq', 'Brendan', 'Matteo', 'Hiroshi', 'Samuel'];
    const femaleNames = ['Sarah', 'Emily', 'Charlotte', 'Priya', 'Grace', 'Amara', 'Mei', 'Natalie', 'Yuki', 'Elena', 'Fatima', 'Ingrid', 'Saoirse', 'Valentina', 'Adaeze', 'Leila', 'Brigitte', 'Chiara', 'Ananya', 'Miriam'];
    const surnames    = ['Harrison', 'Mitchell', 'Clarke', 'Watson', 'Brown', 'Hughes', 'Fletcher', 'Patel', 'Ahmed', "O'Sullivan", 'Murphy', 'Okafor', 'Garcia', 'Chen', 'Kumar', 'Dubois', 'Hoffmann', 'Tanaka', 'Adeyemi', 'Petrov', 'Andersen', 'Kowalski', 'Nakamura', 'Ferreira', 'Al-Rashid', 'Johansson', 'Mensah', 'Reyes', 'Nguyen', 'Bergmann'];

    const childMaleNames   = ['Oliver', 'Jack', 'Leo', 'Ethan', 'Noah', 'Remy', 'Kai', 'Idris', 'Matteo', 'Eli'];
    const childFemaleNames = ['Sophie', 'Ava', 'Mia', 'Layla', 'Zara', 'Niamh', 'Aisha', 'Chloe', 'Freya', 'Imani'];
    const infantMaleNames  = ['Finn', 'Theo', 'Archie', 'Rory', 'Ezra', 'Bodhi'];
    const infantFemaleNames = ['Isla', 'Luna', 'Rosie', 'Ivy', 'Wren', 'Cora'];

    const randomAdult = (usedGiven: Set<string>) => {
      const gender: 'Male' | 'Female' = Math.random() < 0.5 ? 'Male' : 'Female';
      const pool = gender === 'Male' ? maleNames : femaleNames;
      const available = pool.filter(n => !usedGiven.has(n));
      const givenName = pick(available.length ? available : pool);
      usedGiven.add(givenName);
      return { givenName, surname: pick(surnames), gender };
    };

    const usedGiven = new Set<string>();
    const leadAdult = randomAdult(usedGiven);
    const leadSurname = leadAdult.surname;

    this.passengerForms.update(forms => forms.map((pax, idx) => {
      if (pax.type === 'ADT') {
        const adult = idx === 0 ? leadAdult : randomAdult(usedGiven);
        const dob = randomDob(18, 70);
        return {
          ...pax, givenName: adult.givenName, surname: adult.surname, dob, gender: adult.gender,
          ...(idx === 0 ? { email: `${adult.givenName.toLowerCase()}.${adult.surname.toLowerCase().replace(/[^a-z]/g, '')}@example.com`, phone: `+44 7${randInt(100, 999)} ${randInt(100000, 999999)}` } : {}),
        };
      } else if (pax.type === 'CHD') {
        const isMale = Math.random() < 0.5;
        const givenName = pick(isMale ? childMaleNames : childFemaleNames);
        return { ...pax, givenName, surname: leadSurname, dob: randomDob(2, 11), gender: isMale ? 'Male' : 'Female' };
      } else {
        const isMale = Math.random() < 0.5;
        const givenName = pick(isMale ? infantMaleNames : infantFemaleNames);
        const infantDob = new Date(Date.now() - randInt(30, 700) * 86400000).toISOString().slice(0, 10);
        return { ...pax, givenName, surname: leadSurname, dob: infantDob, gender: isMale ? 'Male' : 'Female' };
      }
    }));
  }
  // #endregion TEMP

  // ── Private helpers ──────────────────────────────────────────────────────

  cabinGroups(offers: SearchOffer[]): { cabinCode: string; offers: SearchOffer[] }[] {
    const groups: { cabinCode: string; offers: SearchOffer[] }[] = [];
    let current: { cabinCode: string; offers: SearchOffer[] } | null = null;
    for (const offer of offers) {
      if (!current || current.cabinCode !== offer.cabinCode) {
        current = { cabinCode: offer.cabinCode, offers: [offer] };
        groups.push(current);
      } else {
        current.offers.push(offer);
      }
    }
    return groups;
  }

  #groupOffersByFlight(offers: SearchOffer[]): FlightGroup[] {
    const map = new Map<string, FlightGroup>();
    for (const offer of offers) {
      const key = `${offer.flightNumber}|${offer.departureDate}|${offer.departureTime}`;
      if (!map.has(key)) {
        map.set(key, {
          flightNumber: offer.flightNumber,
          origin: offer.origin,
          destination: offer.destination,
          departureTime: offer.departureTime,
          arrivalTime: offer.arrivalTime,
          arrivalDayOffset: offer.arrivalDayOffset,
          durationMinutes: offer.durationMinutes,
          aircraftType: offer.aircraftType,
          offers: [],
        });
      }
      map.get(key)!.offers.push(offer);
    }
    for (const group of map.values()) {
      group.offers.sort((a, b) => {
        const cabinDiff = (CABIN_ORDER[a.cabinCode] ?? 9) - (CABIN_ORDER[b.cabinCode] ?? 9);
        return cabinDiff !== 0 ? cabinDiff : a.totalAmount - b.totalAmount;
      });
    }
    return Array.from(map.values()).sort((a, b) =>
      a.departureTime.localeCompare(b.departureTime)
    );
  }

  #initPassengerForms(): void {
    const forms: PassengerForm[] = [];
    const addPax = (type: 'ADT' | 'CHD' | 'INF') =>
      forms.push({
        passengerId: `PAX-${forms.length + 1}`,
        type,
        givenName: '',
        surname: '',
        dob: '',
        gender: 'Unspecified',
        email: '',
        phone: '',
        loyaltyNumber: '',
      });

    for (let i = 0; i < this.adults(); i++) addPax('ADT');
    for (let i = 0; i < this.children(); i++) addPax('CHD');
    for (let i = 0; i < this.infants(); i++) addPax('INF');
    this.passengerForms.set(forms);
  }

  async #createBasketAndContinue(segments: { offerId: string; sessionId: string }[], passengerCount: number): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const basketSummary = await this.#svc.createBasket(segments, passengerCount, this.isStandby() ? 'Standby' : 'Revenue');
      this.basket.set(basketSummary);
      this.#initPassengerForms();
      this.step.set('passengers');
    } catch (err: any) {
      const status = err?.status;
      if (status === 410) {
        this.error.set('One or more offers have expired. Please search again.');
        this.selectedOutboundOffer.set(null);
        this.selectedInboundOffer.set(null);
        this.step.set('outbound-results');
      } else if (status === 422) {
        this.error.set(err?.error?.message ?? 'Insufficient availability. Please select different flights.');
      } else {
        this.error.set(err?.error?.message ?? 'Failed to create basket. Please try again.');
      }
    } finally {
      this.loading.set(false);
    }
  }
}
