/**
 * LoyaltyApiService
 *
 * Connects to the real Loyalty REST API. All methods preserve the same
 * Observable<T> contract as the previous mock implementation so no consumer
 * changes are required beyond the account component's transaction loading.
 *
 * Authentication headers are injected automatically by loyaltyAuthInterceptor.
 * Token refresh on 401 is also handled by the interceptor.
 *
 * Real API base: https://reservation-system-db-api-loyalty-gufra2fxfdd2eka6.uksouth-01.azurewebsites.net
 */

import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError, switchMap, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { LoyaltyCustomer, AuthSession, LoyaltyTransaction, LoyaltyTier, TransactionType } from '../models/loyalty.model';
import { environment } from '../environments/environment';

export interface LoginParams {
  email: string;
  password: string;
}

export interface RegisterParams {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  dateOfBirth: string;
  nationality: string;
  phoneNumber: string;
}

export interface UpdateProfileParams {
  givenName?: string;
  surname?: string;
  phoneNumber?: string;
  dateOfBirth?: string;
  nationality?: string;
  preferredLanguage?: string;
}

// ── API response shapes ──────────────────────────────────────────────────────
// These may differ from the internal model; mappers normalise them.

interface ApiTokens {
  accessToken: string;
  refreshToken: string;
}

interface ApiAuthResponse extends ApiTokens {
  loyaltyNumber?: string;
  customer?: ApiCustomerProfile;
}

interface ApiCustomerProfile {
  loyaltyNumber: string;
  givenName: string;
  surname: string;
  email: string;
  phoneNumber?: string;
  dateOfBirth?: string;
  tier: LoyaltyTier;
  pointsBalance: number;
  memberSince: string;
}

interface ApiTransaction {
  transactionId: string;
  transactionType: string;
  pointsDelta: number;
  balanceAfter: number;
  bookingReference?: string;
  flightNumber?: string;
  description: string;
  transactionDate: string;
}

interface ApiTransactionsResponse {
  loyaltyNumber: string;
  page: number;
  pageSize: number;
  totalCount: number;
  transactions: ApiTransaction[];
}

// ── Mappers ──────────────────────────────────────────────────────────────────

function mapCustomer(api: ApiCustomerProfile): LoyaltyCustomer {
  return {
    loyaltyNumber: api.loyaltyNumber,
    givenName: api.givenName,
    surname: api.surname,
    email: api.email,
    phone: api.phoneNumber ?? '',
    dateOfBirth: api.dateOfBirth ?? '',
    nationality: '',
    preferredLanguage: 'en',
    tier: api.tier ?? 'Blue',
    pointsBalance: api.pointsBalance ?? 0,
    tierProgressPoints: 0,
    memberSince: api.memberSince ?? '',
    transactions: [] // loaded separately via getTransactions()
  };
}

function mapTransaction(api: ApiTransaction): LoyaltyTransaction {
  return {
    transactionId: api.transactionId,
    type: api.transactionType as TransactionType,
    points: api.pointsDelta,
    description: api.description,
    referenceBooking: api.bookingReference,
    transactionDate: api.transactionDate,
    runningBalance: api.balanceAfter
  };
}

function handleError(error: HttpErrorResponse): Observable<never> {
  const apiMessage =
    error.error?.message ??
    error.error?.error ??
    error.statusText ??
    'An unexpected error occurred.';
  return throwError(() => ({ status: error.status, message: apiMessage }));
}

// ── Service ──────────────────────────────────────────────────────────────────

const BASE = `${environment.loyaltyApiBaseUrl}/api/v1`;

@Injectable({ providedIn: 'root' })
export class LoyaltyApiService {
  private readonly http = inject(HttpClient);

