import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { Order, Passenger } from '../../../models/order.model';
import { Seatmap, CabinSeatmap, SeatOffer } from '../../../models/flight.model';

interface SeatSelection {
  passengerId: string;
  segmentId: string;
  seatNumber: string;
}

@Component({
  selector: 'app-manage-seat',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './manage-seat.html',
  styleUrl: './manage-seat.css'
})
export class ManageSeatComponent implements OnInit {
  order = signal<Order | null>(null);
  seatmap = signal<Seatmap | null>(null);
  loading = signal(true);
  seatmapLoading = signal(false);
  errorMessage = signal('');
  successMessage = signal('');
  submitting = signal(false);

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  activePassengerIndex = signal(0);
  selections = signal<SeatSelection[]>([]);

  readonly activeCabin = computed((): CabinSeatmap | null => {
    const sm = this.seatmap();
    const o = this.order();
    if (!sm || !o) return null;
    const seg = o.flightSegments[0];
    if (!seg) return null;
    return sm.cabins.find(c => c.cabinCode === seg.cabinCode) ?? sm.cabins[0] ?? null;
  });

  readonly activePassenger = computed((): Passenger | null => {
    const o = this.order();
    if (!o) return null;
    return o.passengers[this.activePassengerIndex()] ?? null;
  });

  readonly totalSeatCost = computed((): number => {
    const sm = this.seatmap();
    const cabin = this.activeCabin();
    if (!sm || !cabin) return 0;
    return this.selections().reduce((sum, sel) => {
      const seat = cabin.seats.find(s => s.seatNumber === sel.seatNumber);
      return sum + (seat?.price ?? 0);
    }, 0);
  });

  readonly currency = computed((): string => {
    const cabin = this.activeCabin();
    if (!cabin) return 'GBP';
    return cabin.seats.find(s => s.price > 0)?.currency ?? 'GBP';
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

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private retailApi: RetailApiService
  ) {}

  ngOnInit(): void {
    const navState = (this.router.getCurrentNavigation()?.extras.state ?? history.state) as Record<string, string>;
    const gn = navState?.['givenName'] ?? '';
    const sn = navState?.['surname'] ?? '';

    this.route.queryParams.subscribe(params => {
      const ref = params['bookingRef'] ?? '';
      this.bookingRef.set(ref);
      this.givenName.set(gn);
      this.surname.set(sn);

      if (!ref || !gn || !sn) {
        this.router.navigate(['/manage-booking']);
        return;
      }
      this.loadOrder(ref, gn, sn);
    });
  }

  private loadOrder(ref: string, gn: string, sn: string): void {
    this.loading.set(true);
    this.retailApi.retrieveOrder({ bookingReference: ref, givenName: gn, surname: sn }).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
        const seg = order.flightSegments[0];
        if (seg) {
          this.loadSeatmap(seg.segmentId, seg.flightNumber, seg.cabinCode);
        }
        // Pre-populate existing seat selections
        const existing: SeatSelection[] = [];
        for (const pax of order.passengers) {
          for (const seg2 of order.flightSegments) {
            const flightItem = order.orderItems.find(
              oi => oi.type === 'Flight' && oi.segmentRef === seg2.segmentId && oi.passengerRefs.includes(pax.passengerId)
            );
            const seat = flightItem?.seatAssignments?.find(s => s.passengerId === pax.passengerId);
            if (seat) {
              existing.push({ passengerId: pax.passengerId, segmentId: seg2.segmentId, seatNumber: seat.seatNumber });
            }
          }
        }
        this.selections.set(existing);
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

  selectPassenger(index: number): void {
    this.activePassengerIndex.set(index);
  }

  isSeatSelected(seat: SeatOffer): boolean {
    const pax = this.activePassenger();
    if (!pax) return false;
    const seg = this.order()?.flightSegments[0];
    return this.selections().some(
      s => s.passengerId === pax.passengerId && s.segmentId === (seg?.segmentId ?? '') && s.seatNumber === seat.seatNumber
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

    const updated = this.selections().filter(
      s => !(s.passengerId === pax.passengerId && s.segmentId === seg.segmentId)
    );
    if (!this.isSeatSelected(seat)) {
      updated.push({ passengerId: pax.passengerId, segmentId: seg.segmentId, seatNumber: seat.seatNumber });
    }
    this.selections.set(updated);
  }

  getSeatLabel(passengerId: string): string {
    const seg = this.order()?.flightSegments[0];
    const sel = this.selections().find(s => s.passengerId === passengerId && s.segmentId === (seg?.segmentId ?? ''));
    return sel?.seatNumber ?? 'Not selected';
  }

  confirmSelection(): void {
    if (this.submitting()) return;
    this.submitting.set(true);
    this.errorMessage.set('');
    this.retailApi.updateSeats(this.bookingRef(), this.selections()).subscribe({
      next: (res) => {
        this.submitting.set(false);
        if (res.success) {
          this.successMessage.set('Seat selection updated successfully!');
        } else {
          this.errorMessage.set('Seat update failed. Please try again.');
        }
      },
      error: (err: { message?: string }) => {
        this.submitting.set(false);
        this.errorMessage.set(err?.message ?? 'Seat update failed. Please try again.');
      }
    });
  }

  formatCurrency(amount: number, currency: string): string {
    return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(amount);
  }

  get detailQueryParams() {
    return { bookingRef: this.bookingRef() };
  }

  get detailState() {
    return { givenName: this.givenName(), surname: this.surname() };
  }
}
