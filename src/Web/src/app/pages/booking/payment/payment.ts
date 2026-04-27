import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { BookingStateService } from '../../../services/booking-state.service';
import { LoyaltyStateService } from '../../../services/loyalty-state.service';
import { RetailApiService, ConfirmBasketResponse } from '../../../services/retail-api.service';
import { PaymentSummary, Order, Payment, CardDetails } from '../../../models/order.model';
import { Basket } from '../../../models/order.model';
import { PaymentFormComponent } from '../../../components/payment-form/payment-form';
import { LucideAngularModule } from 'lucide-angular';

function mapConfirmResponseToOrder(result: ConfirmBasketResponse): Order {
  const payments: Payment[] = result.payment ? [{
    paymentReference: result.payment.paymentReference,
    description:      result.payment.description,
    method:           result.payment.method,
    cardLast4:        result.payment.cardLast4,
    cardType:         result.payment.cardType,
    cardholderName:   result.payment.cardholderName,
    maskedCardNumber: result.payment.maskedCardNumber,
    authorisedAmount: result.payment.authorisedAmount,
    settledAmount:    result.payment.settledAmount,
    currency:         result.payment.currency,
    status:           result.payment.status as 'Settled' | 'Authorised' | 'Refunded' | 'Voided',
    authorisedAt:     result.payment.authorisedAt,
    settledAt:        result.payment.settledAt
  }] : [];

  return {
    orderId:           result.orderId,
    bookingReference:  result.bookingReference,
    orderStatus:       result.orderStatus as 'Confirmed' | 'Changed' | 'Cancelled',
    bookingType:       result.bookingType as 'Revenue' | 'Reward',
    channelCode:       result.channelCode,
    currency:          result.currency,
    fareTotal:         result.fareTotal,
    seatTotal:         result.seatTotal,
    bagTotal:          result.bagTotal,
    productTotal:      result.productTotal,
    totalAmount:       result.totalAmount,
    totalPointsAmount: result.totalPointsAmount,
    createdAt:         result.bookedAt,
    passengers: result.passengers.map(p => ({
      passengerId:   p.passengerId,
      type:          p.type as 'ADT' | 'CHD',
      givenName:     p.givenName,
      surname:       p.surname,
      dob:           p.dob ?? '',
      gender:        (p.gender ?? '') as 'Male' | 'Female' | 'Other' | '',
      loyaltyNumber: p.loyaltyNumber ?? null,
      contacts:      p.contacts ? { email: p.contacts.email ?? '', phone: p.contacts.phone ?? '' } : null,
      docs:          p.docs.map(d => ({
        type:           d.type as 'PASSPORT' | 'ID_CARD',
        number:         d.number,
        issuingCountry: d.issuingCountry,
        issueDate:      d.issueDate ?? '',
        expiryDate:     d.expiryDate,
        nationality:    d.nationality
      }))
    })),
    flightSegments: result.flightSegments.map(s => ({
      segmentId:         s.segmentId,
      flightNumber:      s.flightNumber,
      origin:            s.origin,
      destination:       s.destination,
      departureDateTime: s.departureDateTime,
      arrivalDateTime:   s.arrivalDateTime,
      aircraftType:      s.aircraftType,
      operatingCarrier:  s.operatingCarrier,
      marketingCarrier:  s.marketingCarrier,
      cabinCode:         s.cabinCode as any,
      bookingClass:      s.bookingClass
    })),
    orderItems: result.orderItems.map(i => ({
      orderItemId:      i.orderItemId,
      type:             i.type as 'Flight' | 'Seat' | 'Bag' | 'Product' | 'SSR',
      segmentRef:       i.segmentRef,
      passengerRefs:    i.passengerRefs,
      fareFamily:       i.fareFamily,
      fareBasisCode:    i.fareBasisCode,
      unitPrice:        i.unitPrice,
      taxes:            i.taxes,
      totalPrice:       i.totalPrice,
      isRefundable:     i.isRefundable,
      isChangeable:     i.isChangeable,
      paymentReference: i.paymentReference,
      eTickets:         i.eTickets,
      seatNumber:       i.seatNumber,
      seatPosition:     i.seatPosition,
      additionalBags:   i.additionalBags,
      productName:      i.productName,
      productOfferId:   i.productOfferId,
      ssrCode:          i.ssrCode
    })),
    payments,
    pointsRedemption: result.pointsRedemption ? {
      redemptionReference: result.pointsRedemption.redemptionReference,
      loyaltyNumber:       result.pointsRedemption.loyaltyNumber,
      pointsRedeemed:      result.pointsRedemption.pointsRedeemed,
      status:              result.pointsRedemption.status as 'Authorised' | 'Settled' | 'Reversed',
      authorisedAt:        result.pointsRedemption.authorisedAt,
      settledAt:           result.pointsRedemption.settledAt
    } : undefined
  };
}

