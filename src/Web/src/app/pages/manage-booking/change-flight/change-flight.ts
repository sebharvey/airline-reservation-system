import { Component, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RetailApiService } from '../../../services/retail-api.service';
import { Order, FlightSegment } from '../../../models/order.model';
import { FlightOffer } from '../../../models/flight.model';

@Component({
  selector: 'app-change-flight',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './change-flight.html',
  styleUrl: './change-flight.css'
})
export class ChangeFlightComponent implements OnInit {
  order = signal<Order | null>(null);
  loading = signal(true);
  searching = signal(false);
  confirming = signal(false);
  errorMessage = signal('');
  successMessage = signal('');

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  newDate = signal('');
  selectedSegment = signal<FlightSegment | null>(null);
  flightOffers = signal<FlightOffer[]>([]);
  selectedOffer = signal<FlightOffer | null>(null);

  // Payment form (shown when add-collect > 0)
  cardholderName = signal('');
  cardNumber = signal('');
  expiryMonth = signal('');
  expiryYear = signal('');
  cvv = signal('');
  paymentSubmitted = signal(false);

  readonly changeableSegments = computed((): FlightSegment[] => {
    const o = this.order();
    if (!o) return [];
    return o.flightSegments.filter(seg =>
      o.orderItems.some(oi => oi.type === 'Flight' && oi.segmentRef === seg.segmentId && oi.isChangeable)
    );
  });

  readonly addCollectAmount = computed((): number => {
    const offer = this.selectedOffer();
    const o = this.order();
    if (!offer || !o) return 0;
    const seg = this.selectedSegment();
    if (!seg) return 0;
    const existingItem = o.orderItems.find(oi => oi.type === 'Flight' && oi.segmentRef === seg.segmentId);
    if (!existingItem) return 0;
    return Math.max(0, offer.totalPrice - existingItem.totalPrice);
  });

  readonly addCollectEstimate = computed((): string => {
    const diff = this.addCollectAmount();
    const offer = this.selectedOffer();
    if (!offer) return '';
    if (diff <= 0) return 'No additional charge';
    return `Add collect: ${this.formatCurrency(diff, offer.currency)}`;
  });

  readonly requiresPayment = computed((): boolean => this.addCollectAmount() > 0);

  readonly expiryYears = computed(() => {
    const current = new Date().getFullYear();
    return Array.from({ length: 12 }, (_, i) => current + i);
  });

  readonly expiryMonths = [
    { value: '01', label: '01 - Jan' }, { value: '02', label: '02 - Feb' },
    { value: '03', label: '03 - Mar' }, { value: '04', label: '04 - Apr' },
    { value: '05', label: '05 - May' }, { value: '06', label: '06 - Jun' },
    { value: '07', label: '07 - Jul' }, { value: '08', label: '08 - Aug' },
    { value: '09', label: '09 - Sep' }, { value: '10', label: '10 - Oct' },
    { value: '11', label: '11 - Nov' }, { value: '12', label: '12 - Dec' }
  ];

  readonly cardDisplayNumber = computed(() => {
    const raw = this.cardNumber().replace(/\D/g, '').substring(0, 16);
    return raw.replace(/(.{4})/g, '$1 ').trim();
  });

  constructor(
    private router: Router,
    private retailApi: RetailApiService
  ) {}

  ngOnInit(): void {
    const navState = (this.router.getCurrentNavigation()?.extras.state ?? history.state) as Record<string, string>;
    const gn = navState?.['givenName'] ?? '';
    const sn = navState?.['surname'] ?? '';

    if (!this.retailApi.hasActiveManageBookingSession() || !gn || !sn) {
      this.router.navigate(['/manage-booking']);
      return;
    }

    this.givenName.set(gn);
    this.surname.set(sn);
    this.loadOrder();
  }

  private loadOrder(): void {
    this.loading.set(true);
    this.retailApi.retrieveOrder().subscribe({
      next: (order) => {
        this.bookingRef.set(order.bookingReference);
        this.order.set(order);
        this.loading.set(false);
        const segs = this.changeableSegments();
        if (segs.length > 0) {
          this.selectedSegment.set(segs[0]);
        }
      },
      error: (err: { status?: number; message?: string }) => {
        if (err.status === 401) { this.router.navigate(['/manage-booking']); return; }
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking.');
        this.loading.set(false);
      }
    });
  }

