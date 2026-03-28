import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { ScheduleService, ScheduleSummary } from '../../services/schedule.service';

@Component({
  selector: 'app-schedules',
  templateUrl: './schedules.html',
  styleUrl: './schedules.css',
})
export class SchedulesComponent implements OnInit {
  #scheduleService = inject(ScheduleService);

  schedules = signal<ScheduleSummary[]>([]);
  filter = signal('');
  loading = signal(false);
  error = signal('');
  loaded = signal(false);

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
}
