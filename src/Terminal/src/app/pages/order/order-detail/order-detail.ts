import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { OrderService, OrderDetail, OrderPassenger, FlightSegment, OrderItem, OrderPayment, OrderHistoryEvent } from '../../../services/order.service';

@Component({
  selector: 'app-order-detail',
  templateUrl: './order-detail.html',
  styleUrl: './order-detail.css',
})
export class OrderDetailComponent implements OnInit {
  #route = inject(ActivatedRoute);
  #router = inject(Router);
  #orderService = inject(OrderService);

  bookingRef = '';
  loading = signal(false);
  error = signal('');
  order = signal<OrderDetail | null>(null);
  activeTab = signal<'itinerary' | 'passengers' | 'ancillaries' | 'payments' | 'history'>('itinerary');
  copied = signal(false);

  passengers = computed<OrderPassenger[]>(() =>
    this.order()?.orderData?.dataLists?.passengers ?? []
  );

  segments = computed<FlightSegment[]>(() =>
    this.order()?.orderData?.dataLists?.flightSegments ?? []
  );

  flightItems = computed<OrderItem[]>(() =>
    (this.order()?.orderData?.orderItems ?? []).filter(i => i.itemType === 'Flight')
  );

  ancillaryItems = computed<OrderItem[]>(() =>
    (this.order()?.orderData?.orderItems ?? []).filter(i => i.itemType !== 'Flight')
  );

  payments = computed<OrderPayment[]>(() =>
    this.order()?.orderData?.payments ?? []
  );

  history = computed<OrderHistoryEvent[]>(() =>
    this.order()?.orderData?.history ?? []
  );

  ngOnInit(): void {
    this.bookingRef = this.#route.snapshot.paramMap.get('bookingRef') ?? '';
    this.loadOrder();
  }

  async loadOrder(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#orderService.getOrderByRef(this.bookingRef);
      if (result) {
        this.order.set(result);
      } else {
        this.error.set(`Order "${this.bookingRef}" was not found.`);
      }
    } catch {
      this.error.set('Failed to load order details. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  switchTab(tab: 'itinerary' | 'passengers' | 'ancillaries' | 'payments' | 'history'): void {
    this.activeTab.set(tab);
  }

  goBack(): void {
    this.#router.navigate(['/order']);
  }

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
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

  itemTypeBadgeClass(type: string): string {
    return {
      Flight: 'item-flight',
      Seat: 'item-seat',
      Bag: 'item-bag',
      SSR: 'item-ssr',
    }[type] ?? 'item-other';
  }

  paymentStatusClass(status: string): string {
    return {
      Settled: 'pay-settled',
      Authorised: 'pay-authorised',
      Refunded: 'pay-refunded',
      Voided: 'pay-voided',
    }[status] ?? '';
  }

  passengerTypeLabel(type: string): string {
    return { ADT: 'Adult', CHD: 'Child', INF: 'Infant', YTH: 'Youth' }[type] ?? type;
  }

  getETicketForPaxSegment(passengerId: string, segmentId: string): string {
    const item = this.flightItems().find(
      i => i.passengerId === passengerId && i.segmentId === segmentId
    );
    return item?.eTicketNumber ?? '—';
  }

  getSeatForPaxSegment(passengerId: string, segmentId: string): string {
    const item = this.ancillaryItems().find(
      i => i.itemType === 'Seat' && i.passengerId === passengerId && i.segmentId === segmentId
    );
    return item?.seatNumber ?? '—';
  }

  formatAmount(amount: number | null | undefined, currency?: string | null): string {
    if (amount == null) return '—';
    return new Intl.NumberFormat('en-GB', {
      style: 'currency',
      currency: currency || this.order()?.currencyCode || 'GBP',
    }).format(amount);
  }

  formatDate(iso: string | null | undefined): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
    });
  }

  formatDateTime(iso: string | null | undefined): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }

  formatTime(iso: string | null | undefined): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleTimeString('en-GB', {
      hour: '2-digit', minute: '2-digit',
    });
  }

  duration(dep: string | null | undefined, arr: string | null | undefined): string {
    if (!dep || !arr) return '';
    const diffMs = new Date(arr).getTime() - new Date(dep).getTime();
    const hours = Math.floor(diffMs / 3600000);
    const mins = Math.floor((diffMs % 3600000) / 60000);
    return `${hours}h ${mins}m`;
  }
}
