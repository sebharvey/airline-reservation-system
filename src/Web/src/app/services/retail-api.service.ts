/**
 * RetailApiService
 *
 * Channel-facing service for all flight retailing operations.
 * Currently returns mock data as Observables simulating API responses.
 *
 * To connect to real APIs, replace each method body with an HttpClient call:
 *   return this.http.post<FlightOffer[]>('/api/v1/search/slice', params);
 *
 * The Observable<T> contract remains identical — no consumer changes required.
 */

import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { delay } from 'rxjs/operators';
import { FlightOffer, Seatmap, BagPolicyResponse, FlightStatus, CabinCode } from '../models/flight.model';
import { Order, BoardingPass } from '../models/order.model';
import { getMockFlightOffers, MOCK_FLIGHT_STATUS } from '../data/mock/flight-offers.mock';
import { getMockSeatmap } from '../data/mock/seatmap.mock';
import { MOCK_ORDERS } from '../data/mock/orders.mock';
import { MOCK_BAG_POLICIES } from '../data/mock/bag-policy.mock';

export interface SearchSliceParams {
  origin: string;
  destination: string;
  departDate: string;
  adults: number;
  children: number;
}

export interface RetrieveOrderParams {
  bookingReference: string;
  givenName: string;
  surname: string;
}

const API_DELAY_MS = 600;

@Injectable({ providedIn: 'root' })
export class RetailApiService {

  /**
   * POST /v1/search/slice
   * Search for available flights for a single directional slice.
   */
  searchSlice(params: SearchSliceParams): Observable<FlightOffer[]> {
    const offers = getMockFlightOffers(
      params.origin,
      params.destination,
      params.departDate,
      params.adults,
      params.children
    );
    return of(offers).pipe(delay(API_DELAY_MS));
  }

  /**
   * GET /v1/flights/{flightId}/seatmap
   * Retrieve seatmap with pricing and availability for a flight.
   */
  getFlightSeatmap(flightId: string, flightNumber: string, cabinCode?: CabinCode): Observable<Seatmap> {
    return of(getMockSeatmap(flightId, flightNumber, cabinCode)).pipe(delay(API_DELAY_MS));
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
    const ref = params.bookingReference.toUpperCase().trim();
    const order = MOCK_ORDERS[ref];
    if (!order) {
      return throwError(() => ({
        status: 404,
        message: 'Booking not found. Please check your reference and name.'
      })).pipe(delay(API_DELAY_MS));
    }
    // Validate passenger name (case-insensitive check on first passenger)
    const nameMatch = order.passengers.some(
      p => p.surname.toLowerCase() === params.surname.toLowerCase().trim() ||
           p.givenName.toLowerCase() === params.givenName.toLowerCase().trim()
    );
    if (!nameMatch) {
      return throwError(() => ({
        status: 404,
        message: 'Booking not found. Please check your reference and name.'
      })).pipe(delay(API_DELAY_MS));
    }
    return of({ ...order }).pipe(delay(API_DELAY_MS));
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
