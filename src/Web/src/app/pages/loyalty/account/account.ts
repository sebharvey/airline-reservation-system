import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LoyaltyApiService, TransferPointsResult } from '../../../services/loyalty-api.service';
import { LoyaltyStateService } from '../../../services/loyalty-state.service';
import { TIER_CONFIG, LoyaltyTier, LoyaltyTransaction, TransactionType, CustomerOrderItem } from '../../../models/loyalty.model';
import { COUNTRIES, PRIORITY_COUNTRIES } from '../register/register';

export type AccountTab = 'overview' | 'transactions' | 'transfer' | 'profile' | 'flights' | 'preferences';

interface TierBenefit {
  tier: LoyaltyTier;
  benefits: string[];
}

const TIER_BENEFITS: TierBenefit[] = [
  {
    tier: 'Blue',
    benefits: [
      'Earn 1 point per £1 spent',
      'Access to member-only fares',
      'Online check-in priority',
    ]
  },
  {
    tier: 'Silver',
    benefits: [
      'Earn 1.5 points per £1 spent',
      'Priority check-in',
      'Extra baggage allowance (1 bag)',
      'Lounge access on long-haul',
    ]
  },
  {
    tier: 'Gold',
    benefits: [
      'Earn 2 points per £1 spent',
      'Priority boarding',
      'Complimentary seat upgrades (subject to availability)',
      'Lounge access on all routes',
      'Dedicated Gold service line',
    ]
  },
  {
    tier: 'Platinum',
    benefits: [
      'Earn 3 points per £1 spent',
      'Guaranteed seat upgrades',
      'Unlimited lounge access + guest',
      'Personal account manager',
      'Complimentary companion ticket annually',
    ]
  }
];

const TRANSACTION_TYPE_CONFIG: Record<TransactionType, { label: string; cssClass: string }> = {
  Accrual:    { label: 'Accrual',    cssClass: 'badge-accrual' },
  Redemption: { label: 'Redemption', cssClass: 'badge-redemption' },
  Adjustment: { label: 'Adjustment', cssClass: 'badge-adjustment' },
  Expiry:     { label: 'Expiry',     cssClass: 'badge-expiry' },
};

const LANGUAGES = [
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
  selector: 'app-loyalty-account',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './account.html',
  styleUrl: './account.css'
})
export class LoyaltyAccountComponent implements OnInit {
  private readonly loyaltyApi = inject(LoyaltyApiService);
  private readonly loyaltyState = inject(LoyaltyStateService);
  private readonly router = inject(Router);

  readonly priorityCountries = PRIORITY_COUNTRIES;
  readonly otherCountries = COUNTRIES.filter(c => !['GB', 'US'].includes(c.code));
  readonly priorityLanguages = LANGUAGES.filter(l => ['en-GB', 'en-US'].includes(l.code));
  readonly otherLanguages = LANGUAGES.filter(l => !['en-GB', 'en-US'].includes(l.code));
  readonly languages = LANGUAGES;
  readonly tierBenefits = TIER_BENEFITS;
  readonly transactionTypeConfig = TRANSACTION_TYPE_CONFIG;
  readonly tierConfig = TIER_CONFIG;

  activeTab = signal<AccountTab>('overview');

  // Profile edit signals
  profileGivenName = signal('');
  profileSurname = signal('');
  profilePhone = signal('');
  profileDateOfBirth = signal('');
  profileGender = signal('');
  profileNationality = signal('');
  profileLanguage = signal('');
  profileAddressLine1 = signal('');
  profileAddressLine2 = signal('');
  profileCity = signal('');
  profileStateOrRegion = signal('');
  profilePostalCode = signal('');
  profileCountryCode = signal('');
  profilePassportNumber = signal('');
  profilePassportIssueDate = signal('');
  profilePassportIssuer = signal('');
  profilePassportExpiryDate = signal('');
  profileKnownTravellerNumber = signal('');

