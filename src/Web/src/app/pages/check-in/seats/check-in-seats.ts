import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { CheckInStateService, CheckInSeatSelection } from '../../../services/check-in-state.service';
import { Order } from '../../../models/order.model';
import { Seatmap, CabinSeatmap, SeatOffer } from '../../../models/flight.model';

interface SeatSelection {
  passengerId: string;
  segmentId: string;
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
  order = signal<Order | null>(null);
  seatmap = signal<Seatmap | null>(null);
  loading = signal(true);
  seatmapLoading = signal(false);
  submitting = signal(false);
  errorMessage = signal('');

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');
  passengerIds = signal<string[]>([]);

  activePassengerIndex = signal(0);
  selections = signal<SeatSelection[]>([]);

  readonly checkedInPassengers = computed(() => {
    const o = this.order();
    const ids = this.passengerIds();
    if (!o) return [];
    return o.passengers.filter(p => ids.includes(p.passengerId));
  });

  readonly activePassenger = computed(() => {
    return this.checkedInPassengers()[this.activePassengerIndex()] ?? null;
  });

  readonly activeCabin = computed((): CabinSeatmap | null => {
    const sm = this.seatmap();
    const o = this.order();
    if (!sm || !o) return null;
    const seg = o.flightSegments[0];
    if (!seg) return null;
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

  /** True if seats are always free for this cabin (J/F fare includes seat selection). */
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
    private route: ActivatedRoute,
    private router: Router,
    private retailApi: RetailApiService,
    private checkInState: CheckInStateService
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const ref = params['bookingRef'] ?? '';
      const gn = params['givenName'] ?? '';
      const sn = params['surname'] ?? '';
      const paxIds = (params['passengerIds'] ?? '').split(',').filter(Boolean);
      this.bookingRef.set(ref);
      this.givenName.set(gn);
      this.surname.set(sn);
      this.passengerIds.set(paxIds);

      if (!ref) {
        this.router.navigate(['/check-in']);
        return;
      }
      this.loadOrder(ref, gn, sn);
    });
  }

  private loadOrder(ref: string, gn: string, sn: string): void {
    this.loading.set(true);
    this.retailApi.retrieveForCheckIn({ bookingReference: ref, givenName: gn, surname: sn }).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
        const seg = order.flightSegments[0];
        if (seg) {
          this.loadSeatmap(seg.segmentId, seg.flightNumber, seg.cabinCode);
        }
      },
      error: (err: { message?: string }) => {
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking.');
        this.loading.set(false);
      }
    });
  }

  private loadSeatmap(flightId: string, flightNumber: string, cabinCode: string): void {
    this.seatmapLoading.set(true);
    this.retailApi.getFlightSeatmap(flightId, flightNumber, cabinCode as 'F' | 'J' | 'W' | 'Y').subscribe({
      next: (sm) => {
        this.seatmap.set(sm);
        this.seatmapLoading.set(false);
      },
      error: () => {
        this.seatmapLoading.set(false);
      }
    });
  }

  /**
   * Returns the charge for selecting a given seat for the active passenger.
   * Free if: J/F cabin, OR passenger already has a purchased Seat orderItem.
   */
  getSeatCharge(passengerId: string, seat: SeatOffer): number {
    const o = this.order();
    const seg = o?.flightSegments[0];
    if (!seg) return 0;
    if (seg.cabinCode === 'J' || seg.cabinCode === 'F') return 0;
    // Already paid for seat selection — change is free
    const hasPaidSeat = o?.orderItems.some(
      oi => oi.type === 'Seat' &&
            oi.segmentRef === seg.segmentId &&
            oi.passengerRefs.includes(passengerId)
    );
    if (hasPaidSeat) return 0;
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
      s => s.passengerId === pax.passengerId && s.segmentId === seg.segmentId && s.seatNumber === seat.seatNumber
    );
  }

  isSeatTakenByOther(seat: SeatOffer): boolean {
    const pax = this.activePassenger();
    const seg = this.order()?.flightSegments[0];
    return this.selections().some(
      s => s.seatNumber === seat.seatNumber && s.segmentId === (seg?.segmentId ?? '') && s.passengerId !== pax?.passengerId
    );
  }

  selectSeat(seat: SeatOffer): void {
    if (seat.availability === 'sold' || this.isSeatTakenByOther(seat)) return;
    const pax = this.activePassenger();
    const seg = this.order()?.flightSegments[0];
    if (!pax || !seg) return;

    const price = this.getSeatCharge(pax.passengerId, seat);
    const updated = this.selections().filter(
      s => !(s.passengerId === pax.passengerId && s.segmentId === seg.segmentId)
    );
    if (!this.isSeatSelected(seat)) {
      updated.push({
        passengerId: pax.passengerId,
        segmentId: seg.segmentId,
        seatNumber: seat.seatNumber,
        price,
        currency: seat.currency
      });
    }
    this.selections.set(updated);
  }

  getSeatLabel(passengerId: string): string {
    const seg = this.order()?.flightSegments[0];
    const sel = this.selections().find(s => s.passengerId === passengerId && s.segmentId === (seg?.segmentId ?? ''));
    return sel?.seatNumber ?? 'Not selected';
  }

  skip(): void {
    this.checkInState.setSeatSelections([]);
    this.navigateNext();
  }

  confirmAndContinue(): void {
    if (this.submitting()) return;
    if (this.selections().length === 0) {
      this.checkInState.setSeatSelections([]);
      this.navigateNext();
      return;
    }
    this.submitting.set(true);
    this.retailApi.updateSeats(this.bookingRef(), this.selections()).subscribe({
      next: () => {
        // Save paid seat selections to state
        const paidSeats: CheckInSeatSelection[] = this.selections()
          .filter(s => s.price > 0)
          .map(s => ({
            passengerId: s.passengerId,
            segmentId: s.segmentId,
            seatNumber: s.seatNumber,
            seatPrice: s.price,
            currency: s.currency
          }));
        this.checkInState.setSeatSelections(paidSeats);
        this.submitting.set(false);
        this.navigateNext();
      },
      error: (err: { message?: string }) => {
        this.submitting.set(false);
        this.errorMessage.set(err?.message ?? 'Seat update failed. You can still proceed.');
      }
    });
  }

  private navigateNext(): void {
    const queryParams = {
      bookingRef: this.bookingRef(),
      givenName: this.givenName(),
      surname: this.surname(),
      passengerIds: this.passengerIds().join(',')
    };
    if (this.checkInState.totalPaymentAmount() > 0) {
      this.router.navigate(['/check-in/payment'], { queryParams });
    } else {
      this.router.navigate(['/check-in/boarding-pass'], { queryParams });
    }
  }
}
