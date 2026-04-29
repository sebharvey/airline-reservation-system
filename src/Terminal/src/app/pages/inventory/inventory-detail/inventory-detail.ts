import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LucideAngularModule } from 'lucide-angular';
import {
  InventoryService,
  InventoryOrders,
  InventoryOrderRow,
} from '../../../services/inventory.service';

interface OrderLine {
  passengerName: string | null;
  passengerType: string | null;
  showName: boolean;
  itemType: 'Flight' | 'Seat' | 'Bag' | 'Product';
  description: string;
  cabinCode: string | null;
  seatNumber: string | null;
  fare: number | null;
  tax: number | null;
  total: number | null;
  currency: string;
}

interface OrderGroup {
  bookingReference: string;
  passengerCount: number;
  lines: OrderLine[];
}

interface FlightStats {
  bookingCount: number;
  passengerCount: number;
  totalFare: number;
  totalTax: number;
  totalAncillary: number;
  grandTotal: number;
}

interface FareFamilyStat {
  name: string;
  paxCount: number;
  revenue: number;
}

@Component({
  selector: 'app-inventory-detail',
  templateUrl: './inventory-detail.html',
  styleUrl: './inventory-detail.css',
  imports: [RouterLink, LucideAngularModule],
})
export class InventoryDetailComponent implements OnInit {
  #route = inject(ActivatedRoute);
  #router = inject(Router);
  #inventoryService = inject(InventoryService);

  loading = signal(true);
  error = signal('');
  data = signal<InventoryOrders | null>(null);
  copiedRef = signal<string | null>(null);

  cabinRows = computed(() => {
    const d = this.data();
    if (!d?.cabins) return [];
    const rows = [];
    if (d.cabins.f && d.cabins.f.totalSeats > 0) rows.push({ code: 'F', label: 'First',           data: d.cabins.f });
    if (d.cabins.j && d.cabins.j.totalSeats > 0) rows.push({ code: 'J', label: 'Business',        data: d.cabins.j });
    if (d.cabins.w && d.cabins.w.totalSeats > 0) rows.push({ code: 'W', label: 'Premium Economy', data: d.cabins.w });
    if (d.cabins.y && d.cabins.y.totalSeats > 0) rows.push({ code: 'Y', label: 'Economy',         data: d.cabins.y });
    return rows;
  });

  /** Groups per-passenger rows by booking reference and expands each into individual order lines. */
  groupedOrders = computed<OrderGroup[]>(() => {
    const orders = this.data()?.orders ?? [];
    const map = new Map<string, InventoryOrderRow[]>();
    for (const row of orders) {
      const arr = map.get(row.bookingReference) ?? [];
      arr.push(row);
      map.set(row.bookingReference, arr);
    }

    return Array.from(map.entries()).map(([bookingRef, rows]) => {
      const lines: OrderLine[] = [];
      for (const row of rows) {
        // Flight fare line — always the first entry for this passenger.
        lines.push({
          passengerName: row.passengerName,
          passengerType: row.passengerType,
          showName: true,
          itemType: 'Flight',
          description: [row.fareFamily, row.fareBasisCode].filter(Boolean).join(' / ') || '—',
          cabinCode: row.cabinCode,
          seatNumber: row.seatNumber,
          fare:  row.baseFareAmount,
          tax:   row.taxAmount,
          total: row.totalFareAmount,
          currency: row.currency,
        });

        // One line per ancillary item for this passenger.
        for (const anc of row.ancillaries) {
          lines.push({
            passengerName: row.passengerName,
            passengerType: row.passengerType,
            showName: false,
            itemType: anc.productType as 'Seat' | 'Bag' | 'Product',
            description: anc.description,
            cabinCode: null,
            seatNumber: null,
            fare:  null,
            tax:   null,
            total: anc.amount > 0 ? anc.amount : null,
            currency: row.currency,
          });
        }
      }
      return { bookingReference: bookingRef, passengerCount: rows.length, lines };
    });
  });

