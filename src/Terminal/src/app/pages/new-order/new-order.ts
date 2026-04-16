import { Component, inject, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import {
  NewOrderService,
  SearchOffer,
  BasketSummary,
  ConfirmResponse,
  BasketPassenger,
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
  aircraftType: string;
  offers: SearchOffer[];
}

type Step = 'search' | 'outbound-results' | 'inbound-results' | 'passengers' | 'payment' | 'confirmed';

const CABIN_ORDER: Record<string, number> = { F: 0, J: 1, W: 2, Y: 3 };

@Component({
  selector: 'app-new-order',
  templateUrl: './new-order.html',
  styleUrl: './new-order.css',
})
export class NewOrderComponent {
  #svc = inject(NewOrderService);
  #router = inject(Router);

  // ── Search form ──────────────────────────────────────────────────────────
  tripType = signal<'one-way' | 'return'>('one-way');
  origin = signal('');
  destination = signal('');
  outboundDate = signal('');
  returnDate = signal('');
  adults = signal(1);
  children = signal(0);
  infants = signal(0);

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

  // ── Payment ──────────────────────────────────────────────────────────────
  payMethod = signal('CreditCard');
  cardNumber = signal('');
  cardExpiry = signal('');
  cardCvv = signal('');
  cardName = signal('');

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
      const res = await this.#svc.searchSlice(
        this.origin(),
        this.destination(),
        this.outboundDate(),
        this.totalPax(),
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
      await this.#createBasketAndContinue([offer.offerId], this.totalPax());
    }
  }

  async selectInbound(offer: SearchOffer): Promise<void> {
    this.selectedInboundOffer.set(offer);
    this.error.set('');
    const outbound = this.selectedOutboundOffer();
    if (!outbound) return;
    await this.#createBasketAndContinue([outbound.offerId, offer.offerId], this.totalPax());
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
      this.step.set('payment');
    } catch (err: any) {
      this.error.set(err?.error?.message ?? 'Failed to save passengers. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  // ── Payment ──────────────────────────────────────────────────────────────

  async confirmBooking(): Promise<void> {
    const basketSummary = this.basket();
    if (!basketSummary) return;
    if (!this.cardNumber() || !this.cardExpiry() || !this.cardCvv() || !this.cardName()) {
      this.error.set('Please fill in all payment details.');
      return;
    }
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#svc.confirmBasket(basketSummary.basketId, {
        method: this.payMethod(),
        cardNumber: this.cardNumber(),
        expiryDate: this.cardExpiry(),
        cvv: this.cardCvv(),
        cardholderName: this.cardName(),
      });
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
  }

  viewOrder(): void {
    const ref = this.confirmed()?.bookingReference;
    if (ref) this.#router.navigate(['/order', ref]);
  }

  startOver(): void {
    this.step.set('search');
    this.outboundOffers.set([]);
    this.inboundOffers.set([]);
    this.selectedOutboundOffer.set(null);
    this.selectedInboundOffer.set(null);
    this.basket.set(null);
    this.passengerForms.set([]);
    this.confirmed.set(null);
    this.error.set('');
    this.cardNumber.set('');
    this.cardExpiry.set('');
    this.cardCvv.set('');
    this.cardName.set('');
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

  duration(dep: string, arr: string, dayOffset = 0): string {
    const [dh, dm] = dep.split(':').map(Number);
    const [ah, am] = arr.split(':').map(Number);
    let mins = (ah * 60 + am + dayOffset * 24 * 60) - (dh * 60 + dm);
    if (mins < 0) mins += 24 * 60;
    return `${Math.floor(mins / 60)}h ${mins % 60 > 0 ? (mins % 60) + 'm' : ''}`.trim();
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

  // ── Private helpers ──────────────────────────────────────────────────────

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
          aircraftType: offer.aircraftType,
          offers: [],
        });
      }
      map.get(key)!.offers.push(offer);
    }
    for (const group of map.values()) {
      group.offers.sort(
        (a, b) => (CABIN_ORDER[a.cabinCode] ?? 9) - (CABIN_ORDER[b.cabinCode] ?? 9)
      );
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

  async #createBasketAndContinue(offerIds: string[], passengerCount: number): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const basketSummary = await this.#svc.createBasket(offerIds, passengerCount);
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
