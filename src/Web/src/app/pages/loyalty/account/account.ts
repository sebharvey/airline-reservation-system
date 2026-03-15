import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { LoyaltyApiService } from '../../../services/loyalty-api.service';
import { LoyaltyStateService } from '../../../services/loyalty-state.service';
import { TIER_CONFIG, LoyaltyTier, LoyaltyTransaction, TransactionType } from '../../../models/loyalty.model';
import { COUNTRIES } from '../register/register';

export type AccountTab = 'overview' | 'transactions' | 'profile';

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
  { code: 'en', name: 'English' },
  { code: 'fr', name: 'French' },
  { code: 'de', name: 'German' },
  { code: 'es', name: 'Spanish' },
  { code: 'it', name: 'Italian' },
  { code: 'pt', name: 'Portuguese' },
  { code: 'ar', name: 'Arabic' },
  { code: 'zh', name: 'Chinese' },
  { code: 'ja', name: 'Japanese' },
  { code: 'hi', name: 'Hindi' },
];

@Component({
  selector: 'app-loyalty-account',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  templateUrl: './account.html',
  styleUrl: './account.css'
})
export class LoyaltyAccountComponent implements OnInit {
  private readonly loyaltyApi = inject(LoyaltyApiService);
  private readonly loyaltyState = inject(LoyaltyStateService);
  private readonly router = inject(Router);

  readonly countries = COUNTRIES;
  readonly languages = LANGUAGES;
  readonly tierBenefits = TIER_BENEFITS;
  readonly transactionTypeConfig = TRANSACTION_TYPE_CONFIG;
  readonly tierConfig = TIER_CONFIG;

  activeTab = signal<AccountTab>('overview');

  // Profile edit signals
  profileGivenName = signal('');
  profileSurname = signal('');
  profilePhone = signal('');
  profileNationality = signal('');
  profileLanguage = signal('');

  profileLoading = signal(false);
  profileError = signal<string | null>(null);
  profileSuccess = signal(false);

  logoutLoading = signal(false);

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
  }

  private syncProfileFromCustomer(): void {
    const c = this.customer();
    if (!c) return;
    this.profileGivenName.set(c.givenName);
    this.profileSurname.set(c.surname);
    this.profilePhone.set(c.phone);
    this.profileNationality.set(c.nationality);
    this.profileLanguage.set(c.preferredLanguage);
  }

  setTab(tab: AccountTab): void {
    this.activeTab.set(tab);
    if (tab === 'profile') {
      this.profileError.set(null);
      this.profileSuccess.set(false);
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

  saveProfile(): void {
    const c = this.customer();
    if (!c) return;

    this.profileError.set(null);
    this.profileSuccess.set(false);

    if (!this.profileGivenName() || !this.profileSurname() || !this.profilePhone()) {
      this.profileError.set('First name, last name, and phone are required.');
      return;
    }

    this.profileLoading.set(true);
    this.loyaltyApi.updateProfile(c.loyaltyNumber, {
      givenName: this.profileGivenName(),
      surname: this.profileSurname(),
      phone: this.profilePhone(),
      nationality: this.profileNationality(),
      preferredLanguage: this.profileLanguage(),
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

  logout(): void {
    this.logoutLoading.set(true);
    this.loyaltyApi.logout().subscribe({
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
