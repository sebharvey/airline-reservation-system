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

interface SeatCell {
  seat: SeatmapSeat | null;
  cssClass: string;
  tooltip: string;
  aisleBefore: boolean;
}

interface CabinGrid {
  cabinCode: string;
  cabinName: string;
  headers: { label: string; aisleBefore: boolean }[];
  rows: { rowNumber: number; cells: SeatCell[] }[];
}

interface EntryGroup {
  bookingReference: string;
  bookingType: string;
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
  paxFilter = signal<'all' | 'confirmed' | 'standby'>('all');

  // Seat action state
  pendingSeat = signal<SeatmapSeat | null>(null);
  seatOpLoading = signal(false);
  seatOpError = signal('');

  // Computed helpers
  inSeatSelectMode = computed(() => this.selectedEntry() !== null);

  canReleaseSeat = computed(() => {
    const entry = this.selectedEntry();
    return !!entry?.seatNumber && !this.seatOpLoading();
  });

  canConfirmSeat = computed(() =>
    this.pendingSeat() !== null && !this.seatOpLoading()
  );

  manifestStats = computed(() => {
    const m = this.manifest();
    if (!m) return null;
    const standby = m.entries.filter(e => e.bookingType === 'Standby').length;
    return {
      total: m.entries.length,
      checkedIn: m.entries.filter(e => e.checkedIn).length,
      confirmed: m.entries.length - standby,
      standby,
    };
  });

  cabinStats = computed(() => {
    const f = this.flight();
    if (!f) return [];
    const codes = ['f', 'j', 'w', 'y'] as const;
    return codes
      .filter(k => f[k] !== null)
      .map(k => ({ code: k.toUpperCase(), ...f[k]! }));
  });

