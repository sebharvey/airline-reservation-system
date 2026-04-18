import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface OrderSummary {
  bookingReference: string;
  orderStatus: string;
  channelCode: string;
  currency: string;
  totalAmount: number | null;
  createdAt: string;
  leadPassengerName: string;
  route: string;
}

export interface PassengerContacts {
  email: string | null;
  phone: string | null;
}

export interface PassengerTravelDocument {
  type: string | null;
  number: string | null;
  issuingCountry: string | null;
  expiryDate: string | null;
  nationality: string | null;
}

export interface OrderPassenger {
  passengerId: string;
  givenName: string;
  surname: string;
  dob: string | null;
  type: string;
  gender: string | null;
  contacts: PassengerContacts | null;
  docs: PassengerTravelDocument[];
  loyaltyNumber: string | null;
}

export interface FlightSegment {
  segmentId: string;
  flightNumber: string;
  origin: string;
  destination: string;
  departureTime: string;
  arrivalTime: string;
  cabinClass: string;
  fareClass: string | null;
  departureDate: string | null;
}

export interface TaxLine {
  code: string;
  amount: number;
  description: string | null;
}

export interface OrderItem {
  itemId: string;
  itemType: string;
  description: string | null;
  passengerId: string | null;
  segmentId: string | null;
  status: string;
  eTicketNumber: string | null;
  seatNumber: string | null;
  bagWeightKg: number | null;
  additionalBags: number | null;
  ssrCode: string | null;
  name: string | null;
  amount: number | null;
  fareAmount: number | null;
  taxAmount: number | null;
  totalAmount: number | null;
  lineTotal: number | null;
  taxLines: TaxLine[] | null;
  currency: string | null;
  passengerCount: number | null;
}

export interface ItemTotals {
  subtotalFare: number;
  subtotalTax: number;
  grandTotal: number;
  currency: string;
}

export interface SsrOption {
  ssrCatalogueId: string;
  ssrCode: string;
  label: string;
  category: string;
  isActive: boolean;
}

export interface SsrItem {
  ssrCode: string;
  passengerRef: string;
  segmentRef: string;
}

export interface SsrPatchAction {
  action: 'add' | 'remove';
  ssrCode: string;
  passengerRef: string;
  segmentRef: string;
}

export interface PaymentEvent {
  eventType: string;
  amount: number;
  currency: string;
  notes: string | null;
  createdAt: string;
}

export interface OrderPayment {
  paymentId: string;
  amount: number;
  currency: string;
  status: string;
  paymentMethod: string | null;
  cardType: string | null;
  cardLast4: string | null;
  authorisedAt: string | null;
  settledAt: string | null;
  events?: PaymentEvent[];
}

export interface OrderHistoryEvent {
  eventType: string;
  description: string;
  timestamp: string;
}

export interface TicketCoupon {
  couponNumber: number;
  status: string;
  marketing: { carrier: string; flightNumber: string } | null;
  operating: { carrier: string; flightNumber: string } | null;
  origin: string;
  destination: string;
  departureDate: string | null;
  departureTime: string | null;
  classOfService: string | null;
  cabin: string | null;
  fareBasisCode: string | null;
  notValidBefore: string | null;
  notValidAfter: string | null;
  baggageAllowance: { type: string; quantity: number | null; weightKg: number | null } | null;
  seat: string | null;
  fareComponent: { amount: number; currency: string } | null;
}

