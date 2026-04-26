import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';
import { OrderDetail } from './order.service';

export type { OrderDetail } from './order.service';

// ── Bag types ─────────────────────────────────────────────────────────────────

export interface BagOffer {
  bagOfferId: string;
  bagSequence: number;
  price: number;
  tax: number;
  currency: string;
}

export interface BagPolicy {
  freeBagsIncluded: number;
  maxWeightKgPerBag: number;
  offers: BagOffer[];
}

// ── Seatmap types ─────────────────────────────────────────────────────────────

export interface SeatOffer {
  seatOfferId: string;
  seatNumber: string;
  column: string;
  rowNumber: number;
  position: string;
  cabinCode: string;
  price: number;
  tax: number;
  currency: string;
  availability: 'available' | 'held' | 'sold';
  attributes: string[];
}

export interface CabinSeatmap {
  cabinCode: string;
  cabinName: string;
  columns: string[];
  layout: string;
  startRow: number;
  endRow: number;
  seats: SeatOffer[];
}

export interface DcsSeatmap {
  flightId: string;
  flightNumber: string;
  aircraftType: string;
  cabins: CabinSeatmap[];
}

// ── Basket selection types ────────────────────────────────────────────────────

export interface DcsBagSelection {
  passengerId: string;
  segmentId: string;
  basketItemRef: string;
  bagOfferId: string;
  additionalBags: number;
  price: number;
  tax: number;
  currency: string;
}

export interface DcsSeatSelection {
  passengerId: string;
  segmentId: string;
  basketItemRef: string;
  seatOfferId: string;
  seatNumber: string;
  seatPosition: string;
  price: number;
  tax: number;
  currency: string;
}

// ── Payment summary ───────────────────────────────────────────────────────────

export interface DcsPaymentSummary {
  basketId: string;
  currency: string;
  totals: {
    bags: number;
    seats: number;
    grandTotal: number;
  };
}

// ── Basket confirm ────────────────────────────────────────────────────────────

export interface DcsConfirmResult {
  bookingReference: string;
  totalAmount: number;
  currency: string;
  paymentReference?: string;
}

// ── Check-in result ───────────────────────────────────────────────────────────

export interface DcsCheckInResult {
  bookingReference: string;
  checkedIn: string[];
}

// ── Boarding card ─────────────────────────────────────────────────────────────

export interface DcsBoardingCard {
  ticketNumber: string;
  passengerId: string;
  flightNumber: string;
  departureDate: string;
  seatNumber: string;
  cabinCode: string;
  sequenceNumber: string;
  origin: string;
  destination: string;
  bcbpString: string;
}

@Injectable({ providedIn: 'root' })
export class CheckInDeskService {
  #http = inject(HttpClient);
  #retailAdminUrl = `${environment.retailApiUrl}/api/v1/admin`;
  #retailUrl = `${environment.retailApiUrl}/api/v1`;
  #operationsUrl = `${environment.operationsApiUrl}/api/v1`;

  async getOrder(bookingRef: string): Promise<OrderDetail> {
    return firstValueFrom(
      this.#http.get<OrderDetail>(`${this.#retailAdminUrl}/orders/${bookingRef.toUpperCase()}`)
    );
  }

  async getBagOffers(inventoryId: string, cabinCode: string): Promise<BagPolicy> {
    return firstValueFrom(
      this.#http.get<BagPolicy>(
        `${this.#retailUrl}/bags/offers?inventoryId=${encodeURIComponent(inventoryId)}&cabinCode=${encodeURIComponent(cabinCode)}`
      )
    );
  }

  async getSeatmap(inventoryId: string, flightNumber: string, cabinCode: string): Promise<DcsSeatmap> {
    const params = `flightNumber=${encodeURIComponent(flightNumber)}&cabinCode=${encodeURIComponent(cabinCode)}`;
    return firstValueFrom(
      this.#http.get<DcsSeatmap>(`${this.#retailAdminUrl}/flights/${inventoryId}/seatmap?${params}`)
    );
  }

  async createCheckInBasket(bookingRef: string, passengerCount: number, currency: string): Promise<{ basketId: string }> {
    return firstValueFrom(
      this.#http.post<{ basketId: string }>(`${this.#retailUrl}/basket`, {
        bookingReference: bookingRef.toUpperCase(),
        segments: [],
        currency,
        bookingType: 'Revenue',
        basketType: 'CheckIn',
        passengerCount,
      })
    );
  }

  async updateBasketBags(basketId: string, bags: DcsBagSelection[]): Promise<void> {
    await firstValueFrom(
      this.#http.put(`${this.#retailUrl}/basket/${basketId}/bags`, bags)
    );
  }

  async updateBasketSeats(basketId: string, seats: DcsSeatSelection[]): Promise<void> {
    await firstValueFrom(
      this.#http.put(`${this.#retailUrl}/basket/${basketId}/seats`, seats)
    );
  }

  async getPaymentSummary(basketId: string): Promise<DcsPaymentSummary> {
    return firstValueFrom(
      this.#http.get<DcsPaymentSummary>(`${this.#retailUrl}/basket/${basketId}/payment-summary`)
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
    }
  ): Promise<DcsConfirmResult> {
    return firstValueFrom(
      this.#http.post<DcsConfirmResult>(`${this.#retailUrl}/basket/${basketId}/confirm`, {
        channelCode: 'DCS',
        payment,
        bookingType: 'Revenue',
      })
    );
  }

  async submitCheckIn(bookingRef: string, departureAirport: string): Promise<DcsCheckInResult> {
    return firstValueFrom(
      this.#http.post<DcsCheckInResult>(`${this.#operationsUrl}/oci/checkin`, {
        bookingReference: bookingRef.toUpperCase(),
        departureAirport: departureAirport.toUpperCase(),
      })
    );
  }

  async getBoardingDocs(ticketNumbers: string[], departureAirport: string): Promise<DcsBoardingCard[]> {
    const res = await firstValueFrom(
      this.#http.post<{ boardingCards: DcsBoardingCard[] }>(`${this.#operationsUrl}/oci/boarding-docs`, {
        departureAirport: departureAirport.toUpperCase(),
        ticketNumbers,
      })
    );
    return res.boardingCards ?? [];
  }
}
