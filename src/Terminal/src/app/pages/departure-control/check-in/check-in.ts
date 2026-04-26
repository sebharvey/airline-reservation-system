import { Component, inject, signal, computed } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  CheckInDeskService,
  BagOffer,
  BagPolicy,
  CabinSeatmap,
  DcsSeatmap,
  DcsBagSelection,
  DcsSeatSelection,
  DcsBoardingCard,
} from '../../../services/check-in-desk.service';
import {
  OrderDetail,
  OrderPassenger,
  FlightSegment,
  OrderItem,
} from '../../../services/order.service';

// ── Local interfaces ──────────────────────────────────────────────────────────

interface PaxTicket {
  passenger: OrderPassenger;
  eTicketNumber: string | null;
  checkInStatus: 'pending' | 'success' | 'failed';
  checkInError: string | null;
}

interface FlightBagData {
  segment: FlightSegment;
  policy: BagPolicy | null;
  loading: boolean;
  error: string;
}

interface SeatmapEntry {
  segment: FlightSegment;
  seatmap: DcsSeatmap | null;
  loading: boolean;
  error: string;
}

interface LocalBagSelection {
  passengerId: string;
  segmentId: string;
  bagOffer: BagOffer | null;
  additionalBags: number;
  currency: string;
}

interface LocalSeatSelection {
  passengerId: string;
  segmentId: string;
  seatOfferId: string;
  seatNumber: string;
  seatPosition: string;
  price: number;
  tax: number;
  currency: string;
}

@Component({
  selector: 'app-departure-control-check-in',
  imports: [FormsModule],
  templateUrl: './check-in.html',
  styleUrl: './check-in.css',
})
export class DepartureControlCheckInComponent {
  #svc = inject(CheckInDeskService);

  // ── Search ───────────────────────────────────────────────────────────────
  bookingRef = signal('');
  searchError = signal('');
  searching = signal(false);

  // ── Order state ───────────────────────────────────────────────────────────
  order = signal<OrderDetail | null>(null);
  paxTickets = signal<PaxTicket[]>([]);
  departureAirport = signal('');
  basketId = signal<string | null>(null);
  basketCreating = signal(false);
  isStandby = signal(false);

  readonly departureAirports = computed((): string[] => {
    const segs = this.order()?.orderData?.dataLists?.flightSegments ?? [];
    return [...new Set(segs.map(s => s.origin))];
  });

  readonly flightSegments = computed((): FlightSegment[] =>
    this.order()?.orderData?.dataLists?.flightSegments ?? []
  );

  // ── PAX selection ─────────────────────────────────────────────────────────
  selectedPaxId = signal<string | null>(null);

  readonly selectedPax = computed(() =>
    this.paxTickets().find(pt => pt.passenger.passengerId === this.selectedPaxId()) ?? null
  );

  // ── Bag data ─────────────────────────────────────────────────────────────
  flightBagData = signal<FlightBagData[]>([]);
  activeBagFlightIdx = signal(0);

  readonly activeBagData = computed(() => this.flightBagData()[this.activeBagFlightIdx()] ?? null);

  // ── Seatmap data ──────────────────────────────────────────────────────────
  seatmapEntries = signal<SeatmapEntry[]>([]);
  activeSeatFlightIdx = signal(0);

  readonly activeSeatEntry = computed(() => this.seatmapEntries()[this.activeSeatFlightIdx()] ?? null);
  readonly activeSeatCabin = computed((): CabinSeatmap | null => {
    const entry = this.activeSeatEntry();
    if (!entry?.seatmap) return null;
    const seg = entry.segment;
    return (
      entry.seatmap.cabins.find(c => c.cabinCode === seg.cabinClass) ??
      entry.seatmap.cabins[0] ??
      null
    );
  });

  // ── Local selections ──────────────────────────────────────────────────────
  bagSelections = signal<Map<string, LocalBagSelection>>(new Map());
  seatSelections = signal<Map<string, LocalSeatSelection>>(new Map());

