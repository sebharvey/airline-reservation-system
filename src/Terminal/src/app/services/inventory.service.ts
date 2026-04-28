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
  departureGate: string | null;
  aircraftRegistration: string | null;
  f: CabinInventory | null;
  j: CabinInventory | null;
  w: CabinInventory | null;
  y: CabinInventory | null;
  totalSeats: number;
  totalSeatsAvailable: number;
  loadFactor: number;
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

export interface ManifestEntry {
  orderId: string;
  bookingReference: string;
  passengerId: string;
  givenName: string;
  surname: string;
  eTicketNumber: string;
  seatNumber: string | null;
  cabinCode: string;
  bookingType: string;
  checkedIn: boolean;
  checkedInAt: string | null;
  ssrCodes: string[];
  gender: string | null;
  dateOfBirth: string | null;
  ptcCode: string;
}

export interface FlightManifest {
  entries: ManifestEntry[];
}

export interface DisruptionCancelOutcome {
  bookingReference: string;
  outcome: 'Rebooked' | 'CancelledWithRefund' | 'Failed';
  replacementFlightNumber?: string;
  replacementDepartureDate?: string;
  failureReason?: string;
}

export interface DisruptionCancelResponse {
  flightNumber: string;
  departureDate: string;
  affectedPassengerCount: number;
  rebookedCount: number;
  cancelledWithRefundCount: number;
  failedCount: number;
  outcomes: DisruptionCancelOutcome[];
  processedAt: string;
}

export interface IropsOrderItem {
  bookingReference: string;
  bookingType: string;
  cabinCode: string;
  loyaltyTier?: string;
  loyaltyNumber?: string;
  bookingDate: string;
  passengerCount: number;
  passengerNames: string[];
}

export interface IropsOrdersResponse {
  flightNumber: string;
  departureDate: string;
  origin: string;
  destination: string;
  orders: IropsOrderItem[];
}

export interface IropsRebookOrderResponse {
  bookingReference: string;
  outcome: 'Rebooked' | 'Failed';
  replacementFlightNumber?: string;
  replacementDepartureDate?: string;
  failureReason?: string;
}

export interface AircraftType {
  aircraftTypeCode: string;
  manufacturer: string;
  friendlyName: string | null;
  totalSeats: number;
  isActive: boolean;
}

@Injectable({ providedIn: 'root' })
export class InventoryService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.retailApiUrl}/api/v1/admin`;
  #operationsBaseUrl = `${environment.operationsApiUrl}/api/v1/admin`;

  lastSelectedDate: string = new Date().toISOString().slice(0, 10);

  async getFlightInventory(departureDate: string): Promise<FlightInventoryGroup[]> {
    return firstValueFrom(
      this.#http.get<FlightInventoryGroup[]>(
        `${this.#baseUrl}/inventory`,
        { params: { departureDate } }
      )
    );
  }

  async cancelFlight(flightNumber: string, departureDate: string): Promise<DisruptionCancelResponse> {
    return firstValueFrom(
      this.#http.post<DisruptionCancelResponse>(
        `${this.#operationsBaseUrl}/disruption/cancel`,
        { flightNumber, departureDate }
      )
    );
  }

  async cancelFlightInventoryOnly(flightNumber: string, departureDate: string): Promise<void> {
    await firstValueFrom(
      this.#http.post<void>(
        `${this.#operationsBaseUrl}/inventory/cancel`,
        { flightNumber, departureDate }
      )
    );
  }

  async getIropsOrders(flightNumber: string, departureDate: string): Promise<IropsOrdersResponse> {
    return firstValueFrom(
      this.#http.get<IropsOrdersResponse>(
        `${this.#operationsBaseUrl}/disruption/orders`,
        { params: { flightNumber, departureDate } }
      )
    );
  }

  async rebookIropsOrder(bookingReference: string, flightNumber: string, departureDate: string): Promise<IropsRebookOrderResponse> {
    return firstValueFrom(
      this.#http.post<IropsRebookOrderResponse>(
        `${this.#operationsBaseUrl}/disruption/rebook-order`,
        { bookingReference, flightNumber, departureDate }
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

  async getFlightManifest(flightNumber: string, departureDate: string): Promise<FlightManifest> {
    return firstValueFrom(
      this.#http.get<FlightManifest>(
        `${this.#baseUrl}/manifest`,
        { params: { flightNumber, departureDate } }
      )
    );
  }

  async releaseSeat(
    eTicketNumber: string,
    bookingReference: string,
    passengerId: string,
    inventoryId: string,
    orderId: string,
    cabinCode: string,
  ): Promise<void> {
    await firstValueFrom(
      this.#http.post<void>(
        `${this.#baseUrl}/manifest/release-seat`,
        { eTicketNumber, bookingReference, passengerId, inventoryId, orderId, cabinCode }
      )
    );
  }

  async assignSeat(
    eTicketNumber: string,
    bookingReference: string,
    passengerId: string,
    inventoryId: string,
    seatNumber: string,
  ): Promise<void> {
    await firstValueFrom(
      this.#http.post<void>(
        `${this.#baseUrl}/manifest/assign-seat`,
        { eTicketNumber, bookingReference, passengerId, inventoryId, seatNumber }
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

  async getAircraftTypes(): Promise<AircraftType[]> {
    return firstValueFrom(
      this.#http.get<AircraftType[]>(`${this.#baseUrl}/aircraft-types`)
    );
  }

  async changeAircraftType(flightNumber: string, departureDate: string, newAircraftType: string): Promise<void> {
    await firstValueFrom(
      this.#http.post<void>(
        `${this.#operationsBaseUrl}/disruption/change`,
        { flightNumber, departureDate, newAircraftType }
      )
    );
  }

  async setInventoryOperationalData(
    inventoryId: string,
    departureGate: string | null,
    aircraftRegistration: string | null,
  ): Promise<void> {
    await firstValueFrom(
      this.#http.patch<void>(
        `${this.#operationsBaseUrl}/inventory/${inventoryId}/operational-data`,
        { departureGate, aircraftRegistration }
      )
    );
  }
}
