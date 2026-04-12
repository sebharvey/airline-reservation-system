import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface SsrCatalogueEntry {
  ssrCode: string;
  label: string;
  category: string;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class SsrCatalogueService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.retailApiUrl}/api/v1/ssr`;

  async getAll(): Promise<SsrCatalogueEntry[]> {
    return firstValueFrom(this.#http.get<SsrCatalogueEntry[]>(this.#baseUrl));
  }
}
