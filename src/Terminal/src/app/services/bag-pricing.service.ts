import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface BagPricing {
  pricingId: string;
  bagSequence: number;
  currencyCode: string;
  price: number;
  isActive: boolean;
  validFrom: string;
  validTo: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateBagPricingRequest {
  bagSequence: number;
  currencyCode: string;
  price: number;
  validFrom: string;
  validTo?: string | null;
}

export interface UpdateBagPricingRequest {
  price: number;
  isActive: boolean;
  validFrom: string;
  validTo?: string | null;
}

@Injectable({ providedIn: 'root' })
export class BagPricingService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.operationsApiUrl}/api/v1/admin/bag-pricing`;

  async getAll(): Promise<BagPricing[]> {
    return firstValueFrom(
      this.#http.get<BagPricing[]>(this.#baseUrl)
    );
  }

  async getById(pricingId: string): Promise<BagPricing> {
    return firstValueFrom(
      this.#http.get<BagPricing>(`${this.#baseUrl}/${pricingId}`)
    );
  }

  async create(request: CreateBagPricingRequest): Promise<BagPricing> {
    return firstValueFrom(
      this.#http.post<BagPricing>(this.#baseUrl, request)
    );
  }

  async update(pricingId: string, request: UpdateBagPricingRequest): Promise<BagPricing> {
    return firstValueFrom(
      this.#http.put<BagPricing>(`${this.#baseUrl}/${pricingId}`, request)
    );
  }

  async delete(pricingId: string): Promise<void> {
    await firstValueFrom(
      this.#http.delete(`${this.#baseUrl}/${pricingId}`)
    );
  }
}
