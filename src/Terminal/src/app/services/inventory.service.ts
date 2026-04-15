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
  bookingReference: string | null;
  passengerName: string | null;
  cabinCode: string;
  seatNumber: string | null;
  status: string;
  holdType: string;
  standbyPriority: number | null;
  createdAt: string;
}

export type SeatAvailability = 'available' | 'held' | 'sold';

export interface SeatmapSeat {
  seatOfferId: string;
  seatNumber: string;
  column: string;
  rowNumber: number;
  position: string;
  cabinCode: string;
  availability: SeatAvailability;
  attributes: string[];
}

export interface CabinSeatmap {
  cabinCode: string;
  cabinName: string;
  columns: string[];
  layout: string;
  startRow: number;
  endRow: number;
  seats: SeatmapSeat[];
}

export interface FlightSeatmap {
  flightId: string;
  flightNumber: string;
  aircraftType: string;
  cabins: CabinSeatmap[];
}

@Injectable({ providedIn: 'root' })
export class InventoryService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.retailApiUrl}/api/v1/admin`;

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
        `${this.#baseUrl}/inventory/${inventoryId}/holds`
      )
    );
  }

  async getFlightSeatmap(inventoryId: string, flightNumber: string, aircraftType: string): Promise<FlightSeatmap> {
    const params = `flightNumber=${encodeURIComponent(flightNumber)}&aircraftType=${encodeURIComponent(aircraftType)}`;
    return firstValueFrom(
      this.#http.get<FlightSeatmap>(
        `${environment.retailApiUrl}/api/v1/flights/${inventoryId}/seatmap?${params}`
      )
    );
  }
}
