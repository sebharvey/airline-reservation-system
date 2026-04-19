import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface PaymentListItem {
  paymentId: string;
  bookingReference: string | null;
  method: string;
  cardType: string | null;
  cardLast4: string | null;
  currencyCode: string;
  amount: number;
  authorisedAmount: number | null;
  settledAmount: number | null;
  status: string;
  authorisedAt: string | null;
  settledAt: string | null;
  description: string | null;
  createdAt: string;
  updatedAt: string;
  eventCount: number;
}

export interface PaymentDetail {
  paymentId: string;
  bookingReference: string | null;
  method: string;
  cardType: string | null;
  cardLast4: string | null;
  currencyCode: string;
  amount: number;
  authorisedAmount: number | null;
  settledAmount: number | null;
  status: string;
  authorisedAt: string | null;
  settledAt: string | null;
  description: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface PaymentEvent {
  paymentEventId: string;
  paymentId: string;
  eventType: string;
  productType: string;
  amount: number;
  currencyCode: string;
  notes: string | null;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class PaymentService {
  #http = inject(HttpClient);

  getPaymentsByDate(date: string): Promise<PaymentListItem[]> {
    return firstValueFrom(
      this.#http.get<PaymentListItem[]>(`${environment.adminApiUrl}/api/v1/admin/payments?date=${date}`)
    );
  }

  getPayment(paymentId: string): Promise<PaymentDetail> {
    return firstValueFrom(
      this.#http.get<PaymentDetail>(`${environment.adminApiUrl}/api/v1/admin/payments/${paymentId}`)
    );
  }

  getPaymentEvents(paymentId: string): Promise<PaymentEvent[]> {
    return firstValueFrom(
      this.#http.get<PaymentEvent[]>(`${environment.adminApiUrl}/api/v1/admin/payments/${paymentId}/events`)
    );
  }
}
