import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface CustomerSummary {
  loyaltyNumber: string;
  givenName: string;
  surname: string;
  tierCode: string;
  pointsBalance: number;
  isActive: boolean;
  createdAt: string;
}

export interface IdentityDetails {
  userAccountId: string;
  email: string;
  isEmailVerified: boolean;
  isLocked: boolean;
  failedLoginAttempts: number;
  lastLoginAt: string | null;
  passwordChangedAt: string;
  createdAt: string;
  updatedAt: string;
}

export interface CustomerDetail {
  loyaltyNumber: string;
  givenName: string;
  surname: string;
  dateOfBirth: string | null;
  gender: string | null;
  nationality: string | null;
  preferredLanguage: string;
  phoneNumber: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  stateOrRegion: string | null;
  postalCode: string | null;
  countryCode: string | null;
  passportNumber: string | null;
  passportIssueDate: string | null;
  passportIssuer: string | null;
  passportExpiryDate: string | null;
  knownTravellerNumber: string | null;
  tierCode: string;
  pointsBalance: number;
  tierProgressPoints: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  identity: IdentityDetails | null;
}

export interface UpdateIdentityRequest {
  email?: string | null;
  isLocked?: boolean;
}

export interface SetPasswordRequest {
  newPassword: string;
}

export interface UpdateCustomerRequest {
  givenName?: string;
  surname?: string;
  dateOfBirth?: string | null;
  gender?: string | null;
  nationality?: string | null;
  phoneNumber?: string | null;
  preferredLanguage?: string;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  stateOrRegion?: string | null;
  postalCode?: string | null;
  countryCode?: string | null;
  passportNumber?: string | null;
  passportIssueDate?: string | null;
  passportIssuer?: string | null;
  passportExpiryDate?: string | null;
  knownTravellerNumber?: string | null;
}

export interface Transaction {
  transactionId: string;
  transactionType: string;
  pointsDelta: number;
  balanceAfter: number;
  bookingReference: string | null;
  flightNumber: string | null;
  description: string;
  transactionDate: string;
}

export interface TransactionsResponse {
  loyaltyNumber: string;
  page: number;
  pageSize: number;
  totalCount: number;
  transactions: Transaction[];
}

export interface AddPointsRequest {
  points: number;
  description: string;
}

export interface CustomerOrderItem {
  customerOrderId: string;
  orderId: string;
  bookingReference: string;
  createdAt: string;
}

export interface CustomerOrdersResponse {
  loyaltyNumber: string;
  orders: CustomerOrderItem[];
}

export interface CustomerNote {
  noteId: string;
  noteText: string;
  createdBy: string;
  createdAt: string;
  updatedAt: string;
}

export interface CustomerNotesResponse {
  loyaltyNumber: string;
  notes: CustomerNote[];
}

@Injectable({ providedIn: 'root' })
export class CustomerService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.loyaltyApiUrl}/api/v1/admin/customers`;

  async searchCustomers(query?: string): Promise<CustomerSummary[]> {
    return firstValueFrom(
      this.#http.post<CustomerSummary[]>(`${this.#baseUrl}/search`, { query: query ?? '' })
    );
  }

  async getCustomer(loyaltyNumber: string): Promise<CustomerDetail> {
    return firstValueFrom(
      this.#http.get<CustomerDetail>(`${this.#baseUrl}/${loyaltyNumber}`)
    );
  }

  async updateCustomer(loyaltyNumber: string, data: UpdateCustomerRequest): Promise<void> {
    await firstValueFrom(
      this.#http.patch(`${this.#baseUrl}/${loyaltyNumber}`, data)
    );
  }

  async getTransactions(loyaltyNumber: string, page = 1, pageSize = 20): Promise<TransactionsResponse> {
    return firstValueFrom(
      this.#http.get<TransactionsResponse>(
        `${this.#baseUrl}/${loyaltyNumber}/transactions`,
        { params: { page: page.toString(), pageSize: pageSize.toString() } }
      )
    );
  }

  async addPoints(loyaltyNumber: string, request: AddPointsRequest): Promise<void> {
    await firstValueFrom(
      this.#http.post(`${this.#baseUrl}/${loyaltyNumber}/points`, request)
    );
  }

  async deleteCustomer(loyaltyNumber: string): Promise<void> {
    await firstValueFrom(
      this.#http.delete(`${this.#baseUrl}/${loyaltyNumber}`)
    );
  }

  async setAccountStatus(loyaltyNumber: string, isActive: boolean): Promise<void> {
    await firstValueFrom(
      this.#http.patch(`${this.#baseUrl}/${loyaltyNumber}/status`, { isActive })
    );
  }

  async updateIdentity(loyaltyNumber: string, request: UpdateIdentityRequest): Promise<void> {
    await firstValueFrom(
      this.#http.patch(`${this.#baseUrl}/${loyaltyNumber}/identity`, request)
    );
  }

  async setPassword(loyaltyNumber: string, request: SetPasswordRequest): Promise<void> {
    await firstValueFrom(
      this.#http.post(`${this.#baseUrl}/${loyaltyNumber}/identity/set-password`, request)
    );
  }

  async markEmailVerified(loyaltyNumber: string): Promise<void> {
    await firstValueFrom(
      this.#http.post(`${this.#baseUrl}/${loyaltyNumber}/identity/verify-email`, {})
    );
  }

  async getCustomerOrders(loyaltyNumber: string): Promise<CustomerOrdersResponse> {
    return firstValueFrom(
      this.#http.get<CustomerOrdersResponse>(
        `${this.#baseUrl}/${loyaltyNumber}/orders`
      )
    );
  }

  async getNotes(loyaltyNumber: string): Promise<CustomerNotesResponse> {
    return firstValueFrom(
      this.#http.get<CustomerNotesResponse>(`${this.#baseUrl}/${loyaltyNumber}/notes`)
    );
  }

  async addNote(loyaltyNumber: string, noteText: string): Promise<CustomerNote> {
    return firstValueFrom(
      this.#http.post<CustomerNote>(`${this.#baseUrl}/${loyaltyNumber}/notes`, { noteText })
    );
  }

  async updateNote(loyaltyNumber: string, noteId: string, noteText: string): Promise<void> {
    await firstValueFrom(
      this.#http.put(`${this.#baseUrl}/${loyaltyNumber}/notes/${noteId}`, { noteText })
    );
  }

  async deleteNote(loyaltyNumber: string, noteId: string): Promise<void> {
    await firstValueFrom(
      this.#http.delete(`${this.#baseUrl}/${loyaltyNumber}/notes/${noteId}`)
    );
  }
}
