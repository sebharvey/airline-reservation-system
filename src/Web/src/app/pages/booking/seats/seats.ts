import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { BookingStateService } from '../../../services/booking-state.service';
import { RetailApiService } from '../../../services/retail-api.service';
import { BasketFlightOffer } from '../../../models/order.model';
import { Seatmap, CabinSeatmap, SeatOffer } from '../../../models/flight.model';
import { BasketSeatSelection } from '../../../models/order.model';

interface SeatmapEntry {
  flightOffer: BasketFlightOffer;
  seatmap: Seatmap | null;
  loading: boolean;
  error: string;
}

interface SelectedSeat {
  passengerId: string;
  segmentId: string;
  basketItemRef: string;
  seatOffer: SeatOffer;
}

@Component({
  selector: 'app-seats',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './seats.html',
  styleUrl: './seats.css'
})
export class SeatsComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly bookingState = inject(BookingStateService);
  private readonly retailApi = inject(RetailApiService);

  readonly basket = this.bookingState.basket;

  seatmapEntries = signal<SeatmapEntry[]>([]);
  activePassengerIndex = signal(0);
  activeFlightIndex = signal(0);
  saving = signal(false);
  saveError = signal('');

  // Map of `${passengerId}__${basketItemId}` -> SelectedSeat
  selections = signal<Map<string, SelectedSeat>>(new Map());

  readonly passengers = computed(() => this.basket()?.passengers ?? []);

  readonly activePassenger = computed(() =>
    this.passengers()[this.activePassengerIndex()] ?? null
  );

  readonly activeEntry = computed(() =>
    this.seatmapEntries()[this.activeFlightIndex()] ?? null
  );

  readonly passengerLabels = computed(() =>
    this.passengers().map((p, i) => ({
      index: i,
      label: `${p.givenName} ${p.surname}` || (p.type === 'ADT' ? `Adult ${i + 1}` : `Child ${i + 1}`)
    }))
  );

  readonly flightLabels = computed(() =>
    this.seatmapEntries().map((e, i) => ({
      index: i,
      label: `${e.flightOffer.origin} → ${e.flightOffer.destination}`
    }))
  );

  readonly selectionCount = computed(() => this.selections().size);

  readonly activeCabin = computed((): CabinSeatmap | null => {
    const entry = this.activeEntry();
    if (!entry?.seatmap) return null;
    return entry.seatmap.cabins.find(c => c.cabinCode === entry.flightOffer.cabinCode)
      ?? entry.seatmap.cabins[0]
      ?? null;
  });

  ngOnInit(): void {
    if (!this.basket()) {
      this.router.navigate(['/']);
      return;
    }
    this.loadSeatmaps();
  }

  private loadSeatmaps(): void {
    const basket = this.basket();
    if (!basket) return;

    const entries: SeatmapEntry[] = basket.flightOffers.map(fo => ({
      flightOffer: fo,
      seatmap: null,
      loading: true,
      error: ''
    }));
    this.seatmapEntries.set(entries);

    basket.flightOffers.forEach((fo, idx) => {
      this.retailApi.getFlightSeatmap(fo.inventoryId, fo.flightNumber, fo.aircraftType, fo.cabinCode).subscribe({
        next: (seatmap) => {
          this.seatmapEntries.update(list => {
            const updated = [...list];
            updated[idx] = { ...updated[idx], seatmap, loading: false };
            return updated;
          });
        },
        error: () => {
          this.seatmapEntries.update(list => {
            const updated = [...list];
            updated[idx] = { ...updated[idx], loading: false, error: 'Failed to load seatmap' };
            return updated;
          });
        }
      });
    });
  }

  getSelectionKey(passengerId: string, basketItemId: string): string {
    return `${passengerId}__${basketItemId}`;
  }

  getSelectedSeat(passengerId: string, basketItemId: string): SelectedSeat | undefined {
    return this.selections().get(this.getSelectionKey(passengerId, basketItemId));
  }

  isSeatSelected(seatOffer: SeatOffer, basketItemId: string): boolean {
    const pax = this.activePassenger();
    if (!pax) return false;
    const sel = this.getSelectedSeat(pax.passengerId, basketItemId);
    return sel?.seatOffer.seatOfferId === seatOffer.seatOfferId;
  }

  isSeatTakenByOther(seatOffer: SeatOffer, basketItemId: string): boolean {
    for (const [key, sel] of this.selections()) {
      const pax = this.activePassenger();
      if (!pax) return false;
      if (key.startsWith(pax.passengerId)) continue;
      if (sel.seatOffer.seatOfferId === seatOffer.seatOfferId &&
          sel.basketItemRef === basketItemId) {
        return true;
      }
    }
    return false;
  }

  selectSeat(seatOffer: SeatOffer, entry: SeatmapEntry): void {
    if (seatOffer.availability !== 'available') return;
    if (this.isSeatTakenByOther(seatOffer, entry.flightOffer.basketItemId)) return;
    const pax = this.activePassenger();
    if (!pax) return;

    const key = this.getSelectionKey(pax.passengerId, entry.flightOffer.basketItemId);
    const newMap = new Map(this.selections());

    if (newMap.get(key)?.seatOffer.seatOfferId === seatOffer.seatOfferId) {
      newMap.delete(key);
    } else {
      newMap.set(key, {
        passengerId: pax.passengerId,
        segmentId: entry.flightOffer.inventoryId,
        basketItemRef: entry.flightOffer.basketItemId,
        seatOffer
      });
    }
    this.selections.set(newMap);
  }

  getSeatRows(cabin: CabinSeatmap): number[] {
    const rows: number[] = [];
    for (let r = cabin.startRow; r <= cabin.endRow; r++) {
      rows.push(r);
    }
    return rows;
  }

  getSeatForRowCol(cabin: CabinSeatmap, row: number, col: string): SeatOffer | null {
    return cabin.seats.find(s => s.rowNumber === row && s.column === col) ?? null;
  }

  getSeatClass(seat: SeatOffer | null, basketItemId: string): string {
    if (!seat) return 'seat seat-gap';
    if (seat.availability === 'sold') return 'seat seat-sold';
    if (seat.availability === 'held') return 'seat seat-held';
    if (this.isSeatSelected(seat, basketItemId)) return 'seat seat-selected';
    if (this.isSeatTakenByOther(seat, basketItemId)) return 'seat seat-other';
    return 'seat seat-available';
  }

  getSeatPrice(seat: SeatOffer, cabinCode: string): string {
    if (cabinCode === 'J' || cabinCode === 'F') return 'Incl.';
    if (seat.price === 0) return 'Free';
    return `${seat.currency} ${seat.price.toFixed(0)}`;
  }

  onSkip(): void {
    const basketId = this.basket()?.basketId;
    if (!basketId) return;
    this.saving.set(true);
    this.saveError.set('');
    this.retailApi.updateBasketSeats(basketId, []).subscribe({
      next: () => {
        this.bookingState.setSeatSelections([]);
        this.saving.set(false);
        this.router.navigate(['/booking/bags']);
      },
      error: () => {
        this.saving.set(false);
        this.saveError.set('Failed to update seat selections. Please try again.');
      }
    });
  }

  onContinue(): void {
    const basket = this.basket();
    if (!basket) return;

    const selectionsList: BasketSeatSelection[] = [];
    for (const sel of this.selections().values()) {
      selectionsList.push({
        passengerId: sel.passengerId,
        segmentId: sel.segmentId,
        basketItemRef: sel.basketItemRef,
        seatOfferId: sel.seatOffer.seatOfferId,
        seatPosition: sel.seatOffer.position,
        price: sel.seatOffer.price,
        currency: sel.seatOffer.currency
      });
    }

    this.saving.set(true);
    this.saveError.set('');
    this.retailApi.updateBasketSeats(basket.basketId, selectionsList).subscribe({
      next: () => {
        this.bookingState.setSeatSelections(selectionsList);
        this.saving.set(false);
        this.router.navigate(['/booking/bags']);
      },
      error: () => {
        this.saving.set(false);
        this.saveError.set('Failed to save seat selections. Please try again.');
      }
    });
  }

  hasAisle(layout: string, colIndex: number): boolean {
    // layout e.g. "2-4-2" - insert gap after each group
    const groups = layout.split('-').map(Number);
    let count = 0;
    for (let g = 0; g < groups.length - 1; g++) {
      count += groups[g];
      if (colIndex === count - 1) return true;
    }
    return false;
  }
}
