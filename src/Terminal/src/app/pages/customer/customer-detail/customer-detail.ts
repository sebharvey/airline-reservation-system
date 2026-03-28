import { Component, inject, signal, OnInit } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  CustomerService,
  CustomerDetail,
  Transaction,
  UpdateCustomerRequest,
} from '../../../services/customer.service';

@Component({
  selector: 'app-customer-detail',
  imports: [FormsModule],
  templateUrl: './customer-detail.html',
  styleUrl: './customer-detail.css',
})
export class CustomerDetailComponent implements OnInit {
  #route = inject(ActivatedRoute);
  #router = inject(Router);
  #customerService = inject(CustomerService);

  loyaltyNumber = '';
  activeTab = signal<'details' | 'transactions'>('details');
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  success = signal('');

  customer = signal<CustomerDetail | null>(null);
  editing = signal(false);

  // Edit form fields
  editGivenName = signal('');
  editSurname = signal('');
  editDateOfBirth = signal('');
  editGender = signal('');
  editNationality = signal('');
  editPhoneNumber = signal('');
  editPreferredLanguage = signal('');
  editAddressLine1 = signal('');
  editAddressLine2 = signal('');
  editCity = signal('');
  editStateOrRegion = signal('');
  editPostalCode = signal('');
  editCountryCode = signal('');

  // Transactions
  transactions = signal<Transaction[]>([]);
  transactionsLoading = signal(false);
  transactionsPage = signal(1);
  transactionsTotalCount = signal(0);
  transactionsPageSize = 20;

  ngOnInit(): void {
    this.loyaltyNumber = this.#route.snapshot.paramMap.get('loyaltyNumber') ?? '';
    this.loadCustomer();
  }

  async loadCustomer(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const c = await this.#customerService.getCustomer(this.loyaltyNumber);
      this.customer.set(c);
      this.populateEditForm(c);
    } catch {
      this.error.set('Failed to load customer details.');
    } finally {
      this.loading.set(false);
    }
  }

  populateEditForm(c: CustomerDetail): void {
    this.editGivenName.set(c.givenName);
    this.editSurname.set(c.surname);
    this.editDateOfBirth.set(c.dateOfBirth ?? '');
    this.editGender.set(c.gender ?? '');
    this.editNationality.set(c.nationality ?? '');
    this.editPhoneNumber.set(c.phoneNumber ?? '');
    this.editPreferredLanguage.set(c.preferredLanguage ?? 'en-GB');
    this.editAddressLine1.set(c.addressLine1 ?? '');
    this.editAddressLine2.set(c.addressLine2 ?? '');
    this.editCity.set(c.city ?? '');
    this.editStateOrRegion.set(c.stateOrRegion ?? '');
    this.editPostalCode.set(c.postalCode ?? '');
    this.editCountryCode.set(c.countryCode ?? '');
  }

  startEdit(): void {
    const c = this.customer();
    if (c) this.populateEditForm(c);
    this.editing.set(true);
    this.success.set('');
  }

  cancelEdit(): void {
    this.editing.set(false);
    this.success.set('');
    this.error.set('');
  }

  async saveEdit(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    this.success.set('');

    const data: UpdateCustomerRequest = {
      givenName: this.editGivenName(),
      surname: this.editSurname(),
      dateOfBirth: this.editDateOfBirth() || null,
      gender: this.editGender() || null,
      nationality: this.editNationality() || null,
      phoneNumber: this.editPhoneNumber() || null,
      preferredLanguage: this.editPreferredLanguage(),
      addressLine1: this.editAddressLine1() || null,
      addressLine2: this.editAddressLine2() || null,
      city: this.editCity() || null,
      stateOrRegion: this.editStateOrRegion() || null,
      postalCode: this.editPostalCode() || null,
      countryCode: this.editCountryCode() || null,
    };

    try {
      await this.#customerService.updateCustomer(this.loyaltyNumber, data);
      this.success.set('Customer updated successfully.');
      this.editing.set(false);
      await this.loadCustomer();
    } catch {
      this.error.set('Failed to save changes. Please try again.');
    } finally {
      this.saving.set(false);
    }
  }

  async switchTab(tab: 'details' | 'transactions'): Promise<void> {
    this.activeTab.set(tab);
    if (tab === 'transactions' && this.transactions().length === 0) {
      await this.loadTransactions();
    }
  }

  async loadTransactions(): Promise<void> {
    this.transactionsLoading.set(true);
    try {
      const result = await this.#customerService.getTransactions(
        this.loyaltyNumber, this.transactionsPage(), this.transactionsPageSize
      );
      this.transactions.set(result.transactions);
      this.transactionsTotalCount.set(result.totalCount);
    } catch {
      this.error.set('Failed to load transactions.');
    } finally {
      this.transactionsLoading.set(false);
    }
  }

  async nextPage(): Promise<void> {
    this.transactionsPage.update(p => p + 1);
    await this.loadTransactions();
  }

  async prevPage(): Promise<void> {
    this.transactionsPage.update(p => Math.max(1, p - 1));
    await this.loadTransactions();
  }

  get totalPages(): number {
    return Math.ceil(this.transactionsTotalCount() / this.transactionsPageSize) || 1;
  }

  goBack(): void {
    this.#router.navigate(['/customer']);
  }

  tierBadgeClass(tier: string): string {
    return { Blue: 'badge-blue', Silver: 'badge-silver', Gold: 'badge-gold', Platinum: 'badge-platinum' }[tier] ?? '';
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  formatDateTime(iso: string): string {
    return new Date(iso).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit',
    });
  }

  formatPoints(points: number): string {
    return points.toLocaleString();
  }

  typeBadgeClass(type: string): string {
    return {
      Earn: 'txn-earn',
      Redeem: 'txn-redeem',
      Adjustment: 'txn-adjustment',
      Expiry: 'txn-expiry',
      Reinstate: 'txn-reinstate',
    }[type] ?? '';
  }

  setField(setter: (val: string) => void, event: Event): void {
    setter((event.target as HTMLInputElement).value);
  }
}
