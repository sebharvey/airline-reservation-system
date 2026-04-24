import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { InventoryService, FlightInventoryGroup, CabinInventory, InventoryHold, CabinSeatmap, SeatmapSeat, FlightSeatmap, DisruptionCancelResponse } from '../../services/inventory.service';

@Component({
  selector: 'app-inventory',
  templateUrl: './inventory.html',
  styleUrl: './inventory.css',
  imports: [FormsModule, RouterLink],
})
export class InventoryComponent implements OnInit {
  #inventoryService = inject(InventoryService);
  #router = inject(Router);

  flights = signal<FlightInventoryGroup[]>([]);
  selectedDate = signal(this.#inventoryService.lastSelectedDate);
  loading = signal(false);
  error = signal('');
  loaded = signal(false);

  stats = computed(() => {
    const all = this.flights();
    const totalFlights = all.length;
    const totalSeats = all.reduce((s, f) => s + f.totalSeats, 0);
    const totalAvailable = all.reduce((s, f) => s + f.totalSeatsAvailable, 0);
    const avgLoad = totalSeats > 0
      ? Math.round((totalSeats - totalAvailable) / totalSeats * 100)
      : 0;
    const cancelled = all.filter(f => f.status === 'Cancelled').length;
    return { totalFlights, totalSeats, totalAvailable, avgLoad, cancelled };
  });

  async ngOnInit(): Promise<void> {
    await this.loadInventory();
  }

  async loadInventory(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#inventoryService.getFlightInventory(this.selectedDate());
      this.flights.set(result);
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load flight inventory. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  onDateChange(val: string): void {
    this.selectedDate.set(val);
    this.#inventoryService.lastSelectedDate = val;
    this.loadInventory();
  }

  changeDay(offset: number): void {
    const d = new Date(this.selectedDate() + 'T00:00:00Z');
    d.setUTCDate(d.getUTCDate() + offset);
    const next = d.toISOString().slice(0, 10);
    this.selectedDate.set(next);
    this.#inventoryService.lastSelectedDate = next;
    this.loadInventory();
  }

  cabinDisplay(cabin: CabinInventory | null): string {
    if (!cabin || cabin.totalSeats === 0) return '—';
    return `${cabin.seatsAvailable}/${cabin.totalSeats}`;
  }

  cabinClass(cabin: CabinInventory | null): string {
    if (!cabin || cabin.totalSeats === 0) return 'cabin-empty';
    const pct = cabin.seatsAvailable / cabin.totalSeats;
    if (pct <= 0.1) return 'cabin-critical';
    if (pct <= 0.3) return 'cabin-low';
    return 'cabin-ok';
  }

  availPct(flight: FlightInventoryGroup): number {
    if (flight.totalSeats === 0) return 0;
    return Math.round(flight.totalSeatsAvailable / flight.totalSeats * 100);
  }

  loadBarClass(loadFactor: number): string {
    if (loadFactor >= 90) return 'bar-critical';
    if (loadFactor >= 70) return 'bar-high';
    if (loadFactor >= 40) return 'bar-medium';
    return 'bar-low';
  }

  statusClass(status: string): string {
    if (status === 'Active') return 'badge-active';
    if (status === 'Ticketing Closed') return 'badge-warning';
    return 'badge-inactive';
  }

  // Holds modal
  holdsModalFlight = signal<FlightInventoryGroup | null>(null);
  holds = signal<InventoryHold[]>([]);
  holdsLoading = signal(false);
  holdsError = signal('');
  holdsTab = signal<'confirmed' | 'standby' | 'seatmap'>('confirmed');
  copiedHoldRef = signal<string | null>(null);

  // Holds filters
  holdsFilterRef = signal('');
  holdsFilterName = signal('');
  holdsFilterCabin = signal('');
  holdsFilterSeat = signal('');

  holdsFiltersActive = computed(() =>
    !!this.holdsFilterRef() || !!this.holdsFilterName() || !!this.holdsFilterCabin() || !!this.holdsFilterSeat()
  );

  clearHoldsFilters(): void {
    this.holdsFilterRef.set('');
    this.holdsFilterName.set('');
    this.holdsFilterCabin.set('');
    this.holdsFilterSeat.set('');
  }

  copyBookingReference(ref: string, event: Event): void {
    event.stopPropagation();
    navigator.clipboard.writeText(ref).then(() => {
      this.copiedHoldRef.set(ref);
      setTimeout(() => this.copiedHoldRef.set(null), 2000);
    });
  }

  confirmedHolds = computed(() => {
    const ref = this.holdsFilterRef().toLowerCase();
    const name = this.holdsFilterName().toLowerCase();
    const cabin = this.holdsFilterCabin();
    const seat = this.holdsFilterSeat().toLowerCase();
    return this.holds()
      .filter(h => h.holdType === 'Revenue')
      .filter(h => !ref || (h.bookingReference?.toLowerCase().includes(ref) ?? false))
      .filter(h => !name || (h.passengerName?.toLowerCase().includes(name) ?? false))
      .filter(h => !cabin || h.cabinCode === cabin)
      .filter(h => !seat || (h.seatNumber?.toLowerCase().includes(seat) ?? false));
  });

  standbyHolds = computed(() => {
    const ref = this.holdsFilterRef().toLowerCase();
    const name = this.holdsFilterName().toLowerCase();
    const cabin = this.holdsFilterCabin();
    const seat = this.holdsFilterSeat().toLowerCase();
    return this.holds()
      .filter(h => h.holdType === 'Standby')
      .filter(h => !ref || (h.bookingReference?.toLowerCase().includes(ref) ?? false))
      .filter(h => !name || (h.passengerName?.toLowerCase().includes(name) ?? false))
      .filter(h => !cabin || h.cabinCode === cabin)
      .filter(h => !seat || (h.seatNumber?.toLowerCase().includes(seat) ?? false))
      .sort((a, b) => (b.standbyPriority ?? 0) - (a.standbyPriority ?? 0));
  });

  // Seat map tab
  seatmap = signal<FlightSeatmap | null>(null);
  seatmapLoading = signal(false);
  seatmapError = signal('');

  async openHoldsModal(flight: FlightInventoryGroup): Promise<void> {
    this.holdsModalFlight.set(flight);
    this.holds.set([]);
    this.holdsError.set('');
    this.holdsLoading.set(true);
    this.holdsTab.set('confirmed');
    this.seatmap.set(null);
    this.seatmapError.set('');
    this.clearHoldsFilters();
    try {
      const result = await this.#inventoryService.getInventoryHolds(flight.inventoryId);
      this.holds.set(result);
    } catch {
      this.holdsError.set('Failed to load holds. Please try again.');
    } finally {
      this.holdsLoading.set(false);
    }
  }

  async switchToSeatmap(): Promise<void> {
    this.holdsTab.set('seatmap');
    if (this.seatmap() || this.seatmapLoading()) return;
    const flight = this.holdsModalFlight();
    if (!flight) return;
    this.seatmapLoading.set(true);
    this.seatmapError.set('');
    try {
      const result = await this.#inventoryService.getFlightSeatmap(
        flight.inventoryId, flight.flightNumber, flight.aircraftType
      );
      this.seatmap.set(result);
    } catch {
      this.seatmapError.set('Failed to load seat map. Please try again.');
    } finally {
      this.seatmapLoading.set(false);
    }
  }

  closeHoldsModal(): void {
    this.holdsModalFlight.set(null);
  }

  // Disruption modal
  disruptionModalFlight = signal<FlightInventoryGroup | null>(null);
  disruptionStep = signal<'action' | 'confirm'>('action');
  disruptionLoading = signal(false);
  disruptionError = signal('');

  // Any active flight (non-cancelled) can show the disruption action menu.
  // Cancelled flights navigate directly to the IROPS rebooking page.
  canDisrupt(flight: FlightInventoryGroup): boolean {
    return flight.status !== 'Ticketing Closed';
  }

  openDisruptionModal(flight: FlightInventoryGroup): void {
    if (flight.status === 'Cancelled') {
      this.#router.navigate(['/disruption', flight.flightNumber, flight.departureDate]);
      return;
    }
    this.disruptionModalFlight.set(flight);
    this.disruptionStep.set('action');
    this.disruptionError.set('');
  }

  closeDisruptionModal(): void {
    if (this.disruptionLoading()) return;
    this.disruptionModalFlight.set(null);
  }

  startCancellationConfirm(): void {
    this.disruptionStep.set('confirm');
    this.disruptionError.set('');
  }

  async executeCancellation(): Promise<void> {
    const flight = this.disruptionModalFlight();
    if (!flight) return;
    this.disruptionLoading.set(true);
    this.disruptionError.set('');
    try {
      await this.#inventoryService.cancelFlightInventoryOnly(
        flight.flightNumber,
        flight.departureDate
      );
      this.disruptionModalFlight.set(null);
      await this.#router.navigate(['/disruption', flight.flightNumber, flight.departureDate]);
    } catch {
      this.disruptionError.set('Failed to cancel flight. Please try again.');
    } finally {
      this.disruptionLoading.set(false);
    }
  }

  holdStatusClass(status: string): string {
    return status === 'Confirmed' ? 'badge-active' : 'badge-held';
  }

  formatHoldDate(iso: string): string {
    return iso.slice(0, 16).replace('T', ' ');
  }

  getSeatRows(cabin: CabinSeatmap): number[] {
    const rows: number[] = [];
    for (let r = cabin.startRow; r <= cabin.endRow; r++) rows.push(r);
    return rows;
  }

  getSeatForRowCol(cabin: CabinSeatmap, row: number, col: string): SeatmapSeat | null {
    return cabin.seats.find(s => s.rowNumber === row && s.column === col) ?? null;
  }

  seatClass(seat: SeatmapSeat | null): string {
    if (!seat) return 'sm-seat sm-seat-gap';
    if (seat.availability === 'sold') return 'sm-seat sm-seat-sold';
    if (seat.availability === 'held') return 'sm-seat sm-seat-held';
    return 'sm-seat sm-seat-available';
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

  #todayIso(): string {
    return new Date().toISOString().slice(0, 10);
  }
}
