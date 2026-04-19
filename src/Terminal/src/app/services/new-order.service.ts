import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface SearchOffer {
  offerId: string;
  sessionId: string;
  flightNumber: string;
  departureDate: string;
  departureTime: string;
  arrivalTime: string;
  arrivalDayOffset: number;
  durationMinutes: number;
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

interface ApiFareOffer {
  offerId: string;
  fareBasisCode: string;
  basePrice: number;
  tax: number;
  totalPrice: number;
  currency: string;
  isRefundable: boolean;
  isChangeable: boolean;
}

interface ApiFareFamilyOffer {
  fareFamily: string;
  offer: ApiFareOffer;
}

interface ApiCabin {
  cabinCode: string;
  availableSeats: number;
  fromPrice: number;
  currency: string;
  fromPoints: number | null;
  fareFamilies: ApiFareFamilyOffer[];
}

interface ApiLeg {
  sessionId: string;
  inventoryId: string;
  flightNumber: string;
  origin: string;
  destination: string;
  departureDate: string;
  departureTime: string;
  arrivalTime: string;
  arrivalDayOffset: number;
  durationMinutes: number;
  aircraftType: string;
  cabins: ApiCabin[];
}

interface ApiItinerary {
  legs: ApiLeg[];
  connectionDurationMinutes: number | null;
  combinedFromPrice: number;
  currency: string;
}

interface ApiSliceSearchResponse {
  itineraries: ApiItinerary[];
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
  inventoryId: string | null;
  aircraftType: string | null;
  basketItemId: string;
  flightNumber: string;
  origin: string;
  destination: string;
  departureDateTime: string;
  arrivalDateTime: string;
  cabinCode: string;
  fareFamily: string | null;
  fareAmount: number;
  taxAmount: number;
  totalAmount: number;
}

export interface BasketSummary {
  basketId: string;
  status: string;
  totalFareAmount: number | null;
  totalSeatAmount: number;
  totalBagAmount: number;
  totalProductAmount: number;
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

// ── Products ─────────────────────────────────────────────────────────────────

export interface ProductPrice {
  priceId: string;
  offerId: string;
  currencyCode: string;
  price: number;
  tax: number;
}

export interface Product {
  productId: string;
  name: string;
  description: string;
  imageBase64: string | null;
  ssrCode: string | null;
  isSegmentSpecific: boolean;
  prices: ProductPrice[];
}

export interface ProductGroup {
  productGroupId: string;
  productGroupName: string;
  products: Product[];
}

export interface ProductCatalogue {
  productGroups: ProductGroup[];
}

export interface BasketProductSelection {
  productId: string;
  offerId: string;
  name: string;
  passengerId: string | null;
  segmentId: string | null;
  quantity: number;
  unitPrice: number;
  tax: number;
  price: number;
  currencyCode: string;
}

// ── Seat selection ───────────────────────────────────────────────────────────

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

export interface Seatmap {
  flightId: string;
  flightNumber: string;
  aircraftType: string;
  cabins: CabinSeatmap[];
}

export interface BasketSeatSelection {
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
    bookingType: 'Revenue' | 'Standby' = 'Revenue',
  ): Promise<SearchResponse> {
    const res = await firstValueFrom(
      this.#http.post<ApiSliceSearchResponse>(`${this.#retailUrl}/search/slice`, {
        origin: origin.toUpperCase(),
        destination: destination.toUpperCase(),
        departureDate,
        paxCount,
        bookingType,
      })
    );

    const offers: SearchOffer[] = [];
    for (const itinerary of res.itineraries ?? []) {
      for (const leg of itinerary.legs ?? []) {
        for (const cabin of leg.cabins ?? []) {
          for (const ff of cabin.fareFamilies ?? []) {
            offers.push({
              offerId: ff.offer.offerId,
              sessionId: leg.sessionId,
              flightNumber: leg.flightNumber,
              departureDate: leg.departureDate,
              departureTime: leg.departureTime,
              arrivalTime: leg.arrivalTime,
              arrivalDayOffset: leg.arrivalDayOffset,
              durationMinutes: leg.durationMinutes,
              origin: leg.origin,
              destination: leg.destination,
              aircraftType: leg.aircraftType,
              cabinCode: cabin.cabinCode,
              seatsAvailable: cabin.availableSeats,
              fareFamily: ff.fareFamily,
              fareBasisCode: ff.offer.fareBasisCode,
              currencyCode: ff.offer.currency,
              baseFareAmount: ff.offer.basePrice,
              taxAmount: ff.offer.tax,
              totalAmount: ff.offer.totalPrice,
              isRefundable: ff.offer.isRefundable,
              isChangeable: ff.offer.isChangeable,
            });
          }
        }
      }
    }
    return { offers };
  }

  async createBasket(segments: { offerId: string; sessionId: string }[], passengerCount: number, bookingType: 'Revenue' | 'Standby' = 'Revenue'): Promise<BasketSummary> {
    return firstValueFrom(
      this.#http.post<BasketSummary>(`${this.#retailUrl}/basket`, {
        segments: segments.map(s => ({ offerId: s.offerId, sessionId: s.sessionId })),
        passengerCount,
        currency: 'GBP',
        bookingType,
        loyaltyNumber: null,
      })
    );
  }

  async updatePassengers(basketId: string, passengers: BasketPassenger[]): Promise<void> {
    await firstValueFrom(
      this.#http.put(`${this.#retailUrl}/basket/${basketId}/passengers`, passengers)
    );
  }

  async getProducts(): Promise<ProductCatalogue> {
    return firstValueFrom(
      this.#http.get<ProductCatalogue>(`${this.#retailUrl}/products`)
    );
  }

  async updateProducts(basketId: string, selections: BasketProductSelection[]): Promise<BasketSummary> {
    return firstValueFrom(
      this.#http.put<BasketSummary>(`${this.#retailUrl}/basket/${basketId}/products`, selections)
    );
  }

  async getSeatmap(inventoryId: string, flightNumber: string, aircraftType: string, cabinCode: string): Promise<Seatmap> {
    const params = `aircraftType=${encodeURIComponent(aircraftType)}&flightNumber=${encodeURIComponent(flightNumber)}&cabinCode=${encodeURIComponent(cabinCode)}`;
    return firstValueFrom(
      this.#http.get<Seatmap>(`${this.#retailUrl}/flights/${inventoryId}/seatmap?${params}`)
    );
  }

  async updateSeats(basketId: string, seatSelections: BasketSeatSelection[]): Promise<SeatUpdateResponse> {
    return firstValueFrom(
      this.#http.put<SeatUpdateResponse>(`${this.#retailUrl}/basket/${basketId}/seats`, seatSelections)
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
    bookingType: 'Revenue' | 'Standby' = 'Revenue',
  ): Promise<ConfirmResponse> {
    return firstValueFrom(
      this.#http.post<ConfirmResponse>(`${this.#retailUrl}/basket/${basketId}/confirm`, {
        channelCode: 'CC',
        payment,
        bookingType,
      })
    );
  }
}
