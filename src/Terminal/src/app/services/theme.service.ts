import { Injectable, effect, signal } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly STORAGE_KEY = 'apex-terminal-theme';

  isDark = signal(localStorage.getItem(this.STORAGE_KEY) === 'dark');

  constructor() {
    document.documentElement.classList.toggle('dark', this.isDark());

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
