/**
 * retailAuthInterceptor
 *
 * Attaches the loyalty JWT access token to requests destined for the Retail API
 * when the user is logged in. Guest (non-loyalty) requests are passed through
 * without an Authorization header.
 *
 * Token refresh is handled by loyaltyAuthInterceptor — no retry logic needed here.
 */

import { inject } from '@angular/core';
import { HttpInterceptorFn, HttpErrorResponse } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { Router } from '@angular/router';
import { LoyaltyStateService } from './loyalty-state.service';
import { environment } from '../environments/environment';

export const retailAuthInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.retailApiBaseUrl)) {
    return next(req);
  }

  const loyaltyState = inject(LoyaltyStateService);
  const router = inject(Router);
  const session = loyaltyState.session();
  if (!session?.accessToken) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${session.accessToken}` } })).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 401) {
        loyaltyState.logout();
        router.navigate(['/loyalty']);
      }
      return throwError(() => error);
    })
  );
};
