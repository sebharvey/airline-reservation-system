import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface CabinInventory {
  totalSeats: number;
  seatsAvailable: number;
  seatsSold: number;
  seatsHeld: number;
}

export interface FlightInventoryGroup {
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
}

@Injectable({ providedIn: 'root' })
export class OfferService {
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
}
