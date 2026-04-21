import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { CheckInStateService } from '../../../services/check-in-state.service';
import { Order, CardDetails } from '../../../models/order.model';
import { PaymentFormComponent } from '../../../components/payment-form/payment-form';

@Component({
  selector: 'app-check-in-payment',
  standalone: true,
  imports: [CommonModule, RouterLink, PaymentFormComponent],
  templateUrl: './check-in-payment.html',
  styleUrl: './check-in-payment.css'
})
export class CheckInPaymentComponent implements OnInit {
  order = signal<Order | null>(null);
  loading = signal(true);
  paying = signal(false);
  errorMessage = signal('');
  paymentError = signal('');

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');
  passengerIds = signal<string[]>([]);

  readonly bagSelections = computed(() => this.checkInState.bagSelections());
  readonly seatSelections = computed(() => this.checkInState.seatSelections());
  readonly totalBagAmount = computed(() => this.checkInState.totalBagAmount());
  readonly totalSeatAmount = computed(() => this.checkInState.totalSeatAmount());
  readonly totalAmount = computed(() => this.checkInState.totalPaymentAmount());
  readonly currency = computed(() => this.order()?.currency ?? 'GBP');

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

  onPay(card: CardDetails): void {
    this.paying.set(true);
    this.retailApi.addCheckInAncillaries(
      this.bookingRef(),
      this.bagSelections(),
      this.seatSelections(),
      card.cardLast4,
      card.cardType
    ).subscribe({
      next: () => {
        this.paying.set(false);
        this.navigateToBoardingPass();
      },
      error: (err: { error?: { message?: string }, message?: string }) => {
        this.paying.set(false);
        const msg = (err as any)?.error?.message ?? 'Please check your payment details and try again.';
        this.paymentError.set(msg);
      }
    });
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