@Component({
  selector: 'app-payment',
  standalone: true,
  imports: [CommonModule, RouterLink, PaymentFormComponent, LucideAngularModule],
  templateUrl: './payment.html',
  styleUrl: './payment.css'
})
export class PaymentComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly bookingState = inject(BookingStateService);
  private readonly loyaltyState = inject(LoyaltyStateService);
  private readonly retailApi = inject(RetailApiService);

  readonly basket = this.bookingState.basket;
  readonly isRewardBooking = this.bookingState.isRewardBooking;

  readonly paymentSummary = signal<PaymentSummary | null>(null);
  readonly summaryLoading = signal(true);
  readonly summaryError = signal(false);

  paying = signal(false);
  paymentError = signal('');
  showTaxModal = signal(false);

  readonly totalTaxAmount = computed(() => {
    const summary = this.paymentSummary();
    if (!summary) return 0;
    const seatTax = summary.seatSelections.reduce((s, x) => s + (x.tax ?? 0), 0);
    const bagTax = summary.bagSelections.reduce((s, x) => s + (x.tax ?? 0), 0);
    const productTax = summary.productSelections.reduce((s, x) => s + (x.tax ?? 0), 0);
    return summary.totals.taxAmount + seatTax + bagTax + productTax;
  });

  readonly taxSections = computed((): { label: string; lines: { desc: string; amount: number }[] }[] => {
    const summary = this.paymentSummary();
    if (!summary) return [];
    const sections: { label: string; lines: { desc: string; amount: number }[] }[] = [];

    if (summary.totals.taxAmount > 0) {
      const lines: { desc: string; amount: number }[] = [];
      for (const f of summary.flights) {
        if ((f.taxAmount ?? 0) > 0) {
          if (f.taxLines?.length) {
            for (const tl of f.taxLines) {
              lines.push({
                desc: `${tl.code}${tl.description ? ' – ' + tl.description : ''} (${f.flightNumber})`,
                amount: tl.amount
              });
            }
          } else {
            lines.push({ desc: `${f.flightNumber} ${f.origin} → ${f.destination}`, amount: f.taxAmount });
          }
        }
      }
      if (lines.length > 0) sections.push({ label: 'Fare Taxes', lines });
    }

    const seatLines = summary.seatSelections
      .filter(s => (s.tax ?? 0) > 0)
      .map(s => ({ desc: `Seat ${s.seatNumber} · ${s.seatPosition} (${s.flightNumber})`, amount: s.tax }));
    if (seatLines.length > 0) sections.push({ label: 'Seat Taxes', lines: seatLines });

    const bagLines = summary.bagSelections
      .filter(b => (b.tax ?? 0) > 0)
      .map(b => ({ desc: `${b.additionalBags} additional bag(s) – ${b.flightNumber}`, amount: b.tax }));
    if (bagLines.length > 0) sections.push({ label: 'Baggage Taxes', lines: bagLines });

    const productLines = summary.productSelections
      .filter(p => (p.tax ?? 0) > 0)
      .map(p => ({ desc: p.name, amount: p.tax }));
    if (productLines.length > 0) sections.push({ label: 'Product Taxes', lines: productLines });

    return sections;
  });

  ngOnInit(): void {
    const basket = this.basket();
    if (!basket) {
      this.router.navigate(['/']);
      return;
    }
    this.loadPaymentSummary(basket.basketId);
  }

  private loadPaymentSummary(basketId: string): void {
    this.summaryLoading.set(true);
    this.summaryError.set(false);
    this.retailApi.getBasketPaymentSummary(basketId).subscribe({
      next: (summary) => {
        this.paymentSummary.set(summary);
        this.summaryLoading.set(false);
      },
      error: () => {
        this.summaryLoading.set(false);
        this.summaryError.set(true);
      }
    });
  }

  onPay(card: CardDetails): void {
    const basket = this.basket();
    if (!basket) return;

    this.paying.set(true);

    const loyaltyPointsToRedeem = basket.bookingType === 'Reward' ? basket.totalPointsAmount : undefined;

    this.retailApi.confirmBasket(
      basket.basketId,
      'CreditCard',
      card.cardNumber,
      card.expiryDate,
      card.cvv,
      card.cardholderName,
      loyaltyPointsToRedeem,
      basket.productSelections ?? []
    ).subscribe({
      next: (result) => {
        const order = mapConfirmResponseToOrder(result);
        this.bookingState.confirmOrder(order);
        this.paying.set(false);
        this.router.navigate(['/booking/confirmation']);
      },
      error: (err) => {
        this.paying.set(false);
        const msg = err?.error?.message ?? 'Please check your payment details and try again.';
        this.paymentError.set(msg);
      }
    });
  }

  openTaxModal(): void { this.showTaxModal.set(true); }
  closeTaxModal(): void { this.showTaxModal.set(false); }

  formatPrice(amount: number): string {
    const currency = this.paymentSummary()?.currency ?? this.basket()?.currency ?? 'GBP';
    return `${currency} ${amount.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  formatDateTime(dt: string): string {
    if (!dt) return '';
    return new Date(dt).toLocaleString('en-GB', {
      day: 'numeric', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  }

  formatGrandTotal(): string {
    const summary = this.paymentSummary();
    if (!summary) return '';
    return this.formatPrice(summary.totals.grandTotal);
  }
}