  profileLoading = signal(false);
  profileError = signal<string | null>(null);
  profileSuccess = signal(false);

  // Preferences signals
  prefsMarketing = signal(false);
  prefsAnalytics = signal(false);
  prefsFunctional = signal(true);
  prefsAppNotifications = signal(true);
  preferencesLoading = signal(false);
  preferencesError = signal<string | null>(null);
  preferencesSuccess = signal(false);

  // Transfer points signals
  transferRecipientLoyaltyNumber = signal('');
  transferRecipientEmail = signal('');
  transferPointsAmount = signal<number | null>(null);
  transferLoading = signal(false);
  transferError = signal<string | null>(null);
  transferResult = signal<TransferPointsResult | null>(null);

  // Email change signals
  showEmailChangeForm = signal(false);
  emailChangeNew = signal('');
  emailChangeLoading = signal(false);
  emailChangeError = signal<string | null>(null);
  emailChangePendingModal = signal(false);

  // Delete account signals
  deleteConfirm = signal(false);
  deleteLoading = signal(false);
  deleteError = signal<string | null>(null);

  logoutLoading = signal(false);

  // Flights / orders signals
  flightOrders = signal<CustomerOrderItem[]>([]);
  flightsLoading = signal(false);
  flightsLoaded = signal(false);

