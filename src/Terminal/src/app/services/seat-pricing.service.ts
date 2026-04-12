import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface SeatPricing {
  seatPricingId: string;
  cabinCode: string;
  seatPosition: string;
  currencyCode: string;
  price: number;
  isActive: boolean;
  validFrom: string;
  validTo: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateSeatPricingRequest {
  cabinCode: string;
  seatPosition: string;
  currencyCode: string;
  price: number;
  validFrom: string;
  validTo: string | null;
}

export interface UpdateSeatPricingRequest {
  cabinCode?: string | null;
  seatPosition?: string | null;
  currencyCode?: string | null;
  price?: number | null;
  isActive?: boolean | null;
  validFrom?: string | null;
  validTo?: string | null;
}

@Injectable({ providedIn: 'root' })
export class SeatPricingService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.retailApiUrl}/api/v1/admin/seat-pricing`;

  async getAll(): Promise<SeatPricing[]> {
    return firstValueFrom(
      this.#http.get<SeatPricing[]>(this.#baseUrl)
    );
  }

  async getById(seatPricingId: string): Promise<SeatPricing> {
    return firstValueFrom(
      this.#http.get<SeatPricing>(`${this.#baseUrl}/${seatPricingId}`)
    );
  }

  async create(request: CreateSeatPricingRequest): Promise<SeatPricing> {
    return firstValueFrom(
      this.#http.post<SeatPricing>(this.#baseUrl, request)
    );
  }

  async update(seatPricingId: string, request: UpdateSeatPricingRequest): Promise<SeatPricing> {
    return firstValueFrom(
      this.#http.put<SeatPricing>(`${this.#baseUrl}/${seatPricingId}`, request)
    );
  }

  async delete(seatPricingId: string): Promise<void> {
    await firstValueFrom(
      this.#http.delete(`${this.#baseUrl}/${seatPricingId}`)
    );
  }
}
