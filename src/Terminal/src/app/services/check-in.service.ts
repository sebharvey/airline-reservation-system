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
  additionalBags?: number;
}

interface AdminOrderDetail {
  bookingReference: string;
  orderData: {
    dataLists: {
      flightSegments: { segmentId: string; origin: string; cabinClass?: string }[];
      passengers: AdminOrderPassenger[];
    };
    orderItems: AdminOrderItem[];
    eTickets?: { eTicketNumber: string; passengerId: string }[];
    notes?: OciNote[];
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
  segmentIds: number[];
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

export interface BaggageSubmissionItem {
  bagNumber: number;
  weightKg: number | null;
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
  baggage?: BaggageSubmissionItem[];
}

export interface TimaticNote {
  checkType: string;
  ticketNumber: string;
  status: string;
  detail: string;
}

export interface OciNote {
  dateTime: string;
  type: string;
  message: string;
  paxId?: number;
  segmentId?: number;
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

    // Map each inventory-ID (GUID) to its 1-based position across all flight segments.
    // This matches the server-side flightIndex used by CheckInHelper.ParseSegmentIdForDeparture
    // when writing the integer segmentId onto OCI audit notes.
    const segIndexByGuid = new Map<string, number>(
      segments.map((s, i) => [s.segmentId, i + 1] as [string, number]),
    );

    const ticketByPaxId = new Map<string, string>();
    const segmentIdsByPaxId = new Map<string, number[]>();
    for (const item of orderData.orderItems ?? []) {
      if (
        item.itemType === 'Flight' &&
        item.passengerId &&
        item.eTicketNumber &&
        item.segmentId &&
        matchingSegmentIds.has(item.segmentId)
      ) {
        ticketByPaxId.set(item.passengerId, item.eTicketNumber);
        const segId = segIndexByGuid.get(item.segmentId);
        if (segId !== undefined) {
          const existing = segmentIdsByPaxId.get(item.passengerId) ?? [];
          if (!existing.includes(segId)) existing.push(segId);
          segmentIdsByPaxId.set(item.passengerId, existing);
        }
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
          segmentIds: segmentIdsByPaxId.get(p.passengerId) ?? [],
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
    overrideTimatic = false,
    overrideReason?: string,
  ): Promise<AdminCheckInResponse> {
    return firstValueFrom(
      this.#http.post<AdminCheckInResponse>(
        `${environment.operationsApiUrl}/api/v1/admin/checkin/${bookingReference.toUpperCase()}`,
        { departureAirport, passengers, overrideTimatic, overrideReason },
      ),
    );
  }
}
