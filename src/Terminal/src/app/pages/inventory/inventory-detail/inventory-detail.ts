import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LucideAngularModule } from 'lucide-angular';
import {
  InventoryService,
  InventoryOrders,
  InventoryOrderRow,
} from '../../../services/inventory.service';

@Component({
  selector: 'app-inventory-detail',
  templateUrl: './inventory-detail.html',
  styleUrl: './inventory-detail.css',
  imports: [RouterLink, LucideAngularModule],
})
export class InventoryDetailComponent implements OnInit {
  #route = inject(ActivatedRoute);
  #inventoryService = inject(InventoryService);

  loading = signal(true);
  error = signal('');
  data = signal<InventoryOrders | null>(null);

  copiedRef = signal<string | null>(null);

  cabinRows = computed(() => {
    const d = this.data();
    if (!d?.cabins) return [];
    const map: { code: string; label: string; data: { totalSeats: number; seatsSold: number; seatsAvailable: number; seatsHeld: number } }[] = [];
    if (d.cabins.f && d.cabins.f.totalSeats > 0) map.push({ code: 'F', label: 'First',            data: d.cabins.f });
    if (d.cabins.j && d.cabins.j.totalSeats > 0) map.push({ code: 'J', label: 'Business',         data: d.cabins.j });
    if (d.cabins.w && d.cabins.w.totalSeats > 0) map.push({ code: 'W', label: 'Premium Economy',  data: d.cabins.w });
    if (d.cabins.y && d.cabins.y.totalSeats > 0) map.push({ code: 'Y', label: 'Economy',          data: d.cabins.y });
    return map;
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

  soldPct(cabin: { totalSeats: number; seatsSold: number; seatsHeld: number }): number {
    if (cabin.totalSeats === 0) return 0;
    return Math.round((cabin.seatsSold + cabin.seatsHeld) / cabin.totalSeats * 100);
  }

  availPct(cabin: { totalSeats: number; seatsAvailable: number }): number {
    if (cabin.totalSeats === 0) return 0;
    return Math.round(cabin.seatsAvailable / cabin.totalSeats * 100);
  }

  donutDeg(cabin: { totalSeats: number; seatsSold: number; seatsHeld: number }): number {
    return Math.round(this.soldPct(cabin) * 3.6);
  }

  cabinLabel(code: string): string {
    switch (code) {
      case 'F': return 'First';
      case 'J': return 'Business';
      case 'W': return 'Premium Economy';
      case 'Y': return 'Economy';
      default:  return code;
    }
  }

  hasAncillaries(row: InventoryOrderRow): boolean {
    return row.ancillaries.length > 0;
  }

  ancillaryIcons: Record<string, string> = {
    Seat: 'armchair',
    Bag:  'luggage',
    Product: 'package',
  };

  ancillaryIcon(type: string): string {
    return this.ancillaryIcons[type] ?? 'tag';
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

  ptcLabel(ptc: string | null): string {
    switch (ptc) {
      case 'ADT': return 'Adult';
      case 'CHD': return 'Child';
      case 'INF': return 'Infant';
      case 'YTH': return 'Youth';
      default:    return ptc ?? '';
    }
  }
}
