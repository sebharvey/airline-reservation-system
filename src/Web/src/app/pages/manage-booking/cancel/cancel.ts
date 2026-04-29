import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { Order } from '../../../models/order.model';

@Component({
  selector: 'app-cancel-booking',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './cancel.html',
  styleUrl: './cancel.css'
})
export class CancelBookingComponent implements OnInit {
  order = signal<Order | null>(null);
  loading = signal(true);
  cancelling = signal(false);
  errorMessage = signal('');
  successMessage = signal('');
  cancellationRef = signal('');
  refundAmount = signal(0);
  refundCurrency = signal('GBP');
  copiedText = signal<string | null>(null);

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copiedText.set(text);
      setTimeout(() => this.copiedText.set(null), 2000);
    });
  }

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  readonly isRefundable = computed((): boolean => {
    const o = this.order();
    return o ? o.orderItems.some(i => i.isRefundable) : false;
  });

  readonly refundableAmount = computed((): number => {
    const o = this.order();
    if (!o) return 0;
    return o.orderItems
      .filter(i => i.isRefundable)
      .reduce((sum, i) => sum + i.totalPrice, 0);
  });

  readonly nonRefundableAmount = computed((): number => {
    const o = this.order();
    if (!o) return 0;
    return o.orderItems
      .filter(i => !i.isRefundable)
      .reduce((sum, i) => sum + i.totalPrice, 0);
  });

  readonly passengerSummary = computed((): string => {
    const o = this.order();
    if (!o) return '';
    return o.passengers.map(p => `${p.givenName} ${p.surname}`).join(', ');
  });

  readonly routeSummary = computed((): string => {
    const o = this.order();
    if (!o) return '';
    return o.flightSegments
      .map(s => `${s.origin} \u2192 ${s.destination}`)
      .join(' | ');
  });

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private retailApi: RetailApiService
  ) {}

  ngOnInit(): void {
    const navState = (this.router.getCurrentNavigation()?.extras.state ?? history.state) as Record<string, string>;
    const gn = navState?.['givenName'] ?? '';
    const sn = navState?.['surname'] ?? '';

    this.route.queryParams.subscribe(params => {
      const ref = params['bookingRef'] ?? '';
      this.bookingRef.set(ref);
      this.givenName.set(gn);
      this.surname.set(sn);

      if (!ref || !gn || !sn) {
        this.router.navigate(['/manage-booking']);
        return;
      }
      this.loadOrder(ref, gn, sn);
    });
  }

  private loadOrder(ref: string, _gn: string, _sn: string): void {
    this.loading.set(true);
    this.retailApi.retrieveOrder(ref).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: (err: { status?: number; message?: string }) => {
        if (err.status === 401) { this.router.navigate(['/manage-booking']); return; }
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking.');
        this.loading.set(false);
      }
    });
  }

  confirmCancellation(): void {
    if (this.cancelling()) return;
    this.cancelling.set(true);
    this.errorMessage.set('');

    this.retailApi.cancelOrder(this.bookingRef(), 'PASSENGER_VOLUNTARY').subscribe({
      next: (res) => {
        this.cancelling.set(false);
        if (res.success) {
          this.refundAmount.set(res.refundAmount);
          this.refundCurrency.set(res.currency);
          this.cancellationRef.set(`CXL-${this.bookingRef()}-${Date.now().toString(36).toUpperCase()}`);
          this.successMessage.set('Your booking has been cancelled.');
        } else {
          this.errorMessage.set('Cancellation failed. Please try again or contact support.');
        }
      },
      error: (err: { message?: string }) => {
        this.cancelling.set(false);
        this.errorMessage.set(err?.message ?? 'Cancellation failed. Please try again.');
      }
    });
  }

  keepBooking(): void {
    this.router.navigate(['/manage-booking/detail'], {
      queryParams: { bookingRef: this.bookingRef() },
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }

  formatCurrency(amount: number, currency: string): string {
    return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(amount);
  }

  formatDateTime(dt: string): string {
    return new Date(dt).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit', timeZone: 'UTC'
    });
  }
}
