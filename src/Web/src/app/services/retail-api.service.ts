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
import { Order, OciOrder, BoardingPass, BookingType, Passenger, BasketSeatSelection, BasketBagSelection, BasketSsrSelection, BasketSummary } from '../models/order.model';
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
  passengerCount: number;
  currency?: string;
  loyaltyNumber?: string;
}

export interface CreateBasketResponse {
  basketId: string;
}

export interface BasketApiFlightOffer {
  offerId: string;
  inventoryId: string;
}

export interface BasketApiResponse {
  basketId: string;
  basketData: {
    flightOffers: BasketApiFlightOffer[];
  };
}

export interface IssuedETicket {
  passengerId: string;
  segmentIds: string[];
  eTicketNumber: string;
}

export interface ConfirmBasketResponse {
  bookingReference: string;
  status: string;
  totalPrice: number;
  currency: string;
  bookedAt: string;
  eTickets: IssuedETicket[];
  maskedCardNumber?: string;
  cardType?: string;
  cardholderName?: string;
}

export interface RetrieveOrderParams {
  bookingReference: string;
  givenName: string;
  surname: string;
}

export interface OciRetrieveParams extends RetrieveOrderParams {
  departureAirport: string;
}

// ---- Live API response shape from POST /api/v1/search/slice ----
// The endpoint returns a unified itinerary list.  Direct flights have a single
// entry in legs[]; connecting itineraries (via LHR) have two.

interface SliceApiOffer {
  offerId: string;
  fareBasisCode: string;
  basePrice: number;
  tax: number;
  totalPrice: number;
  currency: string;
  isRefundable: boolean;
  isChangeable: boolean;
}

interface SliceApiFareFamily {
  fareFamily: string;
  offer: SliceApiOffer;
}

interface SliceApiCabin {
  cabinCode: string;
  availableSeats: number;
  fromPrice: number;
  currency: string;
  fromPoints: number | null;
  fareFamilies: SliceApiFareFamily[];
}

interface SliceApiLeg {
  sessionId: string;
  inventoryId: string;
  flightNumber: string;
  origin: string;
  destination: string;
  departureDate: string;
  departureTime: string;
  arrivalTime: string;
  arrivalDayOffset: number;
  aircraftType: string;
  cabins: SliceApiCabin[];
}

interface SliceApiItinerary {
  legs: SliceApiLeg[];
  connectionDurationMinutes: number | null;
  combinedFromPrice: number;
  currency: string;
}

interface SliceSearchApiResponse {
  itineraries: SliceApiItinerary[];
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
  offers: FlightOffer[];
}

/** Compute the ISO arrival datetime string for a leg, accounting for arrivalDayOffset. */
function legArrivalDateTime(leg: SliceApiLeg): string {
  const [y, m, d] = leg.departureDate.split('-').map(Number);
  const arrival = new Date(Date.UTC(y, m - 1, d + (leg.arrivalDayOffset ?? 0)));
  return `${arrival.toISOString().slice(0, 10)}T${leg.arrivalTime}:00`;
}

/** Build a single-leg FlightOffer for basket-state storage (one physical segment). */
function buildLegOffer(
  leg: SliceApiLeg,
  cabin: SliceApiCabin,
  family: SliceApiFareFamily
): FlightOffer {
  return {
    offerId: family.offer.offerId,
    inventoryId: leg.inventoryId,
    segments: [{ offerId: family.offer.offerId, sessionId: leg.sessionId }],
    flightNumber: leg.flightNumber,
    origin: leg.origin,
    destination: leg.destination,
    departureDateTime: `${leg.departureDate}T${leg.departureTime}:00`,
    arrivalDateTime: legArrivalDateTime(leg),
    aircraftType: leg.aircraftType,
    cabinCode: cabin.cabinCode as CabinCode,
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
  };
}

