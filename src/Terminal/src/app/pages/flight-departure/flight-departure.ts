import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import {
  InventoryService,
  FlightInventoryGroup,
  CabinInventory,
  ManifestEntry,
  FlightManifest,
  FlightSeatmap,
  SeatmapSeat,
} from '../../services/inventory.service';

interface SeatmapRow {
  rowNumber: number;
  seats: (SeatmapSeat | null)[];
}

interface CabinGrid {
  cabinCode: string;
  cabinName: string;
  columns: string[];
  rows: SeatmapRow[];
}

@Component({
  selector: 'app-flight-departure',
  standalone: true,
  imports: [DecimalPipe],
  templateUrl: './flight-departure.html',
  styleUrl: './flight-departure.css',
})
export class FlightDepartureComponent implements OnInit {
  #inventoryService = inject(InventoryService);

  readonly today = new Date().toISOString().slice(0, 10);
  readonly todayDisplay = new Date().toLocaleDateString('en-GB', {
    weekday: 'long',
    day: 'numeric',
    month: 'long',
    year: 'numeric',
  });

  flights = signal<FlightInventoryGroup[]>([]);
  loading = signal(false);
  error = signal('');
  loaded = signal(false);

  // Selected flight detail panel
  selectedFlight = signal<FlightInventoryGroup | null>(null);
  manifest = signal<FlightManifest | null>(null);
  seatmap = signal<FlightSeatmap | null>(null);
  detailLoading = signal(false);
  detailError = signal('');

  cabinGrids = computed<CabinGrid[]>(() => {
    const sm = this.seatmap();
    if (!sm) return [];
    return sm.cabins.map(cabin => {
      const rowMap = new Map<number, Map<string, SeatmapSeat>>();
      for (const seat of cabin.seats) {
        if (!rowMap.has(seat.rowNumber)) rowMap.set(seat.rowNumber, new Map());
        rowMap.get(seat.rowNumber)!.set(seat.column, seat);
      }
      const rowNumbers = [...rowMap.keys()].sort((a, b) => a - b);
      const rows: SeatmapRow[] = rowNumbers.map(rn => ({
        rowNumber: rn,
        seats: cabin.columns.map(col => rowMap.get(rn)?.get(col) ?? null),
      }));
      return { cabinCode: cabin.cabinCode, cabinName: cabin.cabinName, columns: cabin.columns, rows };
    });
  });

  stats = computed(() => {
    const all = this.flights();
    const pax = all.reduce((s, f) => s + (f.totalSeats - f.totalSeatsAvailable), 0);
    return {
      total: all.length,
      active: all.filter(f => f.status === 'Active').length,
      cancelled: all.filter(f => f.status === 'Cancelled').length,
      pax,
    };
  });

  manifestStats = computed(() => {
    const m = this.manifest();
    if (!m) return null;
    const checkedIn = m.entries.filter(e => e.checkedIn).length;
    return { total: m.entries.length, checkedIn };
  });

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#inventoryService.getFlightInventory(this.today);
      this.flights.set(result);
      this.loaded.set(true);
    } catch {
      this.error.set("Failed to load today's flights. Please try again.");
    } finally {
      this.loading.set(false);
    }
  }

  async selectFlight(flight: FlightInventoryGroup): Promise<void> {
    const current = this.selectedFlight();
    if (current?.inventoryId === flight.inventoryId) {
      // Toggle off
      this.selectedFlight.set(null);
      this.manifest.set(null);
      this.seatmap.set(null);
      return;
    }

    this.selectedFlight.set(flight);
    this.manifest.set(null);
    this.seatmap.set(null);
    this.detailLoading.set(true);
    this.detailError.set('');

    try {
      const [manifest, seatmap] = await Promise.all([
        this.#inventoryService.getFlightManifest(flight.flightNumber, flight.departureDate),
        this.#inventoryService.getFlightSeatmap(flight.inventoryId, flight.flightNumber, flight.aircraftType),
      ]);
      this.manifest.set(manifest);
      this.seatmap.set(seatmap);
    } catch {
      this.detailError.set('Failed to load flight details. Please try again.');
    } finally {
      this.detailLoading.set(false);
    }
  }

  closeDetail(): void {
    this.selectedFlight.set(null);
    this.manifest.set(null);
    this.seatmap.set(null);
    this.detailError.set('');
  }

  statusClass(status: string): string {
    if (status === 'Active') return 'badge-active';
    if (status === 'Ticketing Closed') return 'badge-warning';
    return 'badge-inactive';
  }

  loadBarClass(loadFactor: number): string {
    if (loadFactor >= 90) return 'bar-critical';
    if (loadFactor >= 70) return 'bar-high';
    if (loadFactor >= 40) return 'bar-medium';
    return 'bar-low';
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

  paxOnBoard(flight: FlightInventoryGroup): number {
    return flight.totalSeats - flight.totalSeatsAvailable;
  }

  seatClass(seat: SeatmapSeat | null, manifest: FlightManifest | null): string {
    if (!seat) return 'seat-gap';
    const occupied = manifest?.entries.find(e => e.seatNumber === seat.seatNumber);
    if (occupied) return occupied.checkedIn ? 'seat-checked-in' : 'seat-booked';
    if (seat.availability === 'held') return 'seat-held';
    if (seat.availability === 'sold') return 'seat-booked';
    return 'seat-open';
  }

  seatTooltip(seat: SeatmapSeat | null, manifest: FlightManifest | null): string {
    if (!seat) return '';
    const entry = manifest?.entries.find(e => e.seatNumber === seat.seatNumber);
    if (entry) {
      const name = `${entry.givenName} ${entry.surname}`.trim();
      const ci = entry.checkedIn ? ' ✓ Checked in' : '';
      return `${seat.seatNumber} — ${name} (${entry.bookingReference})${ci}`;
    }
    return `${seat.seatNumber} — ${seat.availability}`;
  }

  cabinLabel(code: string): string {
    switch (code) {
      case 'F': return 'First';
      case 'J': return 'Business';
      case 'W': return 'Premium Economy';
      case 'Y': return 'Economy';
      default:  return code;
    }
  }

  isAisle(column: string, columns: string[]): boolean {
    // Mark the gap before and after middle columns as aisle separators.
    // Typical layouts: ABC_DEF, AB_DE, ABCD_EF_GHIJ etc.
    // We detect the gap by looking for a jump > 1 in alphabetic order.
    const idx = columns.indexOf(column);
    if (idx <= 0) return false;
    const prev = columns[idx - 1];
    return column.charCodeAt(0) - prev.charCodeAt(0) > 1;
  }
}
