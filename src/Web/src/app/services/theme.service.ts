import { Injectable, effect, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly STORAGE_KEY = 'apex-air-theme';

  isDark = signal(localStorage.getItem(this.STORAGE_KEY) === 'dark');

  constructor() {
    // Apply immediately on init (before first render)
    document.documentElement.classList.toggle('dark', this.isDark());

    // Keep DOM and localStorage in sync whenever the signal changes
    effect(() => {
      const dark = this.isDark();
      document.documentElement.classList.toggle('dark', dark);
      localStorage.setItem(this.STORAGE_KEY, dark ? 'dark' : 'light');
    });
  }

  toggle(): void {
    this.isDark.update(d => !d);
  }
}
