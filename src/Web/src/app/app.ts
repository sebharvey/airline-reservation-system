import { Component, computed, inject, signal } from '@angular/core';
import { Router, NavigationEnd, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';
import { LucideAngularModule } from 'lucide-angular';
import { ThemeService } from './services/theme.service';
import { LoyaltyStateService } from './services/loyalty-state.service';
import { LoyaltyApiService } from './services/loyalty-api.service';
// DEBUG — import for basket debug modal; remove with basket debug feature
import { RetailApiService } from './services/retail-api.service';
import { BookingStateService } from './services/booking-state.service';
import { HttpDebugService, HttpLogEntry } from './services/http-debug.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LucideAngularModule],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
  theme = inject(ThemeService);
  loyaltyState = inject(LoyaltyStateService);
  private readonly loyaltyApi = inject(LoyaltyApiService);

  // DEBUG — basket debug modal state; remove with basket debug feature
  private readonly retailApi = inject(RetailApiService);
  private readonly bookingState = inject(BookingStateService);
  readonly basketDebugOpen = signal(false);
  readonly basketDebugLoading = signal(false);
  readonly basketDebugData = signal<string | null>(null);
  readonly basketDebugError = signal<string | null>(null);

  // API debug modal state
  readonly httpDebug = inject(HttpDebugService);
  readonly httpDebugOpen = signal(false);
  readonly httpDebugSelectedId = signal<number | null>(null);
  readonly httpDebugCopied = signal(false);
  readonly httpDebugTab = signal<'request' | 'response'>('request');
  readonly httpDebugSelected = computed<HttpLogEntry | null>(() => {
    const id = this.httpDebugSelectedId();
    if (id === null) return null;
    return this.httpDebug.entries().find(e => e.id === id) ?? null;
  });

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
    const basketId = this.bookingState.basket()?.basketId ?? null;
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

  // API debug modal methods
  openHttpDebug(): void {
    const entries = this.httpDebug.entries();
    if (entries.length > 0 && this.httpDebugSelectedId() === null) {
      this.httpDebugSelectedId.set(entries[0].id);
    }
    this.httpDebugOpen.set(true);
  }

  closeHttpDebug(): void {
    this.httpDebugOpen.set(false);
  }

  selectHttpEntry(id: number): void {
    this.httpDebugSelectedId.set(id);
    this.httpDebugTab.set('request');
    this.httpDebugCopied.set(false);
  }

  clearHttpLog(): void {
    this.httpDebug.clear();
    this.httpDebugSelectedId.set(null);
    this.httpDebugCopied.set(false);
  }

  copyHttpEntry(): void {
    const entry = this.httpDebugSelected();
    if (!entry) return;
    const payload = {
      timestamp: entry.timestamp,
      method: entry.method,
      url: entry.url,
      request: {
        headers: entry.requestHeaders,
        body: entry.requestBody
      },
      response: {
        status: entry.responseStatus,
        headers: entry.responseHeaders,
        body: entry.responseBody
      },
      durationMs: entry.durationMs
    };
    navigator.clipboard.writeText(JSON.stringify(payload, null, 2)).then(() => {
      this.httpDebugCopied.set(true);
      setTimeout(() => this.httpDebugCopied.set(false), 2000);
    });
  }

  formatJson(value: unknown): string {
    if (value === null || value === undefined) return '(none)';
    if (typeof value === 'string') {
      try {
        return JSON.stringify(JSON.parse(value), null, 2);
      } catch {
        return value;
      }
    }
    return JSON.stringify(value, null, 2);
  }

  logout(): void {
    const refreshToken = this.loyaltyState.session()?.refreshToken;
    this.loyaltyApi.logout(refreshToken).subscribe({
      next: () => { this.loyaltyState.logout(); this.#router.navigate(['/loyalty']); },
      error: () => { this.loyaltyState.logout(); this.#router.navigate(['/loyalty']); }
    });
  }
}
