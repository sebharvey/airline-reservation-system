/**
 * BookingStateService
 *
 * Manages client-side basket and booking flow state using Angular signals.
 * Stores selected offers, passenger details, ancillary selections, and
 * the confirmed order reference across the multi-step booking flow.
 *
 * In a production app this would integrate with session storage / server-side
 * basket management via the Retail API. The shape mirrors the BasketData JSON
 * from the Order domain.
 */

import { Injectable, signal, computed } from '@angular/core';
import { FlightOffer } from '../models/flight.model';
import { Basket, BasketFlightOffer, BasketSeatSelection, BasketBagSelection, Passenger, Order } from '../models/order.model';

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
    taxes: offer.taxes,
    totalPrice: offer.totalPrice,
    isRefundable: offer.isRefundable,
    isChangeable: offer.isChangeable,
    currency: offer.currency
  };
}

@Injectable({ providedIn: 'root' })
export class BookingStateService {
  private readonly _basket = signal<Basket | null>(null);
  private readonly _confirmedOrder = signal<Order | null>(null);
  private readonly _adultCount = signal<number>(1);
  private readonly _childCount = signal<number>(0);
  private readonly _searchParams = signal<{ origin: string; destination: string; departDate: string; returnDate?: string; tripType: string } | null>(null);

  readonly basket = this._basket.asReadonly();
  readonly confirmedOrder = this._confirmedOrder.asReadonly();
  readonly adultCount = this._adultCount.asReadonly();
  readonly childCount = this._childCount.asReadonly();
  readonly searchParams = this._searchParams.asReadonly();

  readonly totalPassengers = computed(() => this._adultCount() + this._childCount());

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

  /** Start a new basket with the selected outbound (and optional inbound) flight offer. */
  startBasket(outboundOffer: FlightOffer, inboundOffer: FlightOffer | null): void {
    const paxCount = this._adultCount() + this._childCount();
    const passengerRefs = Array.from({ length: paxCount }, (_, i) => `PAX-${i + 1}`);

    const flightOffers: BasketFlightOffer[] = [
      buildBasketFlightOffer(outboundOffer, passengerRefs)
    ];
    if (inboundOffer) {
      flightOffers.push(buildBasketFlightOffer(inboundOffer, passengerRefs));
    }

    const totalFare = flightOffers.reduce((sum, o) => sum + o.totalPrice, 0);
    const ttl = new Date();
    ttl.setHours(ttl.getHours() + TTL_HOURS);

    this._basket.set({
      basketId: uuid(),
      flightOffers,
      passengers: [],
      seatSelections: [],
      bagSelections: [],
      totalFareAmount: totalFare,
      totalSeatAmount: 0,
      totalBagAmount: 0,
      totalAmount: totalFare,
      currency: outboundOffer.currency,
      ticketingTimeLimit: ttl.toISOString()
    });
    this._confirmedOrder.set(null);
  }

  /** Save passenger details to the basket. */
  setPassengers(passengers: Passenger[]): void {
    this._basket.update(b => b ? { ...b, passengers } : b);
  }

  /** Save seat selections and recalculate total. */
  setSeatSelections(selections: BasketSeatSelection[]): void {
    this._basket.update(b => {
      if (!b) return b;
      const totalSeats = selections.reduce((sum, s) => sum + s.price, 0);
      return {
        ...b,
        seatSelections: selections,
        totalSeatAmount: totalSeats,
        totalAmount: b.totalFareAmount + totalSeats + b.totalBagAmount
      };
    });
  }

  /** Save bag selections and recalculate total. */
  setBagSelections(selections: BasketBagSelection[]): void {
    this._basket.update(b => {
      if (!b) return b;
      const totalBags = selections.reduce((sum, s) => sum + s.price, 0);
      return {
        ...b,
        bagSelections: selections,
        totalBagAmount: totalBags,
        totalAmount: b.totalFareAmount + b.totalSeatAmount + totalBags
      };
    });
  }

  /** Store the confirmed order after successful payment. */
  confirmOrder(order: Order): void {
    this._confirmedOrder.set(order);
    this._basket.set(null); // Basket is deleted on confirmation
  }

  clearBasket(): void {
    this._basket.set(null);
  }

  clearAll(): void {
    this._basket.set(null);
    this._confirmedOrder.set(null);
  }
}
