import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { BagPolicyService, BagPolicy, CreateBagPolicyRequest, UpdateBagPolicyRequest } from '../../services/bag-policy.service';

@Component({
  selector: 'app-bag-policy',
  imports: [FormsModule],
  templateUrl: './bag-policy.html',
  styleUrl: './bag-policy.css',
})
export class BagPolicyComponent implements OnInit {
  #service = inject(BagPolicyService);

  policies = signal<BagPolicy[]>([]);
  filter = signal('');
  loading = signal(false);
  error = signal('');
  success = signal('');
  loaded = signal(false);

  showForm = signal(false);
  editing = signal<BagPolicy | null>(null);
  saving = signal(false);
  deleting = signal<string | null>(null);

  createForm = signal<CreateBagPolicyRequest>({
    cabinCode: 'Y',
    freeBagsIncluded: 1,
    maxWeightKgPerBag: 23,
  });

  updateForm = signal<UpdateBagPolicyRequest>({
    freeBagsIncluded: 1,
    maxWeightKgPerBag: 23,
    isActive: true,
  });

  filtered = computed(() => {
    const q = this.filter().toLowerCase().trim();
    const all = this.policies();
    if (!q) return all;
    return all.filter(p => p.cabinCode.toLowerCase().includes(q));
  });

  stats = computed(() => {
    const all = this.policies();
    const active = all.filter(p => p.isActive).length;
    const totalBags = all.reduce((sum, p) => sum + p.freeBagsIncluded, 0);
    return { total: all.length, active, totalBags };
  });

  ngOnInit(): void {
    this.loadPolicies();
  }

  async loadPolicies(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#service.getAll();
      this.policies.set(result.sort((a, b) => a.cabinCode.localeCompare(b.cabinCode)));
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load bag policies. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  setFilter(val: string): void {
    this.filter.set(val);
  }

  openCreateForm(): void {
    this.editing.set(null);
    this.createForm.set({ cabinCode: 'Y', freeBagsIncluded: 1, maxWeightKgPerBag: 23 });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  openEditForm(policy: BagPolicy): void {
    this.editing.set(policy);
    this.updateForm.set({
      freeBagsIncluded: policy.freeBagsIncluded,
      maxWeightKgPerBag: policy.maxWeightKgPerBag,
      isActive: policy.isActive,
    });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editing.set(null);
  }

  updateCreateField(field: keyof CreateBagPolicyRequest, value: unknown): void {
    this.createForm.update(f => ({ ...f, [field]: value }));
  }

  updateUpdateField(field: keyof UpdateBagPolicyRequest, value: unknown): void {
    this.updateForm.update(f => ({ ...f, [field]: value }));
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    try {
      const editingPolicy = this.editing();
      if (editingPolicy) {
        await this.#service.update(editingPolicy.policyId, this.updateForm());
        this.success.set('Bag policy updated successfully.');
      } else {
        await this.#service.create(this.createForm());
        this.success.set('Bag policy created successfully.');
      }
      this.showForm.set(false);
      this.editing.set(null);
      await this.loadPolicies();
    } catch {
      this.error.set('Failed to save bag policy. Check the data and try again.');
    } finally {
      this.saving.set(false);
    }
  }

  async deletePolicy(policyId: string): Promise<void> {
    this.deleting.set(policyId);
    this.error.set('');
    this.success.set('');
    try {
      await this.#service.delete(policyId);
      this.success.set('Bag policy deleted successfully.');
      await this.loadPolicies();
    } catch {
      this.error.set('Failed to delete bag policy. Please try again.');
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
}
