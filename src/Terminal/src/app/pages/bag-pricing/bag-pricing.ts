import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BagPricingService, BagPricing, CreateBagPricingRequest, UpdateBagPricingRequest } from '../../services/bag-pricing.service';

@Component({
  selector: 'app-bag-pricing',
  imports: [FormsModule],
  templateUrl: './bag-pricing.html',
  styleUrl: './bag-pricing.css',
})
export class BagPricingComponent implements OnInit {
  #service = inject(BagPricingService);

  pricing = signal<BagPricing[]>([]);
  loading = signal(false);
  error = signal('');
  success = signal('');
  loaded = signal(false);

  showForm = signal(false);
  editing = signal<BagPricing | null>(null);
  saving = signal(false);
  deleting = signal<string | null>(null);

  createForm = signal<CreateBagPricingRequest>({
    bagSequence: 1,
    currencyCode: 'GBP',
    price: 60.00,
    validFrom: '',
    validTo: null,
  });

  updateForm = signal<UpdateBagPricingRequest>({
    price: 60.00,
    isActive: true,
    validFrom: '',
    validTo: null,
  });

  stats = computed(() => {
    const all = this.pricing();
    const active = all.filter(p => p.isActive).length;
    const minPrice = all.length ? Math.min(...all.map(p => p.price)) : 0;
    const maxPrice = all.length ? Math.max(...all.map(p => p.price)) : 0;
    return { total: all.length, active, minPrice, maxPrice };
  });

  ngOnInit(): void {
    this.loadPricing();
  }

  async loadPricing(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#service.getAll();
      this.pricing.set(result.sort((a, b) => a.bagSequence - b.bagSequence));
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load bag pricing rules. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  openCreateForm(): void {
    this.editing.set(null);
    this.createForm.set({ bagSequence: 1, currencyCode: 'GBP', price: 60.00, validFrom: '', validTo: null });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  openEditForm(rule: BagPricing): void {
    this.editing.set(rule);
    this.updateForm.set({
      price: rule.price,
      isActive: rule.isActive,
      validFrom: rule.validFrom ? rule.validFrom.substring(0, 10) : '',
      validTo: rule.validTo ? rule.validTo.substring(0, 10) : null,
    });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editing.set(null);
  }

  updateCreateField(field: keyof CreateBagPricingRequest, value: unknown): void {
    this.createForm.update(f => ({ ...f, [field]: value }));
  }

  updateUpdateField(field: keyof UpdateBagPricingRequest, value: unknown): void {
    this.updateForm.update(f => ({ ...f, [field]: value }));
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    try {
      const editingRule = this.editing();
      if (editingRule) {
        const req: UpdateBagPricingRequest = {
          ...this.updateForm(),
          validFrom: this.updateForm().validFrom
            ? `${this.updateForm().validFrom}T00:00:00Z`
            : '',
          validTo: this.updateForm().validTo
            ? `${this.updateForm().validTo}T23:59:59Z`
            : null,
        };
        await this.#service.update(editingRule.pricingId, req);
        this.success.set('Bag pricing rule updated successfully.');
      } else {
        const req: CreateBagPricingRequest = {
          ...this.createForm(),
          validFrom: this.createForm().validFrom
            ? `${this.createForm().validFrom}T00:00:00Z`
            : '',
          validTo: this.createForm().validTo
            ? `${this.createForm().validTo}T23:59:59Z`
            : null,
        };
        await this.#service.create(req);
        this.success.set('Bag pricing rule created successfully.');
      }
      this.showForm.set(false);
      this.editing.set(null);
      await this.loadPricing();
    } catch {
      this.error.set('Failed to save bag pricing rule. Check the data and try again.');
    } finally {
      this.saving.set(false);
    }
  }

  async deleteRule(pricingId: string): Promise<void> {
    this.deleting.set(pricingId);
    this.error.set('');
    this.success.set('');
    try {
      await this.#service.delete(pricingId);
      this.success.set('Bag pricing rule deleted successfully.');
      await this.loadPricing();
    } catch {
      this.error.set('Failed to delete bag pricing rule. Please try again.');
    } finally {
      this.deleting.set(null);
    }
  }

  sequenceLabel(seq: number): string {
    switch (seq) {
      case 1: return '1st additional bag';
      case 2: return '2nd additional bag';
      case 99: return '3rd+ additional bag';
      default: return `Sequence ${seq}`;
    }
  }

  formatAmount(amount: number, currency: string): string {
    return `${currency} ${amount.toFixed(2)}`;
  }

  formatDate(iso: string | null): string {
    if (!iso) return 'Open';
    return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }
}
