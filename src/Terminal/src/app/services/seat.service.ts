import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface CabinCount {
  cabin: string;
  count: number;
}

export interface AircraftType {
  aircraftTypeCode: string;
  manufacturer: string;
  friendlyName: string | null;
  totalSeats: number;
  cabinCounts: CabinCount[] | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface GetAircraftTypesResponse {
  aircraftTypes: AircraftType[];
}

@Injectable({ providedIn: 'root' })
export class SeatService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.operationsApiUrl}/api/v1`;

  async getAircraftTypes(): Promise<GetAircraftTypesResponse> {
    return firstValueFrom(
      this.#http.get<GetAircraftTypesResponse>(`${this.#baseUrl}/aircraft-types`)
    );
  }
}
