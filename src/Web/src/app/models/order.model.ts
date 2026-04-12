import { CabinCode } from './flight.model';

export type PassengerType = 'ADT' | 'CHD';
export type OrderStatus = 'Confirmed' | 'Changed' | 'Cancelled';
export type BookingType = 'Revenue' | 'Reward';

export interface TravelDocument {
  type: 'PASSPORT' | 'ID_CARD';
  number: string;
  issuingCountry: string;
  expiryDate: string;
  nationality: string;
}

export interface PassengerContacts {
  email: string;
  phone: string;
}

export interface Passenger {
  passengerId: string;
  type: PassengerType;
  givenName: string;
  surname: string;
  dateOfBirth: string;
  gender: 'Male' | 'Female' | 'Other' | '';
  loyaltyNumber: string | null;
  contacts: PassengerContacts | null;
  docs: TravelDocument[];
}

export interface FlightSegment {
  segmentId: string;
  flightNumber: string;
  origin: string;
  destination: string;
  departureDateTime: string;
  arrivalDateTime: string;
  aircraftType: string;
  operatingCarrier: string;
  marketingCarrier: string;
  cabinCode: CabinCode;
  bookingClass: string;
}

export interface SeatAssignment {
  passengerId: string;
  seatNumber: string;
}

export interface ETicket {
  passengerId: string;
  eTicketNumber: string;
}

export interface OrderItem {
  orderItemId: string;
  type: 'Flight' | 'Seat' | 'Bag';
  segmentRef: string;
  passengerRefs: string[];
  fareFamily?: string;
  fareBasisCode?: string;
  unitPrice: number;
  taxes: number;
  totalPrice: number;
  isRefundable?: boolean;
  isChangeable?: boolean;
  paymentReference: string;
  eTickets?: ETicket[];
  seatAssignments?: SeatAssignment[];
  seatNumber?: string;
  seatPosition?: string;
  additionalBags?: number;
  freeBagsIncluded?: number;
}

export interface Payment {
  paymentReference: string;
  description: string;
  method: string;
  cardLast4: string;
  cardType: string;
  cardholderName?: string;
  maskedCardNumber?: string;
  authorisedAmount: number;
  settledAmount: number;
  currency: string;
  status: 'Settled' | 'Authorised' | 'Refunded' | 'Voided';
  authorisedAt: string;
  settledAt: string;
}

export interface PointsRedemption {
  redemptionReference: string;
  loyaltyNumber: string;
  pointsRedeemed: number;
  status: 'Authorised' | 'Settled' | 'Reversed';
  authorisedAt: string;
  settledAt: string;
}

export interface Order {
  orderId: string;
  bookingReference: string;
  orderStatus: OrderStatus;
  bookingType: BookingType;
  channelCode: string;
  currencyCode: string;
  totalAmount: number;
  totalPointsAmount?: number;
  createdAt: string;
  passengers: Passenger[];
  flightSegments: FlightSegment[];
  orderItems: OrderItem[];
  payments: Payment[];
  pointsRedemption?: PointsRedemption;
}

export interface BasketFlightOffer {
  basketItemId: string;
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
  passengerRefs: string[];
  unitPrice: number;
  taxes: number;
  totalPrice: number;
  isRefundable: boolean;
  isChangeable: boolean;
  currency: string;
  pointsPrice?: number;
  pointsTaxes?: number;
}

export interface BasketSeatSelection {
  passengerId: string;
  segmentId: string;
  basketItemRef: string;
  seatOfferId: string;
  seatPosition: string;
  price: number;
  currency: string;
}

export interface BasketBagSelection {
  passengerId: string;
  segmentId: string;
  basketItemRef: string;
  bagOfferId: string;
  additionalBags: number;
  price: number;
  currency: string;
}

export interface BasketSsrSelection {
  ssrCode: string;
  passengerRef: string;
  segmentRef: string;
}

export interface Basket {
  basketId: string;
  bookingType: BookingType;
  flightOffers: BasketFlightOffer[];
  passengers: Passenger[];
  seatSelections: BasketSeatSelection[];
  bagSelections: BasketBagSelection[];
  ssrSelections: BasketSsrSelection[];
  totalFareAmount: number;
  totalPointsAmount: number;
  totalTaxesAmount: number;
  totalSeatAmount: number;
  totalBagAmount: number;
  totalAmount: number;
  currency: string;
  ticketingTimeLimit: string;
  loyaltyNumber?: string;
}

// ─── Basket summary models ────────────────────────────────────────────────────

export interface SummaryTaxLine {
  code: string;
  amount: number;
  description: string | null;
}

export interface SummaryFlight {
  offerId: string;
  sessionId: string | null;
  inventoryId: string | null;
  flightNumber: string;
  origin: string;
  destination: string;
  departureDate: string;
  departureTime: string;
  arrivalTime: string;
  cabinCode: string;
  fareFamily: string;
  validated: boolean;
  baseFareAmount: number;
  taxAmount: number;
  totalAmount: number;
  taxLines: SummaryTaxLine[] | null;
}

export interface BasketSummary {
  basketId: string;
  status: string;
  currency: string;
  expiresAt: string | null;
  flights: SummaryFlight[];
  totalFareAmount: number;
  totalSeatAmount: number;
  totalBagAmount: number;
  totalPrice: number;
}

// ─── Online Check-In (OCI) response models ───────────────────────────────────

export interface OciSeatAssignment {
  passengerId: string;
  seatNumber: string;
}

export interface OciFlightSegment {
  segmentRef: string;
  inventoryId: string;
  flightNumber: string;
  origin: string;
  destination: string;
  departureDateTime: string;
  arrivalDateTime: string;
  cabinCode: string;
  aircraftType: string;
  seatAssignments: OciSeatAssignment[];
}

export interface OciPassenger {
  passengerId: string;
  ticketNumber: string;
  type: string;
  givenName: string;
  surname: string;
}

export interface OciOrder {
  bookingReference: string;
  checkInEligible?: boolean;
  orderStatus: string;
  currencyCode: string;
  passengers: OciPassenger[];
  flightSegments?: OciFlightSegment[];
}

export interface BoardingPass {
  bookingReference: string;
  passengerId: string;
  givenName: string;
  surname: string;
  flightNumber: string;
  origin: string;
  destination: string;
  departureDateTime: string;
  seatNumber: string;
  cabinCode: CabinCode;
  eTicketNumber: string;
  sequenceNumber: string;
  bcbpBarcode: string;
  gate: string;
  boardingTime: string;
}