function mapApiResponseToResult(response: SliceSearchApiResponse): SearchSliceResult {
  const offers: FlightOffer[] = [];

  for (const itinerary of response.itineraries) {
    if (itinerary.legs.length === 1) {
      // ── Direct flight ──────────────────────────────────────────────────────
      const leg = itinerary.legs[0];
      for (const cabin of leg.cabins) {
        for (const family of cabin.fareFamilies) {
          offers.push({
            ...buildLegOffer(leg, cabin, family),
            isConnecting: false
          });
        }
      }
    } else if (itinerary.legs.length === 2) {
      // ── Connecting flight (two legs via LHR) ───────────────────────────────
      const leg1 = itinerary.legs[0];
      const leg2 = itinerary.legs[1];

      // For each cabin available on leg 1, find the matching cabin on leg 2 and
      // create one combined FlightOffer per cabin.  Fare families within each cabin
      // are paired by matching name; unmatched families use the cheapest available.
      for (const cabin1 of leg1.cabins) {
        const cabin2 = leg2.cabins.find(c => c.cabinCode === cabin1.cabinCode);
        if (!cabin2) continue;

        // Pair fare families by name where possible; fall back to cheapest leg-2
        // fare for any leg-1 family that has no name match on leg 2.
        const cheapestFamily2 = cabin2.fareFamilies.reduce((min, f) =>
          f.offer.totalPrice < min.offer.totalPrice ? f : min);

        for (const family1 of cabin1.fareFamilies) {
          const family2 = cabin2.fareFamilies.find(f => f.fareFamily === family1.fareFamily)
            ?? cheapestFamily2;

          // Per-leg FlightOffer objects stored in bookingState (one segment each).
          const legOffer1 = buildLegOffer(leg1, cabin1, family1);
          const legOffer2 = buildLegOffer(leg2, cabin2, family2);

          offers.push({
            // Primary identifiers use leg 1 values; segments contains both.
            offerId: family1.offer.offerId,
            inventoryId: leg1.inventoryId,
            segments: [
              { offerId: family1.offer.offerId, sessionId: leg1.sessionId },
              { offerId: family2.offer.offerId, sessionId: leg2.sessionId }
            ],
            // Display shows itinerary endpoints.
            flightNumber: `${leg1.flightNumber} / ${leg2.flightNumber}`,
            origin: leg1.origin,
            destination: leg2.destination,
            departureDateTime: `${leg1.departureDate}T${leg1.departureTime}:00`,
            arrivalDateTime: legArrivalDateTime(leg2),
            aircraftType: leg1.aircraftType,
            cabinCode: cabin1.cabinCode as CabinCode,
            cabinName: CABIN_NAMES[cabin1.cabinCode] ?? cabin1.cabinCode,
            fareFamily: family1.fareFamily,
            fareBasisCode: family1.offer.fareBasisCode,
            bookingClass: family1.offer.fareBasisCode.charAt(0),
            isRefundable: family1.offer.isRefundable && family2.offer.isRefundable,
            isChangeable: family1.offer.isChangeable && family2.offer.isChangeable,
            unitPrice: family1.offer.basePrice + family2.offer.basePrice,
            taxes: family1.offer.tax + family2.offer.tax,
            totalPrice: family1.offer.totalPrice + family2.offer.totalPrice,
            currency: family1.offer.currency,
            seatsAvailable: Math.min(cabin1.availableSeats, cabin2.availableSeats),
            pointsPrice: (cabin1.fromPoints != null && cabin2.fromPoints != null)
              ? cabin1.fromPoints + cabin2.fromPoints : undefined,
            pointsTaxes: undefined,
            isConnecting: true,
            connectionDurationMinutes: itinerary.connectionDurationMinutes ?? undefined,
            connectingLegs: [
              {
                flightNumber: leg1.flightNumber,
                origin: leg1.origin,
                destination: leg1.destination,
                departureDateTime: `${leg1.departureDate}T${leg1.departureTime}:00`,
                arrivalDateTime: legArrivalDateTime(leg1),
                aircraftType: leg1.aircraftType
              },
              {
                flightNumber: leg2.flightNumber,
                origin: leg2.origin,
                destination: leg2.destination,
                departureDateTime: `${leg2.departureDate}T${leg2.departureTime}:00`,
                arrivalDateTime: legArrivalDateTime(leg2),
                aircraftType: leg2.aircraftType
              }
            ],
            allLegs: [legOffer1, legOffer2]
          });
        }
      }
    }
  }

  return { offers };
}

@Injectable({ providedIn: 'root' })
export class RetailApiService {
  readonly #http = inject(HttpClient);

