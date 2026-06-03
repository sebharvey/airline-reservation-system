import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LucideAngularModule } from 'lucide-angular';
import {
  InventoryService,
  InventoryOrders,
  InventoryOrderRow,
  SalesPerformance,
} from '../../../services/inventory.service';

interface OrderLine {
  itemType: 'Flight' | 'Seat' | 'Bag' | 'Product';
  description: string;
  cabinCode: string | null;
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

interface ChartTick { pos: number; label: string; }
interface ChartBar  { x: number; y: number; w: number; h: number; }

interface RevenueChartData {
  areaPath: string;
  linePath: string;
  yTicks: ChartTick[];
  xLabels: ChartTick[];
  xAxisY: number;
  xMin: number;
  xMax: number;
}

interface OrdersChartData {
  orderBars: ChartBar[];
  paxBars: ChartBar[];
  yTicks: ChartTick[];
  xLabels: ChartTick[];
  xAxisY: number;
  xMin: number;
  xMax: number;
}

interface PerfSummary {
  totalRevenue: number;
  totalOrders: number;
  totalPax: number;
  avgRevPerOrder: number;
  currency: string;
}

interface CabinPriceChartData {
  cabinCode: string;
  cabinLabel: string;
  currency: string;
  seatsSold: number;
  minFare: number;
  maxFare: number;
  priceIncrease: number;
  stepPath: string;
  areaPath: string;
  yTicks: ChartTick[];
  xTicks: ChartTick[];
  xAxisY: number;
  xMin: number;
  xMax: number;
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

  #inventoryId = '';

  // ── Orders state ──────────────────────────────────────────────────────────
  loading = signal(true);
  error = signal('');
  data = signal<InventoryOrders | null>(null);
  copiedRef = signal<string | null>(null);

  // ── Performance state ─────────────────────────────────────────────────────
  performanceDays = signal(21);
  performanceData = signal<SalesPerformance | null>(null);
  performanceLoading = signal(false);
  performanceError = signal('');

  // ── Chart geometry constants ───────────────────────────────────────────────
  readonly #REV   = { VW: 600, VH: 200, L: 56, R: 20, T: 15, B: 45 };
  readonly #ORD   = { VW: 600, VH: 180, L: 40, R: 20, T: 15, B: 40 };
  readonly #PRICE = { VW: 280, VH: 160, L: 56, R: 12, T: 12, B: 36 };

