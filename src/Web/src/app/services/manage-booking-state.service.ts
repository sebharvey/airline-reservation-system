import { Injectable, signal, computed } from '@angular/core';

export interface ManageBookingBagSelection {
  passengerId: string;
  segmentId: string;
  bagOfferId: string;
  additionalBags: number;
  price: number;
  currency: string;
}

@Injectable({ providedIn: 'root' })
export class ManageBookingStateService {
  readonly bagSelections = signal<ManageBookingBagSelection[]>([]);

  readonly totalBagAmount = computed(() =>
    this.bagSelections().reduce((sum, s) => sum + s.price, 0)
  );

  setBagSelections(sels: ManageBookingBagSelection[]): void {
    this.bagSelections.set(sels);
  }

  clear(): void {
    this.bagSelections.set([]);
  }
}