  groupedEntries = computed<EntryGroup[]>(() => {
    const filter = this.paxFilter();
    const allEntries = this.manifest()?.entries ?? [];
    const entries = filter === 'standby'
      ? allEntries.filter(e => e.bookingType === 'Standby')
      : filter === 'confirmed'
      ? allEntries.filter(e => e.bookingType !== 'Standby')
      : allEntries;
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
        groups.push({
          bookingReference: entry.bookingReference,
          bookingType: entry.bookingType,
          entries: [entry],
        });
      }
    }
    return groups;
  });

  // cabinGrids directly consumes selectedEntry, pendingSeat and manifest so
  // that any change to passenger selection produces a new iterable for @for.
  // This is the only reliable way to guarantee @for re-renders its content
  // in Angular 17's block-based control flow when the seats array itself has
  // not changed (zone.js does not always re-check @for embedded views whose
  // own iterable signal is unchanged).
  cabinGrids = computed<CabinGrid[]>(() => {
    const sm       = this.seatmap();
    const manifest = this.manifest();
    const selected = this.selectedEntry();
    const pending  = this.pendingSeat();
    const inSelect = selected !== null;

    if (!sm) return [];

    return sm.cabins.map(cabin => {
      // Build aisle boundary set from layout string (e.g. "2-3-2")
      const groups = cabin.layout.split('-').map(Number).filter(n => n > 0);
      const boundaries = new Set<number>();
      let pos = 0;
      for (let g = 0; g < groups.length - 1; g++) {
        pos += groups[g];
        boundaries.add(pos);
      }

      const headers = cabin.columns.map((label, idx) => ({
        label,
        aisleBefore: idx > 0 && boundaries.has(idx),
      }));

      const byRow = new Map<number, SeatmapSeat[]>();
      for (const seat of cabin.seats) {
        if (!byRow.has(seat.rowNumber)) byRow.set(seat.rowNumber, []);
        byRow.get(seat.rowNumber)!.push(seat);
      }

      const rows = [...byRow.entries()]
        .sort((a, b) => a[0] - b[0])
        .map(([rowNumber, seats]) => {
          const byCols = new Map(seats.map(s => [s.column, s]));
          const cells: SeatCell[] = cabin.columns.map((col, idx) => {
            const seat = byCols.get(col) ?? null;
            const aisleBefore = idx > 0 && boundaries.has(idx);

            let cssClass: string;
            if (!seat) {
              cssClass = 'seat-gap';
            } else if (pending?.seatNumber === seat.seatNumber) {
              cssClass = 'seat-pending';
            } else if (selected?.seatNumber === seat.seatNumber) {
              cssClass = 'seat-selected';
            } else if (seat.availability === 'available') {
              cssClass = inSelect ? 'seat-open seat-selectable' : 'seat-open';
            } else {
              const entry = manifest?.entries.find(e => e.seatNumber === seat.seatNumber);
              cssClass = entry?.checkedIn            ? 'seat-checked-in'
                       : seat.availability === 'sold' ? 'seat-booked'
                       : 'seat-held';
            }

            let tooltip = '';
            if (seat) {
              if (pending?.seatNumber === seat.seatNumber) {
                tooltip = `${seat.seatNumber} — Selected (pending)`;
              } else if (seat.availability === 'available') {
                tooltip = `${seat.seatNumber} — Open`;
              } else {
                const entry = manifest?.entries.find(e => e.seatNumber === seat.seatNumber);
                tooltip = entry
                  ? `${seat.seatNumber} — ${entry.surname}, ${entry.givenName}${entry.checkedIn ? ' (Checked in)' : ''}`
                  : `${seat.seatNumber} — ${seat.availability}`;
              }
            }

            return { seat, cssClass, tooltip, aisleBefore };
          });
          return { rowNumber, cells };
        });

      return { cabinCode: cabin.cabinCode, cabinName: cabin.cabinName, headers, rows };
    });
  });

  // Params cached for seat operations
  #inventoryId = '';
  #flightNumber = '';
  #departureDate = '';
  #aircraftType = '';

  async ngOnInit(): Promise<void> {
    this.#inventoryId   = this.#route.snapshot.paramMap.get('inventoryId') ?? '';
    this.#flightNumber  = this.#route.snapshot.queryParamMap.get('fn') ?? '';
    this.#departureDate = this.#route.snapshot.queryParamMap.get('date') ?? '';
    this.#aircraftType  = this.#route.snapshot.queryParamMap.get('ac') ?? '';

    const stateFlight = history.state?.flight as FlightInventoryGroup | undefined;
    if (stateFlight) this.flight.set(stateFlight);

    await this.#loadData();
  }

  async #loadData(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [manifest, seatmap] = await Promise.all([
        this.#inventoryService.getFlightManifest(this.#flightNumber, this.#departureDate),
        this.#inventoryService.getFlightSeatmap(this.#inventoryId, this.#flightNumber, this.#aircraftType),
      ]);
      this.manifest.set(manifest);
      this.seatmap.set(seatmap);
    } catch {
      this.error.set('Failed to load passengers or seatmap. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  // Refreshes manifest + seatmap without hiding the UI (used after seat ops)
  async #silentRefresh(): Promise<void> {
    try {
      const [manifest, seatmap] = await Promise.all([
        this.#inventoryService.getFlightManifest(this.#flightNumber, this.#departureDate),
        this.#inventoryService.getFlightSeatmap(this.#inventoryId, this.#flightNumber, this.#aircraftType),
      ]);
      this.manifest.set(manifest);
      this.seatmap.set(seatmap);
    } catch {
      this.seatOpError.set('Data refresh failed — displayed data may be stale.');
    }
  }

  async refresh(): Promise<void> {
    this.selectedEntry.set(null);
    this.pendingSeat.set(null);
    this.seatOpError.set('');
    await this.#loadData();
  }

  goBack(): void {
    this.#router.navigate(['/flight-departure']);
  }

  selectEntry(entry: ManifestEntry): void {
    const isSame = this.selectedEntry()?.eTicketNumber === entry.eTicketNumber;
    this.selectedEntry.set(isSame ? null : entry);
    this.pendingSeat.set(null);
    this.seatOpError.set('');
  }

  selectSeatFromMap(seat: SeatmapSeat): void {
    if (!this.inSeatSelectMode()) return;
    if (seat.availability !== 'available') return;
    this.pendingSeat.set(
      this.pendingSeat()?.seatNumber === seat.seatNumber ? null : seat
    );
    this.seatOpError.set('');
  }

  async releaseSeat(): Promise<void> {
    const entry = this.selectedEntry();
    if (!entry?.seatNumber || this.seatOpLoading()) return;

    this.seatOpLoading.set(true);
    this.seatOpError.set('');
    try {
      await this.#inventoryService.releaseSeat(
        entry.eTicketNumber,
        entry.bookingReference,
        entry.passengerId,
        this.#inventoryId,
        entry.orderId,
        entry.cabinCode,
      );
      await this.#silentRefresh();
      const updated = this.manifest()?.entries.find(e => e.eTicketNumber === entry.eTicketNumber);
      this.selectedEntry.set(updated ?? null);
      this.pendingSeat.set(null);
    } catch {
      this.seatOpError.set('Failed to release seat. Please try again.');
    } finally {
      this.seatOpLoading.set(false);
    }
  }

  async confirmSeat(): Promise<void> {
    const entry = this.selectedEntry();
    const seat  = this.pendingSeat();
    if (!entry || !seat || this.seatOpLoading()) return;

    this.seatOpLoading.set(true);
    this.seatOpError.set('');
    try {
      await this.#inventoryService.assignSeat(
        entry.eTicketNumber,
        entry.bookingReference,
        entry.passengerId,
        this.#inventoryId,
        seat.seatNumber,
      );
      await this.#silentRefresh();
      const updated = this.manifest()?.entries.find(e => e.eTicketNumber === entry.eTicketNumber);
      this.selectedEntry.set(updated ?? null);
      this.pendingSeat.set(null);
    } catch {
      this.seatOpError.set('Failed to confirm seat. Please try again.');
    } finally {
      this.seatOpLoading.set(false);
    }
  }

  statusClass(status: string): string {
    if (status === 'Active') return 'badge-active';
    if (status === 'Ticketing Closed') return 'badge-warning';
    return 'badge-inactive';
  }

  cabinLabel(code: string): string {
    return ({ F: 'First', J: 'Business', W: 'Premium Economy', Y: 'Economy' } as Record<string, string>)[code] ?? code;
  }

  bookingTypeBadgeClass(bookingType: string): string {
    return bookingType === 'Standby' ? 'booking-type-standby' : 'booking-type-confirmed';
  }

  setFilter(filter: 'all' | 'confirmed' | 'standby'): void {
    this.paxFilter.set(filter);
    this.selectedEntry.set(null);
    this.pendingSeat.set(null);
    this.seatOpError.set('');
  }
}
