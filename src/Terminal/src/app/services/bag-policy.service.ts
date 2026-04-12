import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface BagPolicy {
  policyId: string;
  cabinCode: string;
  freeBagsIncluded: number;
  maxWeightKgPerBag: number;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateBagPolicyRequest {
  cabinCode: string;
  freeBagsIncluded: number;
  maxWeightKgPerBag: number;
}

export interface UpdateBagPolicyRequest {
  freeBagsIncluded: number;
  maxWeightKgPerBag: number;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class BagPolicyService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.operationsApiUrl}/api/v1/admin/bag-policies`;

  async getAll(): Promise<BagPolicy[]> {
    return firstValueFrom(
      this.#http.get<BagPolicy[]>(this.#baseUrl)
    );
  }

  async getById(policyId: string): Promise<BagPolicy> {
    return firstValueFrom(
      this.#http.get<BagPolicy>(`${this.#baseUrl}/${policyId}`)
    );
  }

  async create(request: CreateBagPolicyRequest): Promise<BagPolicy> {
    return firstValueFrom(
      this.#http.post<BagPolicy>(this.#baseUrl, request)
    );
  }

  async update(policyId: string, request: UpdateBagPolicyRequest): Promise<BagPolicy> {
    return firstValueFrom(
      this.#http.put<BagPolicy>(`${this.#baseUrl}/${policyId}`, request)
    );
  }

  async delete(policyId: string): Promise<void> {
    await firstValueFrom(
      this.#http.delete(`${this.#baseUrl}/${policyId}`)
    );
  }
}
