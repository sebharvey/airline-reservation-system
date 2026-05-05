import { LucideAngularModule } from 'lucide-angular';
import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { InventoryService, FlightInventoryGroup, CabinInventory } from '../../services/inventory.service';

@Component({
  selector: 'app-inventory',
  templateUrl: './inventory.html',
  styleUrl: './inventory.css',
  imports: [FormsModule, LucideAngularModule],
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
      this.flights.set(result.flights);
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

  openFlightDetail(flight: FlightInventoryGroup): void {
    this.#router.navigate(['/inventory', flight.inventoryId]);
  }

}
