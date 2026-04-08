import { Injectable, signal, computed } from '@angular/core';
import { OciOrder, BoardingPass } from '../models/order.model';

export interface OciTravelDocument {
  passengerId: string;
  ticketNumber: string;
  type: 'PASSPORT' | 'ID_CARD';
  number: string;
  issuingCountry: string;
  issueDate: string;
  expiryDate: string;
  nationality: string;
}

export interface CheckInBagSelection {
  passengerId: string;
  segmentId: string;
  bagOfferId: string;
  additionalBags: number;
  price: number;
  currency: string;
}

export interface CheckInSeatSelection {
  passengerId: string;
  segmentId: string;
  seatNumber: string;
  seatPrice: number;
  currency: string;
}

@Injectable({ providedIn: 'root' })
export class CheckInStateService {
  readonly currentOrder = signal<OciOrder | null>(null);
  readonly departureAirport = signal<string>('');
  readonly selectedPassengerIds = signal<string[]>([]);
  readonly travelDocuments = signal<OciTravelDocument[]>([]);
  readonly bagSelections = signal<CheckInBagSelection[]>([]);
  readonly seatSelections = signal<CheckInSeatSelection[]>([]);
  readonly boardingPasses = signal<BoardingPass[]>([]);
  readonly checkedInTicketNumbers = signal<string[]>([]);

  readonly totalBagAmount = computed(() =>
    this.bagSelections().reduce((sum, s) => sum + s.price, 0)
  );

  readonly totalSeatAmount = computed(() =>
    this.seatSelections().reduce((sum, s) => sum + s.seatPrice, 0)
  );

  readonly totalPaymentAmount = computed(() =>
    this.totalBagAmount() + this.totalSeatAmount()
  );

  setCurrentOrder(order: OciOrder): void {
    this.currentOrder.set(order);
  }

  setDepartureAirport(code: string): void {
    this.departureAirport.set(code.toUpperCase());
  }

  setPassengerCheckInData(passengerIds: string[], travelDocs: OciTravelDocument[]): void {
    this.selectedPassengerIds.set(passengerIds);
    this.travelDocuments.set(travelDocs);
  }

  setBagSelections(sels: CheckInBagSelection[]): void {
    this.bagSelections.set(sels);
  }

  setSeatSelections(sels: CheckInSeatSelection[]): void {
    this.seatSelections.set(sels);
  }

  setBoardingPasses(passes: BoardingPass[]): void {
    this.boardingPasses.set(passes);
  }

  setCheckedInTicketNumbers(tickets: string[]): void {
    this.checkedInTicketNumbers.set(tickets);
  }

  clear(): void {
    this.currentOrder.set(null);
    this.departureAirport.set('');
    this.selectedPassengerIds.set([]);
    this.travelDocuments.set([]);
    this.bagSelections.set([]);
    this.seatSelections.set([]);
    this.boardingPasses.set([]);
    this.checkedInTicketNumbers.set([]);
  }
}
