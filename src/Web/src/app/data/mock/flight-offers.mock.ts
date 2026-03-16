import { FlightOffer } from '../../models/flight.model';
import { FlightStatus } from '../../models/flight.model';

// Apex Air IATA code: AX | Hub: LHR
// Flights are dated ~2 months from now for realistic mock data

function futureDate(daysFromNow: number, timeUtc: string): string {
  const d = new Date();
  d.setDate(d.getDate() + daysFromNow);
  return d.toISOString().split('T')[0] + 'T' + timeUtc + 'Z';
}

export function getMockFlightOffers(
  origin: string,
  destination: string,
  departDate: string,
  adults: number,
  children: number
): FlightOffer[] {
  const paxCount = adults + (children || 0);

  const route = [origin.toUpperCase(), destination.toUpperCase()].join('-');

  const ROUTES: Record<string, Omit<FlightOffer, 'offerId' | 'inventoryId'>[]> = {
    'LHR-JFK': [
      {
        flightNumber: 'AX001',
        origin: 'LHR', destination: 'JFK',
        departureDateTime: departDate + 'T08:00:00Z',
        arrivalDateTime: departDate + 'T13:10:00Z',
        aircraftType: 'A351',
        cabinCode: 'J', cabinName: 'Business Class',
        fareFamily: 'Business Flex', fareBasisCode: 'JFLEXGB', bookingClass: 'J',
        isRefundable: true, isChangeable: true,
        unitPrice: 1850.00, taxes: 312.50, totalPrice: 2162.50,
        currency: 'GBP', seatsAvailable: 8
      },
      {
        flightNumber: 'AX001',
        origin: 'LHR', destination: 'JFK',
        departureDateTime: departDate + 'T08:00:00Z',
        arrivalDateTime: departDate + 'T13:10:00Z',
        aircraftType: 'A351',
        cabinCode: 'W', cabinName: 'Premium Economy',
        fareFamily: 'Premium Economy', fareBasisCode: 'WFLEXGB', bookingClass: 'W',
        isRefundable: false, isChangeable: true,
        unitPrice: 780.00, taxes: 145.00, totalPrice: 925.00,
        currency: 'GBP', seatsAvailable: 14
      },
      {
        flightNumber: 'AX001',
        origin: 'LHR', destination: 'JFK',
        departureDateTime: departDate + 'T08:00:00Z',
        arrivalDateTime: departDate + 'T13:10:00Z',
        aircraftType: 'A351',
        cabinCode: 'Y', cabinName: 'Economy',
        fareFamily: 'Economy Light', fareBasisCode: 'YLOWGB', bookingClass: 'V',
        isRefundable: false, isChangeable: false,
        unitPrice: 299.00, taxes: 87.50, totalPrice: 386.50,
        currency: 'GBP', seatsAvailable: 42
      },
      {
        flightNumber: 'AX001',
        origin: 'LHR', destination: 'JFK',
        departureDateTime: departDate + 'T08:00:00Z',
        arrivalDateTime: departDate + 'T13:10:00Z',
        aircraftType: 'A351',
        cabinCode: 'Y', cabinName: 'Economy',
        fareFamily: 'Economy Flex', fareBasisCode: 'YFLEXGB', bookingClass: 'Y',
        isRefundable: true, isChangeable: true,
        unitPrice: 449.00, taxes: 87.50, totalPrice: 536.50,
        currency: 'GBP', seatsAvailable: 18
      }
    ],
    'JFK-LHR': [
      {
        flightNumber: 'AX002',
        origin: 'JFK', destination: 'LHR',
        departureDateTime: departDate + 'T13:00:00Z',
        arrivalDateTime: addDays(departDate, 1) + 'T01:15:00Z',
        aircraftType: 'A351',
        cabinCode: 'J', cabinName: 'Business Class',
        fareFamily: 'Business Flex', fareBasisCode: 'JFLEXUS', bookingClass: 'J',
        isRefundable: true, isChangeable: true,
        unitPrice: 1950.00, taxes: 318.50, totalPrice: 2268.50,
        currency: 'GBP', seatsAvailable: 6
      },
      {
        flightNumber: 'AX002',
        origin: 'JFK', destination: 'LHR',
        departureDateTime: departDate + 'T13:00:00Z',
        arrivalDateTime: addDays(departDate, 1) + 'T01:15:00Z',
        aircraftType: 'A351',
        cabinCode: 'Y', cabinName: 'Economy',
        fareFamily: 'Economy Light', fareBasisCode: 'YLOWUS', bookingClass: 'V',
        isRefundable: false, isChangeable: false,
        unitPrice: 319.00, taxes: 95.00, totalPrice: 414.00,
        currency: 'GBP', seatsAvailable: 38
      },
      {
        flightNumber: 'AX002',
        origin: 'JFK', destination: 'LHR',
        departureDateTime: departDate + 'T13:00:00Z',
        arrivalDateTime: addDays(departDate, 1) + 'T01:15:00Z',
        aircraftType: 'A351',
        cabinCode: 'Y', cabinName: 'Economy',
        fareFamily: 'Economy Flex', fareBasisCode: 'YFLEXUS', bookingClass: 'Y',
        isRefundable: true, isChangeable: true,
        unitPrice: 469.00, taxes: 95.00, totalPrice: 564.00,
        currency: 'GBP', seatsAvailable: 22
      }
    ],
    'LHR-SIN': [
      {
        flightNumber: 'AX301',
        origin: 'LHR', destination: 'SIN',
        departureDateTime: departDate + 'T21:30:00Z',
        arrivalDateTime: addDays(departDate, 1) + 'T17:45:00Z',
        aircraftType: 'A351',
        cabinCode: 'J', cabinName: 'Business Class',
        fareFamily: 'Business Flex', fareBasisCode: 'JFLEXGB', bookingClass: 'J',
        isRefundable: true, isChangeable: true,
        unitPrice: 2450.00, taxes: 395.00, totalPrice: 2845.00,
        currency: 'GBP', seatsAvailable: 10
      },
      {
        flightNumber: 'AX301',
        origin: 'LHR', destination: 'SIN',
        departureDateTime: departDate + 'T21:30:00Z',
        arrivalDateTime: addDays(departDate, 1) + 'T17:45:00Z',
        aircraftType: 'A351',
        cabinCode: 'W', cabinName: 'Premium Economy',
        fareFamily: 'Premium Economy', fareBasisCode: 'WFLEXGB', bookingClass: 'W',
        isRefundable: false, isChangeable: true,
        unitPrice: 980.00, taxes: 168.00, totalPrice: 1148.00,
        currency: 'GBP', seatsAvailable: 20
      },
      {
        flightNumber: 'AX301',
        origin: 'LHR', destination: 'SIN',
        departureDateTime: departDate + 'T21:30:00Z',
        arrivalDateTime: addDays(departDate, 1) + 'T17:45:00Z',
        aircraftType: 'A351',
        cabinCode: 'Y', cabinName: 'Economy',
        fareFamily: 'Economy Light', fareBasisCode: 'YLOWGB', bookingClass: 'V',
        isRefundable: false, isChangeable: false,
        unitPrice: 489.00, taxes: 112.00, totalPrice: 601.00,
        currency: 'GBP', seatsAvailable: 55
      },
      {
        flightNumber: 'AX301',
        origin: 'LHR', destination: 'SIN',
        departureDateTime: departDate + 'T21:30:00Z',
        arrivalDateTime: addDays(departDate, 1) + 'T17:45:00Z',
        aircraftType: 'A351',
        cabinCode: 'Y', cabinName: 'Economy',
        fareFamily: 'Economy Flex', fareBasisCode: 'YFLEXGB', bookingClass: 'Y',
        isRefundable: true, isChangeable: true,
        unitPrice: 649.00, taxes: 112.00, totalPrice: 761.00,
        currency: 'GBP', seatsAvailable: 30
      }
    ],
    'LHR-DEL': [
      {
        flightNumber: 'AX411',
        origin: 'LHR', destination: 'DEL',
        departureDateTime: departDate + 'T20:30:00Z',
        arrivalDateTime: addDays(departDate, 1) + 'T09:00:00Z',
        aircraftType: 'B789',
        cabinCode: 'J', cabinName: 'Business Class',
        fareFamily: 'Business Flex', fareBasisCode: 'JFLEXGB', bookingClass: 'J',
        isRefundable: true, isChangeable: true,
        unitPrice: 2100.00, taxes: 345.00, totalPrice: 2445.00,
        currency: 'GBP', seatsAvailable: 12
      },
      {
        flightNumber: 'AX411',
        origin: 'LHR', destination: 'DEL',
        departureDateTime: departDate + 'T20:30:00Z',
        arrivalDateTime: addDays(departDate, 1) + 'T09:00:00Z',
        aircraftType: 'B789',
        cabinCode: 'Y', cabinName: 'Economy',
        fareFamily: 'Economy Light', fareBasisCode: 'YLOWGB', bookingClass: 'V',
        isRefundable: false, isChangeable: false,
        unitPrice: 379.00, taxes: 95.00, totalPrice: 474.00,
        currency: 'GBP', seatsAvailable: 60
      }
    ],
    'LHR-BGI': [
      {
        flightNumber: 'AX101',
        origin: 'LHR', destination: 'BGI',
        departureDateTime: departDate + 'T10:15:00Z',
        arrivalDateTime: departDate + 'T14:30:00Z',
        aircraftType: 'A339',
        cabinCode: 'J', cabinName: 'Business Class',
        fareFamily: 'Business Flex', fareBasisCode: 'JFLEXGB', bookingClass: 'J',
        isRefundable: true, isChangeable: true,
        unitPrice: 1650.00, taxes: 278.00, totalPrice: 1928.00,
        currency: 'GBP', seatsAvailable: 16
      },
      {
        flightNumber: 'AX101',
        origin: 'LHR', destination: 'BGI',
        departureDateTime: departDate + 'T10:15:00Z',
        arrivalDateTime: departDate + 'T14:30:00Z',
        aircraftType: 'A339',
        cabinCode: 'Y', cabinName: 'Economy',
        fareFamily: 'Economy Light', fareBasisCode: 'YLOWGB', bookingClass: 'V',
        isRefundable: false, isChangeable: false,
        unitPrice: 349.00, taxes: 78.00, totalPrice: 427.00,
        currency: 'GBP', seatsAvailable: 72
      }
    ]
  };

  // Also handle reverse routes generically
  const reverseRoute = [destination.toUpperCase(), origin.toUpperCase()].join('-');
  const offers = ROUTES[route] || ROUTES[reverseRoute] || generateGenericOffers(origin, destination, departDate);

  return offers.map((o, i) => ({
    ...o,
    offerId: `offer-${origin}-${destination}-${i + 1}-${Date.now()}`,
    inventoryId: `inv-${o.flightNumber}-${departDate.slice(0, 10)}-${o.cabinCode}`,
    totalPrice: o.totalPrice * paxCount
  }));
}

