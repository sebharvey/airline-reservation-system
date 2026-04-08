import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

// ── Schedule Group types ──────────────────────────────────────────────────────

export interface ScheduleGroupSummary {
  scheduleGroupId: string;
  name: string;
  seasonStart: string;
  seasonEnd: string;
  isActive: boolean;
  scheduleCount: number;
  createdBy: string;
  createdAt: string;
}

export interface GetScheduleGroupsResponse {
  count: number;
  groups: ScheduleGroupSummary[];
}

export interface CreateScheduleGroupRequest {
  name: string;
  seasonStart: string;
  seasonEnd: string;
  isActive: boolean;
  createdBy: string;
}

export interface UpdateScheduleGroupRequest {
  name: string;
  seasonStart: string;
  seasonEnd: string;
  isActive: boolean;
}

// ── Schedule types ────────────────────────────────────────────────────────────

export interface ScheduleSummary {
  scheduleId: string;
  scheduleGroupId: string;
  flightNumber: string;
  origin: string;
  destination: string;
  departureTime: string;
  arrivalTime: string;
  arrivalDayOffset: number;
  departureTimeUtc: string | null;
  arrivalTimeUtc: string | null;
  arrivalDayOffsetUtc: number | null;
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

export interface ImportSchedulesToInventoryRequest {
  scheduleGroupId?: string;
  toDate?: string;
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
  #baseUrl = `${environment.operationsApiUrl}/api/v1`;

  // ── Schedule Groups ─────────────────────────────────────────────────────────

  async getScheduleGroups(): Promise<GetScheduleGroupsResponse> {
    return firstValueFrom(
      this.#http.get<GetScheduleGroupsResponse>(`${this.#baseUrl}/schedule-groups`)
    );
  }

  async createScheduleGroup(request: CreateScheduleGroupRequest): Promise<ScheduleGroupSummary> {
    return firstValueFrom(
      this.#http.post<ScheduleGroupSummary>(`${this.#baseUrl}/schedule-groups`, request)
    );
  }

  async updateScheduleGroup(scheduleGroupId: string, request: UpdateScheduleGroupRequest): Promise<ScheduleGroupSummary> {
    return firstValueFrom(
      this.#http.put<ScheduleGroupSummary>(`${this.#baseUrl}/schedule-groups/${scheduleGroupId}`, request)
    );
  }

  async deleteScheduleGroup(scheduleGroupId: string): Promise<void> {
    return firstValueFrom(
      this.#http.delete<void>(`${this.#baseUrl}/schedule-groups/${scheduleGroupId}`)
    );
  }

  // ── Schedules ───────────────────────────────────────────────────────────────

  async getSchedules(scheduleGroupId?: string): Promise<GetSchedulesResponse> {
    let url = `${this.#baseUrl}/schedules`;
    if (scheduleGroupId) url += `?scheduleGroupId=${scheduleGroupId}`;
    return firstValueFrom(
      this.#http.get<GetSchedulesResponse>(url)
    );
  }

  async importSchedulesToInventory(request: ImportSchedulesToInventoryRequest): Promise<ImportSchedulesToInventoryResponse> {
    return firstValueFrom(
      this.#http.post<ImportSchedulesToInventoryResponse>(`${this.#baseUrl}/schedules/import-inventory`, request)
    );
  }
}