  readonly bagTotal = computed(() => {
    let total = 0;
    for (const sel of this.bagSelections().values()) {
      if (sel.bagOffer && sel.additionalBags > 0) {
        total += (sel.bagOffer.price + (sel.bagOffer.tax ?? 0)) * sel.additionalBags;
      }
    }
    return total;
  });

  readonly seatTotal = computed(() => {
    let total = 0;
    for (const sel of this.seatSelections().values()) {
      total += sel.price + (sel.tax ?? 0);
    }
    return total;
  });

  readonly grandTotal = computed(() => this.bagTotal() + this.seatTotal());

  readonly currency = computed(() => this.order()?.currency ?? 'GBP');

  // ── Payment modal ─────────────────────────────────────────────────────────
  showPaymentModal = signal(false);
  payMethod = signal<'CreditCard' | 'Cash'>('CreditCard');
  cardName = signal('');
  cardNumber = signal('');
  expiryMonth = signal('');
  expiryYear = signal('');
  cardCvv = signal('');
  paymentSubmitted = signal(false);
  paymentProcessing = signal(false);
  paymentError = signal('');
  paymentSuccess = signal(false);

  readonly cardDisplayNumber = computed(() => {
    const raw = this.cardNumber().replace(/\D/g, '').substring(0, 16);
    return raw.replace(/(.{4})/g, '$1 ').trim();
  });

  readonly expiryMonths = [
    { value: '01', label: '01 — Jan' }, { value: '02', label: '02 — Feb' },
    { value: '03', label: '03 — Mar' }, { value: '04', label: '04 — Apr' },
    { value: '05', label: '05 — May' }, { value: '06', label: '06 — Jun' },
    { value: '07', label: '07 — Jul' }, { value: '08', label: '08 — Aug' },
    { value: '09', label: '09 — Sep' }, { value: '10', label: '10 — Oct' },
    { value: '11', label: '11 — Nov' }, { value: '12', label: '12 — Dec' },
  ];

  readonly expiryYears = computed(() => {
    const cur = new Date().getFullYear();
    return Array.from({ length: 12 }, (_, i) => cur + i);
  });

  // ── Check-in submission ───────────────────────────────────────────────────
  checkingIn = signal(false);
  checkInError = signal('');
  checkInDone = signal(false);
  boardingCards = signal<DcsBoardingCard[]>([]);

  // ── Copied PNR ────────────────────────────────────────────────────────────
  copiedRef = signal<string | null>(null);

  // ─────────────────────────────────────────────────────────────────────────
  // Search
  // ─────────────────────────────────────────────────────────────────────────

  async search(): Promise<void> {
    const ref = this.bookingRef().trim().toUpperCase();
    if (!ref) {
      this.searchError.set('Please enter a booking reference.');
      return;
    }
    this.searching.set(true);
    this.searchError.set('');
    this.order.set(null);
    this.paxTickets.set([]);
    this.selectedPaxId.set(null);
    this.basketId.set(null);
    this.checkInDone.set(false);
    this.boardingCards.set([]);

    try {
      const detail = await this.#svc.getOrder(ref);
      this.order.set(detail);
      this.#buildPaxTickets(detail);
      this.#detectStandby(detail);

      // Auto-select first departure airport
      const airports = [...new Set((detail.orderData?.dataLists?.flightSegments ?? []).map(s => s.origin))];
      if (airports.length > 0) this.departureAirport.set(airports[0]);

      // Create check-in basket upfront
      const paxCount = detail.orderData?.dataLists?.passengers?.length ?? 1;
      this.basketCreating.set(true);
      try {
        const basket = await this.#svc.createCheckInBasket(ref, paxCount, detail.currency ?? 'GBP');
        this.basketId.set(basket.basketId);
      } catch {
        // Non-fatal — payment will be skipped if basket fails
      } finally {
        this.basketCreating.set(false);
      }
    } catch (err: any) {
      this.searchError.set(
        err?.status === 404
          ? 'Booking not found. Please check the reference and try again.'
          : err?.error?.message ?? 'Failed to retrieve booking. Please try again.'
      );
    } finally {
      this.searching.set(false);
    }
  }

