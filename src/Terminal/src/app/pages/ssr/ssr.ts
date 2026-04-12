import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SsrService, SsrOption } from '../../services/ssr.service';

type CategoryFilter = 'all' | 'Meal' | 'Mobility' | 'Accessibility';

@Component({
  selector: 'app-ssr',
  imports: [FormsModule],
  templateUrl: './ssr.html',
  styleUrl: './ssr.css',
})
export class SsrComponent implements OnInit {
  #ssrService = inject(SsrService);

  search = signal('');
  categoryFilter = signal<CategoryFilter>('all');
  selectedSsr = signal<SsrOption | null>(null);
  showAddForm = signal(false);
  loading = signal(false);
  error = signal('');
  success = signal('');

  editingSsr = signal<SsrOption | null>(null);
  editLabel = signal('');
  editCategory = signal('');

  newSsr = signal({ ssrCode: '', label: '', category: 'Meal' });

  ssrOptions = signal<SsrOption[]>([]);

  readonly categories = ['Meal', 'Mobility', 'Accessibility'];

  filteredOptions = computed(() => {
    const q = this.search().toLowerCase();
    return this.ssrOptions().filter(s => {
      const matchText = !q ||
        s.ssrCode.toLowerCase().includes(q) ||
        s.label.toLowerCase().includes(q) ||
        s.category.toLowerCase().includes(q);
      const filter = this.categoryFilter();
      const matchCategory = filter === 'all' || s.category === filter;
      return matchText && matchCategory;
    });
  });

  stats = computed(() => ({
    total: this.ssrOptions().length,
    meal: this.ssrOptions().filter(s => s.category === 'Meal').length,
    mobility: this.ssrOptions().filter(s => s.category === 'Mobility').length,
    accessibility: this.ssrOptions().filter(s => s.category === 'Accessibility').length,
  }));

  async ngOnInit(): Promise<void> {
    await this.loadOptions();
  }

  async loadOptions(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const options = await this.#ssrService.getSsrOptions();
      this.ssrOptions.set(options);
    } catch {
      this.error.set('Failed to load SSR catalogue.');
    } finally {
      this.loading.set(false);
    }
  }

  setSearch(v: string): void { this.search.set(v); }
  setCategoryFilter(v: string): void { this.categoryFilter.set(v as CategoryFilter); }

  selectSsr(s: SsrOption): void {
    if (this.selectedSsr()?.ssrCode === s.ssrCode) {
      this.selectedSsr.set(null);
      this.editingSsr.set(null);
    } else {
      this.selectedSsr.set(s);
      this.editingSsr.set(null);
    }
  }

  startEdit(s: SsrOption): void {
    this.editingSsr.set(s);
    this.editLabel.set(s.label);
    this.editCategory.set(s.category);
  }

  cancelEdit(): void {
    this.editingSsr.set(null);
  }

  async saveEdit(): Promise<void> {
    const s = this.editingSsr();
    if (!s) return;

    this.error.set('');
    this.success.set('');

    try {
      await this.#ssrService.updateSsrOption(s.ssrCode, {
        label: this.editLabel(),
        category: this.editCategory(),
      });
      this.success.set('SSR option updated successfully.');
      this.editingSsr.set(null);
      await this.loadOptions();
      this.clearMessages();
    } catch {
      this.error.set('Failed to update SSR option.');
    }
  }

  async addSsr(): Promise<void> {
    const n = this.newSsr();
    if (!n.ssrCode || !n.label || !n.category) return;

    this.error.set('');
    this.success.set('');
    try {
      await this.#ssrService.createSsrOption({
        ssrCode: n.ssrCode.toUpperCase(),
        label: n.label,
        category: n.category,
      });
      this.success.set('SSR option created successfully.');
      this.newSsr.set({ ssrCode: '', label: '', category: 'Meal' });
      this.showAddForm.set(false);
      await this.loadOptions();
      this.clearMessages();
    } catch {
      this.error.set('Failed to create SSR option. The code may already exist.');
    }
  }

  async deactivateSsr(s: SsrOption): Promise<void> {
    if (!confirm(`Deactivate SSR code "${s.ssrCode}" (${s.label})? It will no longer be available for booking.`)) {
      return;
    }

    this.error.set('');
    this.success.set('');
    try {
      await this.#ssrService.deactivateSsrOption(s.ssrCode);
      this.success.set(`SSR code ${s.ssrCode} deactivated.`);
      this.selectedSsr.set(null);
      await this.loadOptions();
      this.clearMessages();
    } catch {
      this.error.set('Failed to deactivate SSR option.');
    }
  }

  setNewSsr(field: string, val: string): void {
    this.newSsr.update(n => ({ ...n, [field]: val }));
  }

  categoryBadgeClass(category: string): string {
    switch (category) {
      case 'Meal':          return 'badge-meal';
      case 'Mobility':      return 'badge-mobility';
      case 'Accessibility': return 'badge-accessibility';
      default:              return '';
    }
  }

  private clearMessages(): void {
    setTimeout(() => {
      this.success.set('');
      this.error.set('');
    }, 3000);
  }
}
