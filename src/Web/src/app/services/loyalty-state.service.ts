/**
 * LoyaltyStateService
 *
 * Manages client-side loyalty authentication state using Angular signals.
 * Holds the current authenticated customer and session tokens.
 *
 * In production this would integrate with token refresh, secure storage,
 * and route guards. The signal-based API makes it easy to add these later.
 */

import { Injectable, signal, computed } from '@angular/core';
import { LoyaltyCustomer, AuthSession } from '../models/loyalty.model';

@Injectable({ providedIn: 'root' })
export class LoyaltyStateService {
  private readonly _session = signal<AuthSession | null>(null);

  readonly session = this._session.asReadonly();

  readonly isLoggedIn = computed(() => this._session() !== null);

  readonly currentCustomer = computed<LoyaltyCustomer | null>(() => this._session()?.customer ?? null);

  setSession(session: AuthSession): void {
    this._session.set(session);
  }

  updateCustomer(customer: LoyaltyCustomer): void {
    this._session.update(s => s ? { ...s, customer } : s);
  }

  logout(): void {
    this._session.set(null);
  }
}
