import { Seatmap, CabinSeatmap, SeatOffer, CabinCode, SeatPosition, AvailabilityStatus } from '../../models/flight.model';

// Generates a seatmap for the A351 aircraft
// Business (J): rows 1-8, 1-2-1 layout (A, D, G, K) - no charge
// Premium Economy (W): rows 10-18, 2-4-2 (A, B, D, E, F, G, J, K) - Window £70, Aisle £50, Middle £20
// Economy (Y): rows 22-54, 3-3-3 (A, B, C, D, E, F, G, H, K) - Window £70, Aisle £50, Middle £20

const SEAT_PRICES: Record<CabinCode, Record<SeatPosition, number>> = {
  F: { Window: 0, Aisle: 0, Middle: 0 },
  J: { Window: 0, Aisle: 0, Middle: 0 },    // Business - no ancillary charge
  W: { Window: 70, Aisle: 50, Middle: 20 },
  Y: { Window: 70, Aisle: 50, Middle: 20 }
};

// Some seats are pre-sold/held for realism
const SOLD_SEATS = new Set(['1A', '2D', '3G', '5K', '10A', '10B', '11D', '11E', '12G', '22A', '22D', '23B', '24K', '25C', '26F']);
const HELD_SEATS = new Set(['4A', '4D', '15A', '15B', '30A', '30D', '31B']);

function seatAvailability(seatNumber: string): AvailabilityStatus {
  if (SOLD_SEATS.has(seatNumber)) return 'sold';
  if (HELD_SEATS.has(seatNumber)) return 'held';
  return 'available';
}

function buildSeat(
  rowNumber: number,
  column: string,
  cabinCode: CabinCode,
  position: SeatPosition,
  attributes: string[] = []
): SeatOffer {
  const seatNumber = `${rowNumber}${column}`;
  const price = SEAT_PRICES[cabinCode][position];
  return {
    seatOfferId: `so-${seatNumber.toLowerCase()}-v1`,
    seatNumber,
    column,
    rowNumber,
    position,
    cabinCode,
    price,
    currency: 'GBP',
    availability: seatAvailability(seatNumber),
    attributes
  };
}

function buildBusinessCabin(): CabinSeatmap {
  // 1-2-1 layout: A | D G | K
  const seats: SeatOffer[] = [];
  for (let row = 1; row <= 8; row++) {
    const attrs = row <= 2 ? ['ExtraLegroom'] : [];
    seats.push(buildSeat(row, 'A', 'J', 'Window', attrs));
    seats.push(buildSeat(row, 'D', 'J', 'Aisle', attrs));
    seats.push(buildSeat(row, 'G', 'J', 'Aisle', attrs));
    seats.push(buildSeat(row, 'K', 'J', 'Window', attrs));
  }
  return {
    cabinCode: 'J', cabinName: 'Business Class',
    columns: ['A', 'D', 'G', 'K'],
    layout: '1-2-1',
    startRow: 1, endRow: 8,
    seats
  };
}

function buildPremiumEconomyCabin(): CabinSeatmap {
  // 2-4-2 layout: A B | D E F G | J K
  const positionMap: Record<string, SeatPosition> = {
    A: 'Window', B: 'Aisle',
    D: 'Aisle', E: 'Middle', F: 'Middle', G: 'Aisle',
    J: 'Aisle', K: 'Window'
  };
  const seats: SeatOffer[] = [];
  for (let row = 10; row <= 18; row++) {
    const attrs = row === 10 ? ['ExtraLegroom'] : [];
    for (const col of ['A', 'B', 'D', 'E', 'F', 'G', 'J', 'K']) {
      seats.push(buildSeat(row, col, 'W', positionMap[col], attrs));
    }
  }
  return {
    cabinCode: 'W', cabinName: 'Premium Economy',
    columns: ['A', 'B', 'D', 'E', 'F', 'G', 'J', 'K'],
    layout: '2-4-2',
    startRow: 10, endRow: 18,
    seats
  };
}

function buildEconomyCabin(): CabinSeatmap {
  // 3-3-3 layout: A B C | D E F | G H K
  const positionMap: Record<string, SeatPosition> = {
    A: 'Window', B: 'Middle', C: 'Aisle',
    D: 'Aisle', E: 'Middle', F: 'Aisle',
    G: 'Aisle', H: 'Middle', K: 'Window'
  };
  const seats: SeatOffer[] = [];
  // Only generate rows 22-36 for mock (representative subset)
  for (let row = 22; row <= 36; row++) {
    const attrs = row === 22 ? ['ExtraLegroom'] : [];
    for (const col of ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'K']) {
      seats.push(buildSeat(row, col, 'Y', positionMap[col], attrs));
    }
  }
  return {
    cabinCode: 'Y', cabinName: 'Economy',
    columns: ['A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'K'],
    layout: '3-3-3',
    startRow: 22, endRow: 54,
    seats
  };
}

export function getMockSeatmap(flightId: string, flightNumber: string, cabinCode?: CabinCode): Seatmap {
  const allCabins = [buildBusinessCabin(), buildPremiumEconomyCabin(), buildEconomyCabin()];
  const cabins = cabinCode ? allCabins.filter(c => c.cabinCode === cabinCode) : allCabins;
  return {
    flightId,
    flightNumber,
    aircraftType: 'A351',
    cabins
  };
}
