import { Component, inject, signal, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { OrderService, OrderSummary } from '../../../services/order.service';

@Component({
  selector: 'app-order-list',
  templateUrl: './order-list.html',
  styleUrl: './order-list.css',
})
export class OrderListComponent implements OnInit {
  #orderService = inject(OrderService);
  #router = inject(Router);

  orders = signal<OrderSummary[]>([]);
  search = signal('');
  loading = signal(false);
  error = signal('');
  loaded = signal(false);
  searchMode = signal(false);
  copiedRef = signal<string | null>(null);

  async ngOnInit(): Promise<void> {
    await this.loadRecentOrders();
  }

  async loadRecentOrders(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    this.searchMode.set(false);
    try {
      const result = await this.#orderService.getRecentOrders(10);
      this.orders.set(result);
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load orders. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  async searchByPnr(): Promise<void> {
    const pnr = this.search().trim().toUpperCase();
    if (!pnr) {
      await this.loadRecentOrders();
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.searchMode.set(true);
    try {
      const result = await this.#orderService.getOrderByRef(pnr);
      if (result) {
        const segments = result.orderData?.dataLists?.flightSegments ?? [];
        const route = segments.length > 0
          ? `${segments[0].origin} → ${this.#getTurnaroundDestination(segments)}`
          : '';
        const passengers = result.orderData?.dataLists?.passengers ?? [];
        const leadPassengerName = passengers.length > 0
          ? `${passengers[0].givenName} ${passengers[0].surname}`.trim()
          : '';
        this.orders.set([{ ...result, route, leadPassengerName } as unknown as OrderSummary]);
      } else {
        this.orders.set([]);
      }
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to search orders. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  async clearSearch(): Promise<void> {
    this.search.set('');
    await this.loadRecentOrders();
  }

  openOrder(bookingRef: string): void {
    this.#router.navigate(['/order', bookingRef]);
  }

  setSearch(val: string): void {
    this.search.set(val);
  }

  copyToClipboard(text: string, event: Event): void {
    event.stopPropagation();
    navigator.clipboard.writeText(text).then(() => {
      this.copiedRef.set(text);
      setTimeout(() => this.copiedRef.set(null), 2000);
    });
  }

  statusBadgeClass(status: string): string {
    return {
      Confirmed: 'badge-confirmed',
      Cancelled: 'badge-cancelled',
      Changed: 'badge-changed',
      Draft: 'badge-draft',
    }[status] ?? 'badge-default';
  }

  channelBadgeClass(channel: string): string {
    return {
      WEB: 'channel-web',
      APP: 'channel-app',
      CC: 'channel-cc',
      NDC: 'channel-ndc',
      GDS: 'channel-gds',
      KIOSK: 'channel-kiosk',
      AIRPORT: 'channel-airport',
    }[channel] ?? '';
  }

  formatAmount(amount: number | null, currency: string): string {
    if (amount === null) return '—';
    return `${amount.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}\u00A0${currency || 'GBP'}`;
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
    });
  }

  formatDateTime(iso: string): string {
    return new Date(iso).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }

  #getTurnaroundDestination(segments: { origin: string; destination: string }[]): string {
    const visited = new Set<string>([segments[0].origin]);
    for (let i = 0; i < segments.length; i++) {
      const dest = segments[i].destination;
      if (visited.has(dest)) {
        return i > 0 ? segments[i - 1].destination : dest;
      }
      visited.add(dest);
    }
    return segments[segments.length - 1].destination;
  }
}
