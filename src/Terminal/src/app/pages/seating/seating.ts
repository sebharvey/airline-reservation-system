import { LucideAngularModule } from 'lucide-angular';
import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  SeatPricingService,
  SeatPricing,
  CreateSeatPricingRequest,
  UpdateSeatPricingRequest,
} from '../../services/seat-pricing.service';

@Component({
  selector: 'app-seating',
  imports: [FormsModule, LucideAngularModule],
  templateUrl: './seating.html',
  styleUrl: './seating.css',
})
export class SeatingComponent implements OnInit {
  #service = inject(SeatPricingService);

  pricings = signal<SeatPricing[]>([]);
  filter = signal('');
  loading = signal(false);
  error = signal('');
  success = signal('');
  loaded = signal(false);

  // Form state
  showForm = signal(false);
  editing = signal<SeatPricing | null>(null);
  saving = signal(false);
  deleting = signal<string | null>(null);

  // Form fields
  form = signal<CreateSeatPricingRequest>({
    cabinCode: 'Y',
    seatPosition: 'Window',
    currencyCode: 'GBP',
    price: 0,
    validFrom: '',
    validTo: null,
  });

  // Edit-only extra fields (isActive)
  formIsActive = signal(true);

  filtered = computed(() => {
    const q = this.filter().toLowerCase().trim();
    const all = this.pricings();
    if (!q) return all;
    return all.filter(
      p =>
        p.cabinCode.toLowerCase().includes(q) ||
        p.seatPosition.toLowerCase().includes(q) ||
        p.currencyCode.toLowerCase().includes(q)
    );
  });

  stats = computed(() => {
    const all = this.pricings();
    const activePricings = all.filter(p => p.isActive).length;
    const cabins = new Set(all.map(p => p.cabinCode)).size;
    return { total: all.length, active: activePricings, cabins };
  });

  ngOnInit(): void {
    this.loadPricings();
  }

  async loadPricings(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#service.getAll();
      this.pricings.set(result);
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load seat pricing rules. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  setFilter(val: string): void {
    this.filter.set(val);
  }

  openCreateForm(): void {
    this.editing.set(null);
    this.form.set({
      cabinCode: 'Y',
      seatPosition: 'Window',
      currencyCode: 'GBP',
      price: 0,
      validFrom: '',
      validTo: null,
    });
    this.formIsActive.set(true);
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  openEditForm(pricing: SeatPricing): void {
    this.editing.set(pricing);
    this.form.set({
      cabinCode: pricing.cabinCode,
      seatPosition: pricing.seatPosition,
      currencyCode: pricing.currencyCode,
      price: pricing.price,
      validFrom: pricing.validFrom ? pricing.validFrom.substring(0, 10) : '',
      validTo: pricing.validTo ? pricing.validTo.substring(0, 10) : null,
    });
    this.formIsActive.set(pricing.isActive);
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editing.set(null);
  }

  updateField(field: string, value: unknown): void {
    this.form.update(f => ({ ...f, [field]: value }));
  }

  async savePricing(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    this.success.set('');

    const data = this.form();

    try {
      const editingPricing = this.editing();
      if (editingPricing) {
        const request: UpdateSeatPricingRequest = {
          cabinCode: data.cabinCode,
          seatPosition: data.seatPosition,
          currencyCode: data.currencyCode,
          price: data.price,
          isActive: this.formIsActive(),
          validFrom: data.validFrom ? `${data.validFrom}T00:00:00Z` : null,
          validTo: data.validTo ? `${data.validTo}T23:59:59Z` : null,
        };
        await this.#service.update(editingPricing.seatPricingId, request);
        this.success.set('Seat pricing rule updated successfully.');
      } else {
        const request: CreateSeatPricingRequest = {
          ...data,
          validFrom: data.validFrom ? `${data.validFrom}T00:00:00Z` : new Date().toISOString(),
          validTo: data.validTo ? `${data.validTo}T23:59:59Z` : null,
        };
        await this.#service.create(request);
        this.success.set('Seat pricing rule created successfully.');
      }
      this.showForm.set(false);
      this.editing.set(null);
      await this.loadPricings();
    } catch {
      this.error.set('Failed to save seat pricing rule. Please check the data and try again.');
    } finally {
      this.saving.set(false);
    }
  }

  async deletePricing(seatPricingId: string): Promise<void> {
    this.deleting.set(seatPricingId);
    this.error.set('');
    this.success.set('');
    try {
      await this.#service.delete(seatPricingId);
      this.success.set('Seat pricing rule deleted successfully.');
      await this.loadPricings();
    } catch {
      this.error.set('Failed to delete seat pricing rule. Please try again.');
    } finally {
      this.deleting.set(null);
    }
  }

  cabinLabel(code: string): string {
    switch (code) {
      case 'F': return 'First';
      case 'J': return 'Business';
      case 'W': return 'Premium Economy';
      case 'Y': return 'Economy';
      default: return code;
    }
  }

  formatDate(iso: string | null): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  formatPrice(price: number, currency: string): string {
    return `${price.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}\u00A0${currency}`;
  }
}
