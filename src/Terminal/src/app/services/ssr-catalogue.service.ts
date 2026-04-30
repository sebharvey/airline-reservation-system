import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface SsrCatalogueEntry {
  ssrCode: string;
  label: string;
  category: string;
}

@Injectable({ providedIn: 'root' })
export class SsrCatalogueService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.retailApiUrl}/api/v1/admin/ssr/options`;

  async getAll(): Promise<SsrCatalogueEntry[]> {
    const response = await firstValueFrom(
      this.#http.get<{ ssrOptions: SsrCatalogueEntry[] }>(this.#baseUrl)
    );
    return response.ssrOptions;
  }
}
