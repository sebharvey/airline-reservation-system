import { Component, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { CheckInStateService, CheckInSeatSelection } from '../../../services/check-in-state.service';
import { OciOrder } from '../../../models/order.model';
import { Seatmap, CabinSeatmap, SeatOffer } from '../../../models/flight.model';

interface SeatSelection {
  passengerId: string;
  segmentRef: string;
  seatNumber: string;
  price: number;
  currency: string;
}

@Component({
  selector: 'app-check-in-seats',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './check-in-seats.html',
  styleUrl: './check-in-seats.css'
})
export class CheckInSeatsComponent implements OnInit {
  order = signal<OciOrder | null>(null);
  seatmap = signal<Seatmap | null>(null);
  seatmapLoading = signal(false);
  submitting = signal(false);
  errorMessage = signal('');

  activePassengerIndex = signal(0);
  selections = signal<SeatSelection[]>([]);

  readonly checkedInPassengers = computed(() => {
    const o = this.order();
    const ids = this.checkInState.selectedPassengerIds();
    if (!o) return [];
    return o.passengers.filter(p => ids.includes(p.passengerId));
  });

  readonly activePassenger = computed(() =>
    this.checkedInPassengers()[this.activePassengerIndex()] ?? null
  );

  readonly activeCabin = computed((): CabinSeatmap | null => {
    const sm = this.seatmap();
    const seg = this.order()?.flightSegments[0];
    if (!sm || !seg) return null;
    return sm.cabins.find(c => c.cabinCode === seg.cabinCode) ?? sm.cabins[0] ?? null;
  });

  readonly seatRows = computed((): { rowNumber: number; seats: (SeatOffer | null)[] }[] => {
    const cabin = this.activeCabin();
    if (!cabin) return [];
    const rows: Map<number, (SeatOffer | null)[]> = new Map();
    for (let r = cabin.startRow; r <= cabin.endRow; r++) {
      rows.set(r, cabin.columns.map(() => null));
    }
    for (const seat of cabin.seats) {
      const row = rows.get(seat.rowNumber);
      if (row) {
        const idx = cabin.columns.indexOf(seat.column);
        if (idx !== -1) row[idx] = seat;
      }
    }
    return Array.from(rows.entries())
      .sort((a, b) => a[0] - b[0])
      .map(([rowNumber, seats]) => ({ rowNumber, seats }));
  });

  readonly seatsAlwaysFree = computed((): boolean => {
    const cabinCode = this.order()?.flightSegments[0]?.cabinCode;
    return cabinCode === 'J' || cabinCode === 'F';
  });

  readonly totalSeatCost = computed((): number =>
    this.selections().reduce((sum, s) => sum + s.price, 0)
  );

  readonly currency = computed((): string =>
    this.order()?.currencyCode ?? 'GBP'
  );

  constructor(
    private router: Router,
    private retailApi: RetailApiService,
    private checkInState: CheckInStateService
  ) {}

  ngOnInit(): void {
    const order = this.checkInState.currentOrder();
    if (!order) {
      this.router.navigate(['/check-in']);
      return;
    }
    this.order.set(order);

    // Skip seat selection entirely if all checked-in passengers already have seats on the order
    const checkedInIds = this.checkInState.selectedPassengerIds();
    const seg = order.flightSegments[0];
    if (seg) {
      const assignedIds = seg.seatAssignments.map(s => s.passengerId);
      const allSeated = checkedInIds.length > 0 && checkedInIds.every(id => assignedIds.includes(id));
      if (allSeated) {
        this.checkInState.setSeatSelections([]);
        this.router.navigate(['/check-in/bags']);
        return;
      }
      this.loadSeatmap(seg.inventoryId, seg.flightNumber, seg.aircraftType);
    }
  }

  private loadSeatmap(inventoryId: string, flightNumber: string, cabinCode: string): void {
    this.seatmapLoading.set(true);
    this.retailApi.getFlightSeatmap(inventoryId, flightNumber, cabinCode).subscribe({
      next: (sm) => { this.seatmap.set(sm); this.seatmapLoading.set(false); },
      error: () => { this.seatmapLoading.set(false); }
    });
  }

  getSeatCharge(seat: SeatOffer): number {
    if (this.seatsAlwaysFree()) return 0;
    return seat.price;
  }

  getSeatPriceLabel(seat: SeatOffer): string {
    if (this.seatsAlwaysFree()) return 'Incl.';
    if (seat.price === 0) return 'Free';
    return `${seat.currency} ${seat.price.toFixed(0)}`;
  }

  selectPassenger(index: number): void {
    this.activePassengerIndex.set(index);
  }

  isSeatSelected(seat: SeatOffer): boolean {
    const pax = this.activePassenger();
    const seg = this.order()?.flightSegments[0];
    if (!pax || !seg) return false;
    return this.selections().some(
      s => s.passengerId === pax.passengerId && s.segmentRef === seg.segmentRef && s.seatNumber === seat.seatNumber
    );
  }

  isSeatTakenByOther(seat: SeatOffer): boolean {
    const pax = this.activePassenger();
    const seg = this.order()?.flightSegments[0];
    return this.selections().some(
      s => s.seatNumber === seat.seatNumber && s.segmentRef === (seg?.segmentRef ?? '') && s.passengerId !== pax?.passengerId
    );
  }

  selectSeat(seat: SeatOffer): void {
    if (seat.availability === 'sold' || this.isSeatTakenByOther(seat)) return;
    const pax = this.activePassenger();
    const seg = this.order()?.flightSegments[0];
    if (!pax || !seg) return;

    const price = this.getSeatCharge(seat);
    const updated = this.selections().filter(
      s => !(s.passengerId === pax.passengerId && s.segmentRef === seg.segmentRef)
    );
    if (!this.isSeatSelected(seat)) {
      updated.push({ passengerId: pax.passengerId, segmentRef: seg.segmentRef, seatNumber: seat.seatNumber, price, currency: seat.currency });
    }
    this.selections.set(updated);
  }

  getSeatLabel(passengerId: string): string {
    const seg = this.order()?.flightSegments[0];
    const sel = this.selections().find(s => s.passengerId === passengerId && s.segmentRef === (seg?.segmentRef ?? ''));
    return sel?.seatNumber ?? 'Not selected';
  }

  skip(): void {
    this.checkInState.setSeatSelections([]);
    this.router.navigate(['/check-in/bags']);
  }

  confirmAndContinue(): void {
    if (this.submitting()) return;
    if (this.selections().length === 0) {
      this.checkInState.setSeatSelections([]);
      this.router.navigate(['/check-in/bags']);
      return;
    }
    this.submitting.set(true);
    this.retailApi.updateSeats(
      this.checkInState.currentOrder()!.bookingReference,
      this.selections().map(s => ({ passengerId: s.passengerId, segmentId: s.segmentRef, seatNumber: s.seatNumber }))
    ).subscribe({
      next: () => {
        const paidSeats: CheckInSeatSelection[] = this.selections()
          .filter(s => s.price > 0)
          .map(s => ({ passengerId: s.passengerId, segmentId: s.segmentRef, seatNumber: s.seatNumber, seatPrice: s.price, currency: s.currency }));
        this.checkInState.setSeatSelections(paidSeats);
        this.submitting.set(false);
        this.router.navigate(['/check-in/bags']);
      },
      error: (err: { message?: string }) => {
        this.submitting.set(false);
        this.errorMessage.set(err?.message ?? 'Seat update failed. You can still proceed.');
      }
    });
  }
}
