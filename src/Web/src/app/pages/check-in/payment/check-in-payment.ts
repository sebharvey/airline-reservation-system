import { Component, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { CheckInStateService } from '../../../services/check-in-state.service';
import { CardDetails } from '../../../models/order.model';
import { PaymentFormComponent } from '../../../components/payment-form/payment-form';

@Component({
  selector: 'app-check-in-payment',
  standalone: true,
  imports: [CommonModule, RouterLink, PaymentFormComponent],
  templateUrl: './check-in-payment.html',
  styleUrl: './check-in-payment.css'
})
export class CheckInPaymentComponent implements OnInit {
  paying = signal(false);
  paymentError = signal('');

  readonly order = computed(() => this.checkInState.currentOrder());
  readonly bagSelections = computed(() => this.checkInState.bagSelections());
  readonly seatSelections = computed(() => this.checkInState.seatSelections());
  readonly totalBagAmount = computed(() => this.checkInState.totalBagAmount());
  readonly totalSeatAmount = computed(() => this.checkInState.totalSeatAmount());
  readonly totalAmount = computed(() => this.checkInState.totalPaymentAmount());
  readonly currency = computed(() => this.order()?.currency ?? 'GBP');

  constructor(
    private router: Router,
    private retailApi: RetailApiService,
    private checkInState: CheckInStateService
  ) {}

  ngOnInit(): void {
    if (!this.checkInState.currentOrder()) {
      this.router.navigate(['/check-in']);
      return;
    }
    if (this.checkInState.totalPaymentAmount() === 0) {
      this.router.navigate(['/check-in/boarding-pass']);
    }
  }

  getPassengerName(passengerId: string): string {
    const pax = this.order()?.passengers.find(p => p.passengerId === passengerId);
    return pax ? `${pax.givenName} ${pax.surname}` : passengerId;
  }

  getSegmentRoute(segmentId: string): string {
    const seg = this.order()?.flightSegments?.find(s => s.inventoryId === segmentId);
    return seg ? `${seg.origin}–${seg.destination}` : segmentId;
  }

  onPay(card: CardDetails): void {
    const order = this.order();
    if (!order) return;

    this.paying.set(true);
    this.paymentError.set('');

    this.retailApi.addCheckInAncillaries(
      order.bookingReference,
      this.bagSelections(),
      this.seatSelections(),
      card.cardLast4,
      card.cardType
    ).subscribe({
      next: (result) => {
        this.paying.set(false);
        if (result.documents?.length) {
          this.checkInState.setEmdDocuments(result.documents);
        }
        this.router.navigate(['/check-in/boarding-pass']);
      },
      error: (err: { message?: string }) => {
        this.paying.set(false);
        this.paymentError.set(err?.message ?? 'Please check your payment details and try again.');
      }
    });
  }
}