  stats = computed<FlightStats>(() => {
    const orders = this.data()?.orders ?? [];
    const bookingRefs = new Set(orders.map(r => r.bookingReference));
    let totalFare = 0, totalTax = 0, totalAncillary = 0;
    for (const row of orders) {
      totalFare      += row.baseFareAmount  ?? 0;
      totalTax       += row.taxAmount       ?? 0;
      totalAncillary += row.ancillaries.reduce((s, a) => s + a.amount, 0);
    }
    return {
      bookingCount:   bookingRefs.size,
      passengerCount: orders.length,
      totalFare:      Math.round(totalFare      * 100) / 100,
      totalTax:       Math.round(totalTax       * 100) / 100,
      totalAncillary: Math.round(totalAncillary * 100) / 100,
      grandTotal:     Math.round((totalFare + totalTax + totalAncillary) * 100) / 100,
    };
  });

  fareFamilyStats = computed<FareFamilyStat[]>(() => {
    const orders = this.data()?.orders ?? [];
    const map = new Map<string, { paxCount: number; revenue: number }>();
    for (const row of orders) {
      const name = row.fareFamily ?? '(Unknown)';
      const entry = map.get(name) ?? { paxCount: 0, revenue: 0 };
      entry.paxCount++;
      entry.revenue += (row.baseFareAmount ?? 0) + (row.taxAmount ?? 0);
      map.set(name, entry);
    }
    return Array.from(map.entries())
      .map(([name, v]) => ({ name, paxCount: v.paxCount, revenue: Math.round(v.revenue * 100) / 100 }))
      .sort((a, b) => b.revenue - a.revenue);
  });

  async ngOnInit(): Promise<void> {
    const inventoryId = this.#route.snapshot.paramMap.get('inventoryId') ?? '';
    try {
      const result = await this.#inventoryService.getInventoryOrders(inventoryId);
      this.data.set(result);
    } catch {
      this.error.set('Failed to load flight orders. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  goBack(): void {
    this.#router.navigate(['/inventory']);
  }

  soldPct(cabin: { totalSeats: number; seatsSold: number; seatsHeld: number }): number {
    if (cabin.totalSeats === 0) return 0;
    return Math.round((cabin.seatsSold + cabin.seatsHeld) / cabin.totalSeats * 100);
  }

  donutDeg(cabin: { totalSeats: number; seatsSold: number; seatsHeld: number }): number {
    return Math.round(this.soldPct(cabin) * 3.6);
  }

  statusClass(status: string): string {
    if (status === 'Active') return 'badge-active';
    if (status === 'Ticketing Closed') return 'badge-warning';
    return 'badge-inactive';
  }

  cabinClass(code: string): string {
    switch (code) {
      case 'F': return 'cabin-f';
      case 'J': return 'cabin-j';
      case 'W': return 'cabin-w';
      default:  return 'cabin-y';
    }
  }

  lineIcon(type: string): string {
    switch (type) {
      case 'Flight':  return 'plane';
      case 'Seat':    return 'armchair';
      case 'Bag':     return 'luggage';
      case 'Product': return 'package';
      default:        return 'tag';
    }
  }

  ptcLabel(ptc: string | null): string {
    switch (ptc) {
      case 'ADT': return 'Adult';
      case 'CHD': return 'Child';
      case 'INF': return 'Infant';
      case 'YTH': return 'Youth';
      default:    return ptc ?? '';
    }
  }

  formatAmount(amount: number | null, currency: string): string {
    if (amount === null || amount === undefined) return '—';
    return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(amount);
  }

  copyBookingRef(ref: string, event: Event): void {
    event.stopPropagation();
    navigator.clipboard.writeText(ref).then(() => {
      this.copiedRef.set(ref);
      setTimeout(() => this.copiedRef.set(null), 2000);
    });
  }
}
