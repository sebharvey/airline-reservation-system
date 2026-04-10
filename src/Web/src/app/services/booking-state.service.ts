/**
 * BookingStateService
 *
 * Manages client-side basket and booking flow state using Angular signals.
 * Stores selected offers, passenger details, ancillary selections, and
 * the confirmed order reference across the multi-step booking flow.
 *
 * Supports both revenue (money) and reward (points) booking types.
 *
 * In a production app this would integrate with session storage / server-side
 * basket management via the Retail API. The shape mirrors the BasketData JSON
 * from the Order domain.
 */

import { Injectable, signal, computed } from '@angular/core';
import { FlightOffer } from '../models/flight.model';
import { Basket, BasketFlightOffer, BasketSeatSelection, BasketBagSelection, BasketSsrSelection, Passenger, Order, BookingType } from '../models/order.model';

const BASKET_ID_KEY = 'apex_basket_id';

function uuid(): string {
  return 'bsk-' + Math.random().toString(36).slice(2, 10);
}

const TTL_HOURS = 24;

function buildBasketFlightOffer(offer: FlightOffer, passengerRefs: string[]): BasketFlightOffer {
  return {
    basketItemId: 'BI-' + Math.random().toString(36).slice(2, 6),
    offerId: offer.offerId,
    inventoryId: offer.inventoryId,
    flightNumber: offer.flightNumber,
    origin: offer.origin,
    destination: offer.destination,
    departureDateTime: offer.departureDateTime,
    arrivalDateTime: offer.arrivalDateTime,
    aircraftType: offer.aircraftType,
    cabinCode: offer.cabinCode,
    cabinName: offer.cabinName,
    fareFamily: offer.fareFamily,
    fareBasisCode: offer.fareBasisCode,
    passengerRefs,
    unitPrice: offer.unitPrice,
    taxes: offer.taxes * passengerRefs.length,
    totalPrice: offer.totalPrice * passengerRefs.length,
    isRefundable: offer.isRefundable,
    isChangeable: offer.isChangeable,
    currency: offer.currency,
    pointsPrice: offer.pointsPrice != null ? offer.pointsPrice * passengerRefs.length : offer.pointsPrice,
    pointsTaxes: offer.pointsTaxes != null ? offer.pointsTaxes * passengerRefs.length : offer.pointsTaxes
  };
}

@Injectable({ providedIn: 'root' })
export class BookingStateService {
  private readonly _basket = signal<Basket | null>(null);
  private readonly _confirmedOrder = signal<Order | null>(null);
  private readonly _adultCount = signal<number>(1);
  private readonly _childCount = signal<number>(0);
  private readonly _searchParams = signal<{ origin: string; destination: string; departDate: string; returnDate?: string; tripType: string } | null>(null);
  private readonly _bookingType = signal<BookingType>('Revenue');

  readonly basket = this._basket.asReadonly();
  readonly confirmedOrder = this._confirmedOrder.asReadonly();
  readonly adultCount = this._adultCount.asReadonly();
  readonly childCount = this._childCount.asReadonly();
  readonly searchParams = this._searchParams.asReadonly();
  readonly bookingType = this._bookingType.asReadonly();

  readonly isRewardBooking = computed(() => this._bookingType() === 'Reward');

  readonly totalPassengers = computed(() => this._adultCount() + this._childCount());

  setBookingType(type: BookingType): void {
    this._bookingType.set(type);
  }

  setSearchParams(params: { origin: string; destination: string; departDate: string; returnDate?: string; tripType: string; adults: number; children: number }): void {
    this._adultCount.set(params.adults);
    this._childCount.set(params.children);
    this._searchParams.set({
      origin: params.origin,
      destination: params.destination,
      departDate: params.departDate,
      returnDate: params.returnDate,
      tripType: params.tripType
    });
  }

