import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { InventoryService, FlightInventoryGroup, CabinInventory } from '../../services/inventory.service';

@Component({
  selector: 'app-flight-departure',
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
}
