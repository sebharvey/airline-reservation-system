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
import { FlightOffer, Seatmap, BagPolicyResponse, FlightSummary, FlightStatus, ScheduledFlightNumber, CabinCode } from '../models/flight.model';
import { Order, OciOrder, BoardingPass, BookingType, Passenger, BasketSeatSelection, BasketBagSelection, BasketSsrSelection } from '../models/order.model';
import { MOCK_ORDERS } from '../data/mock/orders.mock';


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
   * PUT /v1/basket/{basketId}/ssrs
   * Store SSR selections in the basket (no charge).
   */
  updateBasketSsrs(basketId: string, ssrSelections: BasketSsrSelection[]): Observable<void> {
    const base = environment.retailApiBaseUrl;
    return this.#http.put<void>(`${base}/api/v1/basket/${basketId}/ssrs`, ssrSelections);
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
    const base = environment.retailApiBaseUrl;
    return this.#http.get<BagPolicyResponse>(
      `${base}/api/v1/bags/offers?inventoryId=${encodeURIComponent(inventoryId)}&cabinCode=${encodeURIComponent(cabinCode)}`
    );
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
   * POST /v1/orders/oci/retrieve
   * Retrieve a booking for the online check-in journey.
   * Returns an OCI-specific response with enriched passenger and flight segment data.
   */
  retrieveOciOrder(params: RetrieveOrderParams): Observable<OciOrder> {
    const base = environment.retailApiBaseUrl;
    const body = {
      bookingReference: params.bookingReference.toUpperCase().trim(),
      surname: params.surname.trim()
    };
    return this.#http.post<OciOrder>(`${base}/api/v1/orders/oci/retrieve`, body).pipe(
      catchError((err: HttpErrorResponse) => {
        const message = err.status === 404
          ? 'Booking not found. Please check your reference and surname.'
          : 'Unable to retrieve booking. Please try again.';
        return throwError(() => ({ status: err.status, message }));
      })
    );
  }

  /**
   * POST /v1/orders/oci/{bookingRef}/passenger-details
   * Save APIS travel document data for each passenger to the booking.
   */
  saveOciPassengerDetails(bookingRef: string, travelDocs: { passengerId: string; type: string; number: string; issuingCountry: string; issueDate: string; expiryDate: string; nationality: string }[]): Observable<void> {
    const base = environment.retailApiBaseUrl;
    const body = {
      passengers: travelDocs.map(d => ({
        passengerId: d.passengerId,
        travelDocument: {
          type: d.type,
          number: d.number,
          issuingCountry: d.issuingCountry,
          nationality: d.nationality,
          issueDate: d.issueDate,
          expiryDate: d.expiryDate
        }
      }))
    };
    return this.#http.post<void>(`${base}/api/v1/orders/oci/${encodeURIComponent(bookingRef)}/passenger-details`, body);
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
   * POST /v1/oci/checkin
   * Complete online check-in: marks passengers as checked-in in the Delivery MS.
   * Returns a status confirmation — boarding passes are retrieved separately
   * via POST /v1/oci/boardingpasses.
   */
  submitOciCheckIn(
    bookingRef: string,
    passengers: { passengerId: string; inventoryIds: string[] }[]
  ): Observable<{ status: string; bookingReference: string; message: string }> {
    const base = environment.retailApiBaseUrl;
    const body = { bookingReference: bookingRef, passengers };
    return this.#http
      .post<{ status: string; bookingReference: string; message: string }>(`${base}/api/v1/oci/checkin`, body)
      .pipe(
        catchError((err: HttpErrorResponse) => throwError(() => ({
          status: err.status,
          message: err.error?.message ?? 'Check-in failed. Please try again or visit the airport desk.'
        })))
      );
  }

  /**
   * POST /v1/oci/boardingpasses
   * Retrieve boarding passes for all checked-in passengers on a booking,
   * reading from the delivery.Ticket and delivery.Manifest database tables.
   */
  getOciBoardingPasses(bookingRef: string): Observable<BoardingPass[]> {
    const base = environment.retailApiBaseUrl;
    const body = { bookingReference: bookingRef };
    return this.#http
      .post<{ boardingPasses: BoardingPass[] }>(`${base}/api/v1/oci/boardingpasses`, body)
      .pipe(
        map(res => res.boardingPasses),
        catchError((err: HttpErrorResponse) => throwError(() => ({
          status: err.status,
          message: err.error?.message ?? 'Unable to retrieve boarding passes. Please try again.'
        })))
      );
  }

  /**
   * GET /v1/flights?date=yyyy-MM-dd
   * List available flights for a given date from the Operations API.
   */
  getFlights(date?: string): Observable<FlightSummary[]> {
    const base = environment.operationsApiBaseUrl;
    const dateParam = date ? `?date=${date}` : '';
    return this.#http.get<FlightSummary[]>(`${base}/api/v1/flights${dateParam}`).pipe(
      catchError(() => of([]))
    );
  }

  /**
   * GET /v1/flight-numbers
   * List flight numbers with route details from active schedules for the current period.
   */
  getFlightNumbers(): Observable<ScheduledFlightNumber[]> {
    const base = environment.operationsApiBaseUrl;
    return this.#http.get<ScheduledFlightNumber[]>(`${base}/api/v1/flight-numbers`).pipe(
      catchError(() => of([]))
    );
  }

  /**
   * GET /v1/flights/{flightNumber}/status
   * Get real-time flight status from the Operations API.
   */
  getFlightStatus(flightNumber: string): Observable<FlightStatus | null> {
    const base = environment.operationsApiBaseUrl;
    return this.#http
      .get<FlightStatus>(`${base}/api/v1/flights/${encodeURIComponent(flightNumber)}/status`)
      .pipe(
        catchError((err: HttpErrorResponse) => {
          if (err.status === 404) return of(null);
          return throwError(() => err);
        })
      );
  }

  /**
   * PATCH /v1/orders/{bookingRef}/seats  (post-sale seat change)
   */
  updateSeats(bookingRef: string, seatSelections: { passengerId: string; segmentId: string; seatNumber: string }[]): Observable<{ success: boolean }> {
    const base = environment.retailApiBaseUrl;
    const body = { seatSelections };
    return this.#http
      .patch<{ bookingReference: string; updated: boolean }>(
        `${base}/api/v1/orders/${encodeURIComponent(bookingRef)}/seats`, body
      )
      .pipe(
        map(res => ({ success: res.updated })),
        catchError((err: HttpErrorResponse) => throwError(() => ({
          status: err.status,
          message: err.error?.message ?? 'Seat update failed. Please try again.'
        })))
      );
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
    bookingRef: string,
    bags: { passengerId: string; segmentId: string; additionalBags: number; bagOfferId: string; price: number }[],
    payment: { method: string; cardNumber: string; expiryDate: string; cvv: string; cardholderName: string }
  ): Observable<{ success: boolean; paymentReference: string }> {
    const base = environment.retailApiBaseUrl;
    const body = {
      bagSelections: bags.map(b => ({
        bagOfferId: b.bagOfferId,
        passengerRef: b.passengerId,
        inventoryId: b.segmentId
      })),
      payment
    };
    return this.#http
      .post<{ bookingReference: string; totalBagAmount: number; paymentId: string }>(
        `${base}/api/v1/orders/${encodeURIComponent(bookingRef)}/bags`, body
      )
      .pipe(
        map(res => ({ success: true, paymentReference: res.paymentId })),
        catchError((err: HttpErrorResponse) => throwError(() => ({
          status: err.status,
          message: err.error?.message ?? 'Bag purchase failed. Please try again.'
        })))
      );
  }

  /**
   * POST /v1/orders/oci/{bookingRef}/bags
   * Purchase additional bags during online check-in.
   */
  addOciBags(
    bookingRef: string,
    bagSelections: { passengerId: string; segmentRef: string; bagOfferId: string; additionalBags: number }[],
    payment?: { method: string; cardNumber: string; expiryDate: string; cvv: string; cardholderName: string }
  ): Observable<{ bookingReference: string; bagsPurchased: number; paymentReference?: string }> {
    const base = environment.retailApiBaseUrl;
    return this.#http.post<{ bookingReference: string; bagsPurchased: number; paymentReference?: string }>(
      `${base}/api/v1/orders/oci/${bookingRef}/bags`,
      { bagSelections, payment: payment ?? null }
    ).pipe(
      catchError((err: HttpErrorResponse) => {
        const message = err.status === 404
          ? 'Booking not found.'
          : 'Unable to process bag purchase. Please try again.';
        return throwError(() => ({ status: err.status, message }));
      })
    );
  }

  /**
   * POST /v1/orders/{bookingRef}/cancel
   * Cancel a confirmed booking.
   */
  cancelOrder(bookingRef: string, _reason: string): Observable<{ success: boolean; refundAmount: number; currency: string }> {
    const base = environment.retailApiBaseUrl;
    return this.#http
      .post<{ bookingReference: string; orderStatus: string; refundableAmount: number; refundInitiated: boolean }>(
        `${base}/api/v1/orders/${encodeURIComponent(bookingRef)}/cancel`, {}
      )
      .pipe(
        map(res => ({ success: true, refundAmount: res.refundableAmount, currency: 'GBP' })),
        catchError((err: HttpErrorResponse) => throwError(() => ({
          status: err.status,
          message: err.error?.message ?? 'Cancellation failed. Please try again.'
        })))
      );
  }

  /**
   * POST /v1/orders/{bookingRef}/change
   * Change a confirmed flight.
   */
  changeOrder(
    bookingRef: string,
    newOfferId: string,
    payment?: { method: string; cardNumber: string; expiryDate: string; cvv: string; cardholderName: string }
  ): Observable<{ success: boolean; addCollect: number; currency: string; newFlightNumber?: string }> {
    const base = environment.retailApiBaseUrl;
    const body: Record<string, unknown> = { newOfferId };
    if (payment) body['payment'] = payment;
    return this.#http
      .post<{ bookingReference: string; newFlightNumber: string; totalDue: number; paymentId?: string }>(
        `${base}/api/v1/orders/${encodeURIComponent(bookingRef)}/change`, body
      )
      .pipe(
        map(res => ({ success: true, addCollect: res.totalDue, currency: 'GBP', newFlightNumber: res.newFlightNumber })),
        catchError((err: HttpErrorResponse) => throwError(() => ({
          status: err.status,
          message: err.error?.message ?? 'Flight change failed. Please try again.'
        })))
      );
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
