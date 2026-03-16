import { Injectable, signal, computed } from '@angular/core';

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
  readonly bagSelections = signal<CheckInBagSelection[]>([]);
  readonly seatSelections = signal<CheckInSeatSelection[]>([]);

  readonly totalBagAmount = computed(() =>
    this.bagSelections().reduce((sum, s) => sum + s.price, 0)
  );

  readonly totalSeatAmount = computed(() =>
    this.seatSelections().reduce((sum, s) => sum + s.seatPrice, 0)
  );

  readonly totalPaymentAmount = computed(() =>
    this.totalBagAmount() + this.totalSeatAmount()
  );

  setBagSelections(sels: CheckInBagSelection[]): void {
    this.bagSelections.set(sels);
  }

  setSeatSelections(sels: CheckInSeatSelection[]): void {
    this.seatSelections.set(sels);
  }

  clear(): void {
    this.bagSelections.set([]);
    this.seatSelections.set([]);
  }
}
