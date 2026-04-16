import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface SearchOffer {
  offerId: string;
  flightNumber: string;
  departureDate: string;
  departureTime: string;
  arrivalTime: string;
  arrivalDayOffset: number;
  origin: string;
  destination: string;
  aircraftType: string;
  cabinCode: string;
  fareBasisCode: string;
  fareFamily: string;
  currencyCode: string;
  baseFareAmount: number;
  taxAmount: number;
  totalAmount: number;
  isRefundable: boolean;
  isChangeable: boolean;
  seatsAvailable: number;
}

export interface SearchResponse {
  offers: SearchOffer[];
}

// ── API response shapes ───────────────────────────────────────────────────────

interface ApiSliceOffer {
  offerId: string;
  flightNumber: string;
  departureDate: string;
  departureTime: string;
  arrivalTime: string;
  arrivalDayOffset: number;
  origin: string;
  destination: string;
  aircraftType: string;
  cabinCode: string;
  fareBasisCode: string;
  fareFamily: string;
  currencyCode: string;
  baseFareAmount: number;
  taxAmount: number;
  totalAmount: number;
  isRefundable: boolean;
  isChangeable: boolean;
  seatsAvailable: number;
}

interface ApiSliceSearchResponse {
  offers: ApiSliceOffer[];
}

export interface BasketPassenger {
  passengerId: string;
  type: 'ADT' | 'CHD' | 'INF' | 'YTH';
  givenName: string;
  surname: string;
  dob: string | null;
  gender: string | null;
  loyaltyNumber: string | null;
  contacts: { email: string | null; phone: string | null } | null;
  docs: unknown[];
}

export interface BasketFlight {
  offerId: string;
  flightNumber: string;
  origin: string;
  destination: string;
  departureDateTime: string;
  arrivalDateTime: string;
  cabinCode: string;
  fareFamily: string | null;
  totalAmount: number;
}

export interface BasketSummary {
  basketId: string;
  status: string;
  totalFareAmount: number | null;
  totalSeatAmount: number;
  totalBagAmount: number;
  totalPrice: number;
  totalPointsAmount: number | null;
  currency: string;
  expiresAt: string;
  flights: BasketFlight[];
}

export interface ConfirmResponse {
  bookingReference: string;
  status: string;
  totalPrice: number;
  currency: string;
  eTickets: Array<{
    eTicketNumber: string;
    passengerId: string;
    segmentIds: string[];
  }>;
}

// ── Payment summary ──────────────────────────────────────────────────────────

export interface PaymentSummaryFlight {
  offerId: string;
  flightNumber: string;
  origin: string;
  destination: string;
  departureDateTime: string;
  arrivalDateTime: string;
  cabinCode: string;
  fareFamily: string | null;
  baseFareAmount: number;
  taxAmount: number;
  totalAmount: number;
}

export interface PaymentSummaryPassenger {
  passengerId: string;
  type: string;
  givenName: string;
  surname: string;
}

export interface PaymentSummaryTotals {
  fareAmount: number;
  taxAmount: number;
  seatAmount: number;
  bagAmount: number;
  productAmount: number;
  pointsAmount: number;
  grandTotal: number;
}

export interface PaymentSummary {
  basketId: string;
  bookingType: string;
  currency: string;
  ticketingTimeLimit: string | null;
  flights: PaymentSummaryFlight[];
  passengers: PaymentSummaryPassenger[];
  seatSelections: Array<{
    passengerId: string;
    seatNumber: string;
    seatPosition: string;
    flightNumber: string;
    price: number;
  }>;
  bagSelections: Array<{
    passengerId: string;
    additionalBags: number;
    flightNumber: string;
    price: number;
  }>;
  totals: PaymentSummaryTotals;
}

// ── Seat selection ───────────────────────────────────────────────────────────

export interface SeatSelection {
  seatOfferId: string;
  passengerRef: string;
  flightOfferId: string;
}

export interface SeatUpdateResponse {
  basketId: string;
  totalSeatAmount: number;
  totalAmount: number;
}

@Injectable({ providedIn: 'root' })
export class NewOrderService {
  #http = inject(HttpClient);
  #retailUrl = `${environment.retailApiUrl}/api/v1/admin`;

  async searchSlice(
    origin: string,
    destination: string,
    departureDate: string,
    paxCount: number,
  ): Promise<SearchResponse> {
    const res = await firstValueFrom(
      this.#http.post<ApiSliceSearchResponse>(`${this.#retailUrl}/search/slice`, {
        origin: origin.toUpperCase(),
        destination: destination.toUpperCase(),
        departureDate,
        paxCount,
        bookingType: 'Revenue',
      })
    );

    const offers: SearchOffer[] = (res.offers ?? []).map(o => ({
      offerId: o.offerId,
      flightNumber: o.flightNumber,
      departureDate: o.departureDate,
      departureTime: o.departureTime,
      arrivalTime: o.arrivalTime,
      arrivalDayOffset: o.arrivalDayOffset,
      origin: o.origin,
      destination: o.destination,
      aircraftType: o.aircraftType,
      cabinCode: o.cabinCode,
      fareBasisCode: o.fareBasisCode,
      fareFamily: o.fareFamily,
      currencyCode: o.currencyCode,
      baseFareAmount: o.baseFareAmount,
      taxAmount: o.taxAmount,
      totalAmount: o.totalAmount,
      isRefundable: o.isRefundable,
      isChangeable: o.isChangeable,
      seatsAvailable: o.seatsAvailable,
    }));
    return { offers };
  }

  async createBasket(offerIds: string[], passengerCount: number): Promise<BasketSummary> {
    return firstValueFrom(
      this.#http.post<BasketSummary>(`${this.#retailUrl}/basket`, {
        segments: offerIds.map(offerId => ({ offerId })),
        passengerCount,
        currency: 'GBP',
        bookingType: 'Revenue',
        loyaltyNumber: null,
      })
    );
  }

  async updatePassengers(basketId: string, passengers: BasketPassenger[]): Promise<void> {
    await firstValueFrom(
      this.#http.put(`${this.#retailUrl}/basket/${basketId}/passengers`, { passengers })
    );
  }

  async getBasketSummary(basketId: string): Promise<BasketSummary> {
    return firstValueFrom(
      this.#http.get<BasketSummary>(`${this.#retailUrl}/basket/${basketId}/summary`)
    );
  }

  async getPaymentSummary(basketId: string): Promise<PaymentSummary> {
    return firstValueFrom(
      this.#http.get<PaymentSummary>(`${this.#retailUrl}/basket/${basketId}/payment-summary`)
    );
  }

  async updateSeats(basketId: string, seatSelections: SeatSelection[]): Promise<SeatUpdateResponse> {
    return firstValueFrom(
      this.#http.put<SeatUpdateResponse>(`${this.#retailUrl}/basket/${basketId}/seats`, { seatSelections })
    );
  }

  async confirmBasket(
    basketId: string,
    payment: {
      method: string;
      cardNumber: string;
      expiryDate: string;
      cvv: string;
      cardholderName: string;
    },
  ): Promise<ConfirmResponse> {
    return firstValueFrom(
      this.#http.post<ConfirmResponse>(`${this.#retailUrl}/basket/${basketId}/confirm`, {
        channelCode: 'CC',
        payment,
      })
    );
  }
}
