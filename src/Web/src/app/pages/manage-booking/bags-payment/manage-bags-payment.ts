import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RetailApiService } from '../../../services/retail-api.service';
import { ManageBookingStateService } from '../../../services/manage-booking-state.service';
import { Order } from '../../../models/order.model';

@Component({
  selector: 'app-manage-bags-payment',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './manage-bags-payment.html',
  styleUrl: './manage-bags-payment.css'
})
export class ManageBagsPaymentComponent implements OnInit {
  order = signal<Order | null>(null);
  loading = signal(true);
  paying = signal(false);
  submitted = signal(false);
  errorMessage = signal('');
  paymentError = signal('');

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  // Payment form
  cardholderName = signal('');
  cardNumber = signal('');
  expiryMonth = signal('');
  expiryYear = signal('');
  cvv = signal('');

  readonly bagSelections = computed(() => this.manageBookingState.bagSelections());
  readonly totalAmount = computed(() => this.manageBookingState.totalBagAmount());
  readonly currency = computed(() => this.order()?.currencyCode ?? 'GBP');

  readonly cardDisplayNumber = computed(() => {
    const raw = this.cardNumber().replace(/\D/g, '').substring(0, 16);
    return raw.replace(/(.{4})/g, '$1 ').trim();
  });

  readonly cardLast4 = computed(() => {
    const raw = this.cardNumber().replace(/\D/g, '');
    return raw.slice(-4);
  });

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

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private retailApi: RetailApiService,
    private manageBookingState: ManageBookingStateService
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const ref = params['bookingRef'] ?? '';
      const gn = params['givenName'] ?? '';
      const sn = params['surname'] ?? '';
      this.bookingRef.set(ref);
      this.givenName.set(gn);
      this.surname.set(sn);

      if (!ref) {
        this.router.navigate(['/manage-booking']);
        return;
      }

      if (this.manageBookingState.bagSelections().length === 0) {
        this.router.navigate(['/manage-booking/bags'], { queryParams: { bookingRef: ref, givenName: gn, surname: sn } });
        return;
      }

      this.loadOrder(ref, gn, sn);
    });
  }

  private loadOrder(ref: string, gn: string, sn: string): void {
    this.retailApi.retrieveOrder({ bookingReference: ref, givenName: gn, surname: sn }).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: (err: { message?: string }) => {
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking.');
        this.loading.set(false);
      }
    });
  }

  getPassengerName(passengerId: string): string {
    const pax = this.order()?.passengers.find(p => p.passengerId === passengerId);
    return pax ? `${pax.givenName} ${pax.surname}` : passengerId;
  }

  getSegmentRoute(segmentId: string): string {
    const seg = this.order()?.flightSegments.find(s => s.segmentId === segmentId);
    return seg ? `${seg.origin}–${seg.destination}` : segmentId;
  }

  onCardNumberInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const digits = input.value.replace(/\D/g, '').substring(0, 16);
    this.cardNumber.set(digits);
    input.value = digits.replace(/(.{4})/g, '$1 ').trim();
  }

  detectCardType(): string {
    const num = this.cardNumber().replace(/\D/g, '');
    if (num.startsWith('4')) return 'Visa';
    if (/^5[1-5]/.test(num) || /^2[2-7]/.test(num)) return 'Mastercard';
    if (/^3[47]/.test(num)) return 'Amex';
    return 'Card';
  }

  isFormValid(): boolean {
    const name = this.cardholderName().trim();
    const num = this.cardNumber().replace(/\D/g, '');
    const month = this.expiryMonth();
    const year = this.expiryYear();
    const cvv = this.cvv().trim();
    return !!(name && num.length === 16 && month && year && cvv.length >= 3);
  }

  onPay(): void {
    this.submitted.set(true);
    this.paymentError.set('');
    if (!this.isFormValid()) return;

    this.paying.set(true);
    const bags = this.bagSelections().map(b => ({
      passengerId: b.passengerId,
      segmentId: b.segmentId,
      additionalBags: b.additionalBags,
      bagOfferId: b.bagOfferId,
      price: b.price
    }));

    this.retailApi.addManageBookingBags(
      this.bookingRef(),
      bags,
      this.cardLast4(),
      this.detectCardType()
    ).subscribe({
      next: () => {
        this.paying.set(false);
        this.manageBookingState.clear();
        this.router.navigate(['/manage-booking/bags-confirmation'], {
          queryParams: {
            bookingRef: this.bookingRef(),
            givenName: this.givenName(),
            surname: this.surname()
          }
        });
      },
      error: (err: { message?: string }) => {
        this.paying.set(false);
        this.paymentError.set(err?.message ?? 'Payment failed. Please try again.');
      }
    });
  }

  onBack(): void {
    this.router.navigate(['/manage-booking/bags'], {
      queryParams: { bookingRef: this.bookingRef(), givenName: this.givenName(), surname: this.surname() }
    });
  }
}