export interface TicketData {
  passenger: {
    surname: string;
    givenName: string;
    passengerTypeCode: string;
    frequentFlyer: { carrier: string; number: string; tier: string } | null;
  } | null;
  fareConstruction: {
    pricingCurrency: string;
    collectingCurrency: string;
    baseFare: number;
    equivalentFarePaid: number;
    nucAmount: number;
    roeApplied: number;
    fareCalculationLine: string | null;
    taxes: Array<{ code: string; amount: number; currency: string; description: string }>;
    totalTaxes: number;
    totalAmount: number;
  } | null;
  formOfPayment: {
    type: string;
    cardType: string | null;
    maskedPan: string | null;
    expiryMmYy: string | null;
    approvalCode: string | null;
    amount: number;
    currency: string;
  } | null;
  endorsementsRestrictions: string | null;
  coupons: TicketCoupon[];
  ssrCodes: Array<{ code: string; description: string; segmentRef: string }>;
  changeHistory: Array<{ eventType: string; occurredAt: string; actor: string; detail: string }>;
}

export interface Ticket {
  ticketId: string;
  eTicketNumber: string;
  bookingReference: string;
  passengerId: string;
  isVoided: boolean;
  voidedAt: string | null;
  ticketData: TicketData | null;
  createdAt: string;
  updatedAt: string;
  version: number;
}

export interface OrderData {
  dataLists: {
    passengers: OrderPassenger[];
    flightSegments: FlightSegment[];
  };
  orderItems: OrderItem[];
  payments: OrderPayment[];
  history: OrderHistoryEvent[];
  itemTotals: ItemTotals | null;
}

export interface OrderDetail {
  orderId: string;
  bookingReference: string;
  orderStatus: string;
  channelCode: string;
  currency: string;
  totalAmount: number | null;
  ticketingTimeLimit: string | null;
  createdAt: string;
  updatedAt: string;
  version: number;
  orderData: OrderData | null;
}

@Injectable({ providedIn: 'root' })
export class OrderService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.retailApiUrl}/api/v1/admin/orders`;

  async getRecentOrders(limit = 10): Promise<OrderSummary[]> {
    return firstValueFrom(
      this.#http.get<OrderSummary[]>(`${this.#baseUrl}?limit=${limit}`)
    );
  }

  async getOrderByRef(bookingRef: string): Promise<OrderDetail | null> {
    try {
      return await firstValueFrom(
        this.#http.get<OrderDetail>(`${this.#baseUrl}/${bookingRef.toUpperCase()}`)
      );
    } catch (err: any) {
      if (err?.status === 404) return null;
      throw err;
    }
  }

  async updateOrderPassengers(bookingRef: string, passengers: OrderPassenger[]): Promise<void> {
    await firstValueFrom(
      this.#http.patch(
        `${environment.retailApiUrl}/api/v1/orders/${bookingRef.toUpperCase()}/passengers`,
        { passengers },
      )
    );
  }

  async getSsrOptions(): Promise<SsrOption[]> {
    const response = await firstValueFrom(
      this.#http.get<{ ssrOptions: SsrOption[] }>(`${environment.retailApiUrl}/api/v1/ssr/options`)
    );
    return response.ssrOptions;
  }

  async updateOrderSsrs(bookingRef: string, actions: SsrPatchAction[]): Promise<void> {
    await firstValueFrom(
      this.#http.patch(
        `${environment.retailApiUrl}/api/v1/orders/${bookingRef.toUpperCase()}/ssrs`,
        { actions }
      )
    );
  }

  async getTicketsByBookingRef(bookingRef: string): Promise<Ticket[]> {
    return firstValueFrom(
      this.#http.get<Ticket[]>(
        `${environment.retailApiUrl}/api/v1/admin/orders/${bookingRef.toUpperCase()}/tickets`
      )
    );
  }

  // TODO: Remove — temporary debug methods
  async getOrderDebug(bookingRef: string): Promise<unknown> {
    return firstValueFrom(
      this.#http.get<unknown>(
        `${environment.retailApiUrl}/api/v1/admin/orders/${bookingRef.toUpperCase()}/debug`
      )
    );
  }

  async getOrderDebugTickets(bookingRef: string): Promise<unknown> {
    return firstValueFrom(
      this.#http.get<unknown>(
        `${environment.retailApiUrl}/api/v1/admin/orders/${bookingRef.toUpperCase()}/debug/tickets`
      )
    );
  }
}
