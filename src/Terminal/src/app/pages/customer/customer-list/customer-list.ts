import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { LucideAngularModule } from 'lucide-angular';
import { CustomerService, CustomerSummary } from '../../../services/customer.service';
import { CustomerSearchStateService } from '../../../services/customer-search-state.service';

@Component({
  selector: 'app-customer-list',
  imports: [LucideAngularModule],
  templateUrl: './customer-list.html',
  styleUrl: './customer-list.css',
})
export class CustomerListComponent implements OnInit {
  #customerService = inject(CustomerService);
  #router = inject(Router);
  #searchState = inject(CustomerSearchStateService);

  customers = signal<CustomerSummary[]>([]);
  search = signal('');
  loading = signal(false);
  error = signal('');
  loaded = signal(false);
  copiedRef = signal<string | null>(null);

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

  async ngOnInit(): Promise<void> {
    if (this.#searchState.loaded()) {
      this.search.set(this.#searchState.query());
      if (this.#searchState.dirty()) {
        this.#searchState.dirty.set(false);
        await this.searchCustomers();
      } else {
        this.customers.set(this.#searchState.results());
        this.loaded.set(true);
      }
    }
  }

  async searchCustomers(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#customerService.searchCustomers(this.search());
      this.customers.set(result);
      this.loaded.set(true);
      this.#searchState.query.set(this.search());
      this.#searchState.results.set(result);
      this.#searchState.loaded.set(true);
    } catch {
      this.error.set('Failed to load customers. Please try again.');
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

  copyToClipboard(text: string, event: Event): void {
    event.stopPropagation();
    navigator.clipboard.writeText(text).then(() => {
      this.copiedRef.set(text);
      setTimeout(() => this.copiedRef.set(null), 2000);
    });
  }
}
