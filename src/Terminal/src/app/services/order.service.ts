import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface OrderSummary {
  bookingReference: string;
  orderStatus: string;
  channelCode: string;
  currencyCode: string;
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
  dateOfBirth: string | null;
  type: string;
  gender: string | null;
  contacts: PassengerContacts | null;
  travelDocument: PassengerTravelDocument | null;
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

export interface OrderItem {
  itemId: string;
  itemType: string;
  passengerId: string | null;
  segmentId: string | null;
  status: string;
  eTicketNumber: string | null;
  seatNumber: string | null;
  bagWeightKg: number | null;
  ssrCode: string | null;
  amount: number | null;
  currency: string | null;
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
  authorisedAt: string | null;
  settledAt: string | null;
  events?: PaymentEvent[];
}

export interface OrderHistoryEvent {
  eventType: string;
  description: string;
  timestamp: string;
}

export interface OrderData {
  dataLists: {
    passengers: OrderPassenger[];
    flightSegments: FlightSegment[];
  };
  orderItems: OrderItem[];
  ssrItems: SsrItem[];
  payments: OrderPayment[];
  history: OrderHistoryEvent[];
}

export interface OrderDetail {
  orderId: string;
  bookingReference: string;
  orderStatus: string;
  channelCode: string;
  currencyCode: string;
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
    return firstValueFrom(
      this.#http.get<SsrOption[]>(`${environment.retailApiUrl}/api/v1/ssr/options`)
    );
  }

  async updateOrderSsrs(bookingRef: string, actions: SsrPatchAction[]): Promise<void> {
    await firstValueFrom(
      this.#http.patch(
        `${environment.retailApiUrl}/api/v1/orders/${bookingRef.toUpperCase()}/ssrs`,
        { actions }
      )
    );
  }
}
