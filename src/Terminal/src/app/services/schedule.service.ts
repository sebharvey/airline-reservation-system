import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface ScheduleSummary {
  scheduleId: string;
  flightNumber: string;
  origin: string;
  destination: string;
  departureTime: string;
  arrivalTime: string;
  arrivalDayOffset: number;
  daysOfWeek: number;
  aircraftType: string;
  validFrom: string;
  validTo: string;
  flightsCreated: number;
  operatingDateCount: number;
}

export interface GetSchedulesResponse {
  count: number;
  schedules: ScheduleSummary[];
}

export interface CabinDefinition {
  cabinCode: string;
  totalSeats: number;
}

export interface ImportSchedulesToInventoryRequest {
  cabins: CabinDefinition[];
}

export interface ImportSchedulesToInventoryResponse {
  schedulesProcessed: number;
  inventoriesCreated: number;
  inventoriesSkipped: number;
  faresCreated: number;
}

@Injectable({ providedIn: 'root' })
export class ScheduleService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.operationsApiUrl}/api/v1/schedules`;

  async getSchedules(): Promise<GetSchedulesResponse> {
    return firstValueFrom(
      this.#http.get<GetSchedulesResponse>(this.#baseUrl)
    );
  }

  async importSchedulesToInventory(request: ImportSchedulesToInventoryRequest): Promise<ImportSchedulesToInventoryResponse> {
    return firstValueFrom(
      this.#http.post<ImportSchedulesToInventoryResponse>(`${this.#baseUrl}/import-inventory`, request)
    );
  }
}
