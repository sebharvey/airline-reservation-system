import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../services/retail-api.service';
import { FlightStatus, ScheduledFlightNumber } from '../../models/flight.model';
import { AIRPORTS } from '../../data/airports';

export type FlightStatusCode = FlightStatus['status'];
export type DisplayStatusCode = FlightStatusCode | 'InFlight';

interface StatusDisplay {
  label: string;
  cssClass: string;
}

const STATUS_CONFIG: Record<DisplayStatusCode, StatusDisplay> = {
  OnTime:    { label: 'On Time',   cssClass: 'status-ontime' },
  Delayed:   { label: 'Delayed',   cssClass: 'status-delayed' },
  Boarding:  { label: 'Boarding',  cssClass: 'status-boarding' },
  Departed:  { label: 'Departed',  cssClass: 'status-departed' },
  InFlight:  { label: 'In Flight', cssClass: 'status-inflight' },
  Landed:    { label: 'Landed',    cssClass: 'status-landed' },
  Cancelled: { label: 'Cancelled', cssClass: 'status-cancelled' },
};

@Component({
  selector: 'app-flight-status',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './flight-status.html',
  styleUrl: './flight-status.css'
})
export class FlightStatusComponent implements OnInit {
  private readonly retailApi = inject(RetailApiService);

  readonly statusConfig = STATUS_CONFIG;
  flightNumbers = signal<ScheduledFlightNumber[]>([]);

  flightNumber = signal('');
  loading = signal(false);
  searched = signal(false);
  result = signal<FlightStatus | null | 'not-found'>(null);

  readonly flightStatus = computed(() => {
    const r = this.result();
    return r === 'not-found' ? null : r;
  });

  readonly notFound = computed(() => this.result() === 'not-found');

  ngOnInit(): void {
    this.retailApi.getFlightNumbers().subscribe(flights => {
      this.flightNumbers.set(flights);
    });
  }

  setFlightNumber(v: string): void {
    this.flightNumber.set(v);
  }

  search(): void {
    const fn = this.flightNumber().trim();
    if (!fn) return;

    this.loading.set(true);
    this.searched.set(false);
    this.result.set(null);

    this.retailApi.getFlightStatus(fn).subscribe({
      next: (status) => {
        this.loading.set(false);
        this.searched.set(true);
        this.result.set(status === null ? 'not-found' : status);
      },
      error: () => {
        this.loading.set(false);
        this.searched.set(true);
        this.result.set('not-found');
      }
    });
  }

  getAirportName(code: string): string {
    const a = AIRPORTS.find(ap => ap.code === code);
    return a ? `${a.city} (${a.code})` : code;
  }

  getAirportCity(code: string): string {
    return AIRPORTS.find(ap => ap.code === code)?.city ?? code;
  }

  computeDisplayStatus(flight: FlightStatus): DisplayStatusCode {
    if (flight.status === 'Cancelled') return 'Cancelled';

    const now = new Date();
    const departure = new Date(flight.estimatedDepartureDateTime ?? flight.scheduledDepartureDateTime);
    const arrival = new Date(flight.estimatedArrivalDateTime ?? flight.scheduledArrivalDateTime);
    const minutesToDeparture = (departure.getTime() - now.getTime()) / 60000;

    if (now >= arrival) return 'Landed';
    if (now >= departure) return 'InFlight';
    if (minutesToDeparture <= 60) return 'Boarding';

    return flight.status;
  }

  getStatusLabel(status: DisplayStatusCode): string {
    return STATUS_CONFIG[status]?.label ?? status;
  }

  getStatusCssClass(status: DisplayStatusCode): string {
    return STATUS_CONFIG[status]?.cssClass ?? '';
  }

  formatDateTime(iso: string): string {
    return new Date(iso).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  }

  formatTime(iso: string): string {
    return new Date(iso).toLocaleTimeString('en-GB', {
      hour: '2-digit', minute: '2-digit'
    });
  }

  isEstimatedDifferent(scheduled: string, estimated: string | null): boolean {
    if (!estimated) return false;
    return new Date(scheduled).getTime() !== new Date(estimated).getTime();
  }

}
