import { Component, inject } from '@angular/core';
import { Router, NavigationEnd, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';
import { ThemeService } from './services/theme.service';
import { LoyaltyStateService } from './services/loyalty-state.service';
import { LoyaltyApiService } from './services/loyalty-api.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  theme = inject(ThemeService);
  loyaltyState = inject(LoyaltyStateService);
  private readonly loyaltyApi = inject(LoyaltyApiService);

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

  isHome = toSignal(
    this.#router.events.pipe(
      filter(e => e instanceof NavigationEnd),
      map(() => this.#router.url === '/'),
      startWith(this.#router.url === '/')
    ),
    { initialValue: this.#router.url === '/' }
  );

  logout(): void {
    const refreshToken = this.loyaltyState.session()?.refreshToken;
    this.loyaltyApi.logout(refreshToken).subscribe({
      next: () => { this.loyaltyState.logout(); this.#router.navigate(['/loyalty']); },
      error: () => { this.loyaltyState.logout(); this.#router.navigate(['/loyalty']); }
    });
  }
}