  /** Start a new basket with the selected flight offers (one per segment).
   *  @param apiBasketId  The basketId returned by POST /v1/basket on the Retail API.
   *                      Falls back to a locally-generated ID if not provided.
   */
  startBasket(offers: FlightOffer[], apiBasketId?: string): void {
    const paxCount = this._adultCount() + this._childCount();
    const passengerRefs = Array.from({ length: paxCount }, (_, i) => `PAX-${i + 1}`);
    const bookingType = this._bookingType();

    const flightOffers: BasketFlightOffer[] = offers.map(o => buildBasketFlightOffer(o, passengerRefs));

    const totalFare = flightOffers.reduce((sum, o) => sum + o.totalPrice, 0);
    const totalPoints = flightOffers.reduce((sum, o) => sum + (o.pointsPrice ?? 0), 0);
    const totalTaxes = flightOffers.reduce((sum, o) => sum + (o.pointsTaxes ?? o.taxes), 0);
    const ttl = new Date();
    ttl.setHours(ttl.getHours() + TTL_HOURS);

    const basketId = apiBasketId ?? uuid();
    localStorage.setItem(BASKET_ID_KEY, basketId);
    this._basket.set({
      basketId,
      bookingType,
      flightOffers,
      passengers: [],
      seatSelections: [],
      bagSelections: [],
      ssrSelections: [],
      totalFareAmount: bookingType === 'Reward' ? 0 : totalFare,
      totalPointsAmount: bookingType === 'Reward' ? totalPoints : 0,
      totalTaxesAmount: bookingType === 'Reward' ? totalTaxes : 0,
      totalSeatAmount: 0,
      totalBagAmount: 0,
      totalAmount: bookingType === 'Reward' ? totalTaxes : totalFare,
      currency: offers[0].currency,
      ticketingTimeLimit: ttl.toISOString()
    });
    this._confirmedOrder.set(null);
  }

  /** Save passenger details to the basket. */
  setPassengers(passengers: Passenger[]): void {
    this._basket.update(b => b ? { ...b, passengers } : b);
  }

  /** Set loyalty number on basket (for reward bookings). */
  setLoyaltyNumber(loyaltyNumber: string): void {
    this._basket.update(b => b ? { ...b, loyaltyNumber } : b);
  }

  /** Save seat selections and recalculate total. */
  setSeatSelections(selections: BasketSeatSelection[]): void {
    this._basket.update(b => {
      if (!b) return b;
      const totalSeats = selections.reduce((sum, s) => sum + s.price, 0);
      const base = b.bookingType === 'Reward' ? b.totalTaxesAmount : b.totalFareAmount;
      return {
        ...b,
        seatSelections: selections,
        totalSeatAmount: totalSeats,
        totalAmount: base + totalSeats + b.totalBagAmount
      };
    });
  }

  /** Save SSR selections to the basket (no charge — total unchanged). */
  setSsrSelections(selections: BasketSsrSelection[]): void {
    this._basket.update(b => b ? { ...b, ssrSelections: selections } : b);
  }

  /** Save bag selections and recalculate total. */
  setBagSelections(selections: BasketBagSelection[]): void {
    this._basket.update(b => {
      if (!b) return b;
      const totalBags = selections.reduce((sum, s) => sum + s.price, 0);
      const base = b.bookingType === 'Reward' ? b.totalTaxesAmount : b.totalFareAmount;
      return {
        ...b,
        bagSelections: selections,
        totalBagAmount: totalBags,
        totalAmount: base + b.totalSeatAmount + totalBags
      };
    });
  }

  /** Store the confirmed order after successful payment. */
  confirmOrder(order: Order): void {
    this._confirmedOrder.set(order);
    this._basket.set(null);
    localStorage.removeItem(BASKET_ID_KEY);
  }

  clearBasket(): void {
    this._basket.set(null);
    localStorage.removeItem(BASKET_ID_KEY);
  }

  clearAll(): void {
    this._basket.set(null);
    this._confirmedOrder.set(null);
    this._bookingType.set('Revenue');
    localStorage.removeItem(BASKET_ID_KEY);
  }
}
