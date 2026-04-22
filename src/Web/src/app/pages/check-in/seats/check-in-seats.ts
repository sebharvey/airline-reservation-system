import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { CheckInStateService } from '../../../services/check-in-state.service';
import { RetailApiService } from '../../../services/retail-api.service';
import { OciFlightSegment, OciPassenger } from '../../../models/order.model';
import { Seatmap, CabinSeatmap, SeatOffer, CabinCode } from '../../../models/flight.model';
import { BasketSeatSelection, CheckInSeatSelection } from '../../../models/order.model';

interface SeatmapEntry {
  segment: OciFlightSegment;
  seatmap: Seatmap | null;
  loading: boolean;
  error: string;
}

interface RowItem {
  type: 'seat' | 'spacer';
  seat?: SeatOffer | null;
}

interface SelectedSeat {
  passengerId: string;
  segmentRef: string;
  inventoryId: string;
  seatOffer: SeatOffer;
}

@Component({
  selector: 'app-check-in-seats',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './check-in-seats.html',
  styleUrl: './check-in-seats.css'
})
export class CheckInSeatsComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly checkInState = inject(CheckInStateService);
  private readonly retailApi = inject(RetailApiService);

  seatmapEntries = signal<SeatmapEntry[]>([]);
  activePassengerIndex = signal(0);
  activeFlightIndex = signal(0);
  saving = signal(false);
  saveError = signal('');

  // Key: `${passengerId}__${segmentRef}` -> SelectedSeat
  selections = signal<Map<string, SelectedSeat>>(new Map());

  readonly passengers = computed((): OciPassenger[] =>
    this.checkInState.currentOrder()?.passengers ?? []
  );

  readonly segments = computed((): OciFlightSegment[] =>
    this.checkInState.currentOrder()?.flightSegments ?? []
  );

  readonly currency = computed(() =>
    this.checkInState.currentOrder()?.currency ?? 'GBP'
  );

  readonly activePassenger = computed(() =>
    this.passengers()[this.activePassengerIndex()] ?? null
  );

  readonly activeEntry = computed(() =>
    this.seatmapEntries()[this.activeFlightIndex()] ?? null
  );

  readonly passengerLabels = computed(() =>
    this.passengers().map((p, i) => ({
      index: i,
      label: `${p.givenName} ${p.surname}`.trim() || `Passenger ${i + 1}`
    }))
  );

  readonly flightLabels = computed(() =>
    this.seatmapEntries().map((e, i) => ({
      index: i,
      label: `${e.segment.origin} → ${e.segment.destination}`
    }))
  );

  readonly totalSeatCost = computed(() => {
    let total = 0;
    for (const sel of this.selections().values()) {
      total += sel.seatOffer.price;
    }
    return total;
  });

  readonly activeCabin = computed((): CabinSeatmap | null => {
    const entry = this.activeEntry();
    if (!entry?.seatmap) return null;
    return entry.seatmap.cabins.find(c => c.cabinCode === entry.segment.cabinCode)
      ?? entry.seatmap.cabins[0]
      ?? null;
  });

  ngOnInit(): void {
    if (!this.checkInState.currentOrder()) {
      this.router.navigate(['/check-in']);
      return;
    }
    this.loadSeatmaps();
  }

  private loadSeatmaps(): void {
    const segments = this.segments();
    if (segments.length === 0) return;

    const entries: SeatmapEntry[] = segments.map(seg => ({
      segment: seg,
      seatmap: null,
      loading: true,
      error: ''
    }));
    this.seatmapEntries.set(entries);

    segments.forEach((seg, idx) => {
      this.retailApi.getFlightSeatmap(
        seg.inventoryId,
        seg.flightNumber,
        seg.aircraftType,
        seg.cabinCode as CabinCode
      ).subscribe({
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

  getSelectionKey(passengerId: string, segmentRef: string): string {
    return `${passengerId}__${segmentRef}`;
  }

  getSelectedSeat(passengerId: string, segmentRef: string): SelectedSeat | undefined {
    return this.selections().get(this.getSelectionKey(passengerId, segmentRef));
  }

  isSeatSelected(seatOffer: SeatOffer, segmentRef: string): boolean {
    const pax = this.activePassenger();
    if (!pax) return false;
    const sel = this.getSelectedSeat(pax.passengerId, segmentRef);
    return sel?.seatOffer.seatOfferId === seatOffer.seatOfferId;
  }

  isSeatTakenByOther(seatOffer: SeatOffer, segmentRef: string): boolean {
    const pax = this.activePassenger();
    if (!pax) return false;
    for (const [key, sel] of this.selections()) {
      if (key.startsWith(pax.passengerId + '__')) continue;
      if (sel.seatOffer.seatOfferId === seatOffer.seatOfferId && sel.segmentRef === segmentRef) {
        return true;
      }
    }
    return false;
  }

  getCurrentSeat(passengerId: string, segment: OciFlightSegment): string | null {
    return segment.seatAssignments.find(sa => sa.passengerId === passengerId)?.seatNumber ?? null;
  }

  selectSeat(seatOffer: SeatOffer, entry: SeatmapEntry): void {
    if (seatOffer.availability !== 'available') return;
    if (this.isSeatTakenByOther(seatOffer, entry.segment.segmentRef)) return;
    const pax = this.activePassenger();
    if (!pax) return;

    const key = this.getSelectionKey(pax.passengerId, entry.segment.segmentRef);
    const newMap = new Map(this.selections());

    if (newMap.get(key)?.seatOffer.seatOfferId === seatOffer.seatOfferId) {
      newMap.delete(key);
    } else {
      newMap.set(key, {
        passengerId: pax.passengerId,
        segmentRef: entry.segment.segmentRef,
        inventoryId: entry.segment.inventoryId,
        seatOffer
      });
    }
    this.selections.set(newMap);
  }

  getSeatRows(cabin: CabinSeatmap): number[] {
    const rows: number[] = [];
    for (let r = cabin.startRow; r <= cabin.endRow; r++) rows.push(r);
    return rows;
  }

  getHeaderItems(cabin: CabinSeatmap): { type: 'header' | 'spacer'; col?: string }[] {
    const groups = cabin.layout.split('-').map(Number);
    const items: { type: 'header' | 'spacer'; col?: string }[] = [];
    let colIdx = 0;
    for (let g = 0; g < groups.length; g++) {
      for (let i = 0; i < groups[g]; i++) {
        items.push({ type: 'header', col: cabin.columns[colIdx++] });
      }
      if (g < groups.length - 1) items.push({ type: 'spacer' });
    }
    return items;
  }

  getRowItems(cabin: CabinSeatmap, row: number): RowItem[] {
    const groups = cabin.layout.split('-').map(Number);
    const items: RowItem[] = [];
    let colIdx = 0;
    for (let g = 0; g < groups.length; g++) {
      for (let i = 0; i < groups[g]; i++) {
        const col = cabin.columns[colIdx++];
        const seat = cabin.seats.find(s => s.rowNumber === row && s.column === col) ?? null;
        items.push({ type: 'seat', seat });
      }
      if (g < groups.length - 1) items.push({ type: 'spacer' });
    }
    return items;
  }

  getSeatClass(seat: SeatOffer, segmentRef: string): string {
    if (seat.availability === 'sold') return 'seat seat-sold';
    if (seat.availability === 'held') return 'seat seat-held';
    if (this.isSeatSelected(seat, segmentRef)) return 'seat seat-selected';
    if (this.isSeatTakenByOther(seat, segmentRef)) return 'seat seat-held';
    return 'seat seat-available';
  }

  getSeatPrice(seat: SeatOffer): string {
    if (seat.price === 0) return 'Free';
    return `${seat.currency} ${seat.price.toFixed(0)}`;
  }

  onSkip(): void {
    this.checkInState.setSeatSelections([]);
    const basketId = this.checkInState.basketId();
    if (basketId) {
      this.retailApi.updateBasketSeats(basketId, []).subscribe();
    }
    this.router.navigate(['/check-in/bags']);
  }

  onContinue(): void {
    const stateSels: CheckInSeatSelection[] = [];
    const basketSels: BasketSeatSelection[] = [];

    for (const sel of this.selections().values()) {
      stateSels.push({
        passengerId: sel.passengerId,
        segmentId: sel.inventoryId,
        seatNumber: sel.seatOffer.seatNumber,
        seatPrice: sel.seatOffer.price,
        currency: sel.seatOffer.currency
      });
      basketSels.push({
        passengerId: sel.passengerId,
        segmentId: sel.inventoryId,
        basketItemRef: sel.segmentRef,
        seatOfferId: sel.seatOffer.seatOfferId,
        seatNumber: sel.seatOffer.seatNumber,
        seatPosition: sel.seatOffer.position,
        price: sel.seatOffer.price,
        tax: sel.seatOffer.tax ?? 0,
        currency: sel.seatOffer.currency
      });
    }

    this.checkInState.setSeatSelections(stateSels);

    const basketId = this.checkInState.basketId();
    this.saving.set(true);
    this.saveError.set('');

    const proceed = () => {
      this.saving.set(false);
      this.router.navigate(['/check-in/bags']);
    };

    if (basketId && basketSels.length > 0) {
      this.retailApi.updateBasketSeats(basketId, basketSels).subscribe({
        next: proceed,
        error: () => {
          this.saving.set(false);
          this.saveError.set('Failed to save seat selections. Please try again.');
        }
      });
    } else {
      proceed();
    }
  }
}
