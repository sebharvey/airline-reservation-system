import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface SsrOption {
  ssrCode: string;
  label: string;
  category: string;
}

export interface SsrOptionDetail {
  ssrCatalogueId: string;
  ssrCode: string;
  label: string;
  category: string;
  isActive: boolean;
}

export interface SsrOptionListResponse {
  ssrOptions: SsrOption[];
}

export interface CreateSsrOptionRequest {
  ssrCode: string;
  label: string;
  category: string;
}

export interface UpdateSsrOptionRequest {
  label: string;
  category: string;
}

@Injectable({ providedIn: 'root' })
export class SsrService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.adminApiUrl}/api/v1/admin/ssr`;

  async getSsrOptions(): Promise<SsrOption[]> {
    const response = await firstValueFrom(
      this.#http.get<SsrOptionListResponse>(this.#baseUrl)
    );
    return response.ssrOptions;
  }

  async createSsrOption(request: CreateSsrOptionRequest): Promise<SsrOptionDetail> {
    return firstValueFrom(
      this.#http.post<SsrOptionDetail>(this.#baseUrl, request)
    );
  }

  async updateSsrOption(ssrCode: string, request: UpdateSsrOptionRequest): Promise<SsrOptionDetail> {
    return firstValueFrom(
      this.#http.put<SsrOptionDetail>(`${this.#baseUrl}/${ssrCode}`, request)
    );
  }

  async deactivateSsrOption(ssrCode: string): Promise<void> {
    await firstValueFrom(
      this.#http.delete(`${this.#baseUrl}/${ssrCode}`)
    );
  }
}
