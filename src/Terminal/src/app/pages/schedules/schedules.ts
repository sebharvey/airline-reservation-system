import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ScheduleService, ScheduleSummary, CabinDefinition, FareDefinition } from '../../services/schedule.service';

@Component({
  selector: 'app-schedules',
  templateUrl: './schedules.html',
  styleUrl: './schedules.css',
  imports: [FormsModule],
})
export class SchedulesComponent implements OnInit {
  #scheduleService = inject(ScheduleService);

  schedules = signal<ScheduleSummary[]>([]);
  filter = signal('');
  loading = signal(false);
  error = signal('');
  loaded = signal(false);

  // Import to inventory state
  showImportModal = signal(false);
  importing = signal(false);
  importError = signal('');
  importSuccess = signal('');
  cabins = signal<CabinDefinition[]>([]);

  ngOnInit(): void {
    this.loadSchedules();
  }

  filtered = computed(() => {
    const q = this.filter().toLowerCase().trim();
    const all = this.schedules();
    if (!q) return all;
    return all.filter(
      s =>
        s.flightNumber.toLowerCase().includes(q) ||
        s.origin.toLowerCase().includes(q) ||
        s.destination.toLowerCase().includes(q) ||
        s.aircraftType.toLowerCase().includes(q)
    );
  });

  stats = computed(() => {
    const all = this.schedules();
    const routes = new Set(all.map(s => `${s.origin}-${s.destination}`));
    const aircraft = new Set(all.map(s => s.aircraftType));
    const dailyFlights = all.filter(s => s.daysOfWeek === 127).length;
    return {
      total: all.length,
      routes: routes.size,
      aircraftTypes: aircraft.size,
      dailyFlights,
    };
  });

  async loadSchedules(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#scheduleService.getSchedules();
      this.schedules.set(result.schedules);
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load schedules. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  setFilter(val: string): void {
    this.filter.set(val);
  }

  formatTime(time: string): string {
    return time.substring(0, 5);
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  formatDaysOfWeek(bitmask: number): string {
    const days = ['M', 'T', 'W', 'T', 'F', 'S', 'S'];
    return days.map((d, i) => (bitmask & (1 << i) ? d : '.')).join('');
  }

  openImportModal(): void {
    this.importError.set('');
    this.importSuccess.set('');
    this.cabins.set([this.createDefaultCabin()]);
    this.showImportModal.set(true);
  }

  closeImportModal(): void {
    this.showImportModal.set(false);
  }

  createDefaultCabin(): CabinDefinition {
    return {
      cabinCode: 'Y',
      totalSeats: 180,
      fares: [this.createDefaultFare('Y')],
    };
  }

  createDefaultFare(bookingClass: string): FareDefinition {
    return {
      fareBasisCode: '',
      fareFamily: '',
      bookingClass,
      currencyCode: 'GBP',
      baseFareAmount: 0,
      taxAmount: 0,
      isRefundable: false,
      isChangeable: false,
      changeFeeAmount: 0,
      cancellationFeeAmount: 0,
      pointsPrice: null,
      pointsTaxes: null,
    };
  }

  addCabin(): void {
    this.cabins.update(c => [...c, this.createDefaultCabin()]);
  }

  removeCabin(index: number): void {
    this.cabins.update(c => c.filter((_, i) => i !== index));
  }

  addFare(cabinIndex: number): void {
    this.cabins.update(cabins => {
      const updated = [...cabins];
      const cabin = { ...updated[cabinIndex], fares: [...updated[cabinIndex].fares, this.createDefaultFare(updated[cabinIndex].cabinCode)] };
      updated[cabinIndex] = cabin;
      return updated;
    });
  }

  removeFare(cabinIndex: number, fareIndex: number): void {
    this.cabins.update(cabins => {
      const updated = [...cabins];
      const cabin = { ...updated[cabinIndex], fares: updated[cabinIndex].fares.filter((_, i) => i !== fareIndex) };
      updated[cabinIndex] = cabin;
      return updated;
    });
  }

  updateCabin(cabinIndex: number, field: keyof CabinDefinition, value: string | number): void {
    this.cabins.update(cabins => {
      const updated = [...cabins];
      updated[cabinIndex] = { ...updated[cabinIndex], [field]: value };
      return updated;
    });
  }

  updateFare(cabinIndex: number, fareIndex: number, field: keyof FareDefinition, value: string | number | boolean): void {
    this.cabins.update(cabins => {
      const updated = [...cabins];
      const fares = [...updated[cabinIndex].fares];
      fares[fareIndex] = { ...fares[fareIndex], [field]: value };
      updated[cabinIndex] = { ...updated[cabinIndex], fares };
      return updated;
    });
  }

  async importToInventory(): Promise<void> {
    this.importing.set(true);
    this.importError.set('');
    this.importSuccess.set('');
    try {
      const result = await this.#scheduleService.importSchedulesToInventory({ cabins: this.cabins() });
      this.importSuccess.set(
        `Import complete: ${result.schedulesProcessed} schedules processed, ${result.inventoriesCreated} inventories created, ${result.inventoriesSkipped} skipped, ${result.faresCreated} fares created.`
      );
      this.showImportModal.set(false);
      await this.loadSchedules();
    } catch {
      this.importError.set('Failed to import schedules to inventory. Please try again.');
    } finally {
      this.importing.set(false);
    }
  }
}
