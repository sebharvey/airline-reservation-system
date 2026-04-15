import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BookingStateService } from '../../../services/booking-state.service';
import { LoyaltyStateService } from '../../../services/loyalty-state.service';
import { RetailApiService, IssuedETicket } from '../../../services/retail-api.service';
import { PaymentSummary } from '../../../models/order.model';
import { Basket, Order, OrderItem, FlightSegment, Payment, Passenger, PointsRedemption } from '../../../models/order.model';

function randomAlpha(len: number): string {
  const chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ';
  return Array.from({ length: len }, () => chars[Math.floor(Math.random() * chars.length)]).join('');
}

function randomNum(len: number): string {
  return Array.from({ length: len }, () => String(Math.floor(Math.random() * 10))).join('');
}

function generateOrderId(): string {
  return 'ORD-' + randomNum(8);
}

function maskCardNumber(cardNumber: string): string {
  const digits = cardNumber.replace(/\D/g, '');
  const last4 = digits.slice(-4);
  if (digits.length === 15) return `**** ****** *${last4}`;
  return `**** **** **** ${last4}`;
}

function buildOrderFromBasket(basket: Basket, cardLast4: string, cardType: string, bookingRef: string, loyaltyNumber?: string, issuedETickets?: IssuedETicket[], bookedAt?: string, maskedCardNumber?: string, cardholderName?: string): Order {
  const now = bookedAt ?? new Date().toISOString();
  const orderId = generateOrderId();
  const payRef = 'PAY-' + randomNum(8);

  const flightSegments: FlightSegment[] = basket.flightOffers.map((fo, idx) => ({
    segmentId: `SEG-${idx + 1}`,
    flightNumber: fo.flightNumber,
    origin: fo.origin,
    destination: fo.destination,
    departureDateTime: fo.departureDateTime,
    arrivalDateTime: fo.arrivalDateTime,
    aircraftType: fo.aircraftType,
    operatingCarrier: 'AX',
    marketingCarrier: 'AX',
    cabinCode: fo.cabinCode,
    bookingClass: fo.fareBasisCode.charAt(0) || 'Y'
  }));

  const orderItems: OrderItem[] = [];

  // Flight items
  basket.flightOffers.forEach((fo, idx) => {
    const segId = `SEG-${idx + 1}`;
    const paxRefs = fo.passengerRefs;
    const eTickets = paxRefs.map(pRef => {
      const pax = basket.passengers.find(p => p.passengerId === pRef);
      const realTicket = issuedETickets?.find(t => t.passengerId === pRef);
      return {
        passengerId: pax?.passengerId ?? pRef,
        eTicketNumber: realTicket?.eTicketNumber ?? 'N/A'
      };
    });

    const seatsForSeg = basket.seatSelections.filter(s => s.basketItemRef === fo.basketItemId);
    const seatAssignments = seatsForSeg.map(s => ({
      passengerId: s.passengerId,
      seatNumber: s.seatNumber
    }));

    orderItems.push({
      orderItemId: `OI-${randomNum(6)}`,
      type: 'Flight',
      segmentRef: segId,
      passengerRefs: paxRefs,
      fareFamily: fo.fareFamily,
      fareBasisCode: fo.fareBasisCode,
      unitPrice: fo.unitPrice,
      taxes: fo.taxes,
      totalPrice: fo.totalPrice,
      isRefundable: fo.isRefundable,
      isChangeable: fo.isChangeable,
      paymentReference: payRef,
      eTickets,
      seatAssignments
    });
  });

  // Seat items
  basket.seatSelections.forEach(sel => {
    const fo = basket.flightOffers.find(f => f.basketItemId === sel.basketItemRef);
    const segId = fo ? `SEG-${basket.flightOffers.indexOf(fo) + 1}` : 'SEG-1';
    orderItems.push({
      orderItemId: `OI-${randomNum(6)}`,
      type: 'Seat',
      segmentRef: segId,
      passengerRefs: [sel.passengerId],
      unitPrice: sel.price,
      taxes: 0,
      totalPrice: sel.price,
      paymentReference: payRef,
      seatNumber: sel.seatNumber,
      seatPosition: sel.seatPosition
    });
  });

  // Bag items
  basket.bagSelections.forEach(sel => {
    const fo = basket.flightOffers.find(f => f.basketItemId === sel.basketItemRef);
    const segId = fo ? `SEG-${basket.flightOffers.indexOf(fo) + 1}` : 'SEG-1';
    orderItems.push({
      orderItemId: `OI-${randomNum(6)}`,
      type: 'Bag',
      segmentRef: segId,
      passengerRefs: [sel.passengerId],
      unitPrice: sel.price,
      taxes: 0,
      totalPrice: sel.price,
      paymentReference: payRef,
      additionalBags: sel.additionalBags,
      freeBagsIncluded: 1
    });
  });

  // Product items
  basket.productSelections?.forEach(sel => {
    const fo = sel.segmentRef ? basket.flightOffers.find(f => f.basketItemId === sel.segmentRef) : null;
    const segId = fo ? `SEG-${basket.flightOffers.indexOf(fo) + 1}` : 'SEG-1';
    orderItems.push({
      orderItemId: `OI-${randomNum(6)}`,
      type: 'Product',
      segmentRef: segId,
      passengerRefs: [sel.passengerId],
      unitPrice: sel.price,
      taxes: 0,
      totalPrice: sel.price,
      paymentReference: payRef,
      productName: sel.name,
      productOfferId: sel.offerId
    });
  });

  const isReward = basket.bookingType === 'Reward';
  const paymentDescription = isReward ? 'Taxes and fees' : 'Full payment';

  const payment: Payment = {
    paymentReference: payRef,
    description: paymentDescription,
    method: 'CreditCard',
    cardLast4,
    cardType,
    cardholderName,
    maskedCardNumber,
    authorisedAmount: basket.totalAmount,
    settledAmount: basket.totalAmount,
    currency: basket.currency,
    status: 'Settled',
    authorisedAt: now,
    settledAt: now
  };

  let pointsRedemption: PointsRedemption | undefined;
  if (isReward && loyaltyNumber) {
    pointsRedemption = {
      redemptionReference: crypto.randomUUID(),
      loyaltyNumber,
      pointsRedeemed: basket.totalPointsAmount,
      status: 'Settled',
      authorisedAt: now,
      settledAt: now
    };
  }

  return {
    orderId,
    bookingReference: bookingRef,
    orderStatus: 'Confirmed',
    bookingType: basket.bookingType,
    channelCode: 'WEB',
    currency: basket.currency,
    totalAmount: basket.totalAmount,
    totalPointsAmount: isReward ? basket.totalPointsAmount : undefined,
    createdAt: now,
    passengers: basket.passengers,
    flightSegments,
    orderItems,
    payments: [payment],
    pointsRedemption
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
        const rawCardNumber = this.cardNumber().replace(/\D/g, '');
        const resolvedMaskedCard = result.maskedCardNumber ?? maskCardNumber(rawCardNumber);
        const resolvedCardType = result.cardType ?? this.detectCardType();
        const resolvedCardholderName = result.cardholderName ?? this.cardholderName().trim();
        const order = buildOrderFromBasket(basket, this.cardLast4(), resolvedCardType, result.bookingReference, loyaltyNumber, result.eTickets, result.bookedAt, resolvedMaskedCard, resolvedCardholderName);
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

  formatPrice(amount: number): string {
    const currency = this.paymentSummary()?.currency ?? this.basket()?.currency ?? 'GBP';
    return `${currency} ${amount.toFixed(2)}`;
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
