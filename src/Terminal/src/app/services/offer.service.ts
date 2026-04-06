import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

// Per-cabin data as returned by the admin grouped inventory view.
export interface CabinInventory {
  totalSeats: number;
  seatsAvailable: number;
  seatsSold: number;
  seatsHeld: number;
}

// Admin inventory view: one entry per flight with fixed F/J/W/Y cabin breakdown.
export interface FlightInventoryGroup {
  inventoryId: string;
  flightNumber: string;
  departureDate: string;
  departureTime: string;
  arrivalTime: string;
  arrivalDayOffset: number;
  origin: string;
  destination: string;
  aircraftType: string;
  status: string;
  f: CabinInventory | null;
  j: CabinInventory | null;
  w: CabinInventory | null;
  y: CabinInventory | null;
  totalSeats: number;
  totalSeatsAvailable: number;
  loadFactor: number;
  ticketingStatus: string;
}

// Per-cabin entry within a single-flight inventory response.
export interface CabinInventoryDetail {
  cabinCode: string;
  totalSeats: number;
  seatsAvailable: number;
  seatsSold: number;
  seatsHeld: number;
}

// Single-flight inventory response (create / hold / sell / release endpoints).
export interface FlightInventoryResponse {
  inventoryId: string;
  flightNumber: string;
  departureDate: string;
  totalSeats: number;
  seatsAvailable: number;
  status: string;
  cabins: CabinInventoryDetail[];
}

export interface InventoryHold {
  holdId: string;
  orderId: string;
  cabinCode: string;
  paxCount: number;
  status: string;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class OfferService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.retailApiUrl}/api/v1/admin`;
  #offerMsUrl = `${environment.offerMsUrl}/api/v1`;

  async getFlightInventory(departureDate: string): Promise<FlightInventoryGroup[]> {
    return firstValueFrom(
      this.#http.get<FlightInventoryGroup[]>(
        `${this.#baseUrl}/inventory`,
        { params: { departureDate } }
      )
    );
  }

  async getInventoryHolds(inventoryId: string): Promise<InventoryHold[]> {
    return firstValueFrom(
      this.#http.get<InventoryHold[]>(
        `${this.#offerMsUrl}/inventory/${inventoryId}/holds`
      )
    );
  }
}
