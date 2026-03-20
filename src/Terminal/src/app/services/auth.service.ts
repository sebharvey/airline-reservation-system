import { Injectable, signal, computed } from '@angular/core';

export interface AgentUser {
  username: string;
  displayName: string;
  role: 'agent' | 'supervisor' | 'admin';
  station: string;
  agentId: string;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly STORAGE_KEY = 'apex-terminal-session';

  private _user = signal<AgentUser | null>(this.#loadSession());

  isAuthenticated = computed(() => this._user() !== null);
  currentUser = computed(() => this._user());

  #loadSession(): AgentUser | null {
    try {
      const raw = sessionStorage.getItem(this.STORAGE_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }

  login(username: string, _password: string): void {
    // In future this will call an API. For now, allow any credentials.
    const user: AgentUser = {
      username: username.toUpperCase(),
      displayName: this.#formatDisplayName(username),
      role: username.toLowerCase().startsWith('sup') ? 'supervisor' : 'agent',
      station: 'LHR',
      agentId: `LH${Math.floor(1000 + Math.random() * 9000)}`,
    };

    sessionStorage.setItem(this.STORAGE_KEY, JSON.stringify(user));
    this._user.set(user);
  }

  logout(): void {
    sessionStorage.removeItem(this.STORAGE_KEY);
    this._user.set(null);
  }

  #formatDisplayName(username: string): string {
    return username
      .split(/[\._\-]/)
      .map(part => part.charAt(0).toUpperCase() + part.slice(1).toLowerCase())
      .join(' ');
  }
}
