/**
 * RetailApiService
 *
 * Channel-facing service for all flight retailing operations.
 * Calls the live Retail API for flight search and basket creation.
 * Other methods (seatmap, bags, orders, check-in) retain mocks pending
 * those API endpoints being wired up.
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
import { map, delay, catchError } from 'rxjs/operators';
import { environment } from '../environments/environment';
import { FlightOffer, Seatmap, BagPolicyResponse, FlightStatus, CabinCode } from '../models/flight.model';
import { Order, BoardingPass, BookingType, Passenger, BasketSeatSelection, BasketBagSelection } from '../models/order.model';
import { MOCK_ORDERS } from '../data/mock/orders.mock';
import { MOCK_BAG_POLICIES } from '../data/mock/bag-policy.mock';
import { MOCK_FLIGHT_STATUS } from '../data/mock/flight-offers.mock';

export interface SearchSliceParams {
  origin: string;
  destination: string;
  departureDate: string;
  adults: number;
  children: number;
  bookingType?: BookingType;
}

export interface BasketSegment {
  offerId: string;
  sessionId: string;
}

export interface CreateBasketParams {
  segments: BasketSegment[];
  bookingType: BookingType;
  currencyCode?: string;
  loyaltyNumber?: string;
}

export interface CreateBasketResponse {
  basketId: string;
}

export interface IssuedETicket {
  passengerId: string;
  segmentId: string;
  eTicketNumber: string;
}

export interface ConfirmBasketResponse {
  bookingReference: string;
  status: string;
  totalPrice: number;
  currency: string;
  bookedAt: string;
  eTickets: IssuedETicket[];
}

export interface RetrieveOrderParams {
  bookingReference: string;
  givenName: string;
  surname: string;
}

// ---- Live API response shape from POST /api/v1/search/slice ----

interface SearchSliceApiOffer {
  offerId: string;
  fareBasisCode: string;
  basePrice: number;
  tax: number;
  totalPrice: number;
  currency: string;
  isRefundable: boolean;
  isChangeable: boolean;
}

interface SearchSliceApiFareFamily {
  fareFamily: string;
  offer: SearchSliceApiOffer;
}

interface SearchSliceApiCabin {
  cabinCode: string;
  availableSeats: number;
  fromPrice: number;
  currency: string;
  fromPoints: number | null;
  fareFamilies: SearchSliceApiFareFamily[];
}

interface SearchSliceApiFlight {
  flightNumber: string;
  origin: string;
  destination: string;
  departureTime: string;
  arrivalTime: string;
  aircraftType: string;
  cabins: SearchSliceApiCabin[];
}

interface SearchSliceApiResponse {
  sessionId: string;
  flights: SearchSliceApiFlight[];
}

// ----------------------------------------------------------------

const CABIN_NAMES: Record<string, string> = {
  F: 'First Class',
  J: 'Business Class',
  W: 'Premium Economy',
  Y: 'Economy'
};

const API_DELAY_MS = 600;

export interface SearchSliceResult {
  sessionId: string;
  offers: FlightOffer[];
}

function mapApiResponseToResult(response: SearchSliceApiResponse): SearchSliceResult {
  const offers: FlightOffer[] = [];
  for (const flight of response.flights) {
    for (const cabin of flight.cabins) {
      const cabinCode = cabin.cabinCode as CabinCode;
      for (const family of cabin.fareFamilies) {
        offers.push({
          offerId: family.offer.offerId,
          inventoryId: family.offer.offerId,
          flightNumber: flight.flightNumber,
          origin: flight.origin,
          destination: flight.destination,
          departureDateTime: flight.departureTime,
          arrivalDateTime: flight.arrivalTime,
          aircraftType: flight.aircraftType,
          cabinCode,
          cabinName: CABIN_NAMES[cabin.cabinCode] ?? cabin.cabinCode,
          fareFamily: family.fareFamily,
          fareBasisCode: family.offer.fareBasisCode,
          bookingClass: family.offer.fareBasisCode.charAt(0),
          isRefundable: family.offer.isRefundable,
          isChangeable: family.offer.isChangeable,
          unitPrice: family.offer.basePrice,
          taxes: family.offer.tax,
          totalPrice: family.offer.totalPrice,
          currency: family.offer.currency,
          seatsAvailable: cabin.availableSeats,
          pointsPrice: cabin.fromPoints ?? undefined,
          pointsTaxes: undefined
        });
      }
    }
  }
  return { sessionId: response.sessionId, offers };
}

@Injectable({ providedIn: 'root' })
export class RetailApiService {
  readonly #http = inject(HttpClient);

  /**
   * DEBUG — GET /v1/basket/{basketId}
   * Fetch the current basket from the Retail API for debugging the bookflow.
   * Remove this method along with the basket debug modal feature.
   */
  getBasket(basketId: string): Observable<unknown> {
    const base = environment.retailApiBaseUrl;
    return this.#http.get<unknown>(`${base}/api/v1/basket/${basketId}`);
  }

  /**
   * POST /v1/search/slice
   * Search for available flights for a single directional slice.
   */
  searchSlice(params: SearchSliceParams): Observable<SearchSliceResult> {
    const base = environment.retailApiBaseUrl;
    const body = {
      origin: params.origin,
      destination: params.destination,
      departureDate: params.departureDate,
      paxCount: params.adults + (params.children ?? 0),
      bookingType: params.bookingType ?? 'Revenue'
    };
    return this.#http
      .post<SearchSliceApiResponse>(`${base}/api/v1/search/slice`, body)
      .pipe(map(mapApiResponseToResult));
  }

  /**
   * POST /v1/basket
   * Create a basket with the selected offer IDs. Returns the server-assigned basketId.
   */
  createBasket(params: CreateBasketParams): Observable<CreateBasketResponse> {
    const base = environment.retailApiBaseUrl;
    const body = {
      segments: params.segments,
      channelCode: 'WEB',
      currencyCode: params.currencyCode ?? 'GBP',
      bookingType: params.bookingType,
      loyaltyNumber: params.loyaltyNumber ?? null
    };
    return this.#http.post<CreateBasketResponse>(`${base}/api/v1/basket`, body);
  }

  /**
   * PUT /v1/basket/{basketId}/passengers
   * Store passenger details in the basket.
   */
  updateBasketPassengers(basketId: string, passengers: Passenger[]): Observable<void> {
    const base = environment.retailApiBaseUrl;
    return this.#http.put<void>(`${base}/api/v1/basket/${basketId}/passengers`, passengers);
  }

  /**
   * PUT /v1/basket/{basketId}/seats
   * Store seat selections in the basket.
   */
  updateBasketSeats(basketId: string, seatSelections: BasketSeatSelection[]): Observable<void> {
    const base = environment.retailApiBaseUrl;
    return this.#http.put<void>(`${base}/api/v1/basket/${basketId}/seats`, seatSelections);
  }

  /**
   * PUT /v1/basket/{basketId}/bags
   * Store bag selections in the basket.
   */
  updateBasketBags(basketId: string, bagSelections: BasketBagSelection[]): Observable<void> {
    const base = environment.retailApiBaseUrl;
    return this.#http.put<void>(`${base}/api/v1/basket/${basketId}/bags`, bagSelections);
  }

  /**
   * POST /v1/basket/{basketId}/confirm
   * Confirm the basket — processes payment, creates order, issues e-tickets.
   */
  confirmBasket(
    basketId: string,
    paymentMethod: string,
    cardNumber: string,
    expiryDate: string,
    cvv: string,
    cardholderName: string,
    loyaltyPointsToRedeem?: number
  ): Observable<ConfirmBasketResponse> {
    const base = environment.retailApiBaseUrl;
    const body = {
      payment: { method: paymentMethod, cardNumber, expiryDate, cvv, cardholderName },
      loyaltyPointsToRedeem: loyaltyPointsToRedeem ?? null
    };
    return this.#http.post<ConfirmBasketResponse>(`${base}/api/v1/basket/${basketId}/confirm`, body);
  }

  /**
   * GET /v1/flights/{flightId}/seatmap
   * Retrieve seatmap with pricing and availability for a flight.
   */
  getFlightSeatmap(flightId: string, flightNumber: string, aircraftType: string): Observable<Seatmap> {
    const base = environment.retailApiBaseUrl;
    const params = `aircraftType=${encodeURIComponent(aircraftType)}&flightNumber=${encodeURIComponent(flightNumber)}`;
    return this.#http.get<Seatmap>(`${base}/api/v1/flights/${flightId}/seatmap?${params}`);
  }

  /**
   * GET /v1/bags/offers?inventoryId=&cabinCode=
   * Retrieve bag policy and priced bag offers for a flight and cabin.
   */
  getBagOffers(inventoryId: string, cabinCode: CabinCode): Observable<BagPolicyResponse> {
    const response = MOCK_BAG_POLICIES[cabinCode] ?? MOCK_BAG_POLICIES['Y'];
    // Refresh bagOfferIds to simulate snapshot generation
    const freshOffers = {
      ...response,
      additionalBagOffers: response.additionalBagOffers.map(o => ({
        ...o,
        bagOfferId: `${o.bagOfferId}-${Date.now()}`
      }))
    };
    return of(freshOffers).pipe(delay(API_DELAY_MS));
  }

  /**
   * POST /v1/orders/retrieve
   * Retrieve a confirmed order by booking reference and passenger name.
   */
  retrieveOrder(params: RetrieveOrderParams): Observable<Order> {
    const base = environment.retailApiBaseUrl;
    const body = {
      bookingReference: params.bookingReference.toUpperCase().trim(),
      givenName: params.givenName.trim(),
      surname: params.surname.trim()
    };
    return this.#http.post<Order>(`${base}/api/v1/orders/retrieve`, body).pipe(
      catchError((err: HttpErrorResponse) => {
        const message = err.status === 404
          ? 'Booking not found. Please check your reference and name.'
          : 'Unable to retrieve booking. Please try again.';
        return throwError(() => ({ status: err.status, message }));
      })
    );
  }

  /**
   * POST /v1/checkin/retrieve
   * Retrieve booking for online check-in.
   */
  retrieveForCheckIn(params: RetrieveOrderParams): Observable<Order> {
    return this.retrieveOrder(params);
  }

  /**
   * POST /v1/checkin/{bookingRef}
   * Submit check-in and generate boarding cards.
   */
  submitCheckIn(bookingRef: string, passengerIds: string[]): Observable<BoardingPass[]> {
    const order = MOCK_ORDERS[bookingRef.toUpperCase()];
    if (!order) {
      return throwError(() => ({ status: 404, message: 'Booking not found' })).pipe(delay(API_DELAY_MS));
    }

    const boardingPasses: BoardingPass[] = [];
    order.passengers
      .filter(p => passengerIds.includes(p.passengerId))
      .forEach(pax => {
        order.flightSegments.forEach(seg => {
          const flightItem = order.orderItems.find(
            oi => oi.type === 'Flight' && oi.segmentRef === seg.segmentId && oi.passengerRefs.includes(pax.passengerId)
          );
          const eTicket = flightItem?.eTickets?.find(t => t.passengerId === pax.passengerId);
          const seatAssignment = flightItem?.seatAssignments?.find(s => s.passengerId === pax.passengerId);

          const seqNum = String(boardingPasses.length + 1).padStart(4, '0');
          boardingPasses.push({
            bookingReference: order.bookingReference,
            passengerId: pax.passengerId,
            givenName: pax.givenName,
            surname: pax.surname,
            flightNumber: seg.flightNumber,
            origin: seg.origin,
            destination: seg.destination,
            departureDateTime: seg.departureDateTime,
            seatNumber: seatAssignment?.seatNumber ?? 'TBA',
            cabinCode: seg.cabinCode,
            eTicketNumber: eTicket?.eTicketNumber ?? 'N/A',
            sequenceNumber: seqNum,
            bcbpBarcode: buildBcbp(pax.surname, pax.givenName, order.bookingReference, seg, seatAssignment?.seatNumber ?? '001A', seqNum),
            gate: 'B45',
            boardingTime: getBoardingTime(seg.departureDateTime)
          });
        });
      });

    return of(boardingPasses).pipe(delay(API_DELAY_MS));
  }

  /**
   * GET /v1/flights/{flightNumber}/status
   * Get real-time flight status.
   */
  getFlightStatus(flightNumber: string): Observable<FlightStatus | null> {
    const status = MOCK_FLIGHT_STATUS[flightNumber.toUpperCase()] ?? null;
    return of(status).pipe(delay(API_DELAY_MS));
  }

  /**
   * PATCH /v1/orders/{bookingRef}/seats  (post-sale seat change)
   * PATCH /v1/checkin/{bookingRef}/seats (check-in seat selection - no charge)
   */
  updateSeats(_bookingRef: string, _seatSelections: { passengerId: string; segmentId: string; seatNumber: string }[]): Observable<{ success: boolean }> {
    return of({ success: true }).pipe(delay(API_DELAY_MS));
  }

  /**
   * POST /v1/checkin/{bookingRef}/ancillaries
   * Purchase additional bags and/or seats during online check-in.
   */
  addCheckInAncillaries(
    _bookingRef: string,
    _bags: { passengerId: string; segmentId: string; additionalBags: number; bagOfferId: string; price: number }[],
    _seats: { passengerId: string; segmentId: string; seatNumber: string; seatPrice: number }[],
    _cardLast4: string,
    _cardType: string
  ): Observable<{ success: boolean; paymentReference: string }> {
    return of({ success: true, paymentReference: 'AXPAY-CI-' + Date.now() }).pipe(delay(API_DELAY_MS));
  }

  /**
   * POST /v1/orders/{bookingRef}/bags
   * Purchase additional bags post-sale via Manage Booking.
   */
  addManageBookingBags(
    _bookingRef: string,
    _bags: { passengerId: string; segmentId: string; additionalBags: number; bagOfferId: string; price: number }[],
    _cardLast4: string,
    _cardType: string
  ): Observable<{ success: boolean; paymentReference: string }> {
    return of({ success: true, paymentReference: 'AXPAY-MB-' + Date.now() }).pipe(delay(API_DELAY_MS));
  }

  /**
   * POST /v1/orders/{bookingRef}/cancel
   * Cancel a confirmed booking.
   */
  cancelOrder(_bookingRef: string, _reason: string): Observable<{ success: boolean; refundAmount: number; currency: string }> {
    return of({ success: true, refundAmount: 4431.00, currency: 'GBP' }).pipe(delay(API_DELAY_MS));
  }

  /**
   * POST /v1/orders/{bookingRef}/change
   * Change a confirmed flight.
   */
  changeOrder(_bookingRef: string, _newOfferId: string): Observable<{ success: boolean; addCollect: number; currency: string }> {
    return of({ success: true, addCollect: 150.00, currency: 'GBP' }).pipe(delay(API_DELAY_MS));
  }

  /**
   * POST /v1/reward/authorise
   * Authorise a points redemption for a reward booking.
   */
  authorisePointsRedemption(
    _loyaltyNumber: string,
    _pointsAmount: number
  ): Observable<{ success: boolean; redemptionReference: string }> {
    return of({ success: true, redemptionReference: crypto.randomUUID() }).pipe(delay(API_DELAY_MS));
  }

  /**
   * POST /v1/reward/{redemptionReference}/settle
   * Settle a previously authorised points redemption after order confirmation.
   */
  settlePointsRedemption(
    _redemptionReference: string
  ): Observable<{ success: boolean }> {
    return of({ success: true }).pipe(delay(API_DELAY_MS));
  }
}

function buildBcbp(surname: string, givenName: string, bookingRef: string, seg: { flightNumber: string; origin: string; destination: string }, seatNumber: string, seq: string): string {
  const name = `${surname}/${givenName}`.substring(0, 20).padEnd(20);
  const fn = seg.flightNumber.replace('AX', '').padStart(4, '0');
  const seat = seatNumber.padStart(4, '0');
  return `M1${name}E${bookingRef} ${seg.origin}${seg.destination}AX ${fn} ${new Date().toISOString().split('T')[0]}J${seat}${seq}1`;
}

function getBoardingTime(departureDateTime: string): string {
  const d = new Date(departureDateTime);
  d.setMinutes(d.getMinutes() - 30);
  return d.toISOString();
}
