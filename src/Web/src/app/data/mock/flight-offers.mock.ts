import { FlightOffer } from '../../models/flight.model';

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

  // LHR-JFK: 10 flights across the day covering all 4 cabin classes (F, J, W, Y)
  // First Class (F) only available on A351 aircraft (A350-1000)
  const LHR_JFK_FLIGHTS: { flightNumber: string; dep: string; arr: string; aircraft: string }[] = [
    { flightNumber: 'AX001', dep: '07:00:00Z', arr: '12:10:00Z', aircraft: 'A351' },
    { flightNumber: 'AX003', dep: '09:15:00Z', arr: '14:25:00Z', aircraft: 'A351' },
    { flightNumber: 'AX005', dep: '10:30:00Z', arr: '15:40:00Z', aircraft: 'B789' },
    { flightNumber: 'AX007', dep: '11:45:00Z', arr: '16:55:00Z', aircraft: 'A351' },
    { flightNumber: 'AX009', dep: '13:00:00Z', arr: '18:10:00Z', aircraft: 'B789' },
    { flightNumber: 'AX011', dep: '14:30:00Z', arr: '19:40:00Z', aircraft: 'A351' },
    { flightNumber: 'AX013', dep: '16:00:00Z', arr: '21:10:00Z', aircraft: 'B789' },
    { flightNumber: 'AX015', dep: '17:15:00Z', arr: '22:25:00Z', aircraft: 'A351' },
    { flightNumber: 'AX017', dep: '19:00:00Z', arr: '00:10:00Z', aircraft: 'A351' },
    { flightNumber: 'AX019', dep: '21:30:00Z', arr: '02:40:00Z', aircraft: 'B789' },
  ];

  // JFK-LHR: 10 flights covering all 4 cabin classes
  const JFK_LHR_FLIGHTS: { flightNumber: string; dep: string; arr: string; arrOffset: number; aircraft: string }[] = [
    { flightNumber: 'AX002', dep: '07:00:00Z', arr: '19:15:00Z', arrOffset: 0, aircraft: 'A351' },
    { flightNumber: 'AX004', dep: '08:30:00Z', arr: '20:45:00Z', arrOffset: 0, aircraft: 'B789' },
    { flightNumber: 'AX006', dep: '10:00:00Z', arr: '22:15:00Z', arrOffset: 0, aircraft: 'A351' },
    { flightNumber: 'AX008', dep: '11:30:00Z', arr: '23:45:00Z', arrOffset: 0, aircraft: 'B789' },
    { flightNumber: 'AX010', dep: '13:00:00Z', arr: '01:15:00Z', arrOffset: 1, aircraft: 'A351' },
    { flightNumber: 'AX012', dep: '14:15:00Z', arr: '02:30:00Z', arrOffset: 1, aircraft: 'A351' },
    { flightNumber: 'AX014', dep: '15:30:00Z', arr: '03:45:00Z', arrOffset: 1, aircraft: 'B789' },
    { flightNumber: 'AX016', dep: '17:00:00Z', arr: '05:15:00Z', arrOffset: 1, aircraft: 'A351' },
    { flightNumber: 'AX018', dep: '19:30:00Z', arr: '07:45:00Z', arrOffset: 1, aircraft: 'B789' },
    { flightNumber: 'AX020', dep: '22:00:00Z', arr: '10:15:00Z', arrOffset: 1, aircraft: 'A351' },
  ];

  function makeLhrJfkOffers(f: typeof LHR_JFK_FLIGHTS[0]): Omit<FlightOffer, 'offerId' | 'inventoryId'>[] {
    const dep = departDate + 'T' + f.dep;
    const arrDate = f.arr < f.dep ? addDays(departDate, 1) : departDate;
    const arr = arrDate + 'T' + f.arr;
    const offers: Omit<FlightOffer, 'offerId' | 'inventoryId'>[] = [];

    // First Class — A351 only
    if (f.aircraft === 'A351') {
      offers.push({
        flightNumber: f.flightNumber,
        origin: 'LHR', destination: 'JFK',
        departureDateTime: dep, arrivalDateTime: arr,
        aircraftType: f.aircraft,
        cabinCode: 'F', cabinName: 'First Class',
        fareFamily: 'First', fareBasisCode: 'FFIRSTGB', bookingClass: 'F',
        isRefundable: true, isChangeable: true,
        unitPrice: 4500.00, taxes: 520.00, totalPrice: 5020.00,
        currency: 'GBP', seatsAvailable: 4
      });
    }

    // Business Class
    offers.push({
      flightNumber: f.flightNumber,
      origin: 'LHR', destination: 'JFK',
      departureDateTime: dep, arrivalDateTime: arr,
      aircraftType: f.aircraft,
      cabinCode: 'J', cabinName: 'Business Class',
      fareFamily: 'Business Flex', fareBasisCode: 'JFLEXGB', bookingClass: 'J',
      isRefundable: true, isChangeable: true,
      unitPrice: 1850.00, taxes: 312.50, totalPrice: 2162.50,
      currency: 'GBP', seatsAvailable: 8
    });

    // Premium Economy — A351 and B789
    offers.push({
      flightNumber: f.flightNumber,
      origin: 'LHR', destination: 'JFK',
      departureDateTime: dep, arrivalDateTime: arr,
      aircraftType: f.aircraft,
      cabinCode: 'W', cabinName: 'Premium Economy',
      fareFamily: 'Premium Economy', fareBasisCode: 'WFLEXGB', bookingClass: 'W',
      isRefundable: false, isChangeable: true,
      unitPrice: 780.00, taxes: 145.00, totalPrice: 925.00,
      currency: 'GBP', seatsAvailable: 14
    });

    // Economy Light
    offers.push({
      flightNumber: f.flightNumber,
      origin: 'LHR', destination: 'JFK',
      departureDateTime: dep, arrivalDateTime: arr,
      aircraftType: f.aircraft,
      cabinCode: 'Y', cabinName: 'Economy',
      fareFamily: 'Economy Light', fareBasisCode: 'YLOWGB', bookingClass: 'V',
      isRefundable: false, isChangeable: false,
      unitPrice: 299.00, taxes: 87.50, totalPrice: 386.50,
      currency: 'GBP', seatsAvailable: 42
    });

    // Economy Flex
    offers.push({
      flightNumber: f.flightNumber,
      origin: 'LHR', destination: 'JFK',
      departureDateTime: dep, arrivalDateTime: arr,
      aircraftType: f.aircraft,
      cabinCode: 'Y', cabinName: 'Economy',
      fareFamily: 'Economy Flex', fareBasisCode: 'YFLEXGB', bookingClass: 'Y',
      isRefundable: true, isChangeable: true,
      unitPrice: 449.00, taxes: 87.50, totalPrice: 536.50,
      currency: 'GBP', seatsAvailable: 18
    });

    return offers;
  }

  function makeJfkLhrOffers(f: typeof JFK_LHR_FLIGHTS[0]): Omit<FlightOffer, 'offerId' | 'inventoryId'>[] {
    const dep = departDate + 'T' + f.dep;
    const arr = (f.arrOffset > 0 ? addDays(departDate, f.arrOffset) : departDate) + 'T' + f.arr;
    const offers: Omit<FlightOffer, 'offerId' | 'inventoryId'>[] = [];

    // First Class — A351 only
    if (f.aircraft === 'A351') {
      offers.push({
        flightNumber: f.flightNumber,
        origin: 'JFK', destination: 'LHR',
        departureDateTime: dep, arrivalDateTime: arr,
        aircraftType: f.aircraft,
        cabinCode: 'F', cabinName: 'First Class',
        fareFamily: 'First', fareBasisCode: 'FFIRSTUS', bookingClass: 'F',
        isRefundable: true, isChangeable: true,
        unitPrice: 4750.00, taxes: 545.00, totalPrice: 5295.00,
        currency: 'GBP', seatsAvailable: 4
      });
    }

    // Business Class
    offers.push({
      flightNumber: f.flightNumber,
      origin: 'JFK', destination: 'LHR',
      departureDateTime: dep, arrivalDateTime: arr,
      aircraftType: f.aircraft,
      cabinCode: 'J', cabinName: 'Business Class',
      fareFamily: 'Business Flex', fareBasisCode: 'JFLEXUS', bookingClass: 'J',
      isRefundable: true, isChangeable: true,
      unitPrice: 1950.00, taxes: 318.50, totalPrice: 2268.50,
      currency: 'GBP', seatsAvailable: 6
    });

    // Premium Economy — A351 and B789
    offers.push({
      flightNumber: f.flightNumber,
      origin: 'JFK', destination: 'LHR',
      departureDateTime: dep, arrivalDateTime: arr,
      aircraftType: f.aircraft,
      cabinCode: 'W', cabinName: 'Premium Economy',
      fareFamily: 'Premium Economy', fareBasisCode: 'WFLEXUS', bookingClass: 'W',
      isRefundable: false, isChangeable: true,
      unitPrice: 820.00, taxes: 152.00, totalPrice: 972.00,
      currency: 'GBP', seatsAvailable: 12
    });

    // Economy Light
    offers.push({
      flightNumber: f.flightNumber,
      origin: 'JFK', destination: 'LHR',
      departureDateTime: dep, arrivalDateTime: arr,
      aircraftType: f.aircraft,
      cabinCode: 'Y', cabinName: 'Economy',
      fareFamily: 'Economy Light', fareBasisCode: 'YLOWUS', bookingClass: 'V',
      isRefundable: false, isChangeable: false,
      unitPrice: 319.00, taxes: 95.00, totalPrice: 414.00,
      currency: 'GBP', seatsAvailable: 38
    });

    // Economy Flex
    offers.push({
      flightNumber: f.flightNumber,
      origin: 'JFK', destination: 'LHR',
      departureDateTime: dep, arrivalDateTime: arr,
      aircraftType: f.aircraft,
      cabinCode: 'Y', cabinName: 'Economy',
      fareFamily: 'Economy Flex', fareBasisCode: 'YFLEXUS', bookingClass: 'Y',
      isRefundable: true, isChangeable: true,
      unitPrice: 469.00, taxes: 95.00, totalPrice: 564.00,
      currency: 'GBP', seatsAvailable: 22
    });

    return offers;
  }

  const ROUTES: Record<string, Omit<FlightOffer, 'offerId' | 'inventoryId'>[]> = {
    'LHR-JFK': LHR_JFK_FLIGHTS.flatMap(makeLhrJfkOffers),
    'JFK-LHR': JFK_LHR_FLIGHTS.flatMap(makeJfkLhrOffers),
    'LHR-SIN': [
      {
        flightNumber: 'AX301',
        origin: 'LHR', destination: 'SIN',
        departureDateTime: departDate + 'T21:30:00Z',
        arrivalDateTime: addDays(departDate, 1) + 'T17:45:00Z',
        aircraftType: 'A351',
        cabinCode: 'F', cabinName: 'First Class',
        fareFamily: 'First', fareBasisCode: 'FFIRSTGB', bookingClass: 'F',
        isRefundable: true, isChangeable: true,
        unitPrice: 5500.00, taxes: 620.00, totalPrice: 6120.00,
        currency: 'GBP', seatsAvailable: 4
      },
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
        cabinCode: 'W', cabinName: 'Premium Economy',
        fareFamily: 'Premium Economy', fareBasisCode: 'WFLEXGB', bookingClass: 'W',
        isRefundable: false, isChangeable: true,
        unitPrice: 850.00, taxes: 155.00, totalPrice: 1005.00,
        currency: 'GBP', seatsAvailable: 18
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
    totalPrice: o.totalPrice * paxCount,
    pointsPrice: Math.round(o.unitPrice * 10) * paxCount,
    pointsTaxes: o.taxes * paxCount
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
