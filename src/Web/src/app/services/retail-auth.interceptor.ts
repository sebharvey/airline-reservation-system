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
import { HttpInterceptorFn } from '@angular/common/http';
import { LoyaltyStateService } from './loyalty-state.service';
import { environment } from '../environments/environment';

export const retailAuthInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(environment.retailApiBaseUrl)) {
    return next(req);
  }

  const session = inject(LoyaltyStateService).session();
  if (!session?.accessToken) {
    return next(req);
  }

  return next(req.clone({ setHeaders: { Authorization: `Bearer ${session.accessToken}` } }));
};
