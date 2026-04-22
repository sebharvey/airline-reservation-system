import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { InventoryService, IropsOrderItem, IropsOrdersResponse, IropsRebookOrderResponse } from '../../services/inventory.service';

type RebookStatus = 'pending' | 'loading' | 'rebooked' | 'failed';

interface OrderRow {
  order: IropsOrderItem;
  priority: number;
  rebookStatus: RebookStatus;
  rebookResult?: IropsRebookOrderResponse;
}

@Component({
  selector: 'app-disruption',
  templateUrl: './disruption.html',
  styleUrl: './disruption.css',
  imports: [RouterLink],
})
export class DisruptionComponent implements OnInit {
  #inventoryService = inject(InventoryService);
  #route = inject(ActivatedRoute);

  flightNumber = signal('');
  departureDate = signal('');
  flightInfo = signal<IropsOrdersResponse | null>(null);
  rows = signal<OrderRow[]>([]);
  loading = signal(false);
  error = signal('');

  rebookResultModal = signal<OrderRow | null>(null);
  copiedRef = signal<string | null>(null);

  rebookedCount = computed(() => this.rows().filter(r => r.rebookStatus === 'rebooked').length);
  pendingCount = computed(() => this.rows().filter(r => r.rebookStatus === 'pending').length);
  failedCount = computed(() => this.rows().filter(r => r.rebookStatus === 'failed').length);

  async ngOnInit(): Promise<void> {
    const fn = this.#route.snapshot.paramMap.get('flightNumber') ?? '';
    const dd = this.#route.snapshot.paramMap.get('departureDate') ?? '';
    this.flightNumber.set(fn);
    this.departureDate.set(dd);
    await this.loadOrders();
  }

  async loadOrders(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#inventoryService.getIropsOrders(
        this.flightNumber(),
        this.departureDate()
      );
      this.flightInfo.set(result);
      this.rows.set(
        result.orders.map((order, index) => ({
          order,
          priority: index + 1,
          rebookStatus: 'pending',
        }))
      );
    } catch {
      this.error.set('Failed to load affected orders. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  async rebookOrder(row: OrderRow): Promise<void> {
    this.rows.update(rows =>
      rows.map(r => r === row ? { ...r, rebookStatus: 'loading' as RebookStatus } : r)
    );
    try {
      const result = await this.#inventoryService.rebookIropsOrder(
        row.order.bookingReference,
        this.flightNumber(),
        this.departureDate()
      );
      const status: RebookStatus = result.outcome === 'Rebooked' ? 'rebooked' : 'failed';
      this.rows.update(rows =>
        rows.map(r => r.order.bookingReference === row.order.bookingReference
          ? { ...r, rebookStatus: status, rebookResult: result }
          : r
        )
      );
      // Find the updated row and open the result modal
      const updatedRow = this.rows().find(r => r.order.bookingReference === row.order.bookingReference);
      if (updatedRow) this.rebookResultModal.set(updatedRow);
    } catch {
      this.rows.update(rows =>
        rows.map(r => r.order.bookingReference === row.order.bookingReference
          ? {
              ...r,
              rebookStatus: 'failed' as RebookStatus,
              rebookResult: {
                bookingReference: row.order.bookingReference,
                outcome: 'Failed',
                failureReason: 'An unexpected error occurred. Please try again.'
              }
            }
          : r
        )
      );
      const updatedRow = this.rows().find(r => r.order.bookingReference === row.order.bookingReference);
      if (updatedRow) this.rebookResultModal.set(updatedRow);
    }
  }

  copyBookingRef(text: string, event?: Event): void {
    event?.stopPropagation();
    navigator.clipboard.writeText(text).then(() => {
      this.copiedRef.set(text);
      setTimeout(() => this.copiedRef.set(null), 2000);
    });
  }

  closeRebookModal(): void {
    this.rebookResultModal.set(null);
  }

  cabinLabel(code: string): string {
    const labels: Record<string, string> = { F: 'First', J: 'Business', W: 'Prem. Eco', Y: 'Economy' };
    return labels[code] ?? code;
  }

  tierClass(tier?: string): string {
    if (!tier) return 'tier-none';
    return `tier-${tier.toLowerCase()}`;
  }

  formatDate(iso: string): string {
    return iso.slice(0, 10);
  }
}
