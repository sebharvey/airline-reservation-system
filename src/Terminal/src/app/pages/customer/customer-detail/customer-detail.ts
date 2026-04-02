import { Component, inject, signal, OnInit } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import {
  CustomerService,
  CustomerDetail,
  Transaction,
  UpdateCustomerRequest,
  UpdateIdentityRequest,
  SetPasswordRequest,
} from '../../../services/customer.service';
import { CustomerSearchStateService } from '../../../services/customer-search-state.service';

export interface Country {
  code: string;
  name: string;
}

export const COUNTRIES: Country[] = [
  { code: 'GB', name: 'United Kingdom' },
  { code: 'US', name: 'United States' },
  { code: 'CA', name: 'Canada' },
  { code: 'AU', name: 'Australia' },
  { code: 'NZ', name: 'New Zealand' },
  { code: 'IE', name: 'Ireland' },
  { code: 'FR', name: 'France' },
  { code: 'DE', name: 'Germany' },
  { code: 'ES', name: 'Spain' },
  { code: 'IT', name: 'Italy' },
  { code: 'PT', name: 'Portugal' },
  { code: 'NL', name: 'Netherlands' },
  { code: 'BE', name: 'Belgium' },
  { code: 'CH', name: 'Switzerland' },
  { code: 'AT', name: 'Austria' },
  { code: 'SE', name: 'Sweden' },
  { code: 'NO', name: 'Norway' },
  { code: 'DK', name: 'Denmark' },
  { code: 'FI', name: 'Finland' },
  { code: 'PL', name: 'Poland' },
  { code: 'GR', name: 'Greece' },
  { code: 'CZ', name: 'Czech Republic' },
  { code: 'RO', name: 'Romania' },
  { code: 'HU', name: 'Hungary' },
  { code: 'JP', name: 'Japan' },
  { code: 'CN', name: 'China' },
  { code: 'IN', name: 'India' },
  { code: 'KR', name: 'South Korea' },
  { code: 'SG', name: 'Singapore' },
  { code: 'MY', name: 'Malaysia' },
  { code: 'TH', name: 'Thailand' },
  { code: 'AE', name: 'United Arab Emirates' },
  { code: 'SA', name: 'Saudi Arabia' },
  { code: 'ZA', name: 'South Africa' },
  { code: 'NG', name: 'Nigeria' },
  { code: 'KE', name: 'Kenya' },
  { code: 'BR', name: 'Brazil' },
  { code: 'MX', name: 'Mexico' },
  { code: 'AR', name: 'Argentina' },
  { code: 'CL', name: 'Chile' },
  { code: 'TR', name: 'Turkey' },
];

