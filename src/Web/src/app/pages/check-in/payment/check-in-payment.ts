import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RetailApiService } from '../../../services/retail-api.service';
import { CheckInStateService } from '../../../services/check-in-state.service';
import { Order } from '../../../models/order.model';

@Component({
  selector: 'app-check-in-payment',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './check-in-payment.html',
  styleUrl: './check-in-payment.css'
})
export class CheckInPaymentComponent implements OnInit {
  order = signal<Order | null>(null);
  loading = signal(true);
  paying = signal(false);
  submitted = signal(false);
  errorMessage = signal('');
  paymentError = signal('');
  showErrorModal = signal(false);

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');
  passengerIds = signal<string[]>([]);

  // Payment form
  cardholderName = signal('');
  cardNumber = signal('');
  expiryMonth = signal('');
  expiryYear = signal('');
  cvv = signal('');

  readonly bagSelections = computed(() => this.checkInState.bagSelections());
  readonly seatSelections = computed(() => this.checkInState.seatSelections());
  readonly totalBagAmount = computed(() => this.checkInState.totalBagAmount());
  readonly totalSeatAmount = computed(() => this.checkInState.totalSeatAmount());
  readonly totalAmount = computed(() => this.checkInState.totalPaymentAmount());
  readonly currency = computed(() => this.order()?.currency ?? 'GBP');

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
    private checkInState: CheckInStateService
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const ref = params['bookingRef'] ?? '';
      const gn = params['givenName'] ?? '';
      const sn = params['surname'] ?? '';
      const paxIds = (params['passengerIds'] ?? '').split(',').filter(Boolean);
      this.bookingRef.set(ref);
      this.givenName.set(gn);
      this.surname.set(sn);
      this.passengerIds.set(paxIds);

      if (!ref) {
        this.router.navigate(['/check-in']);
        return;
      }

      // If nothing to pay, skip straight to boarding pass
      if (this.checkInState.totalPaymentAmount() === 0) {
        this.navigateToBoardingPass();
        return;
      }

      this.loadOrder(ref, gn, sn);
    });
  }

  private loadOrder(ref: string, gn: string, sn: string): void {
    this.retailApi.retrieveForCheckIn({ bookingReference: ref, givenName: gn, surname: sn }).subscribe({
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
    this.retailApi.addCheckInAncillaries(
      this.bookingRef(),
      this.bagSelections(),
      this.seatSelections(),
      this.cardLast4(),
      this.detectCardType()
    ).subscribe({
      next: () => {
        this.paying.set(false);
        this.navigateToBoardingPass();
      },
      error: (err: { error?: { message?: string }, message?: string }) => {
        this.paying.set(false);
        const msg = (err as any)?.error?.message ?? 'Please check your payment details and try again.';
        this.paymentError.set(msg);
        this.showErrorModal.set(true);
      }
    });
  }

  dismissErrorModal(): void {
    this.showErrorModal.set(false);
  }

  private navigateToBoardingPass(): void {
    this.router.navigate(['/check-in/boarding-pass'], {
      queryParams: {
        bookingRef: this.bookingRef(),
        givenName: this.givenName(),
        surname: this.surname(),
        passengerIds: this.passengerIds().join(',')
      }
    });
  }
}
