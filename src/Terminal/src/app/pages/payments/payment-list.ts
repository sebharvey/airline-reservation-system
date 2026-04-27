import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { LucideAngularModule } from 'lucide-angular';
import { PaymentService, PaymentListItem, PaymentDetail, PaymentEvent } from '../../services/payment.service';

@Component({
  selector: 'app-payment-list',
  imports: [LucideAngularModule],
  templateUrl: './payment-list.html',
  styleUrl: './payment-list.css',
})
export class PaymentListComponent implements OnInit {
  #paymentService = inject(PaymentService);

  payments = signal<PaymentListItem[]>([]);
  selectedDate = signal<string>(this.#todayIso());
  loading = signal(false);
  error = signal('');
  loaded = signal(false);

  copiedRef = signal<string | null>(null);

  // Modal state
  modalOpen = signal(false);
  modalPayment = signal<PaymentDetail | null>(null);
  modalEvents = signal<PaymentEvent[]>([]);
  modalLoading = signal(false);
  modalError = signal('');

  // Summary stats
  totalAmount = computed(() =>
    this.payments().reduce((sum, p) => sum + p.amount, 0)
  );
  settledAmount = computed(() =>
    this.payments().reduce((sum, p) => sum + (p.settledAmount ?? 0), 0)
  );
  statusCounts = computed(() => {
    const counts: Record<string, number> = {};
    for (const p of this.payments()) {
      counts[p.status] = (counts[p.status] ?? 0) + 1;
    }
    return counts;
  });

  async ngOnInit(): Promise<void> {
    await this.loadPayments();
  }

  async loadPayments(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#paymentService.getPaymentsByDate(this.selectedDate());
      this.payments.set(result);
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load payments. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  async openModal(payment: PaymentListItem): Promise<void> {
    this.modalOpen.set(true);
    this.modalPayment.set(null);
    this.modalEvents.set([]);
    this.modalLoading.set(true);
    this.modalError.set('');

    try {
      const [detail, events] = await Promise.all([
        this.#paymentService.getPayment(payment.paymentId),
        this.#paymentService.getPaymentEvents(payment.paymentId),
      ]);
      this.modalPayment.set(detail);
      this.modalEvents.set(events);
    } catch {
      this.modalError.set('Failed to load payment details.');
    } finally {
      this.modalLoading.set(false);
    }
  }

  copyBookingRef(text: string, event?: Event): void {
    event?.stopPropagation();
    navigator.clipboard.writeText(text).then(() => {
      this.copiedRef.set(text);
      setTimeout(() => this.copiedRef.set(null), 2000);
    });
  }

  closeModal(): void {
    this.modalOpen.set(false);
    this.modalPayment.set(null);
    this.modalEvents.set([]);
  }

  setDate(val: string): void {
    this.selectedDate.set(val);
  }

  statusBadgeClass(status: string): string {
    return ({
      Settled:      'badge-settled',
      Authorised:   'badge-authorised',
      Initialised:  'badge-initialised',
      Refunded:     'badge-refunded',
      Voided:       'badge-voided',
      Declined:     'badge-declined',
      Failed:       'badge-failed',
    } as Record<string, string>)[status] ?? 'badge-default';
  }

  eventTypeBadgeClass(eventType: string): string {
    return ({
      Authorised: 'badge-authorised',
      Settled:    'badge-settled',
      Refunded:   'badge-refunded',
      Voided:     'badge-voided',
    } as Record<string, string>)[eventType] ?? 'badge-default';
  }

  formatAmount(amount: number | null, currency: string): string {
    if (amount === null) return '—';
    return `${amount.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}\u00A0${currency || 'GBP'}`;
  }

  formatDateTime(iso: string | null): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }

  formatPaymentId(id: string): string {
    return id.split('-')[0].toUpperCase();
  }

  cardDisplay(payment: PaymentListItem | PaymentDetail): string {
    if (!payment.cardType && !payment.cardLast4) return payment.method;
    const parts = [];
    if (payment.cardType) parts.push(payment.cardType);
    if (payment.cardLast4) parts.push(`···· ${payment.cardLast4}`);
    return parts.join(' ');
  }

  statusCounEntries(): { status: string; count: number }[] {
    return Object.entries(this.statusCounts())
      .map(([status, count]) => ({ status, count }))
      .sort((a, b) => b.count - a.count);
  }

  #todayIso(): string {
    return new Date().toISOString().split('T')[0];
  }
}
