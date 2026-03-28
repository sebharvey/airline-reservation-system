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

@Injectable({ providedIn: 'root' })
export class ScheduleService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.operationsApiUrl}/api/v1/schedules`;

  async getSchedules(): Promise<GetSchedulesResponse> {
    return firstValueFrom(
      this.#http.get<GetSchedulesResponse>(this.#baseUrl)
    );
  }
}