  copiedText = signal<string | null>(null);

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copiedText.set(text);
      setTimeout(() => this.copiedText.set(null), 2000);
    });
  }

  readonly customer = this.loyaltyState.currentCustomer;

  readonly tierInfo = computed(() => {
    const c = this.customer();
    return c ? TIER_CONFIG[c.tier] : null;
  });

  readonly tierProgressPercent = computed(() => {
    const c = this.customer();
    if (!c) return 0;
    const info = TIER_CONFIG[c.tier];
    if (!info.nextTierPointsRequired) return 100;
    const rangeStart = info.pointsRequired;
    const rangeEnd = info.nextTierPointsRequired;
    const progress = c.pointsBalance - rangeStart;
    const range = rangeEnd - rangeStart;
    return Math.min(100, Math.max(0, Math.round((progress / range) * 100)));
  });

  readonly tierProgressLabel = computed(() => {
    const c = this.customer();
    if (!c) return '';
    const info = TIER_CONFIG[c.tier];
    if (!info.nextTierPointsRequired || !info.nextTier) {
      return 'Maximum tier reached';
    }
    const remaining = info.nextTierPointsRequired - c.pointsBalance;
    const formatted = remaining.toLocaleString();
    return `${formatted} points to ${info.nextTier}`;
  });

  readonly sortedTransactions = computed(() => {
    const c = this.customer();
    if (!c) return [];
    return [...c.transactions].sort(
      (a, b) => new Date(b.transactionDate).getTime() - new Date(a.transactionDate).getTime()
    );
  });

  ngOnInit(): void {
    if (!this.loyaltyState.isLoggedIn()) {
      this.router.navigate(['/loyalty']);
      return;
    }
    this.syncProfileFromCustomer();
    this.loadTransactions();
    this.loadPreferences();
  }

  private loadTransactions(): void {
    const c = this.customer();
    if (!c) return;
    this.loyaltyApi.getTransactions(c.loyaltyNumber).subscribe({
      next: (transactions) => {
        const current = this.customer();
        if (current) {
          this.loyaltyState.updateCustomer({ ...current, transactions });
        }
      },
      error: () => { /* silently ignore – existing data (if any) is preserved */ }
    });
  }

  private syncProfileFromCustomer(): void {
    const c = this.customer();
    if (!c) return;
    this.profileGivenName.set(c.givenName);
    this.profileSurname.set(c.surname);
    this.profilePhone.set(c.phone);
    this.profileDateOfBirth.set(c.dateOfBirth);
    this.profileGender.set(c.gender ?? '');
    this.profileNationality.set(c.nationality);
    this.profileLanguage.set(c.preferredLanguage);
    this.profileAddressLine1.set(c.addressLine1 ?? '');
    this.profileAddressLine2.set(c.addressLine2 ?? '');
    this.profileCity.set(c.city ?? '');
    this.profileStateOrRegion.set(c.stateOrRegion ?? '');
    this.profilePostalCode.set(c.postalCode ?? '');
    this.profileCountryCode.set(c.countryCode ?? '');
    this.profilePassportNumber.set(c.passportNumber ?? '');
    this.profilePassportIssueDate.set(c.passportIssueDate ?? '');
    this.profilePassportIssuer.set(c.passportIssuer ?? '');
    this.profilePassportExpiryDate.set(c.passportExpiryDate ?? '');
    this.profileKnownTravellerNumber.set(c.knownTravellerNumber ?? '');
  }

  setTab(tab: AccountTab): void {
    this.activeTab.set(tab);
    if (tab === 'flights' && !this.flightsLoaded()) {
      this.loadFlights();
    }
    if (tab === 'profile') {
      this.profileError.set(null);
      this.profileSuccess.set(false);
      this.showEmailChangeForm.set(false);
      this.emailChangeNew.set('');
      this.emailChangeError.set(null);
    }
    if (tab === 'transfer') {
      this.transferError.set(null);
      this.transferResult.set(null);
      this.transferRecipientLoyaltyNumber.set('');
      this.transferRecipientEmail.set('');
      this.transferPointsAmount.set(null);
    }
    if (tab === 'preferences') {
      this.preferencesError.set(null);
      this.preferencesSuccess.set(false);
      this.deleteConfirm.set(false);
      this.deleteError.set(null);
    }
  }

  getTierBenefitsForCurrentTier(): string[] {
    const c = this.customer();
    if (!c) return [];
    return TIER_BENEFITS.find(b => b.tier === c.tier)?.benefits ?? [];
  }

  getTransactionTypeLabel(type: TransactionType): string {
    return TRANSACTION_TYPE_CONFIG[type]?.label ?? type;
  }

  getTransactionTypeCssClass(type: TransactionType): string {
    return TRANSACTION_TYPE_CONFIG[type]?.cssClass ?? '';
  }

  formatDate(isoString: string): string {
    return new Date(isoString).toLocaleDateString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric'
    });
  }

  pointsDisplay(points: number): string {
    return (points >= 0 ? '+' : '') + points.toLocaleString();
  }

  isPositivePoints(points: number): boolean {
    return points > 0;
  }

  submitTransfer(): void {
    const c = this.customer();
    if (!c) return;

    this.transferError.set(null);
    this.transferResult.set(null);

    const recipientLoyaltyNumber = this.transferRecipientLoyaltyNumber().trim();
    const recipientEmail = this.transferRecipientEmail().trim();
    const points = this.transferPointsAmount();

    if (!recipientLoyaltyNumber || !recipientEmail || !points || points <= 0) {
      this.transferError.set('Please enter the recipient\'s loyalty number, email address, and a positive points amount.');
      return;
    }

    if (recipientLoyaltyNumber.toLowerCase() === c.loyaltyNumber.toLowerCase()) {
      this.transferError.set('You cannot transfer points to your own account.');
      return;
    }

    if (points > c.pointsBalance) {
      this.transferError.set(`Insufficient points balance. You have ${c.pointsBalance.toLocaleString()} points available.`);
      return;
    }

    this.transferLoading.set(true);
    this.loyaltyApi.transferPoints(c.loyaltyNumber, { recipientLoyaltyNumber, recipientEmail, points }).subscribe({
      next: (result) => {
        this.transferLoading.set(false);
        this.transferResult.set(result);
        this.loyaltyState.updateCustomer({ ...c, pointsBalance: result.senderNewBalance });
        this.loadTransactions();
      },
      error: (err) => {
        this.transferLoading.set(false);
        this.transferError.set(err?.message ?? 'Transfer failed. Please check the details and try again.');
      }
    });
  }

  resetTransfer(): void {
    this.transferResult.set(null);
    this.transferError.set(null);
    this.transferRecipientLoyaltyNumber.set('');
    this.transferRecipientEmail.set('');
    this.transferPointsAmount.set(null);
  }

  saveProfile(): void {
    const c = this.customer();
    if (!c) return;

    this.profileError.set(null);
    this.profileSuccess.set(false);

    if (!this.profileGivenName() || !this.profileSurname() || !this.profilePhone()) {
      this.profileError.set('First name, last name, and phone are required.');
      return;
    }

    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const issueDate = this.profilePassportIssueDate();
    if (issueDate) {
      const d = new Date(issueDate);
      if (d >= today) {
        this.profileError.set('Passport issue date must be in the past.');
        return;
      }
    }

    const expiryDate = this.profilePassportExpiryDate();
    if (expiryDate) {
      const d = new Date(expiryDate);
      if (d <= today) {
        this.profileError.set('Passport expiry date must be in the future.');
        return;
      }
    }

    this.profileLoading.set(true);
    this.loyaltyApi.updateProfile(c.loyaltyNumber, {
      givenName: this.profileGivenName(),
      surname: this.profileSurname(),
      phoneNumber: this.profilePhone(),
      dateOfBirth: this.profileDateOfBirth() || undefined,
      gender: this.profileGender() || undefined,
      nationality: this.profileNationality(),
      preferredLanguage: this.profileLanguage(),
      addressLine1: this.profileAddressLine1() || undefined,
      addressLine2: this.profileAddressLine2() || undefined,
      city: this.profileCity() || undefined,
      stateOrRegion: this.profileStateOrRegion() || undefined,
      postalCode: this.profilePostalCode() || undefined,
      countryCode: this.profileCountryCode() || undefined,
      passportNumber: this.profilePassportNumber() || undefined,
      passportIssueDate: this.profilePassportIssueDate() || undefined,
      passportIssuer: this.profilePassportIssuer() || undefined,
      passportExpiryDate: this.profilePassportExpiryDate() || undefined,
      knownTravellerNumber: this.profileKnownTravellerNumber() || undefined,
    }).subscribe({
      next: (updated) => {
        this.loyaltyState.updateCustomer(updated);
        this.profileLoading.set(false);
        this.profileSuccess.set(true);
      },
      error: (err) => {
        this.profileLoading.set(false);
        this.profileError.set(err?.message ?? 'Failed to save profile. Please try again.');
      }
    });
  }

  loadPreferences(): void {
    const c = this.customer();
    if (!c) return;
    this.loyaltyApi.getPreferences(c.loyaltyNumber).subscribe({
      next: (prefs) => {
        this.prefsMarketing.set(prefs.marketingEnabled);
        this.prefsAnalytics.set(prefs.analyticsEnabled);
        this.prefsFunctional.set(prefs.functionalEnabled);
        this.prefsAppNotifications.set(prefs.appNotificationsEnabled);
        const current = this.customer();
        if (current) {
          this.loyaltyState.updateCustomer({ ...current, preferences: prefs });
        }
      },
      error: () => { /* silently ignore; defaults remain */ }
    });
  }

  navigateToManageBooking(order: CustomerOrderItem): void {
    const c = this.customer();
    if (!c) return;
    this.router.navigate(['/manage-booking/detail'], {
      queryParams: { bookingRef: order.bookingReference },
      state: { givenName: c.givenName, surname: c.surname }
    });
  }

  loadFlights(): void {
    const c = this.customer();
    if (!c) return;
    this.flightsLoading.set(true);
    this.loyaltyApi.getCustomerOrders(c.loyaltyNumber).subscribe({
      next: (orders) => {
        this.flightOrders.set(orders);
        this.flightsLoading.set(false);
        this.flightsLoaded.set(true);
      },
      error: () => {
        this.flightsLoading.set(false);
        this.flightsLoaded.set(true);
      }
    });
  }

  savePreferences(): void {
    const c = this.customer();
    if (!c) return;

    this.preferencesError.set(null);
    this.preferencesSuccess.set(false);
    this.preferencesLoading.set(true);

    this.loyaltyApi.updatePreferences(c.loyaltyNumber, {
      marketingEnabled: this.prefsMarketing(),
      analyticsEnabled: this.prefsAnalytics(),
      functionalEnabled: this.prefsFunctional(),
      appNotificationsEnabled: this.prefsAppNotifications(),
    }).subscribe({
      next: () => {
        this.preferencesLoading.set(false);
        this.preferencesSuccess.set(true);
        this.loyaltyState.updateCustomer({
          ...c,
          preferences: {
            marketingEnabled: this.prefsMarketing(),
            analyticsEnabled: this.prefsAnalytics(),
            functionalEnabled: this.prefsFunctional(),
            appNotificationsEnabled: this.prefsAppNotifications(),
          }
        });
      },
      error: (err) => {
        this.preferencesLoading.set(false);
        this.preferencesError.set(err?.message ?? 'Failed to save preferences. Please try again.');
      }
    });
  }

  deleteAccount(): void {
    const c = this.customer();
    if (!c) return;

    this.deleteError.set(null);
    this.deleteLoading.set(true);

    this.loyaltyApi.deleteAccount(c.loyaltyNumber).subscribe({
      next: () => {
        this.loyaltyState.logout();
        this.router.navigate(['/loyalty']);
      },
      error: (err) => {
        this.deleteLoading.set(false);
        this.deleteError.set(err?.message ?? 'Failed to delete account. Please try again.');
      }
    });
  }

  toggleEmailChangeForm(): void {
    this.showEmailChangeForm.update(v => !v);
    this.emailChangeNew.set('');
    this.emailChangeError.set(null);
  }

  submitEmailChange(): void {
    const c = this.customer();
    if (!c) return;

    this.emailChangeError.set(null);
    const newEmail = this.emailChangeNew().trim();

    if (!newEmail) {
      this.emailChangeError.set('Please enter a new email address.');
      return;
    }

    if (newEmail.toLowerCase() === c.email.toLowerCase()) {
      this.emailChangeError.set('The new email address must be different from your current one.');
      return;
    }

    this.emailChangeLoading.set(true);
    this.loyaltyApi.requestEmailChange(c.loyaltyNumber, newEmail).subscribe({
      next: () => {
        this.emailChangeLoading.set(false);
        this.showEmailChangeForm.set(false);
        this.emailChangePendingModal.set(true);
      },
      error: (err) => {
        this.emailChangeLoading.set(false);
        this.emailChangeError.set(err?.message ?? 'Failed to request email change. Please try again.');
      }
    });
  }

  dismissEmailChangeModal(): void {
    this.emailChangePendingModal.set(false);
    const refreshToken = this.loyaltyState.session()?.refreshToken;
    this.loyaltyApi.logout(refreshToken).subscribe({
      next: () => {
        this.loyaltyState.logout();
        this.router.navigate(['/loyalty']);
      },
      error: () => {
        this.loyaltyState.logout();
        this.router.navigate(['/loyalty']);
      }
    });
  }

  logout(): void {
    this.logoutLoading.set(true);
    const refreshToken = this.loyaltyState.session()?.refreshToken;
    this.loyaltyApi.logout(refreshToken).subscribe({
      next: () => {
        this.loyaltyState.logout();
        this.logoutLoading.set(false);
        this.router.navigate(['/loyalty']);
      },
      error: () => {
        this.loyaltyState.logout();
        this.logoutLoading.set(false);
        this.router.navigate(['/loyalty']);
      }
    });
  }
}