function addDays(dateStr: string, days: number): string {
  const d = new Date(dateStr + 'T00:00:00Z');
  d.setDate(d.getDate() + days);
  return d.toISOString().split('T')[0];
}

function generateGenericOffers(origin: string, destination: string, departDate: string): Omit<FlightOffer, 'offerId' | 'inventoryId'>[] {
  const flightNum = 'AX' + String(Math.floor(Math.random() * 900) + 100);
  return [
    {
      flightNumber: flightNum,
      origin, destination,
      departureDateTime: departDate + 'T09:00:00Z',
      arrivalDateTime: addDays(departDate, 1) + 'T05:00:00Z',
      aircraftType: 'B789',
      cabinCode: 'J', cabinName: 'Business Class',
      fareFamily: 'Business Flex', fareBasisCode: 'JFLEXGB', bookingClass: 'J',
      isRefundable: true, isChangeable: true,
      unitPrice: 1900.00, taxes: 320.00, totalPrice: 2220.00,
      currency: 'GBP', seatsAvailable: 10
    },
    {
      flightNumber: flightNum,
      origin, destination,
      departureDateTime: departDate + 'T09:00:00Z',
      arrivalDateTime: addDays(departDate, 1) + 'T05:00:00Z',
      aircraftType: 'B789',
      cabinCode: 'Y', cabinName: 'Economy',
      fareFamily: 'Economy Light', fareBasisCode: 'YLOWGB', bookingClass: 'V',
      isRefundable: false, isChangeable: false,
      unitPrice: 359.00, taxes: 98.00, totalPrice: 457.00,
      currency: 'GBP', seatsAvailable: 50
    },
    {
      flightNumber: flightNum,
      origin, destination,
      departureDateTime: departDate + 'T09:00:00Z',
      arrivalDateTime: addDays(departDate, 1) + 'T05:00:00Z',
      aircraftType: 'B789',
      cabinCode: 'Y', cabinName: 'Economy',
      fareFamily: 'Economy Flex', fareBasisCode: 'YFLEXGB', bookingClass: 'Y',
      isRefundable: true, isChangeable: true,
      unitPrice: 499.00, taxes: 98.00, totalPrice: 597.00,
      currency: 'GBP', seatsAvailable: 35
    }
  ];
}

