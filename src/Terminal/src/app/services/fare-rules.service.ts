import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export type RuleType = 'Money' | 'Points';

export interface TaxLine {
  code: string;
  amount: number;
}

export interface FareRule {
  fareRuleId: string;
  ruleType: RuleType;
  flightNumber: string | null;
  fareBasisCode: string;
  fareFamily: string | null;
  cabinCode: string;
  bookingClass: string;
  currencyCode: string | null;
  minAmount: number | null;
  maxAmount: number | null;
  minPoints: number | null;
  maxPoints: number | null;
  pointsTaxes: number | null;
  taxLines: TaxLine[] | null;
  isRefundable: boolean;
  isChangeable: boolean;
  isPrivate: boolean;
  changeFeeAmount: number;
  cancellationFeeAmount: number;
  validFrom: string | null;
  validTo: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateFareRuleRequest {
  ruleType: RuleType;
  flightNumber?: string | null;
  fareBasisCode: string;
  fareFamily?: string | null;
  cabinCode: string;
  bookingClass: string;
  currencyCode?: string | null;
  minAmount?: number | null;
  maxAmount?: number | null;
  minPoints?: number | null;
  maxPoints?: number | null;
  pointsTaxes?: number | null;
  taxLines?: TaxLine[] | null;
  isRefundable: boolean;
  isChangeable: boolean;
  isPrivate: boolean;
  changeFeeAmount: number;
  cancellationFeeAmount: number;
  validFrom?: string | null;
  validTo?: string | null;
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
