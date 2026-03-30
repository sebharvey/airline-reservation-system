import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../services/retail-api.service';
import { FlightSummary, FlightStatus } from '../../models/flight.model';
import { AIRPORTS } from '../../data/airports';

export type FlightStatusCode = FlightStatus['status'];

interface StatusDisplay {
  label: string;
  cssClass: string;
}

const STATUS_CONFIG: Record<FlightStatusCode, StatusDisplay> = {
  OnTime:    { label: 'On Time',   cssClass: 'status-ontime' },
  Delayed:   { label: 'Delayed',   cssClass: 'status-delayed' },
  Boarding:  { label: 'Boarding',  cssClass: 'status-boarding' },
  Departed:  { label: 'Departed',  cssClass: 'status-departed' },
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

  flights = signal<FlightSummary[]>([]);
  flightsLoading = signal(false);
  flightNumber = signal('');
  flightDate = signal('');
  loading = signal(false);
  searched = signal(false);
  result = signal<FlightStatus | null | 'not-found'>(null);

  readonly flightStatus = computed(() => {
    const r = this.result();
    return r === 'not-found' ? null : r;
  });

  readonly notFound = computed(() => this.result() === 'not-found');

  ngOnInit(): void {
    const today = new Date().toISOString().split('T')[0];
    this.flightDate.set(today);
    this.loadFlights(today);
  }

  setFlightNumber(v: string): void {
    this.flightNumber.set(v);
  }

  setFlightDate(v: string): void {
    this.flightDate.set(v);
    this.flightNumber.set('');
    this.result.set(null);
    this.searched.set(false);
    if (v) {
      this.loadFlights(v);
    } else {
      this.flights.set([]);
    }
  }

  search(): void {
    const fn = this.flightNumber().trim();
    if (!fn) return;

    this.loading.set(true);
    this.searched.set(false);
    this.result.set(null);

    const date = this.flightDate().trim() || undefined;

    this.retailApi.getFlightStatus(fn, date).subscribe({
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

  getStatusLabel(status: FlightStatusCode): string {
    return STATUS_CONFIG[status]?.label ?? status;
  }

  getStatusCssClass(status: FlightStatusCode): string {
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

  private loadFlights(date: string): void {
    this.flightsLoading.set(true);
    this.retailApi.getFlights(date).subscribe({
      next: (flights) => {
        this.flights.set(flights);
        this.flightsLoading.set(false);
      },
      error: () => {
        this.flights.set([]);
        this.flightsLoading.set(false);
      }
    });
  }
}
