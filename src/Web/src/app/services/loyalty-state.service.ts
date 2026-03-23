/**
 * LoyaltyStateService
 *
 * Manages client-side loyalty authentication state using Angular signals.
 * Persists session (tokens + customer) in localStorage so users remain
 * logged in across page refreshes.
 */

import { Injectable, signal, computed } from '@angular/core';
import { LoyaltyCustomer, AuthSession } from '../models/loyalty.model';

const STORAGE_KEY = 'loyalty_session';

@Injectable({ providedIn: 'root' })
export class LoyaltyStateService {
  private readonly _session = signal<AuthSession | null>(null);

  readonly session = this._session.asReadonly();

  readonly isLoggedIn = computed(() => this._session() !== null);

  readonly currentCustomer = computed<LoyaltyCustomer | null>(() => this._session()?.customer ?? null);

  constructor() {
    this.restoreFromStorage();
  }

  private restoreFromStorage(): void {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        const session: AuthSession = JSON.parse(stored);
        if (session?.accessToken && session?.customer) {
          this._session.set(session);
        }
      }
    } catch {
      localStorage.removeItem(STORAGE_KEY);
    }
  }

  setSession(session: AuthSession): void {
    this._session.set(session);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  }

  updateCustomer(customer: LoyaltyCustomer): void {
    this._session.update(s => {
      if (!s) return s;
      const updated = { ...s, customer };
      localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
      return updated;
    });
  }

  updateTokens(accessToken: string, refreshToken: string): void {
    this._session.update(s => {
      if (!s) return s;
      const updated = { ...s, accessToken, refreshToken };
      localStorage.setItem(STORAGE_KEY, JSON.stringify(updated));
      return updated;
    });
  }

  logout(): void {
    this._session.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }
}
