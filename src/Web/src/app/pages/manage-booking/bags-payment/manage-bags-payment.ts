import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { ManageBookingStateService } from '../../../services/manage-booking-state.service';
import { Order, CardDetails } from '../../../models/order.model';
import { PaymentFormComponent } from '../../../components/payment-form/payment-form';

@Component({
  selector: 'app-manage-bags-payment',
  standalone: true,
  imports: [CommonModule, PaymentFormComponent],
  templateUrl: './manage-bags-payment.html',
  styleUrl: './manage-bags-payment.css'
})
export class ManageBagsPaymentComponent implements OnInit {
  order = signal<Order | null>(null);
  loading = signal(true);
  paying = signal(false);
  errorMessage = signal('');
  paymentError = signal('');

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  readonly bagSelections = computed(() => this.manageBookingState.bagSelections());
  readonly totalAmount = computed(() => this.manageBookingState.totalBagAmount());
  readonly currency = computed(() => this.order()?.currency ?? 'GBP');

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private retailApi: RetailApiService,
    private manageBookingState: ManageBookingStateService
  ) {}

  ngOnInit(): void {
    const navState = (this.router.getCurrentNavigation()?.extras.state ?? history.state) as Record<string, string>;
    const gn = navState?.['givenName'] ?? '';
    const sn = navState?.['surname'] ?? '';

    this.route.queryParams.subscribe(params => {
      const ref = params['bookingRef'] ?? '';
      this.bookingRef.set(ref);
      this.givenName.set(gn);
      this.surname.set(sn);

      if (!ref) {
        this.router.navigate(['/manage-booking']);
        return;
      }

      if (this.manageBookingState.bagSelections().length === 0) {
        this.router.navigate(['/manage-booking/bags'], {
          queryParams: { bookingRef: ref },
          state: { givenName: gn, surname: sn }
        });
        return;
      }

      this.loadOrder(ref, gn, sn);
    });
  }

  private loadOrder(ref: string, _gn: string, _sn: string): void {
    this.retailApi.retrieveOrder(ref).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: (err: { status?: number; message?: string }) => {
        if (err.status === 401) { this.router.navigate(['/manage-booking']); return; }
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
    const bags = this.bagSelections().map(b => ({
      passengerId: b.passengerId,
      segmentId: b.segmentId,
      additionalBags: b.additionalBags,
      bagOfferId: b.bagOfferId,
      price: b.price
    }));

    const payment = {
      method: 'CreditCard',
      cardNumber: card.cardNumber,
      expiryDate: `${card.expiryMonth}/${card.expiryYear}`,
      cvv: card.cvv,
      cardholderName: card.cardholderName
    };

    this.retailApi.addManageBookingBags(this.bookingRef(), bags, payment).subscribe({
      next: () => {
        this.paying.set(false);
        this.manageBookingState.clear();
        this.router.navigate(['/manage-booking/bags-confirmation'], {
          queryParams: { bookingRef: this.bookingRef() },
          state: { givenName: this.givenName(), surname: this.surname() }
        });
      },
      error: (err: { error?: { message?: string }, message?: string }) => {
        this.paying.set(false);
        const msg = (err as any)?.error?.message ?? 'Please check your payment details and try again.';
        this.paymentError.set(msg);
      }
    });
  }

  onBack(): void {
    this.router.navigate(['/manage-booking/bags'], {
      queryParams: { bookingRef: this.bookingRef() },
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }
}
