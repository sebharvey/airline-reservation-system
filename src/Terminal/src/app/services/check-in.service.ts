import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { environment } from '../environment';

export interface TravelDocument {
  type: string;
  number: string;
  issuingCountry: string;
  nationality: string;
  issueDate: string;
  expiryDate: string;
}

export interface CheckInPassenger {
  passengerId: string;
  ticketNumber: string;
  givenName: string;
  surname: string;
  passengerTypeCode: string;
  travelDocument: TravelDocument | null;
}

export interface RetrieveResponse {
  bookingReference: string;
  checkInEligible: boolean;
  passengers: CheckInPassenger[];
}

export interface PaxSubmission {
  ticketNumber: string;
  travelDocument: TravelDocument;
}

export interface CheckInResponse {
  bookingReference: string;
  checkedIn: string[];
}

export interface BoardingCard {
  ticketNumber: string;
  passengerId: string;
  flightNumber: string;
  departureDate: string;
  seatNumber: string;
  cabinCode: string;
  sequenceNumber: string;
  origin: string;
  destination: string;
  bcbpString: string;
}

export interface BoardingDocsResponse {
  boardingCards: BoardingCard[];
}

@Injectable({ providedIn: 'root' })
export class CheckInService {
  #http = inject(HttpClient);
  #baseUrl = `${environment.operationsApiUrl}/api/v1/oci`;

  async retrieve(
    bookingReference: string,
    firstName: string,
    lastName: string,
    departureAirport: string,
  ): Promise<RetrieveResponse> {
    return firstValueFrom(
      this.#http.post<RetrieveResponse>(`${this.#baseUrl}/retrieve`, {
        bookingReference,
        firstName,
        lastName,
        departureAirport,
      }),
    );
  }

  async submitPax(bookingReference: string, passengers: PaxSubmission[]): Promise<void> {
    await firstValueFrom(
      this.#http.post(`${this.#baseUrl}/pax`, { bookingReference, passengers }),
    );
  }

  async completeCheckIn(bookingReference: string, departureAirport: string): Promise<CheckInResponse> {
    return firstValueFrom(
      this.#http.post<CheckInResponse>(`${this.#baseUrl}/checkin`, { bookingReference, departureAirport }),
    );
  }

  async getBoardingDocs(departureAirport: string, ticketNumbers: string[]): Promise<BoardingDocsResponse> {
    return firstValueFrom(
      this.#http.post<BoardingDocsResponse>(`${this.#baseUrl}/boarding-docs`, { departureAirport, ticketNumbers }),
    );
  }
}
