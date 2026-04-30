import { Component, OnInit, signal, computed, inject, effect } from '@angular/core';
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

interface SeatSlot {
  slotIndex: number;
  passengerIndex: number;
  flightIndex: number;
  passengerId: string;
  passengerName: string;
  basketItemId: string;
  flightLabel: string;
  shortFlightLabel: string;
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
  activeSlotIndex = signal(0);
  saving = signal(false);
  saveError = signal('');
  justSelected = signal(false);

  // Map of `${passengerId}__${basketItemId}` -> SelectedSeat
  selections = signal<Map<string, SelectedSeat>>(new Map());

  readonly passengers = computed(() => this.basket()?.passengers ?? []);

  // Ordered slots: for each flight, all passengers (segment-first order)
  readonly slots = computed((): SeatSlot[] => {
    const basket = this.basket();
    const entries = this.seatmapEntries();
    if (!basket || entries.length === 0) return [];
    const result: SeatSlot[] = [];
    entries.forEach((entry, fi) => {
      basket.passengers.forEach((pax, pi) => {
        result.push({
          slotIndex: result.length,
          passengerIndex: pi,
          flightIndex: fi,
          passengerId: pax.passengerId,
          passengerName: pax.givenName ? `${pax.givenName} ${pax.surname}`.trim() : (pax.type === 'ADT' ? `Adult ${pi + 1}` : `Child ${pi + 1}`),
          basketItemId: entry.flightOffer.basketItemId,
          flightLabel: `${entry.flightOffer.flightNumber} · ${entry.flightOffer.origin} → ${entry.flightOffer.destination}`,
          shortFlightLabel: `${entry.flightOffer.origin}→${entry.flightOffer.destination}`
        });
      });
    });
    return result;
  });

  readonly currentSlot = computed(() => this.slots()[this.activeSlotIndex()] ?? null);

  readonly currentEntry = computed(() => {
    const slot = this.currentSlot();
    if (!slot) return null;
    return this.seatmapEntries()[slot.flightIndex] ?? null;
  });

  readonly activeCabin = computed((): CabinSeatmap | null => {
    const entry = this.currentEntry();
    if (!entry?.seatmap) return null;
    return entry.seatmap.cabins.find(c => c.cabinCode === entry.flightOffer.cabinCode)
      ?? entry.seatmap.cabins[0]
      ?? null;
  });

  readonly completedCount = computed(() => this.selections().size);
  readonly totalSlots = computed(() => this.slots().length);

  readonly progressPercent = computed(() => {
    const total = this.totalSlots();
    return total ? Math.round((this.completedCount() / total) * 100) : 0;
  });

  readonly currency = computed(() => this.basket()?.currency ?? 'GBP');

  readonly totalSeatCost = computed(() => {
    let total = 0;
    for (const sel of this.selections().values()) {
      total += sel.seatOffer.price ?? 0;
    }
    return total;
  });

  readonly isFirstSlot = computed(() => this.activeSlotIndex() === 0);
  readonly isLastSlot = computed(() => this.activeSlotIndex() === this.totalSlots() - 1);
  readonly allSlotsComplete = computed(() => this.completedCount() === this.totalSlots() && this.totalSlots() > 0);

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

  getSlotSelection(slot: SeatSlot): SelectedSeat | undefined {
    return this.selections().get(this.getSelectionKey(slot.passengerId, slot.basketItemId));
  }

  getSlotStatus(slot: SeatSlot): 'current' | 'done' | 'pending' {
    if (slot.slotIndex === this.activeSlotIndex()) return 'current';
    return this.getSlotSelection(slot) ? 'done' : 'pending';
  }

  isSeatSelectedForCurrentSlot(seatOffer: SeatOffer): boolean {
    const slot = this.currentSlot();
    if (!slot) return false;
    const sel = this.selections().get(this.getSelectionKey(slot.passengerId, slot.basketItemId));
    return sel?.seatOffer.seatOfferId === seatOffer.seatOfferId;
  }

  isSeatTakenByOther(seatOffer: SeatOffer, basketItemId: string): boolean {
    const slot = this.currentSlot();
    if (!slot) return false;
    for (const [key, sel] of this.selections()) {
      if (key.startsWith(slot.passengerId)) continue;
      if (sel.seatOffer.seatOfferId === seatOffer.seatOfferId && sel.basketItemRef === basketItemId) {
        return true;
      }
    }
    return false;
  }

