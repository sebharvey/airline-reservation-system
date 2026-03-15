/**
 * LoyaltyApiService
 *
 * Service for loyalty programme operations: authentication, registration,
 * profile management, and points/tier data.
 *
 * Currently returns mock data. To connect to the real Loyalty API, replace
 * each method body with an HttpClient call. The Observable<T> contract is
 * identical so no consumer changes are required.
 *
 * Real API base: POST /v1/auth/login, GET /v1/customers/{loyaltyNumber}, etc.
 */

import { Injectable } from '@angular/core';
import { Observable, of, throwError } from 'rxjs';
import { delay } from 'rxjs/operators';
import { LoyaltyCustomer, AuthSession, LoyaltyTransaction } from '../models/loyalty.model';
import { MOCK_LOYALTY_CUSTOMERS, MOCK_PASSWORDS } from '../data/mock/loyalty.mock';

export interface LoginParams {
  email: string;
  password: string;
}

export interface RegisterParams {
  givenName: string;
  surname: string;
  email: string;
  password: string;
  dateOfBirth: string;
  nationality: string;
  phone: string;
}

export interface UpdateProfileParams {
  givenName?: string;
  surname?: string;
  phone?: string;
  dateOfBirth?: string;
  nationality?: string;
  preferredLanguage?: string;
}

const API_DELAY_MS = 600;
let NEXT_LOYALTY_NUM = 5000000;

@Injectable({ providedIn: 'root' })
export class LoyaltyApiService {

  /**
   * POST /v1/auth/login
   * Authenticate and return access token + customer profile.
   */
  login(params: LoginParams): Observable<AuthSession> {
    const customer = MOCK_LOYALTY_CUSTOMERS[params.email.toLowerCase().trim()];
    const expectedPassword = MOCK_PASSWORDS[params.email.toLowerCase().trim()];

    if (!customer || params.password !== expectedPassword) {
      return throwError(() => ({
        status: 401,
        message: 'Invalid email address or password.'
      })).pipe(delay(API_DELAY_MS));
    }

    const session: AuthSession = {
      customer: { ...customer },
      accessToken: 'mock-access-token-' + Date.now(),
      refreshToken: 'mock-refresh-token-' + Date.now()
    };
    return of(session).pipe(delay(API_DELAY_MS));
  }

  /**
   * POST /v1/register
   * Register a new loyalty programme member.
   */
  register(params: RegisterParams): Observable<AuthSession> {
    // Simulate duplicate email check
    if (MOCK_LOYALTY_CUSTOMERS[params.email.toLowerCase()]) {
      return throwError(() => ({
        status: 409,
        message: 'An account with this email address already exists.'
      })).pipe(delay(API_DELAY_MS));
    }

    const loyaltyNumber = 'AX' + String(NEXT_LOYALTY_NUM++);
    const newCustomer: LoyaltyCustomer = {
      loyaltyNumber,
      givenName: params.givenName,
      surname: params.surname,
      email: params.email,
      phone: params.phone,
      dateOfBirth: params.dateOfBirth,
      nationality: params.nationality,
      preferredLanguage: 'en',
      tier: 'Blue',
      pointsBalance: 2500,
      tierProgressPoints: 2500,
      memberSince: new Date().toISOString().split('T')[0],
      transactions: [
        {
          transactionId: 'TXN-WELCOME',
          type: 'Accrual',
          points: 2500,
          description: 'Welcome bonus — new member registration',
          transactionDate: new Date().toISOString(),
          runningBalance: 2500
        }
      ]
    };

    // Add to mock store for the session
    MOCK_LOYALTY_CUSTOMERS[params.email.toLowerCase()] = newCustomer;
    MOCK_PASSWORDS[params.email.toLowerCase()] = params.password;

    return of({
      customer: newCustomer,
      accessToken: 'mock-access-token-' + Date.now(),
      refreshToken: 'mock-refresh-token-' + Date.now()
    }).pipe(delay(API_DELAY_MS));
  }

  /**
   * GET /v1/customers/{loyaltyNumber}
   * Retrieve customer profile, tier, and points balance.
   */
  getCustomer(loyaltyNumber: string): Observable<LoyaltyCustomer> {
    const customer = Object.values(MOCK_LOYALTY_CUSTOMERS).find(
      c => c.loyaltyNumber === loyaltyNumber
    );
    if (!customer) {
      return throwError(() => ({ status: 404, message: 'Customer not found' })).pipe(delay(API_DELAY_MS));
    }
    return of({ ...customer }).pipe(delay(API_DELAY_MS));
  }

  /**
   * GET /v1/customers/{loyaltyNumber}/transactions
   * Retrieve paginated points transaction history.
   */
  getTransactions(loyaltyNumber: string): Observable<LoyaltyTransaction[]> {
    const customer = Object.values(MOCK_LOYALTY_CUSTOMERS).find(
      c => c.loyaltyNumber === loyaltyNumber
    );
    if (!customer) {
      return throwError(() => ({ status: 404, message: 'Customer not found' })).pipe(delay(API_DELAY_MS));
    }
    return of([...customer.transactions]).pipe(delay(API_DELAY_MS));
  }

  /**
   * PATCH /v1/customers/{loyaltyNumber}/profile
   * Update profile details.
   */
  updateProfile(loyaltyNumber: string, params: UpdateProfileParams): Observable<LoyaltyCustomer> {
    const customer = Object.values(MOCK_LOYALTY_CUSTOMERS).find(
      c => c.loyaltyNumber === loyaltyNumber
    );
    if (!customer) {
      return throwError(() => ({ status: 404, message: 'Customer not found' })).pipe(delay(API_DELAY_MS));
    }
    Object.assign(customer, params);
    return of({ ...customer }).pipe(delay(API_DELAY_MS));
  }

  /**
   * POST /v1/auth/password/reset-request
   * Request a password reset link. Always returns success to prevent enumeration.
   */
  requestPasswordReset(_email: string): Observable<void> {
    // Always succeeds regardless of whether the email exists
    return of(undefined).pipe(delay(API_DELAY_MS));
  }

  /**
   * POST /v1/auth/password/reset
   * Submit a new password using a valid single-use reset token.
   * Mock: token '123456' always succeeds.
   */
  resetPassword(token: string, _newPassword: string): Observable<void> {
    if (token !== '123456') {
      return throwError(() => ({
        status: 400,
        message: 'Invalid or expired reset token. Please request a new link.'
      })).pipe(delay(API_DELAY_MS));
    }
    return of(undefined).pipe(delay(API_DELAY_MS));
  }

  /**
   * POST /v1/auth/logout
   * Revoke current refresh token.
   */
  logout(): Observable<void> {
    return of(undefined).pipe(delay(300));
  }
}
