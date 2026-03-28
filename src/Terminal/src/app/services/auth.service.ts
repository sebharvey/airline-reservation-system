import { Injectable, inject, signal, computed } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../environment';

export interface AgentUser {
  username: string;
  displayName: string;
  role: string;
  userId: string;
  accessToken: string;
  expiresAt: string;
}

interface LoginResponse {
  accessToken: string;
  userId: string;
  expiresAt: string;
  tokenType: string;
}

interface JwtPayload {
  sub: string;
  unique_name: string;
  email: string;
  jti: string;
  role?: string;
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'?: string;
  iss: string;
  aud: string;
  exp: number;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly STORAGE_KEY = 'apex-terminal-session';

  #http = inject(HttpClient);

  private _user = signal<AgentUser | null>(this.#loadSession());

  isAuthenticated = computed(() => this._user() !== null);
  currentUser = computed(() => this._user());

  get accessToken(): string | null {
    return this._user()?.accessToken ?? null;
  }

  #loadSession(): AgentUser | null {
    try {
      const raw = sessionStorage.getItem(this.STORAGE_KEY);
      if (!raw) return null;
      const user: AgentUser = JSON.parse(raw);
      if (new Date(user.expiresAt) <= new Date()) {
        sessionStorage.removeItem(this.STORAGE_KEY);
        return null;
      }
      return user;
    } catch {
      return null;
    }
  }

  async login(username: string, password: string): Promise<void> {
    const response = await fetch(`${environment.adminApiUrl}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password }),
    });

    if (response.status === 401) {
      throw new Error('Invalid credentials. Please check your username and password.');
    }

    if (response.status === 403) {
      throw new Error('Account is locked or inactive. Contact your administrator.');
    }

    if (!response.ok) {
      throw new Error('An unexpected error occurred. Please try again.');
    }

    const data: LoginResponse = await response.json();
    const claims = this.#decodeToken(data.accessToken);

    const role = claims.role
      ?? claims['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
      ?? '';

    if (role.toLowerCase() !== 'user') {
      throw new Error('Access denied. Your account does not have the required permissions.');
    }

    const user: AgentUser = {
      username: claims.unique_name ?? username.toUpperCase(),
      displayName: this.#formatDisplayName(claims.unique_name ?? username),
      role,
      userId: data.userId,
      accessToken: data.accessToken,
      expiresAt: data.expiresAt,
    };

    sessionStorage.setItem(this.STORAGE_KEY, JSON.stringify(user));
    this._user.set(user);
  }

  logout(): void {
    sessionStorage.removeItem(this.STORAGE_KEY);
    this._user.set(null);
  }

  #decodeToken(token: string): JwtPayload {
    const parts = token.split('.');
    if (parts.length !== 3) {
      throw new Error('Invalid token format.');
    }
    const payload = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(atob(payload));
  }

  #formatDisplayName(username: string): string {
    return username
      .split(/[\._\-]/)
      .map(part => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
      .join(' ');
  }
}
