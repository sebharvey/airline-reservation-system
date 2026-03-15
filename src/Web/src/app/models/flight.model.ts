export type CabinCode = 'F' | 'J' | 'W' | 'Y';
export type SeatPosition = 'Window' | 'Aisle' | 'Middle';
export type AvailabilityStatus = 'available' | 'held' | 'sold';

export interface FlightOffer {
  offerId: string;
  inventoryId: string;
  flightNumber: string;
  origin: string;
  destination: string;
  departureDateTime: string;
  arrivalDateTime: string;
  aircraftType: string;
  cabinCode: CabinCode;
  cabinName: string;
  fareFamily: string;
  fareBasisCode: string;
  bookingClass: string;
  isRefundable: boolean;
  isChangeable: boolean;
  unitPrice: number;
  taxes: number;
  totalPrice: number;
  currency: string;
  seatsAvailable: number;
}

export interface SeatOffer {
  seatOfferId: string;
  seatNumber: string;
  column: string;
  rowNumber: number;
  position: SeatPosition;
  cabinCode: CabinCode;
  price: number;
  currency: string;
  availability: AvailabilityStatus;
  attributes: string[];
}

export interface CabinSeatmap {
  cabinCode: CabinCode;
  cabinName: string;
  columns: string[];
  layout: string;
  startRow: number;
  endRow: number;
  seats: SeatOffer[];
}

export interface Seatmap {
  flightId: string;
  flightNumber: string;
  aircraftType: string;
  cabins: CabinSeatmap[];
}

export interface BagPolicy {
  cabinCode: CabinCode;
  freeBagsIncluded: number;
  maxWeightKgPerBag: number;
}

export interface BagOffer {
  bagOfferId: string;
  bagSequence: number;
  price: number;
  currency: string;
  label: string;
}

export interface BagPolicyResponse {
  policy: BagPolicy;
  additionalBagOffers: BagOffer[];
}

export interface FlightStatus {
  flightNumber: string;
  origin: string;
  destination: string;
  scheduledDepartureDateTime: string;
  scheduledArrivalDateTime: string;
  estimatedDepartureDateTime: string | null;
  estimatedArrivalDateTime: string | null;
  status: 'OnTime' | 'Delayed' | 'Boarding' | 'Departed' | 'Landed' | 'Cancelled';
  gate: string | null;
  terminal: string | null;
  aircraftType: string;
  delayMinutes: number;
  statusMessage: string;
}
