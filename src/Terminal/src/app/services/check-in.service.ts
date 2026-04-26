import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

interface AdminOrderPassenger {
  passengerId: string;
  givenName: string;
  surname: string;
  passengerTypeCode: string;
  docs: {
    type: string;
    number: string;
    issuingCountry: string;
    nationality: string;
    issueDate: string;
    expiryDate: string;
  }[];
}

interface AdminOrderItem {
  itemType: string;
  passengerId?: string;
  segmentId?: string;
  eTicketNumber?: string;
}

interface AdminOrderDetail {
  bookingReference: string;
  orderData: {
    dataLists: {
      flightSegments: { segmentId: string; origin: string }[];
      passengers: AdminOrderPassenger[];
    };
    orderItems: AdminOrderItem[];
    eTickets?: { eTicketNumber: string; passengerId: string }[];
  } | null;
}

interface TicketRecord {
  eTicketNumber: string;
  passengerId: string;
  isVoided: boolean;
  ticketData?: {
    coupons?: { origin: string; status: string }[];
  };
}

export interface LookupResponse {
  bookingReference: string;
  departureAirports: string[];
  orderDetail: AdminOrderDetail;
  tickets: TicketRecord[];
}

export interface CheckInPaxEntry {
  passengerId: string;
  ticketNumber: string;
  givenName: string;
  surname: string;
  passengerTypeCode: string;
  alreadyCheckedIn: boolean;
  existingDoc: {
    type: string;
    number: string;
    issuingCountry: string;
    nationality: string;
    issueDate: string;
    expiryDate: string;
  } | null;
}

export interface PaxSubmission {
  ticketNumber: string;
  travelDocument: {
    type: string;
    number: string;
    issuingCountry: string;
    nationality: string;
    issueDate: string;
    expiryDate: string;
  };
}

export interface TimaticNote {
  checkType: string;
  ticketNumber: string;
  status: string;
  detail: string;
}

export interface BoardingCard {
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

export interface AdminCheckInResponse {
  bookingReference: string;
  timaticNotes: TimaticNote[];
  boardingCards: BoardingCard[];
}

@Injectable({ providedIn: 'root' })
export class CheckInService {
  #http = inject(HttpClient);

  async lookup(bookingReference: string): Promise<LookupResponse> {
    const ref = bookingReference.toUpperCase();
    const [order, tickets] = await Promise.all([
      firstValueFrom(
        this.#http.get<AdminOrderDetail>(`${environment.retailApiUrl}/api/v1/admin/orders/${ref}`),
      ),
      firstValueFrom(
        this.#http.get<TicketRecord[]>(`${environment.retailApiUrl}/api/v1/admin/orders/${ref}/tickets`),
      ).catch(() => [] as TicketRecord[]),
    ]);

    const segments = order.orderData?.dataLists?.flightSegments ?? [];
    const departureAirports = [...new Set(segments.map(s => s.origin))];
    return { bookingReference: order.bookingReference, departureAirports, orderDetail: order, tickets };
  }

  async lookupByETicket(eTicketNumber: string): Promise<LookupResponse> {
    const order = await firstValueFrom(
      this.#http.get<AdminOrderDetail>(
        `${environment.retailApiUrl}/api/v1/admin/orders/e-ticket/${encodeURIComponent(eTicketNumber.trim())}`,
      ),
    );
    const ref = order.bookingReference.toUpperCase();
    const tickets = await firstValueFrom(
      this.#http.get<TicketRecord[]>(`${environment.retailApiUrl}/api/v1/admin/orders/${ref}/tickets`),
    ).catch(() => [] as TicketRecord[]);
    const segments = order.orderData?.dataLists?.flightSegments ?? [];
    const departureAirports = [...new Set(segments.map(s => s.origin))];
    return { bookingReference: order.bookingReference, departureAirports, orderDetail: order, tickets };
  }

  extractPassengers(
    orderDetail: AdminOrderDetail,
    tickets: TicketRecord[],
    departureAirport: string,
  ): CheckInPaxEntry[] {
    const orderData = orderDetail.orderData;
    if (!orderData) return [];

    const segments = orderData.dataLists?.flightSegments ?? [];
    const matchingSegmentIds = new Set(
      segments.filter(s => s.origin === departureAirport).map(s => s.segmentId),
    );

    const ticketByPaxId = new Map<string, string>();
    for (const item of orderData.orderItems ?? []) {
      if (
        item.itemType === 'Flight' &&
        item.passengerId &&
        item.eTicketNumber &&
        item.segmentId &&
        matchingSegmentIds.has(item.segmentId)
      ) {
        ticketByPaxId.set(item.passengerId, item.eTicketNumber);
      }
    }

    if (ticketByPaxId.size === 0 && orderData.eTickets) {
      for (const et of orderData.eTickets) {
        if (et.passengerId && et.eTicketNumber) {
          ticketByPaxId.set(et.passengerId, et.eTicketNumber);
        }
      }
    }

    // Build a set of ticket numbers whose coupon for this departure is already checked in
    const alreadyCheckedInTickets = new Set<string>();
    for (const t of tickets) {
      const coupon = t.ticketData?.coupons?.find(c => c.origin === departureAirport);
      if (coupon?.status === 'CHECKED_IN') {
        alreadyCheckedInTickets.add(t.eTicketNumber);
      }
    }

    return (orderData.dataLists?.passengers ?? [])
      .filter(p => ticketByPaxId.has(p.passengerId))
      .map(p => {
        const ticketNumber = ticketByPaxId.get(p.passengerId)!;
        return {
          passengerId: p.passengerId,
          ticketNumber,
          givenName: p.givenName,
          surname: p.surname,
          passengerTypeCode: p.passengerTypeCode,
          alreadyCheckedIn: alreadyCheckedInTickets.has(ticketNumber),
          existingDoc: p.docs?.[0] ?? null,
        };
      });
  }

  async adminCheckIn(
    bookingReference: string,
    departureAirport: string,
    passengers: PaxSubmission[],
  ): Promise<AdminCheckInResponse> {
    return firstValueFrom(
      this.#http.post<AdminCheckInResponse>(
        `${environment.retailApiUrl}/api/v1/admin/checkin/${bookingReference.toUpperCase()}`,
        { departureAirport, passengers },
      ),
    );
  }
}