  /**
   * GET /v1/basket/{basketId}
   * Fetch the current basket from the Retail API.
   */
  getBasket(basketId: string): Observable<BasketApiResponse> {
    const base = environment.retailApiBaseUrl;
    return this.#http.get<BasketApiResponse>(`${base}/api/v1/basket/${basketId}`);
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
      .post<SliceSearchApiResponse>(`${base}/api/v1/search/slice`, body)
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
      currency: params.currency ?? 'GBP',
      bookingType: params.bookingType,
      loyaltyNumber: params.loyaltyNumber ?? null,
      passengerCount: params.passengerCount
    };
    return this.#http.post<CreateBasketResponse>(`${base}/api/v1/basket`, body);
  }

  /**
   * GET /v1/basket/{basketId}/summary
   * Reprice all offers and return a pricing summary with tax line breakdowns.
   */
  getBasketSummary(basketId: string): Observable<BasketSummary> {
    const base = environment.retailApiBaseUrl;
    return this.#http.get<BasketSummary>(`${base}/api/v1/basket/${basketId}/summary`);
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
  getFlightSeatmap(flightId: string, flightNumber: string, aircraftType: string, cabinCode?: string): Observable<Seatmap> {
    const base = environment.retailApiBaseUrl;
    let params = `aircraftType=${encodeURIComponent(aircraftType)}&flightNumber=${encodeURIComponent(flightNumber)}`;
    if (cabinCode) params += `&cabinCode=${encodeURIComponent(cabinCode)}`;
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
   * POST /v1/oci/retrieve (Operations API)
   * Retrieve a booking for the online check-in journey, filtered by departure airport.
   * Returns passengers with ticket numbers and optionally pre-filled passport data.
   */
  retrieveOciOrder(params: OciRetrieveParams): Observable<OciOrder> {
    const base = environment.operationsApiBaseUrl;
    const body = {
      bookingReference: params.bookingReference.toUpperCase().trim(),
      firstName: params.givenName.trim(),
      lastName: params.surname.trim(),
      departureAirport: params.departureAirport.toUpperCase().trim()
    };
    return this.#http.post<{ bookingReference: string; checkInEligible: boolean; passengers: { passengerId: string; ticketNumber: string; givenName: string; surname: string; passengerTypeCode: string; travelDocument: unknown }[] }>(`${base}/api/v1/oci/retrieve`, body).pipe(
      map(res => ({
        bookingReference: res.bookingReference,
        checkInEligible: res.checkInEligible,
        orderStatus: 'Confirmed',
        currency: 'GBP',
        passengers: res.passengers.map(p => ({
          passengerId: p.passengerId,
          ticketNumber: p.ticketNumber,
          type: p.passengerTypeCode,
          givenName: p.givenName,
          surname: p.surname
        })),
        flightSegments: []
      })),
      catchError((err: HttpErrorResponse) => {
        const message = err.status === 404
          ? 'Booking not found. Please check your details and departure airport.'
          : 'Unable to retrieve booking. Please try again.';
        return throwError(() => ({ status: err.status, message }));
      })
    );
  }

  /**
   * POST /v1/oci/pax (Operations API)
   * Save APIS travel document data for each passenger to the booking.
   */
  saveOciPassengerDetails(bookingRef: string, departureAirport: string, travelDocs: { ticketNumber: string; type: string; number: string; issuingCountry: string; issueDate: string; expiryDate: string; nationality: string }[]): Observable<void> {
    const base = environment.operationsApiBaseUrl;
    const body = {
      bookingReference: bookingRef,
      departureAirport: departureAirport.toUpperCase(),
      passengers: travelDocs.map(d => ({
        ticketNumber: d.ticketNumber,
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
    return this.#http.post<void>(`${base}/api/v1/oci/pax`, body);
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
   * POST /v1/oci/checkin (Operations API)
   * Complete online check-in for a departure airport; calls the Delivery MS to update
   * coupon status to C for all tickets on the booking departing from that airport.
   * Returns the list of checked-in ticket numbers.
   */
  submitOciCheckIn(
    bookingRef: string,
    departureAirport: string
  ): Observable<{ bookingReference: string; checkedIn: string[] }> {
    const base = environment.operationsApiBaseUrl;
    const body = { bookingReference: bookingRef, departureAirport: departureAirport.toUpperCase() };
    return this.#http
      .post<{ bookingReference: string; checkedIn: string[] }>(`${base}/api/v1/oci/checkin`, body)
      .pipe(
        catchError((err: HttpErrorResponse) => throwError(() => ({
          status: err.status,
          message: err.error?.message ?? 'Check-in failed. Please try again or visit the airport desk.'
        })))
      );
  }

  /**
   * POST /v1/oci/boarding-docs (Operations API)
   * Retrieve boarding documents for checked-in tickets at a departure airport.
   * The Delivery MS generates a BCBP string for each segment checked in at that airport.
   */
  getOciBoardingPasses(departureAirport: string, ticketNumbers: string[]): Observable<BoardingPass[]> {
    const base = environment.operationsApiBaseUrl;
    const body = { departureAirport: departureAirport.toUpperCase(), ticketNumbers };
    return this.#http
      .post<{ boardingCards: { ticketNumber: string; passengerId: string; givenName: string; surname: string; flightNumber: string; departureDate: string; seatNumber: string; cabinCode: string; sequenceNumber: string; origin: string; destination: string; bcbpString: string }[] }>(`${base}/api/v1/oci/boarding-docs`, body)
      .pipe(
        map(res => res.boardingCards.map(c => ({
          bookingReference: '',
          passengerId: c.passengerId,
          givenName: c.givenName,
          surname: c.surname,
          flightNumber: c.flightNumber,
          origin: c.origin,
          destination: c.destination,
          departureDateTime: c.departureDate,
          seatNumber: c.seatNumber,
          cabinCode: c.cabinCode as CabinCode,
          eTicketNumber: c.ticketNumber,
          sequenceNumber: c.sequenceNumber,
          bcbpBarcode: c.bcbpString,
          gate: 'TBC',
          boardingTime: ''
        }))),
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
