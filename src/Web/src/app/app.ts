import { Component, inject, signal } from '@angular/core';
import { Router, NavigationEnd, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';
import { ThemeService } from './services/theme.service';
import { LoyaltyStateService } from './services/loyalty-state.service';
import { LoyaltyApiService } from './services/loyalty-api.service';
// DEBUG — import for basket debug modal; remove with basket debug feature
import { RetailApiService } from './services/retail-api.service';

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

  // DEBUG — basket debug modal state; remove with basket debug feature
  private readonly retailApi = inject(RetailApiService);
  readonly basketDebugOpen = signal(false);
  readonly basketDebugLoading = signal(false);
  readonly basketDebugData = signal<string | null>(null);
  readonly basketDebugError = signal<string | null>(null);

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

  // DEBUG — basket debug modal methods; remove with basket debug feature
  openBasketDebug(): void {
    const basketId = sessionStorage.getItem('apex_debug_basket_id');
    this.basketDebugData.set(null);
    this.basketDebugError.set(null);
    this.basketDebugOpen.set(true);

    if (!basketId) {
      this.basketDebugError.set('No active basket found in session.');
      return;
    }

    this.basketDebugLoading.set(true);
    this.retailApi.getBasket(basketId).subscribe({
      next: data => {
        this.basketDebugData.set(JSON.stringify(data, null, 2));
        this.basketDebugLoading.set(false);
      },
      error: err => {
        this.basketDebugError.set(`Error fetching basket: ${err.status ?? ''} ${err.message ?? JSON.stringify(err)}`);
        this.basketDebugLoading.set(false);
      }
    });
  }

  closeBasketDebug(): void {
    this.basketDebugOpen.set(false);
  }
  // END DEBUG

  logout(): void {
    const refreshToken = this.loyaltyState.session()?.refreshToken;
    this.loyaltyApi.logout(refreshToken).subscribe({
      next: () => { this.loyaltyState.logout(); this.#router.navigate(['/loyalty']); },
      error: () => { this.loyaltyState.logout(); this.#router.navigate(['/loyalty']); }
    });
  }
}