export const LANGUAGES = [
  { code: 'en-GB', name: 'English (UK)' },
  { code: 'en-US', name: 'English (US)' },
  { code: 'fr-FR', name: 'French' },
  { code: 'de-DE', name: 'German' },
  { code: 'es-ES', name: 'Spanish' },
  { code: 'it-IT', name: 'Italian' },
  { code: 'pt-PT', name: 'Portuguese' },
  { code: 'ar-SA', name: 'Arabic' },
  { code: 'zh-CN', name: 'Chinese' },
  { code: 'ja-JP', name: 'Japanese' },
  { code: 'hi-IN', name: 'Hindi' },
];

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
  #searchState = inject(CustomerSearchStateService);

  loyaltyNumber = '';
  activeTab = signal<'details' | 'transactions' | 'orders'>('details');
  loading = signal(false);
  saving = signal(false);
  error = signal('');
  success = signal('');
  copied = signal(false);

  customer = signal<CustomerDetail | null>(null);
  editing = signal(false);

  countries = COUNTRIES;
  languages = LANGUAGES;

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
  editPassportNumber = signal('');
  editPassportIssueDate = signal('');
  editPassportIssuer = signal('');
  editPassportExpiryDate = signal('');
  editKnownTravellerNumber = signal('');

  // Transactions
  transactions = signal<Transaction[]>([]);
  transactionsLoading = signal(false);
  transactionsPage = signal(1);
  transactionsTotalCount = signal(0);
  transactionsPageSize = 20;

  // Add points
  showAddPointsForm = signal(false);
  addPointsAmount = signal<number | null>(null);
  addPointsDescription = signal('');
  addPointsSaving = signal(false);

  // Delete account
  showDeleteConfirm = signal(false);
  deleting = signal(false);

  // Status toggle
  togglingStatus = signal(false);

  // Identity edit
  editingIdentity = signal(false);
  editIdentityEmail = signal('');
  editIdentityIsLocked = signal(false);
  savingIdentity = signal(false);

  // Set password
  showSetPasswordForm = signal(false);
  setPasswordValue = signal('');
  setPasswordConfirm = signal('');
  settingPassword = signal(false);

  // Verify email
  verifyingEmail = signal(false);

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
    this.editPassportNumber.set(c.passportNumber ?? '');
    this.editPassportIssueDate.set(c.passportIssueDate ?? '');
    this.editPassportIssuer.set(c.passportIssuer ?? '');
    this.editPassportExpiryDate.set(c.passportExpiryDate ?? '');
    this.editKnownTravellerNumber.set(c.knownTravellerNumber ?? '');
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

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const issueDate = this.editPassportIssueDate();
    if (issueDate) {
      const d = new Date(issueDate);
      if (d >= today) {
        this.error.set('Passport issue date must be in the past.');
        this.saving.set(false);
        return;
      }
    }

    const expiryDate = this.editPassportExpiryDate();
    if (expiryDate) {
      const d = new Date(expiryDate);
      if (d <= today) {
        this.error.set('Passport expiry date must be in the future.');
        this.saving.set(false);
        return;
      }
    }

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
      passportNumber: this.editPassportNumber() || null,
      passportIssueDate: this.editPassportIssueDate() || null,
      passportIssuer: this.editPassportIssuer() || null,
      passportExpiryDate: this.editPassportExpiryDate() || null,
      knownTravellerNumber: this.editKnownTravellerNumber() || null,
    };

    try {
      await this.#customerService.updateCustomer(this.loyaltyNumber, data);
      this.#searchState.dirty.set(true);
      this.success.set('Customer updated successfully.');
      this.editing.set(false);
      await this.loadCustomer();
    } catch {
      this.error.set('Failed to save changes. Please try again.');
    } finally {
      this.saving.set(false);
    }
  }

  async switchTab(tab: 'details' | 'transactions' | 'orders'): Promise<void> {
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

  // Add points
  openAddPointsForm(): void {
    this.showAddPointsForm.set(true);
    this.addPointsAmount.set(null);
    this.addPointsDescription.set('');
    this.error.set('');
    this.success.set('');
  }

  cancelAddPoints(): void {
    this.showAddPointsForm.set(false);
  }

  async submitAddPoints(): Promise<void> {
    const points = this.addPointsAmount();
    const description = this.addPointsDescription().trim();

    if (!points || points <= 0) {
      this.error.set('Please enter a valid number of points greater than zero.');
      return;
    }
    if (!description) {
      this.error.set('Please enter a description for this adjustment.');
      return;
    }

    this.addPointsSaving.set(true);
    this.error.set('');
    this.success.set('');

    try {
      await this.#customerService.addPoints(this.loyaltyNumber, { points, description });
      this.#searchState.dirty.set(true);
      this.success.set(`Successfully assigned ${points.toLocaleString()} points.`);
      this.showAddPointsForm.set(false);
      this.transactionsPage.set(1);
      await Promise.all([this.loadCustomer(), this.loadTransactions()]);
    } catch {
      this.error.set('Failed to assign points. Please try again.');
    } finally {
      this.addPointsSaving.set(false);
    }
  }

  // Delete account
  confirmDelete(): void {
    this.showDeleteConfirm.set(true);
    this.error.set('');
    this.success.set('');
  }

  cancelDelete(): void {
    this.showDeleteConfirm.set(false);
  }

  async deleteAccount(): Promise<void> {
    this.deleting.set(true);
    this.error.set('');
    try {
      await this.#customerService.deleteCustomer(this.loyaltyNumber);
      this.#searchState.dirty.set(true);
      this.#router.navigate(['/customer']);
    } catch {
      this.error.set('Failed to delete account. Please try again.');
      this.showDeleteConfirm.set(false);
    } finally {
      this.deleting.set(false);
    }
  }

  // Toggle account status
  async toggleAccountStatus(): Promise<void> {
    const c = this.customer();
    if (!c) return;

    this.togglingStatus.set(true);
    this.error.set('');
    this.success.set('');

    try {
      await this.#customerService.setAccountStatus(this.loyaltyNumber, !c.isActive);
      this.#searchState.dirty.set(true);
      this.success.set(`Account ${c.isActive ? 'deactivated' : 'activated'} successfully.`);
      await this.loadCustomer();
    } catch {
      this.error.set('Failed to update account status. Please try again.');
    } finally {
      this.togglingStatus.set(false);
    }
  }

  goBack(): void {
    this.#router.navigate(['/customer']);
  }

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), 2000);
    });
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

  setPointsAmount(event: Event): void {
    const val = (event.target as HTMLInputElement).value;
    this.addPointsAmount.set(val ? parseInt(val, 10) : null);
  }

  startEditIdentity(): void {
    const identity = this.customer()?.identity;
    this.editIdentityEmail.set(identity?.email ?? '');
    this.editIdentityIsLocked.set(identity?.isLocked ?? false);
    this.editingIdentity.set(true);
    this.success.set('');
    this.error.set('');
  }

  cancelEditIdentity(): void {
    this.editingIdentity.set(false);
    this.error.set('');
  }

  async saveIdentity(): Promise<void> {
    this.savingIdentity.set(true);
    this.error.set('');
    this.success.set('');

    const identity = this.customer()?.identity;
    const request: UpdateIdentityRequest = {};

    const newEmail = this.editIdentityEmail().trim();
    if (newEmail && newEmail !== identity?.email) {
      request.email = newEmail;
    }
    if (this.editIdentityIsLocked() !== identity?.isLocked) {
      request.isLocked = this.editIdentityIsLocked();
    }

    if (!request.email && request.isLocked === undefined) {
      this.editingIdentity.set(false);
      this.savingIdentity.set(false);
      return;
    }

    try {
      await this.#customerService.updateIdentity(this.loyaltyNumber, request);
      this.success.set('Identity account updated successfully.');
      this.editingIdentity.set(false);
      await this.loadCustomer();
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 409) {
        this.error.set('This email address is already in use.');
      } else {
        this.error.set('Failed to update identity account. Please try again.');
      }
    } finally {
      this.savingIdentity.set(false);
    }
  }

  openSetPasswordForm(): void {
    this.setPasswordValue.set('');
    this.setPasswordConfirm.set('');
    this.showSetPasswordForm.set(true);
    this.success.set('');
    this.error.set('');
  }

  cancelSetPassword(): void {
    this.showSetPasswordForm.set(false);
  }

  async submitSetPassword(): Promise<void> {
    const password = this.setPasswordValue().trim();
    const confirm = this.setPasswordConfirm().trim();

    if (!password) {
      this.error.set('Please enter a new password.');
      return;
    }
    if (password !== confirm) {
      this.error.set('Passwords do not match.');
      return;
    }

    this.settingPassword.set(true);
    this.error.set('');
    this.success.set('');

    try {
      const request: SetPasswordRequest = { newPassword: password };
      await this.#customerService.setPassword(this.loyaltyNumber, request);
      this.success.set('Password updated successfully.');
      this.showSetPasswordForm.set(false);
      await this.loadCustomer();
    } catch (err) {
      if (err instanceof HttpErrorResponse && err.status === 400) {
        this.error.set(err.error?.detail ?? 'Password does not meet requirements.');
      } else {
        this.error.set('Failed to set password. Please try again.');
      }
    } finally {
      this.settingPassword.set(false);
    }
  }

  async markEmailVerified(): Promise<void> {
    this.verifyingEmail.set(true);
    this.error.set('');
    this.success.set('');

    try {
      await this.#customerService.markEmailVerified(this.loyaltyNumber);
      this.success.set('Email address marked as verified.');
      await this.loadCustomer();
    } catch {
      this.error.set('Failed to verify email address. Please try again.');
    } finally {
      this.verifyingEmail.set(false);
    }
  }
}