  /**
   * POST /auth/login
   * Authenticates the user and returns an AuthSession.
   * If the response does not embed a full customer profile the profile is
   * fetched separately with the returned access token.
   */
  login(params: LoginParams): Observable<AuthSession> {
    return this.http
      .post<ApiAuthResponse>(`${BASE}/auth/login`, params)
      .pipe(
        switchMap((res) => {
          const tokens: ApiTokens = { accessToken: res.accessToken, refreshToken: res.refreshToken };

          if (res.customer) {
            return of<AuthSession>({
              customer: mapCustomer(res.customer),
              ...tokens
            });
          }

          // Fetch profile separately when login only returns tokens + loyaltyNumber
          const loyaltyNumber = res.loyaltyNumber ?? '';
          return this.http
            .get<ApiCustomerProfile>(`${BASE}/customers/${loyaltyNumber}/profile`, {
              headers: { Authorization: `Bearer ${tokens.accessToken}` }
            })
            .pipe(
              map((profile): AuthSession => ({
                customer: mapCustomer(profile),
                ...tokens
              }))
            );
        }),
        catchError(handleError)
      );
  }

  /**
   * POST /register
   * Registers a new loyalty programme member and returns an AuthSession.
   */
  register(params: RegisterParams): Observable<AuthSession> {
    return this.http
      .post<ApiAuthResponse>(`${BASE}/register`, params)
      .pipe(
        switchMap((res) => {
          const tokens: ApiTokens = { accessToken: res.accessToken, refreshToken: res.refreshToken };

          if (res.customer) {
            return of<AuthSession>({
              customer: mapCustomer(res.customer),
              ...tokens
            });
          }

          const loyaltyNumber = res.loyaltyNumber ?? '';
          return this.http
            .get<ApiCustomerProfile>(`${BASE}/customers/${loyaltyNumber}/profile`, {
              headers: { Authorization: `Bearer ${tokens.accessToken}` }
            })
            .pipe(
              map((profile): AuthSession => ({
                customer: mapCustomer(profile),
                ...tokens
              }))
            );
        }),
        catchError(handleError)
      );
  }

  /**
   * GET /customers/{loyaltyNumber}/profile
   * Retrieves customer profile, tier and points balance.
   * Transactions are not embedded here – use getTransactions() separately.
   */
  getCustomer(loyaltyNumber: string): Observable<LoyaltyCustomer> {
    return this.http
      .get<ApiCustomerProfile>(`${BASE}/customers/${loyaltyNumber}/profile`)
      .pipe(
        map(mapCustomer),
        catchError(handleError)
      );
  }

  /**
   * GET /customers/{loyaltyNumber}/transactions
   * Retrieves the full points transaction history for the member.
   */
  getTransactions(loyaltyNumber: string): Observable<LoyaltyTransaction[]> {
    return this.http
      .get<ApiTransactionsResponse>(`${BASE}/customers/${loyaltyNumber}/transactions`)
      .pipe(
        map(res => (Array.isArray(res?.transactions) ? res.transactions.map(mapTransaction) : [])),
        catchError(handleError)
      );
  }

  /**
   * PATCH /customers/{loyaltyNumber}/profile
   * Updates editable profile fields and returns the updated customer.
   */
  updateProfile(loyaltyNumber: string, params: UpdateProfileParams): Observable<LoyaltyCustomer> {
    return this.http
      .patch<void>(`${BASE}/customers/${loyaltyNumber}/profile`, params)
      .pipe(
        switchMap(() => this.getCustomer(loyaltyNumber)),
        catchError(handleError)
      );
  }

  /**
   * POST /auth/password-reset/request
   * Sends a password-reset code to the given email address.
   */
  requestPasswordReset(email: string): Observable<void> {
    return this.http
      .post<void>(`${BASE}/auth/password/reset-request`, { email })
      .pipe(catchError(handleError));
  }

  /**
   * POST /auth/password-reset/confirm
   * Resets the password using the 6-digit code and the new password.
   */
  resetPassword(token: string, newPassword: string): Observable<void> {
    return this.http
      .post<void>(`${BASE}/auth/password/reset`, { token, newPassword })
      .pipe(catchError(handleError));
  }

  /**
   * POST /auth/logout
   * Revokes the current refresh token server-side.
   */
  logout(refreshToken?: string): Observable<void> {
    const body = refreshToken ? { refreshToken } : {};
    return this.http
      .post<void>(`${BASE}/auth/logout`, body)
      .pipe(catchError(() => of(undefined as void)));
  }
}
