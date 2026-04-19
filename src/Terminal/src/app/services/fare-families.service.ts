import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface FareFamily {
  fareFamilyId: string;
  name: string;
  description: string | null;
  displayOrder: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateFareFamilyRequest {
  name: string;
  description?: string | null;
  displayOrder: number;
}

export type UpdateFareFamilyRequest = CreateFareFamilyRequest;

@Injectable({ providedIn: 'root' })
export class FareFamiliesService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.operationsApiUrl}/api/v1/admin/fare-families`;

  async getFareFamilies(): Promise<FareFamily[]> {
    return firstValueFrom(this.#http.get<FareFamily[]>(this.#baseUrl));
  }

  async getFareFamily(fareFamilyId: string): Promise<FareFamily> {
    return firstValueFrom(this.#http.get<FareFamily>(`${this.#baseUrl}/${fareFamilyId}`));
  }

  async createFareFamily(request: CreateFareFamilyRequest): Promise<FareFamily> {
    return firstValueFrom(this.#http.post<FareFamily>(this.#baseUrl, request));
  }

  async updateFareFamily(fareFamilyId: string, request: UpdateFareFamilyRequest): Promise<FareFamily> {
    return firstValueFrom(this.#http.put<FareFamily>(`${this.#baseUrl}/${fareFamilyId}`, request));
  }

  async deleteFareFamily(fareFamilyId: string): Promise<void> {
    await firstValueFrom(this.#http.delete(`${this.#baseUrl}/${fareFamilyId}`));
  }
}
