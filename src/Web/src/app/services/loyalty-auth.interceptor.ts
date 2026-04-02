/**
 * loyaltyAuthInterceptor
 *
 * Functional HTTP interceptor that:
 *  1. Attaches the stored Bearer access token to every request destined for
 *     the Loyalty API (auth endpoints are skipped – they don't need a token).
 *  2. On a 401 response, attempts a silent token refresh using the stored
 *     refresh token (via HttpBackend to bypass this interceptor).
 *  3. If the refresh succeeds the original request is retried once with the
 *     new access token.
 *  4. If the refresh fails the user is logged out and the error is propagated.
 */

import { inject } from '@angular/core';
import {
  HttpInterceptorFn,
  HttpRequest,
  HttpHandlerFn,
  HttpErrorResponse,
  HttpBackend,
  HttpClient
} from '@angular/common/http';
import { catchError, switchMap, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { LoyaltyStateService } from './loyalty-state.service';
import { environment } from '../environments/environment';

interface TokenResponse {
  accessToken: string;
  refreshToken: string;
}

const AUTH_PATHS = ['/auth/login', '/auth/logout', '/auth/refresh', '/register'];

function isAuthEndpoint(url: string): boolean {
  return AUTH_PATHS.some(path => url.includes(path));
}

export const loyaltyAuthInterceptor: HttpInterceptorFn = (
  req: HttpRequest<unknown>,
  next: HttpHandlerFn
) => {
  const loyaltyState = inject(LoyaltyStateService);
  const router = inject(Router);
  const backend = inject(HttpBackend);

  // Only intercept requests to the Loyalty API
  if (!req.url.startsWith(environment.loyaltyApiBaseUrl)) {
    return next(req);
  }

  // Auth endpoints don't need a token
  if (isAuthEndpoint(req.url)) {
    return next(req);
  }

  const session = loyaltyState.session();
  const authedReq = session?.accessToken
    ? req.clone({ setHeaders: { Authorization: `Bearer ${session.accessToken}` } })
    : req;

  return next(authedReq).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        // Attempt token refresh if we have a refresh token
        if (session?.refreshToken) {
          // Use HttpBackend directly to bypass this interceptor and avoid loops
          const bypassHttp = new HttpClient(backend);

          return bypassHttp
            .post<TokenResponse>(
              `${environment.loyaltyApiBaseUrl}/api/v1/auth/refresh`,
              { refreshToken: session.refreshToken }
            )
            .pipe(
              switchMap((tokens: TokenResponse) => {
                loyaltyState.updateTokens(tokens.accessToken, tokens.refreshToken);
                const retryReq = req.clone({
                  setHeaders: { Authorization: `Bearer ${tokens.accessToken}` }
                });
                return next(retryReq);
              }),
              catchError(() => {
                // Refresh failed – force logout and return to login
                loyaltyState.logout();
                router.navigate(['/loyalty']);
                return throwError(() => error);
              })
            );
        }

        // No refresh token – force logout and return to login
        loyaltyState.logout();
        router.navigate(['/loyalty']);
      }
      return throwError(() => error);
    })
  );
};
