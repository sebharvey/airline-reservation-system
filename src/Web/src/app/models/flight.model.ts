export type CabinCode = 'F' | 'J' | 'W' | 'Y';
export type SeatPosition = 'Window' | 'Aisle' | 'Middle';
export type AvailabilityStatus = 'available' | 'held' | 'sold';

/** One flight segment within a connecting itinerary, used for display purposes. */
export interface ConnectingLegInfo {
  flightNumber: string;
  origin: string;
  destination: string;
  departureDateTime: string;
  arrivalDateTime: string;
  aircraftType: string;
}

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
  pointsPrice?: number;
  pointsTaxes?: number;
  /**
   * Basket segments to submit when this offer is selected.
   * Direct flights have one entry; connecting itineraries have two (one per leg).
   */
  segments: { offerId: string; sessionId: string }[];
  /** True when this offer represents a two-leg connecting itinerary via LHR. */
  isConnecting?: boolean;
  /** Layover at LHR in minutes; only present when isConnecting is true. */
  connectionDurationMinutes?: number;
  /** Per-leg display detail for connecting itineraries. */
  connectingLegs?: ConnectingLegInfo[];
  /**
   * Per-leg FlightOffer objects used when persisting the basket state.
   * Each entry corresponds to one physical flight segment.
   * For direct flights this is undefined; for connecting it has two entries.
   */
  allLegs?: FlightOffer[];
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

export interface FlightSummary {
  flightNumber: string;
  origin: string;
  destination: string;
  departureTime: string;
  aircraftType: string;
}

export interface ScheduledFlightNumber {
  flightNumber: string;
  origin: string;
  destination: string;
  departureTime: string;
  arrivalTime: string;
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
