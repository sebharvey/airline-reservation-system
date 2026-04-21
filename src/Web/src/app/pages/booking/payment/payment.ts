import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BookingStateService } from '../../../services/booking-state.service';
import { LoyaltyStateService } from '../../../services/loyalty-state.service';
import { RetailApiService, ConfirmBasketResponse } from '../../../services/retail-api.service';
import { PaymentSummary } from '../../../models/order.model';
import { Basket, Order, Payment } from '../../../models/order.model';

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
  imports: [CommonModule, FormsModule, RouterLink],
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

  /** API-driven summary — the single source of truth for the order summary section. */
  readonly paymentSummary = signal<PaymentSummary | null>(null);
  readonly summaryLoading = signal(true);
  readonly summaryError = signal(false);

  // Payment form fields
  cardholderName = signal('');
  cardNumber = signal('');
  expiryMonth = signal('');
  expiryYear = signal('');
  cvv = signal('');

  submitted = signal(false);
  paying = signal(false);
  paymentError = signal('');
  showErrorModal = signal(false);
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
                desc: `${tl.code}${tl.description ? ' \u2013 ' + tl.description : ''} (${f.flightNumber})`,
                amount: tl.amount
              });
            }
          } else {
            lines.push({ desc: `${f.flightNumber} ${f.origin} \u2192 ${f.destination}`, amount: f.taxAmount });
          }
        }
      }
      if (lines.length > 0) sections.push({ label: 'Fare Taxes', lines });
    }

    const seatLines = summary.seatSelections
      .filter(s => (s.tax ?? 0) > 0)
      .map(s => ({ desc: `Seat ${s.seatNumber} \u00b7 ${s.seatPosition} (${s.flightNumber})`, amount: s.tax }));
    if (seatLines.length > 0) sections.push({ label: 'Seat Taxes', lines: seatLines });

    const bagLines = summary.bagSelections
      .filter(b => (b.tax ?? 0) > 0)
      .map(b => ({ desc: `${b.additionalBags} additional bag(s) \u2013 ${b.flightNumber}`, amount: b.tax }));
    if (bagLines.length > 0) sections.push({ label: 'Baggage Taxes', lines: bagLines });

    const productLines = summary.productSelections
      .filter(p => (p.tax ?? 0) > 0)
      .map(p => ({ desc: p.name, amount: p.tax }));
    if (productLines.length > 0) sections.push({ label: 'Product Taxes', lines: productLines });

    return sections;
  });

  readonly cardDisplayNumber = computed(() => {
    const raw = this.cardNumber().replace(/\D/g, '').substring(0, 16);
    return raw.replace(/(.{4})/g, '$1 ').trim();
  });

  readonly expiryYears = computed(() => {
    const current = new Date().getFullYear();
    return Array.from({ length: 12 }, (_, i) => current + i);
  });

  readonly expiryMonths = computed(() => [
    { value: '01', label: '01 - Jan' }, { value: '02', label: '02 - Feb' },
    { value: '03', label: '03 - Mar' }, { value: '04', label: '04 - Apr' },
    { value: '05', label: '05 - May' }, { value: '06', label: '06 - Jun' },
    { value: '07', label: '07 - Jul' }, { value: '08', label: '08 - Aug' },
    { value: '09', label: '09 - Sep' }, { value: '10', label: '10 - Oct' },
    { value: '11', label: '11 - Nov' }, { value: '12', label: '12 - Dec' }
  ]);

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

  fillTestCard(): void {
    const nextYear = (new Date().getFullYear() + 3).toString();
    this.cardholderName.set('Test User');
    this.cardNumber.set('4111111111111111');
    this.expiryMonth.set('12');
    this.expiryYear.set(nextYear);
    this.cvv.set('123');
  }

  onCardNumberInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const digits = input.value.replace(/\D/g, '').substring(0, 16);
    this.cardNumber.set(digits);
    input.value = digits.replace(/(.{4})/g, '$1 ').trim();
  }

  isFormValid(): boolean {
    const name = this.cardholderName().trim();
    const num = this.cardNumber().replace(/\D/g, '');
    const month = this.expiryMonth();
    const year = this.expiryYear();
    const cvv = this.cvv().trim();
    return !!(name && num.length === 16 && month && year && cvv.length >= 3);
  }

  detectCardType(): string {
    const num = this.cardNumber().replace(/\D/g, '');
    if (num.startsWith('4')) return 'Visa';
    if (/^5[1-5]/.test(num) || /^2[2-7]/.test(num)) return 'Mastercard';
    if (/^3[47]/.test(num)) return 'Amex';
    return 'Card';
  }

  onPay(): void {
    this.submitted.set(true);
    this.paymentError.set('');
    if (!this.isFormValid()) return;

    const basket = this.basket();
    if (!basket) return;

    this.paying.set(true);

    const loyaltyNumber = this.loyaltyState.currentCustomer()?.loyaltyNumber;
    const isReward = basket.bookingType === 'Reward';
    const loyaltyPointsToRedeem = isReward ? basket.totalPointsAmount : undefined;

    const expiryDate = `${this.expiryMonth()}/${this.expiryYear().toString().slice(-2)}`;

    this.retailApi.confirmBasket(
      basket.basketId,
      'CreditCard',
      this.cardNumber().replace(/\D/g, ''),
      expiryDate,
      this.cvv(),
      this.cardholderName().trim(),
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
        this.showErrorModal.set(true);
      }
    });
  }

  dismissErrorModal(): void {
    this.showErrorModal.set(false);
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
