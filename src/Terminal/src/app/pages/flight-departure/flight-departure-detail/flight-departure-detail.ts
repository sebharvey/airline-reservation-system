import { LucideAngularModule } from 'lucide-angular';
import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import {
  InventoryService,
  FlightInventoryGroup,
  FlightManifest,
  FlightSeatmap,
  ManifestEntry,
  SeatmapSeat,
} from '../../../services/inventory.service';

interface CabinGrid {
  cabinCode: string;
  cabinName: string;
  columns: string[];
  rows: { rowNumber: number; seats: (SeatmapSeat | null)[] }[];
}

interface EntryGroup {
  bookingReference: string;
  entries: ManifestEntry[];
}

@Component({
  selector: 'app-flight-departure-detail',
  standalone: true,
  imports: [LucideAngularModule, RouterLink],
  templateUrl: './flight-departure-detail.html',
  styleUrl: './flight-departure-detail.css',
})
export class FlightDepartureDetailComponent implements OnInit {
  #route = inject(ActivatedRoute);
  #router = inject(Router);
  #inventoryService = inject(InventoryService);

  flight = signal<FlightInventoryGroup | null>(null);
  manifest = signal<FlightManifest | null>(null);
  seatmap = signal<FlightSeatmap | null>(null);
  loading = signal(true);
  error = signal('');
  selectedEntry = signal<ManifestEntry | null>(null);

  manifestStats = computed(() => {
    const m = this.manifest();
    if (!m) return null;
    return {
      total: m.entries.length,
      checkedIn: m.entries.filter(e => e.checkedIn).length,
    };
  });

  groupedEntries = computed<EntryGroup[]>(() => {
    const entries = this.manifest()?.entries ?? [];
    const sorted = [...entries].sort((a, b) => {
      const refCmp = a.bookingReference.localeCompare(b.bookingReference);
      if (refCmp !== 0) return refCmp;
      const surnameCmp = a.surname.localeCompare(b.surname);
      if (surnameCmp !== 0) return surnameCmp;
      return a.givenName.localeCompare(b.givenName);
    });
    const groups: EntryGroup[] = [];
    for (const entry of sorted) {
      const last = groups[groups.length - 1];
      if (last && last.bookingReference === entry.bookingReference) {
        last.entries.push(entry);
      } else {
        groups.push({ bookingReference: entry.bookingReference, entries: [entry] });
      }
    }
    return groups;
  });

  cabinGrids = computed<CabinGrid[]>(() => {
    const sm = this.seatmap();
    if (!sm) return [];
    return sm.cabins.map(cabin => {
      const byRow = new Map<number, SeatmapSeat[]>();
      for (const seat of cabin.seats) {
        if (!byRow.has(seat.rowNumber)) byRow.set(seat.rowNumber, []);
        byRow.get(seat.rowNumber)!.push(seat);
      }
      const rows = [...byRow.entries()]
        .sort((a, b) => a[0] - b[0])
        .map(([rowNumber, seats]) => {
          const byCols = new Map(seats.map(s => [s.column, s]));
          return { rowNumber, seats: cabin.columns.map(col => byCols.get(col) ?? null) };
        });
      return { cabinCode: cabin.cabinCode, cabinName: cabin.cabinName, columns: cabin.columns, rows };
    });
  });

  async ngOnInit(): Promise<void> {
    const inventoryId = this.#route.snapshot.paramMap.get('inventoryId') ?? '';
    const flightNumber = this.#route.snapshot.queryParamMap.get('fn') ?? '';
    const departureDate = this.#route.snapshot.queryParamMap.get('date') ?? '';
    const aircraftType = this.#route.snapshot.queryParamMap.get('ac') ?? '';

    const stateFlight = history.state?.flight as FlightInventoryGroup | undefined;
    if (stateFlight) this.flight.set(stateFlight);

    this.loading.set(true);
    this.error.set('');
    try {
      const [manifest, seatmap] = await Promise.all([
        this.#inventoryService.getFlightManifest(flightNumber, departureDate),
        this.#inventoryService.getFlightSeatmap(inventoryId, flightNumber, aircraftType),
      ]);
      this.manifest.set(manifest);
      this.seatmap.set(seatmap);
    } catch {
      this.error.set('Failed to load passengers or seatmap. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  goBack(): void {
    this.#router.navigate(['/flight-departure']);
  }

  selectEntry(entry: ManifestEntry): void {
    this.selectedEntry.set(
      this.selectedEntry()?.eTicketNumber === entry.eTicketNumber ? null : entry
    );
  }

  statusClass(status: string): string {
    if (status === 'Active') return 'badge-active';
    if (status === 'Ticketing Closed') return 'badge-warning';
    return 'badge-inactive';
  }

  cabinLabel(code: string): string {
    return ({ F: 'First', J: 'Business', W: 'Premium Economy', Y: 'Economy' } as Record<string, string>)[code] ?? code;
  }

  isAisle(col: string, allCols: string[]): boolean {
    const idx = allCols.indexOf(col);
    if (idx <= 0) return false;
    return col.charCodeAt(0) - allCols[idx - 1].charCodeAt(0) > 1;
  }

  seatClass(seat: SeatmapSeat | null, manifest: FlightManifest | null): string {
    if (!seat) return 'seat-gap';
    const selected = this.selectedEntry();
    if (selected?.seatNumber && selected.seatNumber === seat.seatNumber) return 'seat-selected';
    if (seat.availability === 'available') return 'seat-open';
    const entry = manifest?.entries.find(e => e.seatNumber === seat.seatNumber);
    if (entry?.checkedIn) return 'seat-checked-in';
    if (seat.availability === 'sold') return 'seat-booked';
    return 'seat-held';
  }

  seatTooltip(seat: SeatmapSeat | null, manifest: FlightManifest | null): string {
    if (!seat) return '';
    if (seat.availability === 'available') return `${seat.seatNumber} — Open`;
    const entry = manifest?.entries.find(e => e.seatNumber === seat.seatNumber);
    if (entry) {
      const ci = entry.checkedIn ? ' (Checked in)' : '';
      return `${seat.seatNumber} — ${entry.surname}, ${entry.givenName}${ci}`;
    }
    return `${seat.seatNumber} — ${seat.availability}`;
  }
}
