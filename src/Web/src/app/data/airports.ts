export interface Airport {
  code: string;
  city: string;
  name: string;
}

export const AIRPORTS: Airport[] = [
  { code: 'LHR', city: 'London',          name: 'Heathrow' },
  { code: 'JFK', city: 'New York',         name: 'John F. Kennedy' },
  { code: 'LAX', city: 'Los Angeles',      name: 'Los Angeles Intl' },
  { code: 'MIA', city: 'Miami',            name: 'Miami Intl' },
  { code: 'SFO', city: 'San Francisco',    name: 'San Francisco Intl' },
  { code: 'ORD', city: 'Chicago',          name: "O'Hare Intl" },
  { code: 'BOS', city: 'Boston',           name: 'Logan Intl' },
  { code: 'BGI', city: 'Bridgetown',       name: 'Grantley Adams Intl' },
  { code: 'KIN', city: 'Kingston',         name: 'Norman Manley Intl' },
  { code: 'NAS', city: 'Nassau',           name: 'Lynden Pindling Intl' },
  { code: 'HKG', city: 'Hong Kong',        name: 'Hong Kong Intl' },
  { code: 'NRT', city: 'Tokyo',            name: 'Narita Intl' },
  { code: 'PVG', city: 'Shanghai',         name: 'Pudong Intl' },
  { code: 'PEK', city: 'Beijing',          name: 'Capital Intl' },
  { code: 'SIN', city: 'Singapore',        name: 'Changi' },
  { code: 'BOM', city: 'Mumbai',           name: 'Chhatrapati Shivaji Maharaj Intl' },
  { code: 'DEL', city: 'Delhi',            name: 'Indira Gandhi Intl' },
  { code: 'BLR', city: 'Bangalore',        name: 'Kempegowda Intl' },
];
