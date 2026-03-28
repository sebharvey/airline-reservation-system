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
  tierCode: string;
  pointsBalance: number;
  tierProgressPoints: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
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
}
