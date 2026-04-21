import { ApplicationConfig, provideBrowserGlobalErrorListeners } from '@angular/core';
import { NavigationError, provideRouter, withInMemoryScrolling, withNavigationErrorHandler } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { authInterceptor } from './interceptors/auth.interceptor';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(
      routes,
      withInMemoryScrolling({ scrollPositionRestoration: 'top' }),
      withNavigationErrorHandler((e: NavigationError) => {
        // When a lazy-loaded chunk 404s after a new deployment (stale main bundle),
        // force a full reload so the browser fetches the current chunk manifest.
        if (e.error?.message?.includes('Failed to fetch dynamically imported module')) {
          window.location.assign(e.url);
        }
      }),
    ),
    provideHttpClient(withInterceptors([authInterceptor])),
  ],
};