  // ─────────────────────────────────────────────────────────────────────────
  // PAX selection
  // ─────────────────────────────────────────────────────────────────────────

  selectPax(passengerId: string): void {
    if (this.selectedPaxId() === passengerId) return;
    this.selectedPaxId.set(passengerId);
    this.activeBagFlightIdx.set(0);
    this.activeSeatFlightIdx.set(0);
    this.#loadAncillaryData();
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Ancillary data loading
  // ─────────────────────────────────────────────────────────────────────────

  #loadAncillaryData(): void {
    const segments = this.flightSegments();
    if (segments.length === 0) return;

    // Bags
    const bagEntries: FlightBagData[] = segments.map(s => ({ segment: s, policy: null, loading: true, error: '' }));
    this.flightBagData.set(bagEntries);
    segments.forEach((seg, idx) => {
      this.#svc.getBagOffers(seg.segmentId, seg.cabinClass ?? 'Y')
        .then(policy => {
          this.flightBagData.update(list => {
            const updated = [...list];
            updated[idx] = { ...updated[idx], policy, loading: false };
            return updated;
          });
        })
        .catch(() => {
          this.flightBagData.update(list => {
            const updated = [...list];
            updated[idx] = { ...updated[idx], loading: false, error: 'Failed to load bag policy' };
            return updated;
          });
        });
    });

    // Seatmap
    const seatEntries: SeatmapEntry[] = segments.map(s => ({ segment: s, seatmap: null, loading: true, error: '' }));
    this.seatmapEntries.set(seatEntries);
    segments.forEach((seg, idx) => {
      this.#svc.getSeatmap(seg.segmentId, seg.flightNumber, seg.cabinClass ?? 'Y')
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

  // ─────────────────────────────────────────────────────────────────────────
  // Bag selection
  // ─────────────────────────────────────────────────────────────────────────

  bagKey(passengerId: string, segmentId: string): string {
    return `${passengerId}__${segmentId}`;
  }

  getBagSelection(passengerId: string, segmentId: string): LocalBagSelection | undefined {
    return this.bagSelections().get(this.bagKey(passengerId, segmentId));
  }

  getAdditionalBags(passengerId: string, segmentId: string): number {
    return this.getBagSelection(passengerId, segmentId)?.additionalBags ?? 0;
  }

  selectBags(passengerId: string, seg: FlightSegment, offers: BagOffer[], count: number): void {
    const key = this.bagKey(passengerId, seg.segmentId);
    const newMap = new Map(this.bagSelections());
    if (count === 0) {
      newMap.delete(key);
    } else {
      const offer = offers.find(o => o.bagSequence === count) ?? offers[0] ?? null;
      newMap.set(key, {
        passengerId,
        segmentId: seg.segmentId,
        bagOffer: offer,
        additionalBags: count,
        currency: offer?.currency ?? this.currency(),
      });
    }
    this.bagSelections.set(newMap);
  }

  bagOptionLabel(offers: BagOffer[], count: number): string {
    if (count === 0) return 'No extras';
    const offer = offers.find(o => o.bagSequence === count) ?? offers[0];
    if (!offer) return `+${count} bag${count > 1 ? 's' : ''}`;
    const total = (offer.price + (offer.tax ?? 0)) * count;
    return `+${count} bag${count > 1 ? 's' : ''} · ${this.formatAmount(total, offer.currency)}`;
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Seat selection
  // ─────────────────────────────────────────────────────────────────────────

  seatKey(passengerId: string, segmentId: string): string {
    return `${passengerId}__${segmentId}`;
  }

  isSeatSelected(seat: { seatOfferId: string }, segmentId: string): boolean {
    const paxId = this.selectedPaxId();
    if (!paxId) return false;
    const sel = this.seatSelections().get(this.seatKey(paxId, segmentId));
    return sel?.seatOfferId === seat.seatOfferId;
  }

  isSeatTakenByOther(seat: { seatOfferId: string }, segmentId: string): boolean {
    const paxId = this.selectedPaxId();
    if (!paxId) return false;
    for (const [key, sel] of this.seatSelections()) {
      if (key.startsWith(paxId + '__')) continue;
      if (sel.seatOfferId === seat.seatOfferId && sel.segmentId === segmentId) return true;
    }
    return false;
  }

  selectSeat(seatOffer: { seatOfferId: string; seatNumber: string; position: string; price: number; tax: number; currency: string; availability: string }, segmentId: string): void {
    if (this.isStandby()) return;
    if (seatOffer.availability !== 'available') return;
    const paxId = this.selectedPaxId();
    if (!paxId) return;
    if (this.isSeatTakenByOther(seatOffer, segmentId)) return;

    const key = this.seatKey(paxId, segmentId);
    const newMap = new Map(this.seatSelections());
    if (newMap.get(key)?.seatOfferId === seatOffer.seatOfferId) {
      newMap.delete(key);
    } else {
      newMap.set(key, {
        passengerId: paxId,
        segmentId,
        seatOfferId: seatOffer.seatOfferId,
        seatNumber: seatOffer.seatNumber,
        seatPosition: seatOffer.position,
        price: seatOffer.price,
        tax: seatOffer.tax ?? 0,
        currency: seatOffer.currency,
      });
    }
    this.seatSelections.set(newMap);
  }

  getSeatClass(seatOffer: { seatOfferId: string; availability: string } | null, segmentId: string): string {
    if (!seatOffer) return 'sm-seat sm-seat-gap';
    if (seatOffer.availability === 'sold' || seatOffer.availability === 'held') return 'sm-seat sm-seat-sold';
    if (this.isSeatSelected(seatOffer, segmentId)) return 'sm-seat sm-seat-selected';
    if (this.isSeatTakenByOther(seatOffer, segmentId)) return 'sm-seat sm-seat-other';
    return 'sm-seat sm-seat-available';
  }

  getSeatRows(cabin: CabinSeatmap): number[] {
    const rows: number[] = [];
    for (let r = cabin.startRow; r <= cabin.endRow; r++) rows.push(r);
    return rows;
  }

  getSeatForRowCol(cabin: CabinSeatmap, row: number, col: string): { seatOfferId: string; seatNumber: string; column: string; rowNumber: number; position: string; cabinCode: string; price: number; tax: number; currency: string; availability: 'available' | 'held' | 'sold'; attributes: string[] } | null {
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

  getSeatPrice(seat: { price: number; currency: string }, cabinCode: string): string {
    if (cabinCode === 'J' || cabinCode === 'F') return 'Incl.';
    if (seat.price === 0) return 'Free';
    return `${seat.price.toFixed(0)} ${seat.currency}`;
  }

  getSelectedSeat(passengerId: string, segmentId: string): LocalSeatSelection | undefined {
    return this.seatSelections().get(this.seatKey(passengerId, segmentId));
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Payment modal
  // ─────────────────────────────────────────────────────────────────────────

  openPaymentModal(): void {
    this.paymentSubmitted.set(false);
    this.paymentError.set('');
    this.paymentSuccess.set(false);
    this.showPaymentModal.set(true);
  }

  closePaymentModal(): void {
    if (this.paymentProcessing()) return;
    this.showPaymentModal.set(false);
  }

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

  async confirmPayment(): Promise<void> {
    if (this.payMethod() === 'CreditCard') {
      this.paymentSubmitted.set(true);
      const digits = this.cardNumber().replace(/\D/g, '');
      if (!this.cardName().trim() || digits.length < 16 || !this.expiryMonth() || !this.expiryYear() || this.cardCvv().trim().length < 3) {
        return;
      }
    }

    const basketId = this.basketId();
    if (!basketId) {
      // No basket — proceed directly to check-in (cash only or no items)
      this.paymentSuccess.set(true);
      return;
    }

    this.paymentProcessing.set(true);
    this.paymentError.set('');

    try {
      // Push bag and seat selections to basket
      const bags = this.#buildBagSelections();
      const seats = this.#buildSeatSelections();
      if (bags.length > 0) await this.#svc.updateBasketBags(basketId, bags);
      if (seats.length > 0) await this.#svc.updateBasketSeats(basketId, seats);

      // Confirm basket (process payment)
      const expiryDate = this.payMethod() === 'CreditCard'
        ? `${this.expiryMonth()}/${this.expiryYear().toString().slice(-2)}`
        : '';
      await this.#svc.confirmBasket(basketId, {
        method: this.payMethod(),
        cardNumber: this.cardNumber().replace(/\D/g, ''),
        expiryDate,
        cvv: this.cardCvv(),
        cardholderName: this.cardName(),
      });
      this.paymentSuccess.set(true);
    } catch (err: any) {
      this.paymentError.set(err?.error?.message ?? 'Payment failed. Please check card details and try again.');
    } finally {
      this.paymentProcessing.set(false);
    }
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Check-in submission
  // ─────────────────────────────────────────────────────────────────────────

  canCheckIn(): boolean {
    return (
      !!this.order() &&
      !!this.departureAirport() &&
      !this.checkInDone() &&
      !this.checkingIn()
    );
  }

  hasOutstandingPayment(): boolean {
    return this.grandTotal() > 0 && !this.paymentSuccess();
  }

  async initiateCheckIn(): Promise<void> {
    if (this.hasOutstandingPayment()) {
      this.openPaymentModal();
      return;
    }
    await this.#performCheckIn();
  }

  async proceedToCheckInAfterPayment(): Promise<void> {
    this.showPaymentModal.set(false);
    await this.#performCheckIn();
  }

  async #performCheckIn(): Promise<void> {
    const order = this.order();
    if (!order) return;
    const depAirport = this.departureAirport();
    if (!depAirport) {
      this.checkInError.set('Please select a departure airport.');
      return;
    }

    this.checkingIn.set(true);
    this.checkInError.set('');

    try {
      const result = await this.#svc.submitCheckIn(order.bookingReference, depAirport);
      const checkedInTickets = new Set(result.checkedIn ?? []);

      this.paxTickets.update(list =>
        list.map(pt => {
          if (!pt.eTicketNumber) return { ...pt, checkInStatus: 'failed' as const, checkInError: 'No ticket number found' };
          if (checkedInTickets.has(pt.eTicketNumber)) return { ...pt, checkInStatus: 'success' as const, checkInError: null };
          return { ...pt, checkInStatus: 'failed' as const, checkInError: 'Check-in rejected — please verify travel documents at the desk' };
        })
      );

      this.checkInDone.set(true);

      // Fetch boarding cards for successfully checked-in PAX
      const checkedTickets = this.paxTickets()
        .filter(pt => pt.checkInStatus === 'success' && pt.eTicketNumber)
        .map(pt => pt.eTicketNumber!);
      if (checkedTickets.length > 0) {
        try {
          const cards = await this.#svc.getBoardingDocs(checkedTickets, depAirport);
          this.boardingCards.set(cards);
        } catch {
          // Boarding docs are non-fatal
        }
      }
    } catch (err: any) {
      this.checkInError.set(err?.error?.message ?? 'Check-in failed. Please try again or contact a supervisor.');
    } finally {
      this.checkingIn.set(false);
    }
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Reset
  // ─────────────────────────────────────────────────────────────────────────

  reset(): void {
    this.bookingRef.set('');
    this.searchError.set('');
    this.order.set(null);
    this.paxTickets.set([]);
    this.departureAirport.set('');
    this.basketId.set(null);
    this.selectedPaxId.set(null);
    this.flightBagData.set([]);
    this.seatmapEntries.set([]);
    this.bagSelections.set(new Map());
    this.seatSelections.set(new Map());
    this.showPaymentModal.set(false);
    this.paymentSuccess.set(false);
    this.paymentProcessing.set(false);
    this.paymentError.set('');
    this.cardName.set('');
    this.cardNumber.set('');
    this.expiryMonth.set('');
    this.expiryYear.set('');
    this.cardCvv.set('');
    this.paymentSubmitted.set(false);
    this.checkingIn.set(false);
    this.checkInError.set('');
    this.checkInDone.set(false);
    this.boardingCards.set([]);
    this.isStandby.set(false);
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Clipboard
  // ─────────────────────────────────────────────────────────────────────────

  copyBookingRef(text: string, event?: Event): void {
    event?.stopPropagation();
    navigator.clipboard.writeText(text).then(() => {
      this.copiedRef.set(text);
      setTimeout(() => this.copiedRef.set(null), 2000);
    });
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Formatting helpers
  // ─────────────────────────────────────────────────────────────────────────

  formatAmount(amount: number, currency = 'GBP'): string {
    return `${amount.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency}`;
  }

  formatDate(dateStr: string | null | undefined): string {
    if (!dateStr) return '—';
    try {
      const d = new Date(dateStr);
      return d.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' });
    } catch {
      return dateStr;
    }
  }

  formatDateTime(dateStr: string | null | undefined): string {
    if (!dateStr) return '—';
    try {
      return new Date(dateStr).toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: 'UTC' });
    } catch {
      return dateStr;
    }
  }

  cabinLabel(code: string): string {
    return { F: 'First', J: 'Business', W: 'Prem. Eco.', Y: 'Economy' }[code] ?? code;
  }

  paxTypeLabel(type: string): string {
    return { ADT: 'Adult', CHD: 'Child', INF: 'Infant', YTH: 'Youth' }[type] ?? type;
  }

  // ─────────────────────────────────────────────────────────────────────────
  // Private helpers
  // ─────────────────────────────────────────────────────────────────────────

  #buildPaxTickets(detail: OrderDetail): void {
    const passengers = detail.orderData?.dataLists?.passengers ?? [];
    const items: OrderItem[] = detail.orderData?.orderItems ?? [];

    // Build map: passengerId → eTicketNumber from order items (Flight type)
    const ticketMap = new Map<string, string>();
    for (const item of items) {
      if (item.eTicketNumber && item.passengerId) {
        ticketMap.set(item.passengerId, item.eTicketNumber);
      }
    }

    this.paxTickets.set(
      passengers.map(p => ({
        passenger: p,
        eTicketNumber: ticketMap.get(p.passengerId) ?? null,
        checkInStatus: 'pending',
        checkInError: null,
      }))
    );
  }

  #detectStandby(detail: OrderDetail): void {
    const fareTotal = detail.orderData?.itemTotals?.subtotalFare ?? null;
    const grandTotal = detail.totalAmount ?? null;
    // Standby bookings have zero fare total
    const isStandby = fareTotal === 0 || (grandTotal === 0 && fareTotal === null);
    this.isStandby.set(isStandby);
  }

  #buildBagSelections(): DcsBagSelection[] {
    const selections: DcsBagSelection[] = [];
    for (const sel of this.bagSelections().values()) {
      if (!sel.bagOffer || sel.additionalBags === 0) continue;
      selections.push({
        passengerId: sel.passengerId,
        segmentId: sel.segmentId,
        basketItemRef: sel.segmentId,
        bagOfferId: sel.bagOffer.bagOfferId,
        additionalBags: sel.additionalBags,
        price: sel.bagOffer.price * sel.additionalBags,
        tax: (sel.bagOffer.tax ?? 0) * sel.additionalBags,
        currency: sel.currency,
      });
    }
    return selections;
  }

  #buildSeatSelections(): DcsSeatSelection[] {
    const selections: DcsSeatSelection[] = [];
    for (const sel of this.seatSelections().values()) {
      selections.push({
        passengerId: sel.passengerId,
        segmentId: sel.segmentId,
        basketItemRef: sel.segmentId,
        seatOfferId: sel.seatOfferId,
        seatNumber: sel.seatNumber,
        seatPosition: sel.seatPosition,
        price: sel.price,
        tax: sel.tax,
        currency: sel.currency,
      });
    }
    return selections;
  }
}
