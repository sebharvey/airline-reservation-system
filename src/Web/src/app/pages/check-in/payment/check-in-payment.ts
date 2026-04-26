import { Component, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { CheckInStateService } from '../../../services/check-in-state.service';
import { CardDetails, EmdDocument, PaymentSummary } from '../../../models/order.model';
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
  summaryLoading = signal(true);
  summary = signal<PaymentSummary | null>(null);
  paymentConfirmed = signal(false);
  paymentReference = signal('');
  confirmedDocuments = signal<EmdDocument[]>([]);

  readonly order = computed(() => this.checkInState.currentOrder());
  readonly currency = computed(() => this.summary()?.currency ?? this.order()?.currency ?? 'GBP');
  readonly totalAmount = computed(() =>
    this.summary()?.totals.grandTotal ?? this.checkInState.totalPaymentAmount()
  );

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

    const basketId = this.checkInState.basketId();
    if (!basketId) {
      this.summaryLoading.set(false);
      if (this.checkInState.totalPaymentAmount() === 0) {
        this.router.navigate(['/check-in/hazmat']);
      }
      return;
    }

    this.retailApi.getBasketPaymentSummary(basketId).subscribe({
      next: (s) => {
        this.summary.set(s);
        this.summaryLoading.set(false);
        if (s.totals.grandTotal === 0) {
          this.router.navigate(['/check-in/hazmat']);
        }
      },
      error: () => {
        this.summaryLoading.set(false);
        if (this.checkInState.totalPaymentAmount() === 0) {
          this.router.navigate(['/check-in/hazmat']);
        }
      }
    });
  }

  getPassengerName(passengerId: string): string {
    const pax = this.order()?.passengers.find(p => p.passengerId === passengerId);
    return pax ? `${pax.givenName} ${pax.surname}` : passengerId;
  }

  getSegmentRoute(): string {
    const seg = this.order()?.flightSegments?.[0];
    return seg ? `${seg.origin}–${seg.destination}` : '';
  }

  onPay(card: CardDetails): void {
    const order = this.order();
    if (!order) return;

    this.paying.set(true);
    this.paymentError.set('');

    this.retailApi.addCheckInAncillaries(
      order.bookingReference,
      this.checkInState.bagSelections(),
      this.checkInState.seatSelections(),
      this.checkInState.basketId(),
      card
    ).subscribe({
      next: (result) => {
        this.paying.set(false);
        if (result.documents?.length) {
          this.checkInState.setEmdDocuments(result.documents);
          this.confirmedDocuments.set(result.documents);
        }
        this.paymentReference.set(result.paymentReference ?? '');
        this.paymentConfirmed.set(true);
      },
      error: (err: { message?: string }) => {
        this.paying.set(false);
        this.paymentError.set(err?.message ?? 'Please check your payment details and try again.');
      }
    });
  }

  continueToCheckIn(): void {
    this.router.navigate(['/check-in/hazmat']);
  }

  documentLabel(doc: EmdDocument): string {
    return doc.documentType === 'SeatAncillary' ? 'Seat' : 'Baggage';
  }
}
