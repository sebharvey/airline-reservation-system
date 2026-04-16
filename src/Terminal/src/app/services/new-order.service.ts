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
  changeFeeAmount: number;
  cancellationFeeAmount: number;
  seatsAvailable: number;
  expiresAt: string;
}

export interface SearchResponse {
  origin: string;
  destination: string;
  departureDate: string;
  offers: SearchOffer[];
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
    return firstValueFrom(
      this.#http.post<SearchResponse>(`${this.#retailUrl}/search/slice`, {
        origin: origin.toUpperCase(),
        destination: destination.toUpperCase(),
        departureDate,
        paxCount,
        bookingType: 'Revenue',
      })
    );
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
