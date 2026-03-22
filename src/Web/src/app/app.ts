import { Component, inject, signal } from '@angular/core';
import { Router, NavigationEnd, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';
import { ThemeService } from './services/theme.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  theme = inject(ThemeService);

  #router = inject(Router);

  isBoardingPass = toSignal(
    this.#router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map(() => {
        window.scrollTo({ top: 0, behavior: 'instant' });
        return this.#router.url.startsWith('/check-in/boarding-pass');
      }),
      startWith(this.#router.url.startsWith('/check-in/boarding-pass'))
    ),
    { initialValue: false }
  );

  showSecretModal = signal(false);
  secretValue = signal('');
  hasStoredSecret = signal(!!localStorage.getItem('hostKey'));

  openSecretModal(): void {
    this.secretValue.set('');
    this.showSecretModal.set(true);
  }

  closeSecretModal(): void {
    this.showSecretModal.set(false);
    this.secretValue.set('');
  }

  saveSecret(): void {
    const value = this.secretValue().trim();
    if (!value) return;
    const encoded = btoa(value);
    localStorage.setItem('hostKey', encoded);
    this.hasStoredSecret.set(true);
    this.closeSecretModal();
  }
}
