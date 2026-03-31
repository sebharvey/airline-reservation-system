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

  private _useLocalStorage = false;

  private get _storage(): Storage {
    return this._useLocalStorage ? localStorage : sessionStorage;
  }

  private restoreFromStorage(): void {
    try {
      let stored = localStorage.getItem(STORAGE_KEY);
      if (stored) {
        this._useLocalStorage = true;
      } else {
        stored = sessionStorage.getItem(STORAGE_KEY);
        this._useLocalStorage = false;
      }
      if (stored) {
        const session: AuthSession = JSON.parse(stored);
        if (session?.accessToken && session?.customer) {
          this._session.set(session);
        }
      }
    } catch {
      localStorage.removeItem(STORAGE_KEY);
      sessionStorage.removeItem(STORAGE_KEY);
    }
  }

  setSession(session: AuthSession, rememberMe = false): void {
    this._useLocalStorage = rememberMe;
    this._session.set(session);
    this._storage.setItem(STORAGE_KEY, JSON.stringify(session));
    const other = rememberMe ? sessionStorage : localStorage;
    other.removeItem(STORAGE_KEY);
  }

  updateCustomer(customer: LoyaltyCustomer): void {
    this._session.update(s => {
      if (!s) return s;
      const updated = { ...s, customer };
      this._storage.setItem(STORAGE_KEY, JSON.stringify(updated));
      return updated;
    });
  }

  updateTokens(accessToken: string, refreshToken: string): void {
    this._session.update(s => {
      if (!s) return s;
      const updated = { ...s, accessToken, refreshToken };
      this._storage.setItem(STORAGE_KEY, JSON.stringify(updated));
      return updated;
    });
  }

  logout(): void {
    this._session.set(null);
    localStorage.removeItem(STORAGE_KEY);
    sessionStorage.removeItem(STORAGE_KEY);
  }
}