export const MOCK_FLIGHT_STATUS: Record<string, FlightStatus> = {
  'AX001': {
    flightNumber: 'AX001',
    origin: 'LHR', destination: 'JFK',
    scheduledDepartureDateTime: '2026-05-15T08:00:00Z',
    scheduledArrivalDateTime: '2026-05-15T13:10:00Z',
    estimatedDepartureDateTime: '2026-05-15T08:00:00Z',
    estimatedArrivalDateTime: '2026-05-15T13:10:00Z',
    status: 'OnTime',
    gate: 'B45', terminal: 'T5',
    aircraftType: 'A351',
    delayMinutes: 0,
    statusMessage: 'Flight is on time'
  },
  'AX002': {
    flightNumber: 'AX002',
    origin: 'JFK', destination: 'LHR',
    scheduledDepartureDateTime: '2026-05-15T13:00:00Z',
    scheduledArrivalDateTime: '2026-05-16T01:15:00Z',
    estimatedDepartureDateTime: '2026-05-15T13:45:00Z',
    estimatedArrivalDateTime: '2026-05-16T02:00:00Z',
    status: 'Delayed',
    gate: '12A', terminal: 'T4',
    aircraftType: 'A351',
    delayMinutes: 45,
    statusMessage: 'Delayed by 45 minutes due to late inbound aircraft'
  },
  'AX301': {
    flightNumber: 'AX301',
    origin: 'LHR', destination: 'SIN',
    scheduledDepartureDateTime: '2026-05-15T21:30:00Z',
    scheduledArrivalDateTime: '2026-05-16T17:45:00Z',
    estimatedDepartureDateTime: '2026-05-15T21:30:00Z',
    estimatedArrivalDateTime: '2026-05-16T17:45:00Z',
    status: 'Boarding',
    gate: 'C22', terminal: 'T5',
    aircraftType: 'A351',
    delayMinutes: 0,
    statusMessage: 'Boarding now — Gate C22'
  },
  'AX411': {
    flightNumber: 'AX411',
    origin: 'LHR', destination: 'DEL',
    scheduledDepartureDateTime: '2026-05-15T20:30:00Z',
    scheduledArrivalDateTime: '2026-05-16T09:00:00Z',
    estimatedDepartureDateTime: null,
    estimatedArrivalDateTime: null,
    status: 'OnTime',
    gate: 'A18', terminal: 'T5',
    aircraftType: 'B789',
    delayMinutes: 0,
    statusMessage: 'Flight is on time'
  }
};
