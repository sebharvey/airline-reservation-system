import { Component, OnInit, computed, signal, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { BookingStateService } from '../../../services/booking-state.service';
import { RetailApiService } from '../../../services/retail-api.service';
import { BasketSummary, SummaryFlight, SummaryTaxLine } from '../../../models/order.model';

const CABIN_NAMES: Record<string, string> = {
  F: 'First Class',
  J: 'Business',
  W: 'Premium Economy',
  Y: 'Economy'
};

export interface TaxLineEntry {
  flightNumber: string;
  code: string;
  description: string | null;
  amount: number;
}

@Component({
  selector: 'app-flight-summary',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './flight-summary.html',
  styleUrl: './flight-summary.css'
})
export class FlightSummaryComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly bookingState = inject(BookingStateService);
  private readonly retailApi = inject(RetailApiService);

  readonly basket = this.bookingState.basket;
  readonly isRewardBooking = this.bookingState.isRewardBooking;

  loading = signal(true);
  error = signal('');
  summary = signal<BasketSummary | null>(null);
  taxModalOpen = signal(false);

  readonly totalTaxAmount = computed(() => {
    const s = this.summary();
    if (!s) return 0;
    return s.flights.reduce((sum, f) => sum + f.taxAmount, 0);
  });

  readonly allTaxLines = computed((): TaxLineEntry[] => {
    const s = this.summary();
    if (!s) return [];
    const lines: TaxLineEntry[] = [];
    for (const flight of s.flights) {
      if (flight.taxLines && flight.taxLines.length > 0) {
        for (const tl of flight.taxLines) {
          lines.push({ flightNumber: flight.flightNumber, code: tl.code, description: tl.description, amount: tl.amount });
        }
      } else if (flight.taxAmount > 0) {
        lines.push({ flightNumber: flight.flightNumber, code: 'TAX', description: 'Taxes & charges', amount: flight.taxAmount });
      }
    }
    return lines;
  });

  readonly allTaxLinesTotal = computed(() =>
    this.allTaxLines().reduce((sum, t) => sum + t.amount, 0)
  );

  ngOnInit(): void {
    const basket = this.basket();
    if (!basket) {
      this.router.navigate(['/']);
      return;
    }

    // Use cached summary if already loaded (e.g. user navigated back)
    const cached = this.bookingState.basketSummary();
    if (cached && cached.basketId === basket.basketId) {
      this.summary.set(cached);
      this.loading.set(false);
      return;
    }

    this.retailApi.getBasketSummary(basket.basketId).subscribe({
      next: (s) => {
        this.summary.set(s);
        this.bookingState.setBasketSummary(s);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load pricing summary. Please try again.');
        this.loading.set(false);
      }
    });
  }

  onContinue(): void {
    this.router.navigate(['/booking/passengers']);
  }

  openTaxModal(): void {
    this.taxModalOpen.set(true);
  }

  closeTaxModal(): void {
    this.taxModalOpen.set(false);
  }

  getCabinName(code: string): string {
    return CABIN_NAMES[code] ?? code;
  }

  formatDepartureDate(flight: SummaryFlight): string {
    const [y, m, d] = flight.departureDate.split('-').map(Number);
    return new Date(y, m - 1, d).toLocaleDateString('en-GB', {
      weekday: 'short', day: 'numeric', month: 'short', year: 'numeric'
    });
  }

  formatCurrency(amount: number, currency: string): string {
    return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(amount);
  }
}
