import { LucideAngularModule } from 'lucide-angular';
import { Component, inject, signal, computed, OnInit, OnDestroy } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { InventoryService, FlightInventoryGroup } from '../../../services/inventory.service';
import { PinService } from '../../../services/pin.service';

@Component({
  selector: 'app-flight-management-list',
  standalone: true,
  imports: [DecimalPipe, LucideAngularModule],
  templateUrl: './flight-management-list.html',
  styleUrl: './flight-management-list.css',
})
export class FlightManagementListComponent implements OnInit, OnDestroy {
  #inventoryService = inject(InventoryService);
  #router = inject(Router);
  pinService = inject(PinService);
  #refreshTimer: ReturnType<typeof setInterval> | null = null;

  selectedDate = signal(new Date().toISOString().slice(0, 10));

  selectedDateDisplay = computed(() => {
    const d = new Date(this.selectedDate() + 'T00:00:00Z');
    return d.toLocaleDateString('en-GB', {
      weekday: 'long',
      day: 'numeric',
      month: 'long',
      year: 'numeric',
      timeZone: 'UTC',
    });
  });

  #allDayFlights = signal<FlightInventoryGroup[]>([]);
  loading = signal(false);
  error = signal('');
  loaded = signal(false);

  /** Regular flights for the selected day — excludes any that are currently pinned. */
  flights = computed(() => {
    const pinnedSet = new Set(this.pinService.pinnedIds());
    return this.#allDayFlights().filter(f => !pinnedSet.has(f.inventoryId));
  });

  pinnedFlights = computed(() => this.pinService.pinnedFlights());

  stats = computed(() => {
    const all = [...this.pinnedFlights(), ...this.flights()];
    const pax = all.reduce((s, f) => s + ((f.f?.seatsSold ?? 0) + (f.j?.seatsSold ?? 0) + (f.w?.seatsSold ?? 0) + (f.y?.seatsSold ?? 0)), 0);
    return {
      total: all.length,
      active: all.filter(f => f.status === 'Active').length,
      cancelled: all.filter(f => f.status === 'Cancelled').length,
      pax,
    };
  });

  async ngOnInit(): Promise<void> {
    await this.load();
    this.#refreshTimer = setInterval(() => this.load(), 60_000);
  }

  ngOnDestroy(): void {
    if (this.#refreshTimer !== null) clearInterval(this.#refreshTimer);
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const pinnedIds = this.pinService.pinnedIds();
      const result = await this.#inventoryService.getFlightInventory(
        this.selectedDate(), pinnedIds,
      );
      this.#allDayFlights.set(result.flights);
      this.pinService.mergePinnedData(result.pinnedFlights);
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load flights. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  onDateChange(val: string): void {
    this.selectedDate.set(val);
    this.load();
  }

  changeDay(offset: number): void {
    const d = new Date(this.selectedDate() + 'T00:00:00Z');
    d.setUTCDate(d.getUTCDate() + offset);
    const next = d.toISOString().slice(0, 10);
    this.selectedDate.set(next);
    this.load();
  }

  openFlight(flight: FlightInventoryGroup): void {
    this.#router.navigate(
      ['/flight-management', flight.inventoryId],
      {
        queryParams: { fn: flight.flightNumber, date: flight.departureDate, ac: flight.aircraftType },
        state: { flight },
      }
    );
  }

  togglePin(event: Event, flight: FlightInventoryGroup): void {
    event.stopPropagation();
    if (this.pinService.isPinned(flight.inventoryId)) {
      this.pinService.unpin(flight.inventoryId);
    } else {
      this.pinService.pin(flight);
    }
  }

  statusClass(status: string): string {
    if (status === 'Active') return 'badge-active';
    if (status === 'Delayed') return 'badge-amber';
    if (status === 'Ticketing Closed') return 'badge-silver';
    return 'badge-inactive';
  }

  loadBarClass(loadFactor: number): string {
    if (loadFactor >= 90) return 'bar-critical';
    if (loadFactor >= 70) return 'bar-high';
    if (loadFactor >= 40) return 'bar-medium';
    return 'bar-low';
  }

  paxOnBoard(flight: FlightInventoryGroup): number {
    return (flight.f?.seatsSold ?? 0) + (flight.j?.seatsSold ?? 0) + (flight.w?.seatsSold ?? 0) + (flight.y?.seatsSold ?? 0);
  }
}
