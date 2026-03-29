import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { OfferService, FlightInventoryGroup, CabinInventory } from '../../services/offer.service';

@Component({
  selector: 'app-offer',
  templateUrl: './offer.html',
  styleUrl: './offer.css',
  imports: [FormsModule],
})
export class OfferComponent implements OnInit {
  #offerService = inject(OfferService);

  flights = signal<FlightInventoryGroup[]>([]);
  selectedDate = signal(this.#todayIso());
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
      const result = await this.#offerService.getFlightInventory(this.selectedDate());
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

  loadBarClass(loadFactor: number): string {
    if (loadFactor >= 90) return 'bar-critical';
    if (loadFactor >= 70) return 'bar-high';
    if (loadFactor >= 40) return 'bar-medium';
    return 'bar-low';
  }

  statusClass(status: string): string {
    return status === 'Active' ? 'badge-active' : 'badge-inactive';
  }

  #todayIso(): string {
    return new Date().toISOString().slice(0, 10);
  }
}
