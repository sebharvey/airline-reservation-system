import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface FareRule {
  fareRuleId: string;
  flightNumber: string | null;
  fareBasisCode: string;
  fareFamily: string | null;
  cabinCode: string;
  bookingClass: string;
  currencyCode: string;
  baseFareAmount: number;
  taxAmount: number;
  totalAmount: number;
  isRefundable: boolean;
  isChangeable: boolean;
  changeFeeAmount: number;
  cancellationFeeAmount: number;
  pointsPrice: number | null;
  pointsTaxes: number | null;
  validFrom: string;
  validTo: string;
  createdAt: string;
  updatedAt: string;
}

export interface CreateFareRuleRequest {
  flightNumber?: string | null;
  fareBasisCode: string;
  fareFamily?: string | null;
  cabinCode: string;
  bookingClass: string;
  currencyCode: string;
  baseFareAmount: number;
  taxAmount: number;
  isRefundable: boolean;
  isChangeable: boolean;
  changeFeeAmount: number;
  cancellationFeeAmount: number;
  pointsPrice?: number | null;
  pointsTaxes?: number | null;
  validFrom: string;
  validTo: string;
}

export type UpdateFareRuleRequest = CreateFareRuleRequest;

@Injectable({ providedIn: 'root' })
export class FareRulesService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.operationsApiUrl}/api/v1/admin/fare-rules`;

  async searchFareRules(query?: string): Promise<FareRule[]> {
    return firstValueFrom(
      this.#http.post<FareRule[]>(`${this.#baseUrl}/search`, { query: query ?? '' })
    );
  }

  async getFareRule(fareRuleId: string): Promise<FareRule> {
    return firstValueFrom(
      this.#http.get<FareRule>(`${this.#baseUrl}/${fareRuleId}`)
    );
  }

  async createFareRule(request: CreateFareRuleRequest): Promise<FareRule> {
    return firstValueFrom(
      this.#http.post<FareRule>(this.#baseUrl, request)
    );
  }

  async updateFareRule(fareRuleId: string, request: UpdateFareRuleRequest): Promise<FareRule> {
    return firstValueFrom(
      this.#http.put<FareRule>(`${this.#baseUrl}/${fareRuleId}`, request)
    );
  }

  async deleteFareRule(fareRuleId: string): Promise<void> {
    await firstValueFrom(
      this.#http.delete(`${this.#baseUrl}/${fareRuleId}`)
    );
  }
}