  // ── Derived order data ─────────────────────────────────────────────────────
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
        lines.push({
          itemType: 'Flight',
          description: [row.fareFamily, row.fareBasisCode].filter(Boolean).join(' / ') || '—',
          cabinCode: row.cabinCode,
          fare:  row.baseFareAmount,
          tax:   row.taxAmount,
          total: row.totalFareAmount,
          currency: row.currency,
        });
        for (const anc of row.ancillaries) {
          lines.push({
            itemType: anc.productType as 'Seat' | 'Bag' | 'Product',
            description: anc.description,
            cabinCode: null,
            fare:  null,
            tax:   null,
            total: anc.amount > 0 ? anc.amount : null,
            currency: row.currency,
          });
        }
      }
      const passengerCount = rows[0]?.paxCount ?? rows.length;
      return { bookingReference: bookingRef, passengerCount, lines };
    });
  });

  stats = computed<FlightStats>(() => {
    const orders = this.data()?.orders ?? [];
    const bookingRefs = new Set(orders.map(r => r.bookingReference));
    let totalFare = 0, totalTax = 0, totalAncillary = 0, passengerCount = 0;
    for (const row of orders) {
      passengerCount += row.paxCount;
      totalFare      += row.baseFareAmount  ?? 0;
      totalTax       += row.taxAmount       ?? 0;
      totalAncillary += row.ancillaries.reduce((s, a) => s + a.amount, 0);
    }
    return {
      bookingCount:   bookingRefs.size,
      passengerCount,
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
      entry.paxCount += row.paxCount;
      entry.revenue += (row.baseFareAmount ?? 0) + (row.taxAmount ?? 0);
      map.set(name, entry);
    }
    return Array.from(map.entries())
      .map(([name, v]) => ({ name, paxCount: v.paxCount, revenue: Math.round(v.revenue * 100) / 100 }))
      .sort((a, b) => b.revenue - a.revenue);
  });

  // ── Performance chart computed ─────────────────────────────────────────────
  performanceSummary = computed<PerfSummary | null>(() => {
    const d = this.performanceData();
    if (!d || d.days.length === 0) return null;
    const totalRevenue = d.days.reduce((s, x) => s + x.revenue + x.ancillaryRevenue, 0);
    const totalOrders  = d.days.reduce((s, x) => s + x.orderCount, 0);
    const totalPax     = d.days.reduce((s, x) => s + x.passengerCount, 0);
    return {
      totalRevenue:   Math.round(totalRevenue * 100) / 100,
      totalOrders,
      totalPax,
      avgRevPerOrder: totalOrders > 0 ? Math.round(totalRevenue / totalOrders * 100) / 100 : 0,
      currency: d.currency,
    };
  });

  revenueChart = computed<RevenueChartData | null>(() => {
    const d = this.performanceData();
    if (!d || d.days.length === 0) return null;
    const { VW, VH, L, R, T, B } = this.#REV;
    const PW = VW - L - R;
    const PH = VH - T - B;
    const days = d.days;
    const n = days.length;
    const maxVal = Math.max(...days.map(x => x.revenue + x.ancillaryRevenue), 1);

    const px = (i: number) => n > 1 ? L + (i / (n - 1)) * PW : L + PW / 2;
    const py = (v: number) => T + (1 - v / maxVal) * PH;

    const pts = days.map((x, i) =>
      `${px(i).toFixed(1)},${py(x.revenue + x.ancillaryRevenue).toFixed(1)}`);
    const linePath = `M ${pts.join(' L ')}`;
    const bottom = (T + PH).toFixed(1);
    const areaPath = `${linePath} L ${(L + PW).toFixed(1)},${bottom} L ${L},${bottom} Z`;

    const yTicks: ChartTick[] = [0, 0.33, 0.67, 1].map(pct => ({
      pos:   py(maxVal * pct),
      label: this.#shortAmount(maxVal * pct, d.currency),
    }));

    const step = n > 14 ? 7 : n > 7 ? 3 : 1;
    const xLabels: ChartTick[] = days
      .map((x, i) => ({ pos: px(i), label: x.date.slice(5).replace('-', '/'), i }))
      .filter(({ i }) => i === 0 || i === n - 1 || i % step === 0)
      .map(({ pos, label }) => ({ pos, label }));

    return { areaPath, linePath, yTicks, xLabels, xAxisY: T + PH, xMin: L, xMax: L + PW };
  });

  ordersChart = computed<OrdersChartData | null>(() => {
    const d = this.performanceData();
    if (!d || d.days.length === 0) return null;
    const { VW, VH, L, R, T, B } = this.#ORD;
    const PW = VW - L - R;
    const PH = VH - T - B;
    const days = d.days;
    const n = days.length;
    const maxOrders = Math.max(...days.map(x => x.orderCount), 1);
    const maxPax    = Math.max(...days.map(x => x.passengerCount), 1);
    const maxVal    = Math.max(maxOrders, maxPax);
    const bottom    = T + PH;
    const slotW     = PW / n;
    const barW      = Math.max((slotW - 4) / 2, 2);

    const orderBars: ChartBar[] = days.map((x, i) => {
      const h = n === 0 ? 0 : (x.orderCount / maxVal) * PH;
      return { x: L + slotW * i + 1, y: bottom - h, w: barW, h };
    });
    const paxBars: ChartBar[] = days.map((x, i) => {
      const h = n === 0 ? 0 : (x.passengerCount / maxVal) * PH;
      return { x: L + slotW * i + 1 + barW + 2, y: bottom - h, w: barW, h };
    });

    const py = (pct: number) => T + (1 - pct) * PH;
    const yTicks: ChartTick[] = [0, 0.5, 1].map(pct => ({
      pos:   py(pct),
      label: Math.round(maxVal * pct).toString(),
    }));

    const step = n > 14 ? 7 : n > 7 ? 3 : 1;
    const xLabels: ChartTick[] = days
      .map((x, i) => ({ pos: L + slotW * i + slotW / 2, label: x.date.slice(5).replace('-', '/'), i }))
      .filter(({ i }) => i === 0 || i === n - 1 || i % step === 0)
      .map(({ pos, label }) => ({ pos, label }));

    return { orderBars, paxBars, yTicks, xLabels, xAxisY: bottom, xMin: L, xMax: L + PW };
  });

  // ── Dynamic pricing chart ─────────────────────────────────────────────────
  cabinPriceCharts = computed<CabinPriceChartData[]>(() => {
    const d = this.data();
    if (!d?.orders?.length) return [];

    const { VW, VH, L, R, T, B } = this.#PRICE;
    const PW = VW - L - R;
    const PH = VH - T - B;
    const btm = T + PH;

    const labels: Record<string, string> = {
      F: 'First', J: 'Business', W: 'Premium Economy', Y: 'Economy',
    };
    const currency = d.orders.find(o => o.currency)?.currency ?? 'GBP';

    const byGabin = new Map<string, { fare: number; pax: number }[]>();
    for (const row of d.orders) {
      if (!row.baseFareAmount || row.paxCount <= 0) continue;
      const key = row.cabinCode.toUpperCase();
      const perPaxFare = row.baseFareAmount / row.paxCount;
      const arr = byGabin.get(key) ?? [];
      arr.push({ fare: perPaxFare, pax: row.paxCount });
      byGabin.set(key, arr);
    }

    const charts: CabinPriceChartData[] = [];

    for (const [code, label] of Object.entries(labels)) {
      const entries = byGabin.get(code);
      if (!entries || entries.length < 2) continue;

      const sorted = [...entries].sort((a, b) => a.fare - b.fare);
      const totalSold = sorted.reduce((s, e) => s + e.pax, 0);
      const fares = sorted.map(e => e.fare);
      const minFare = Math.min(...fares);
      const maxFare = Math.max(...fares);
      if (maxFare === minFare) continue;

      const px = (seats: number) => L + (seats / Math.max(totalSold, 1)) * PW;
      const py = (fare: number)  => T + (1 - (fare - minFare) / (maxFare - minFare)) * PH;

      let cumSeats = 0;
      const pathParts: string[] = [];
      for (let i = 0; i < sorted.length; i++) {
        const { fare, pax } = sorted[i];
        if (i === 0) {
          pathParts.push(`M ${px(0).toFixed(1)},${py(fare).toFixed(1)}`);
        } else {
          pathParts.push(`V ${py(fare).toFixed(1)}`);
        }
        cumSeats += pax;
        pathParts.push(`H ${px(cumSeats).toFixed(1)}`);
      }
      const stepPath = pathParts.join(' ');
      const areaPath = `${stepPath} V ${btm.toFixed(1)} H ${px(0).toFixed(1)} Z`;

      const yTicks: ChartTick[] = [minFare, (minFare + maxFare) / 2, maxFare].map(v => ({
        pos:   py(v),
        label: this.#shortAmount(v, currency),
      }));

      const xTicks: ChartTick[] = [0, Math.round(totalSold / 2), totalSold].map(v => ({
        pos:   px(v),
        label: v.toString(),
      }));

      charts.push({
        cabinCode: code,
        cabinLabel: label,
        currency,
        seatsSold: totalSold,
        minFare,
        maxFare,
        priceIncrease: Math.round(((maxFare - minFare) / minFare) * 100),
        stepPath,
        areaPath,
        yTicks,
        xTicks,
        xAxisY: btm,
        xMin: L,
        xMax: L + PW,
      });
    }

    return charts;
  });

  // ── Lifecycle ─────────────────────────────────────────────────────────────
  async ngOnInit(): Promise<void> {
    const inventoryId = this.#route.snapshot.paramMap.get('inventoryId') ?? '';
    this.#inventoryId = inventoryId;
    await Promise.all([
      this.#loadOrders(inventoryId),
      this.#loadPerformance(inventoryId),
    ]);
  }

  async #loadOrders(inventoryId: string): Promise<void> {
    try {
      const result = await this.#inventoryService.getInventoryOrders(inventoryId);
      this.data.set(result);
    } catch {
      this.error.set('Failed to load flight orders. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  async #loadPerformance(inventoryId: string): Promise<void> {
    this.performanceLoading.set(true);
    this.performanceError.set('');
    try {
      const result = await this.#inventoryService.getSalesPerformance(inventoryId, this.performanceDays());
      this.performanceData.set(result);
    } catch {
      this.performanceError.set('Failed to load performance data.');
    } finally {
      this.performanceLoading.set(false);
    }
  }

  async setDays(days: number): Promise<void> {
    this.performanceDays.set(days);
    await this.#loadPerformance(this.#inventoryId);
  }

  async refreshPerformance(): Promise<void> {
    await this.#loadPerformance(this.#inventoryId);
  }

  // ── Navigation ────────────────────────────────────────────────────────────
  goBack(): void {
    this.#router.navigate(['/inventory']);
  }

  // ── Helpers ───────────────────────────────────────────────────────────────
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

  cabinColor(code: string): string {
    switch (code) {
      case 'F': return '#d29922';
      case 'J': return '#6366f1';
      case 'W': return '#58a6ff';
      default:  return '#3fb950';
    }
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

  #shortAmount(amount: number, currency: string): string {
    if (amount === 0) return '0';
    const sym = currency === 'GBP' ? '£' : currency === 'USD' ? '$' : currency === 'EUR' ? '€' : '';
    if (amount >= 1_000_000) return `${sym}${(amount / 1_000_000).toFixed(1)}M`;
    if (amount >= 1_000)     return `${sym}${(amount / 1_000).toFixed(0)}k`;
    return `${sym}${amount.toFixed(0)}`;
  }
}
