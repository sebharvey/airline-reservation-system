import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface WatchlistEntry {
  watchlistId: string;
  givenName: string;
  surname: string;
  dateOfBirth: string;
  passportNumber: string;
  addedBy: string;
  notes: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateWatchlistEntryRequest {
  givenName: string;
  surname: string;
  dateOfBirth: string;
  passportNumber: string;
  notes?: string;
}

export interface UpdateWatchlistEntryRequest {
  givenName: string;
  surname: string;
  dateOfBirth: string;
  passportNumber: string;
  notes?: string;
}

@Injectable({ providedIn: 'root' })
export class WatchlistService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.operationsApiUrl}/api/v1/admin/watchlist-entries`;

  async getAll(): Promise<WatchlistEntry[]> {
    const result = await firstValueFrom(
      this.#http.get<{ entries: WatchlistEntry[] }>(this.#baseUrl)
    );
    return result.entries;
  }

  async getById(watchlistId: string): Promise<WatchlistEntry> {
    return firstValueFrom(
      this.#http.get<WatchlistEntry>(`${this.#baseUrl}/${watchlistId}`)
    );
  }

  async create(request: CreateWatchlistEntryRequest): Promise<WatchlistEntry> {
    return firstValueFrom(
      this.#http.post<WatchlistEntry>(this.#baseUrl, request)
    );
  }

  async update(watchlistId: string, request: UpdateWatchlistEntryRequest): Promise<WatchlistEntry> {
    return firstValueFrom(
      this.#http.put<WatchlistEntry>(`${this.#baseUrl}/${watchlistId}`, request)
    );
  }

  async delete(watchlistId: string): Promise<void> {
    await firstValueFrom(
      this.#http.delete(`${this.#baseUrl}/${watchlistId}`)
    );
  }
}
