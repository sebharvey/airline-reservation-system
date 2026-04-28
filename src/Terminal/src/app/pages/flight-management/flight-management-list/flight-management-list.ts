import { LucideAngularModule } from 'lucide-angular';
import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { InventoryService, FlightInventoryGroup } from '../../../services/inventory.service';

@Component({
  selector: 'app-flight-management-list',
  standalone: true,
  imports: [DecimalPipe, LucideAngularModule],
  templateUrl: './flight-management-list.html',
  styleUrl: './flight-management-list.css',
})
export class FlightManagementListComponent implements OnInit {
  #inventoryService = inject(InventoryService);
  #router = inject(Router);

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
      const result = await this.#inventoryService.getFlightInventory(this.selectedDate());
      this.flights.set(result);
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

  paxOnBoard(flight: FlightInventoryGroup): number {
    return flight.totalSeats - flight.totalSeatsAvailable;
  }
}
