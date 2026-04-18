import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { OrderService, OrderDetail, OrderPassenger, FlightSegment, OrderItem, OrderPayment, OrderHistoryEvent, SsrOption, SsrPatchAction, Ticket, ItemTotals } from '../../../services/order.service';

interface EditForm {
  givenName: string;
  surname: string;
  dob: string | null;
  email: string | null;
  phone: string | null;
}

interface SsrEditForm {
  ssrCode: string;
  passengerRef: string;
  segmentRef: string;
}

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
  activeTab = signal<'orderItems' | 'payments' | 'history' | 'tickets'>('orderItems');
  copied = signal(false);
  copiedText = signal<string | null>(null);
  editingPaxId = signal<string | null>(null);
  editForm = signal<EditForm>({ givenName: '', surname: '', dob: null, email: null, phone: null });
  editSaving = signal(false);
  editError = signal('');

  ssrOptions = signal<SsrOption[]>([]);
  ssrOptionsLoading = signal(false);
  ssrOptionsError = signal('');
  ssrSaving = signal(false);
  ssrError = signal('');
  showAddSsr = signal(false);
  addSsrForm = signal<SsrEditForm>({ ssrCode: '', passengerRef: '', segmentRef: '' });
  addSsrAllSegments = signal(false);

  // Edit-in-place state for existing SSR rows
  editingSsrKey = signal<string | null>(null);
  editSsrForm = signal<SsrEditForm>({ ssrCode: '', passengerRef: '', segmentRef: '' });

  selectedOrderItem = signal<OrderItem | null>(null);
  selectedPassenger = signal<OrderPassenger | null>(null);

  // Tickets tab state
  tickets = signal<Ticket[]>([]);
  ticketsLoading = signal(false);
  ticketsError = signal('');
  selectedTicket = signal<Ticket | null>(null);

  // Debug state — TODO: Remove when debug endpoints are removed
  showDebug = signal(false);
  debugTab = signal<'order' | 'tickets'>('order');
  debugOrderLoading = signal(false);
  debugOrderJson = signal('');
  debugOrderError = signal('');
  debugTicketsLoading = signal(false);
  debugTicketsJson = signal('');
  debugTicketsError = signal('');

  readonly objectKeys = Object.keys;

  passengers = computed<OrderPassenger[]>(() =>
    this.order()?.orderData?.dataLists?.passengers ?? []
  );

  segments = computed<FlightSegment[]>(() =>
    this.order()?.orderData?.dataLists?.flightSegments ?? []
  );

  readonly #itemTypePriority: Record<string, number> = { Flight: 0, Seat: 1, SSR: 2, Bag: 3, Product: 4 };

  allOrderItems = computed<OrderItem[]>(() => {
    const items = this.order()?.orderData?.orderItems ?? [];
    return [...items].sort((a, b) =>
      (this.#itemTypePriority[a.itemType] ?? 99) - (this.#itemTypePriority[b.itemType] ?? 99)
    );
  });

  flightItems = computed<OrderItem[]>(() =>
    (this.order()?.orderData?.orderItems ?? []).filter(i => i.itemType === 'Flight')
  );

  ancillaryItems = computed<OrderItem[]>(() =>
    (this.order()?.orderData?.orderItems ?? []).filter(i => i.itemType !== 'Flight')
  );

  ssrItems = computed<OrderItem[]>(() =>
    (this.order()?.orderData?.orderItems ?? []).filter(i => i.itemType === 'SSR')
  );

  ssrOptionsByCategory = computed<Record<string, SsrOption[]>>(() => {
    const groups: Record<string, SsrOption[]> = {};
    for (const opt of this.ssrOptions()) {
      if (!groups[opt.category]) groups[opt.category] = [];
      groups[opt.category].push(opt);
    }
    return groups;
  });

  itemTotals = computed<ItemTotals | null>(() =>
    this.order()?.orderData?.itemTotals ?? null
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
        this.tickets.set([]);
        this.loadTickets();
      } else {
        this.error.set(`Order "${this.bookingRef}" was not found.`);
      }
    } catch {
      this.error.set('Failed to load order details. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  switchTab(tab: 'orderItems' | 'payments' | 'history' | 'tickets'): void {
    this.activeTab.set(tab);
    if (tab === 'tickets') this.loadTickets();
  }

  openPaxModal(pax: OrderPassenger): void {
    this.selectedPassenger.set(pax);
    this.editingPaxId.set(null);
    this.editError.set('');
  }

  closePaxModal(): void {
    this.selectedPassenger.set(null);
    this.editingPaxId.set(null);
    this.editError.set('');
  }

  openItemModal(item: OrderItem): void {
    this.selectedOrderItem.set(item);
    this.ssrError.set('');
    if (item.itemType === 'SSR') this.loadSsrOptions();
  }

  closeItemModal(): void {
    this.selectedOrderItem.set(null);
    this.ssrError.set('');
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

  copyText(text: string, event?: Event): void {
    event?.stopPropagation();
    navigator.clipboard.writeText(text).then(() => {
      this.copiedText.set(text);
      setTimeout(() => this.copiedText.set(null), 2000);
    });
  }

  startEdit(pax: OrderPassenger): void {
    this.editingPaxId.set(pax.passengerId);
    this.editError.set('');
    this.editForm.set({
      givenName: pax.givenName,
      surname: pax.surname,
      dob: pax.dob,
      email: pax.contacts?.email ?? null,
      phone: pax.contacts?.phone ?? null,
    });
  }

  cancelEdit(): void {
    this.editingPaxId.set(null);
    this.editError.set('');
  }

  updateEditField(field: keyof EditForm, value: string): void {
    this.editForm.update(f => ({ ...f, [field]: value || null }));
  }

  async saveEdit(pax: OrderPassenger): Promise<void> {
    this.editSaving.set(true);
    this.editError.set('');
    const form = this.editForm();
    const updated: OrderPassenger = {
      ...pax,
      givenName: form.givenName,
      surname: form.surname,
      dob: form.dob,
      contacts: { email: form.email, phone: form.phone },
    };
    const updatedPassengers = this.passengers().map(p =>
      p.passengerId === pax.passengerId ? updated : p
    );
    try {
      await this.#orderService.updateOrderPassengers(this.bookingRef, updatedPassengers);
      this.editingPaxId.set(null);
      this.selectedPassenger.set(null);
      await this.loadOrder();
    } catch {
      this.editError.set('Failed to save changes. Please try again.');
    } finally {
      this.editSaving.set(false);
    }
  }

  statusBadgeClass(status: string): string {
    return {
      Confirmed: 'badge-confirmed',
      Standby:   'badge-standby',
      Cancelled: 'badge-cancelled',
      Changed:   'badge-changed',
      Draft:     'badge-draft',
    }[status] ?? 'badge-default';
  }

  itemTypeLabel(type: string): string {
    return type === 'SSR' ? 'Service' : type;
  }

  itemTypeBadgeClass(type: string): string {
    return {
      Flight: 'item-flight',
      Seat: 'item-seat',
      Bag: 'item-bag',
      SSR: 'item-ssr',
      Product: 'item-product',
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
      currency: currency || this.order()?.currency || 'GBP',
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

  async loadTickets(): Promise<void> {
    if (this.tickets().length > 0) return;
    this.ticketsLoading.set(true);
    this.ticketsError.set('');
    try {
      const result = await this.#orderService.getTicketsByBookingRef(this.bookingRef);
      this.tickets.set(result);
    } catch {
      this.ticketsError.set('Failed to load tickets. Please try again.');
    } finally {
      this.ticketsLoading.set(false);
    }
  }

  activeTickets = computed<Ticket[]>(() =>
    this.tickets().filter(t => !t.isVoided)
  );

  voidedTickets = computed<Ticket[]>(() =>
    this.tickets().filter(t => t.isVoided)
  );

  openTicketModal(ticket: Ticket): void {
    this.selectedTicket.set(ticket);
  }

  closeTicketModal(): void {
    this.selectedTicket.set(null);
  }

  couponStatusLabel(status: string): string {
    return {
      O: 'Open', A: 'Airport control', C: 'Checked in',
      B: 'Boarded', F: 'Flown', R: 'Refunded', E: 'Exchanged',
      V: 'Void', S: 'Suspended',
    }[status] ?? status;
  }

  async loadSsrOptions(): Promise<void> {
    if (this.ssrOptions().length > 0) return;
    this.ssrOptionsLoading.set(true);
    this.ssrOptionsError.set('');
    try {
      const opts = await this.#orderService.getSsrOptions();
      this.ssrOptions.set(opts);
    } catch {
      this.ssrOptionsError.set('Failed to load service catalogue.');
    } finally {
      this.ssrOptionsLoading.set(false);
    }
  }

  ssrKey(item: OrderItem): string {
    return `${item.ssrCode}|${item.passengerId}|${item.segmentId}`;
  }

  async removeSsr(item: OrderItem): Promise<void> {
    this.ssrSaving.set(true);
    this.ssrError.set('');
    const action: SsrPatchAction = {
      action: 'remove',
      ssrCode: item.ssrCode ?? '',
      passengerRef: item.passengerId ?? '',
      segmentRef: item.segmentId ?? '',
    };
    try {
      await this.#orderService.updateOrderSsrs(this.bookingRef, [action]);
      this.closeItemModal();
      await this.loadOrder();
    } catch (err: any) {
      this.ssrError.set(
        err?.status === 422
          ? 'Cannot remove service: within the 24-hour amendment cut-off window.'
          : 'Failed to remove service. Please try again.'
      );
    } finally {
      this.ssrSaving.set(false);
    }
  }

  startEditSsr(item: OrderItem): void {
    this.editingSsrKey.set(this.ssrKey(item));
    this.editSsrForm.set({
      ssrCode: item.ssrCode ?? '',
      passengerRef: item.passengerId ?? '',
      segmentRef: item.segmentId ?? '',
    });
    this.ssrError.set('');
  }

  cancelEditSsr(): void {
    this.editingSsrKey.set(null);
    this.ssrError.set('');
  }

  updateEditSsrField(field: keyof SsrEditForm, value: string): void {
    this.editSsrForm.update(f => ({ ...f, [field]: value }));
  }

  ssrExistsForPaxSegment(ssrCode: string, passengerRef: string, segmentRef: string, excludeKey?: string): boolean {
    return this.ssrItems().some(item =>
      item.ssrCode === ssrCode &&
      item.passengerId === passengerRef &&
      item.segmentId === segmentRef &&
      (!excludeKey || this.ssrKey(item) !== excludeKey)
    );
  }

  async saveEditSsr(original: OrderItem): Promise<void> {
    const form = this.editSsrForm();
    if (!form.ssrCode || !form.passengerRef || !form.segmentRef) return;
    if (this.ssrExistsForPaxSegment(form.ssrCode, form.passengerRef, form.segmentRef, this.ssrKey(original))) {
      this.ssrError.set('A service already exists for this passenger and segment. Remove it before adding a new one.');
      return;
    }
    this.ssrSaving.set(true);
    this.ssrError.set('');
    const actions: SsrPatchAction[] = [
      { action: 'remove', ssrCode: original.ssrCode ?? '', passengerRef: original.passengerId ?? '', segmentRef: original.segmentId ?? '' },
      { action: 'add', ssrCode: form.ssrCode, passengerRef: form.passengerRef, segmentRef: form.segmentRef },
    ];
    try {
      await this.#orderService.updateOrderSsrs(this.bookingRef, actions);
      this.editingSsrKey.set(null);
      await this.loadOrder();
    } catch (err: any) {
      this.ssrError.set(
        err?.status === 422
          ? (err?.error?.message ?? 'Cannot edit service: within the 24-hour amendment cut-off window.')
          : 'Failed to save service changes. Please try again.'
      );
    } finally {
      this.ssrSaving.set(false);
    }
  }

  async addSsr(): Promise<void> {
    const form = this.addSsrForm();
    const allSegments = this.addSsrAllSegments();
    if (!form.ssrCode || !form.passengerRef) return;
    if (!allSegments && !form.segmentRef) return;

    const segmentRefs = allSegments
      ? this.segments().map(s => s.segmentId)
      : [form.segmentRef];

    const duplicate = segmentRefs.find(segRef =>
      this.ssrExistsForPaxSegment(form.ssrCode, form.passengerRef, segRef)
    );
    if (duplicate) {
      this.ssrError.set('A service already exists for this passenger on one or more selected segments. Remove it before adding a new one.');
      return;
    }

    this.ssrSaving.set(true);
    this.ssrError.set('');
    const actions: SsrPatchAction[] = segmentRefs.map(segRef => ({
      action: 'add',
      ssrCode: form.ssrCode,
      passengerRef: form.passengerRef,
      segmentRef: segRef,
    }));
    try {
      await this.#orderService.updateOrderSsrs(this.bookingRef, actions);
      this.cancelAddSsr();
      await this.loadOrder();
    } catch (err: any) {
      this.ssrError.set(
        err?.status === 422
          ? (err?.error?.message ?? 'Cannot add service: within the 24-hour amendment cut-off window.')
          : 'Failed to add service. Please try again.'
      );
    } finally {
      this.ssrSaving.set(false);
    }
  }

  cancelAddSsr(): void {
    this.showAddSsr.set(false);
    this.addSsrForm.set({ ssrCode: '', passengerRef: '', segmentRef: '' });
    this.addSsrAllSegments.set(false);
    this.ssrError.set('');
  }

  updateAddSsrField(field: keyof SsrEditForm, value: string): void {
    this.addSsrForm.update(f => ({ ...f, [field]: value }));
  }

  // TODO: Remove when debug endpoints are removed
  async openDebug(): Promise<void> {
    this.showDebug.set(true);
    this.switchDebugTab('order');
  }

  async switchDebugTab(tab: 'order' | 'tickets'): Promise<void> {
    this.debugTab.set(tab);
    if (tab === 'order') {
      if (this.debugOrderJson()) return;
      this.debugOrderLoading.set(true);
      this.debugOrderError.set('');
      try {
        const data = await this.#orderService.getOrderDebug(this.bookingRef);
        this.debugOrderJson.set(JSON.stringify(data, null, 2));
      } catch {
        this.debugOrderError.set('Failed to load order debug data.');
      } finally {
        this.debugOrderLoading.set(false);
      }
    } else {
      if (this.debugTicketsJson()) return;
      this.debugTicketsLoading.set(true);
      this.debugTicketsError.set('');
      try {
        const data = await this.#orderService.getOrderDebugTickets(this.bookingRef);
        this.debugTicketsJson.set(JSON.stringify(data, null, 2));
      } catch {
        this.debugTicketsError.set('Failed to load tickets debug data.');
      } finally {
        this.debugTicketsLoading.set(false);
      }
    }
  }

  closeDebug(): void {
    this.showDebug.set(false);
  }

  getSsrLabel(ssrCode: string | null | undefined): string {
    if (!ssrCode) return '—';
    const opt = this.ssrOptions().find(o => o.ssrCode === ssrCode);
    return opt ? opt.label : ssrCode;
  }

  getPassengerName(passengerRef: string): string {
    const pax = this.passengers().find(p => p.passengerId === passengerRef);
    return pax ? `${pax.givenName} ${pax.surname}` : passengerRef;
  }

  getSegmentLabel(segmentRef: string): string {
    const seg = this.segments().find(s => s.segmentId === segmentRef);
    return seg ? `${seg.flightNumber} (${seg.origin}→${seg.destination})` : segmentRef;
  }
}
