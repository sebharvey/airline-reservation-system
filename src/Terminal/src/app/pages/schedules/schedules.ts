import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ScheduleService, ScheduleSummary, CabinDefinition } from '../../services/schedule.service';

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
    const totalOperating = all.reduce((sum, s) => sum + s.operatingDateCount, 0);
    const totalInventory = all.reduce((sum, s) => sum + s.flightsCreated, 0);
    return {
      total: all.length,
      routes: routes.size,
      aircraftTypes: aircraft.size,
      dailyFlights,
      inventoryCoverage: totalOperating > 0 ? Math.round((totalInventory / totalOperating) * 100) : 0,
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

  formatDuration(departureTime: string, arrivalTime: string, arrivalDayOffset: number): string {
    const [depH, depM] = departureTime.substring(0, 5).split(':').map(Number);
    const [arrH, arrM] = arrivalTime.substring(0, 5).split(':').map(Number);
    let totalMinutes = (arrH * 60 + arrM) - (depH * 60 + depM) + arrivalDayOffset * 1440;
    if (totalMinutes < 0) totalMinutes += 1440;
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;
    return `${hours}h ${minutes.toString().padStart(2, '0')}m`;
  }

  inventoryPercent(schedule: ScheduleSummary): number {
    if (schedule.operatingDateCount === 0) return 0;
    return Math.round((schedule.flightsCreated / schedule.operatingDateCount) * 100);
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
    };
  }

  addCabin(): void {
    this.cabins.update(c => [...c, this.createDefaultCabin()]);
  }

  removeCabin(index: number): void {
    this.cabins.update(c => c.filter((_, i) => i !== index));
  }

  updateCabin(cabinIndex: number, field: keyof CabinDefinition, value: string | number): void {
    this.cabins.update(cabins => {
      const updated = [...cabins];
      updated[cabinIndex] = { ...updated[cabinIndex], [field]: value };
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
        `Import complete: ${result.schedulesProcessed} schedules processed, ${result.inventoriesCreated} inventories created, ${result.inventoriesSkipped} skipped.`
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