  getOtherPassengerSeat(seatOffer: SeatOffer, basketItemId: string): string | null {
    const currentSlot = this.currentSlot();
    for (const [key, sel] of this.selections()) {
      if (currentSlot && key.startsWith(currentSlot.passengerId)) continue;
      if (sel.seatOffer.seatOfferId === seatOffer.seatOfferId && sel.basketItemRef === basketItemId) {
        // Find which passenger this is
        const paxId = key.split('__')[0];
        const slot = this.slots().find(s => s.passengerId === paxId);
        return slot?.passengerName ?? null;
      }
    }
    return null;
  }

  selectSeat(seatOffer: SeatOffer, entry: SeatmapEntry): void {
    if (seatOffer.availability !== 'available') return;
    if (this.isSeatTakenByOther(seatOffer, entry.flightOffer.basketItemId)) return;
    const slot = this.currentSlot();
    if (!slot) return;

    const key = this.getSelectionKey(slot.passengerId, entry.flightOffer.basketItemId);
    const newMap = new Map(this.selections());

    if (newMap.get(key)?.seatOffer.seatOfferId === seatOffer.seatOfferId) {
      // Deselect
      newMap.delete(key);
      this.selections.set(newMap);
    } else {
      newMap.set(key, {
        passengerId: slot.passengerId,
        segmentId: entry.flightOffer.inventoryId,
        basketItemRef: entry.flightOffer.basketItemId,
        seatOffer
      });
      this.selections.set(newMap);
      // Auto-advance to next incomplete slot after a short pause
      this.justSelected.set(true);
      setTimeout(() => {
        this.justSelected.set(false);
        this.advanceToNextPending();
      }, 500);
    }
  }

  private advanceToNextPending(): void {
    const slots = this.slots();
    const currentIdx = this.activeSlotIndex();
    // Look forward first
    for (let i = currentIdx + 1; i < slots.length; i++) {
      if (!this.getSlotSelection(slots[i])) {
        this.activeSlotIndex.set(i);
        return;
      }
    }
    // If all forward are filled, find first unfilled from start
    for (let i = 0; i < currentIdx; i++) {
      if (!this.getSlotSelection(slots[i])) {
        this.activeSlotIndex.set(i);
        return;
      }
    }
    // All filled — stay on last slot
  }

  goToSlot(index: number): void {
    this.activeSlotIndex.set(index);
  }

  goPrev(): void {
    if (!this.isFirstSlot()) {
      this.activeSlotIndex.update(i => i - 1);
    }
  }

  goNext(): void {
    if (!this.isLastSlot()) {
      this.activeSlotIndex.update(i => i + 1);
    }
  }

  getSeatRows(cabin: CabinSeatmap): number[] {
    const rows: number[] = [];
    for (let r = cabin.startRow; r <= cabin.endRow; r++) rows.push(r);
    return rows;
  }

  getSeatForRowCol(cabin: CabinSeatmap, row: number, col: string): SeatOffer | null {
    return cabin.seats.find(s => s.rowNumber === row && s.column === col) ?? null;
  }

  getSeatClass(seat: SeatOffer | null, basketItemId: string): string {
    if (!seat) return 'seat seat-gap';
    if (seat.availability === 'sold') return 'seat seat-sold';
    if (seat.availability === 'held') return 'seat seat-held';
    if (this.isSeatSelectedForCurrentSlot(seat)) return 'seat seat-selected';
    if (this.isSeatTakenByOther(seat, basketItemId)) return 'seat seat-other';
    return 'seat seat-available';
  }

  getSeatPrice(seat: SeatOffer, cabinCode: string): string {
    if (cabinCode === 'J' || cabinCode === 'F') return 'Incl.';
    if (seat.price === 0) return 'Free';
    return `+${seat.price.toFixed(0)}`;
  }

  hasAisle(layout: string, colIndex: number): boolean {
    const groups = layout.split('-').map(Number);
    let count = 0;
    for (let g = 0; g < groups.length - 1; g++) {
      count += groups[g];
      if (colIndex === count - 1) return true;
    }
    return false;
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
        seatNumber: sel.seatOffer.seatNumber,
        seatPosition: sel.seatOffer.position,
        price: sel.seatOffer.price,
        tax: sel.seatOffer.tax ?? 0,
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
}