  selectSegment(seg: FlightSegment): void {
    this.selectedSegment.set(seg);
    this.flightOffers.set([]);
    this.selectedOffer.set(null);
  }

  searchFlights(): void {
    const seg = this.selectedSegment();
    const date = this.newDate();
    const o = this.order();
    if (!seg || !date || !o) return;

    this.searching.set(true);
    this.flightOffers.set([]);
    this.selectedOffer.set(null);
    this.errorMessage.set('');

    this.retailApi.searchSlice({
      origin: seg.origin,
      destination: seg.destination,
      departureDate: date,
      adults: o.passengers.filter(p => p.type === 'ADT').length,
      children: o.passengers.filter(p => p.type === 'CHD').length
    }).subscribe({
      next: (result) => {
        this.flightOffers.set(result.offers);
        this.searching.set(false);
        if (result.offers.length === 0) {
          this.errorMessage.set('No flights found for the selected date. Try a different date.');
        }
      },
      error: (err: { message?: string }) => {
        this.errorMessage.set(err?.message ?? 'Search failed. Please try again.');
        this.searching.set(false);
      }
    });
  }

  selectOffer(offer: FlightOffer): void {
    this.selectedOffer.set(this.selectedOffer()?.offerId === offer.offerId ? null : offer);
    // Reset payment form when a new offer is selected
    this.cardholderName.set('');
    this.cardNumber.set('');
    this.expiryMonth.set('');
    this.expiryYear.set('');
    this.cvv.set('');
    this.paymentSubmitted.set(false);
  }

  onCardNumberInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const digits = input.value.replace(/\D/g, '').substring(0, 16);
    this.cardNumber.set(digits);
    input.value = digits.replace(/(.{4})/g, '$1 ').trim();
  }

  isPaymentFormValid(): boolean {
    if (!this.requiresPayment()) return true;
    const name = this.cardholderName().trim();
    const num = this.cardNumber().replace(/\D/g, '');
    const month = this.expiryMonth();
    const year = this.expiryYear();
    const cvv = this.cvv().trim();
    return !!(name && num.length === 16 && month && year && cvv.length >= 3);
  }

  confirmChange(): void {
    const offer = this.selectedOffer();
    if (!offer || this.confirming()) return;

    this.paymentSubmitted.set(true);
    if (!this.isPaymentFormValid()) return;

    this.confirming.set(true);
    this.errorMessage.set('');

    const payment = this.requiresPayment() ? {
      method: 'CreditCard',
      cardNumber: this.cardNumber().replace(/\D/g, ''),
      expiryDate: `${this.expiryMonth()}/${this.expiryYear()}`,
      cvv: this.cvv().trim(),
      cardholderName: this.cardholderName().trim()
    } : undefined;

    this.retailApi.changeOrder(this.bookingRef(), offer.offerId, payment).subscribe({
      next: (res) => {
        this.confirming.set(false);
        if (res.success) {
          const chargeMsg = res.addCollect > 0
            ? ` An additional charge of ${this.formatCurrency(res.addCollect, offer.currency)} has been applied.`
            : '';
          this.successMessage.set(`Flight changed successfully.${chargeMsg}`);
        } else {
          this.errorMessage.set('Flight change failed. Please try again.');
        }
      },
      error: (err: { message?: string }) => {
        this.confirming.set(false);
        this.errorMessage.set(err?.message ?? 'Flight change failed. Please try again.');
      }
    });
  }

  formatDateTime(dt: string): string {
    return new Date(dt).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit', timeZone: 'UTC'
    });
  }

  formatCurrency(amount: number, currency: string): string {
    return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(amount);
  }

  formatDuration(dep: string, arr: string): string {
    const mins = (new Date(arr).getTime() - new Date(dep).getTime()) / 60000;
    const h = Math.floor(mins / 60);
    const m = Math.round(mins % 60);
    return `${h}h ${m}m`;
  }

  get minDate(): string {
    return new Date().toISOString().split('T')[0];
  }

  get detailState() {
    return { givenName: this.givenName(), surname: this.surname() };
  }
}
