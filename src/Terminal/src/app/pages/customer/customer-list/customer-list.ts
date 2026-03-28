import { Component, inject, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { CustomerService, CustomerSummary } from '../../../services/customer.service';

@Component({
  selector: 'app-customer-list',
  templateUrl: './customer-list.html',
  styleUrl: './customer-list.css',
})
export class CustomerListComponent {
  #customerService = inject(CustomerService);
  #router = inject(Router);

  customers = signal<CustomerSummary[]>([]);
  search = signal('');
  loading = signal(false);
  error = signal('');
  loaded = signal(false);

  filteredCustomers = computed(() => {
    const q = this.search().toLowerCase();
    if (!q) return this.customers();
    return this.customers().filter(c =>
      c.givenName.toLowerCase().includes(q) ||
      c.surname.toLowerCase().includes(q) ||
      c.loyaltyNumber.toLowerCase().includes(q)
    );
  });

  stats = computed(() => {
    const all = this.customers();
    return {
      total: all.length,
      active: all.filter(c => c.isActive).length,
      byTier: {
        blue: all.filter(c => c.tierCode === 'Blue').length,
        silver: all.filter(c => c.tierCode === 'Silver').length,
        gold: all.filter(c => c.tierCode === 'Gold').length,
        platinum: all.filter(c => c.tierCode === 'Platinum').length,
      },
    };
  });

  async loadCustomers(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#customerService.searchCustomers();
      this.customers.set(result);
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load customers. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  async searchCustomers(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#customerService.searchCustomers(this.search());
      this.customers.set(result);
      this.loaded.set(true);
    } catch {
      this.error.set('Search failed. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  openCustomer(loyaltyNumber: string): void {
    this.#router.navigate(['/customer', loyaltyNumber]);
  }

  setSearch(val: string): void {
    this.search.set(val);
  }

  tierBadgeClass(tier: string): string {
    return {
      Blue: 'badge-blue',
      Silver: 'badge-silver',
      Gold: 'badge-gold',
      Platinum: 'badge-platinum',
    }[tier] ?? '';
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  formatPoints(points: number): string {
    return points.toLocaleString();
  }
}
