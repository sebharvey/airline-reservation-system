export interface Airport {
  code: string;
  city: string;
  name: string;
  timezone: string;
}

export const AIRPORTS: Airport[] = [
  { code: 'LHR', city: 'London',          name: 'Heathrow',                         timezone: 'Europe/London' },
  { code: 'JFK', city: 'New York',         name: 'John F. Kennedy',                  timezone: 'America/New_York' },
  { code: 'LAX', city: 'Los Angeles',      name: 'Los Angeles Intl',                 timezone: 'America/Los_Angeles' },
  { code: 'MIA', city: 'Miami',            name: 'Miami Intl',                       timezone: 'America/New_York' },
  { code: 'SFO', city: 'San Francisco',    name: 'San Francisco Intl',               timezone: 'America/Los_Angeles' },
  { code: 'ORD', city: 'Chicago',          name: "O'Hare Intl",                      timezone: 'America/Chicago' },
  { code: 'BOS', city: 'Boston',           name: 'Logan Intl',                       timezone: 'America/New_York' },
  { code: 'BGI', city: 'Bridgetown',       name: 'Grantley Adams Intl',              timezone: 'America/Barbados' },
  { code: 'KIN', city: 'Kingston',         name: 'Norman Manley Intl',               timezone: 'America/Jamaica' },
  { code: 'NAS', city: 'Nassau',           name: 'Lynden Pindling Intl',             timezone: 'America/Nassau' },
  { code: 'HKG', city: 'Hong Kong',        name: 'Hong Kong Intl',                   timezone: 'Asia/Hong_Kong' },
  { code: 'NRT', city: 'Tokyo',            name: 'Narita Intl',                      timezone: 'Asia/Tokyo' },
  { code: 'PVG', city: 'Shanghai',         name: 'Pudong Intl',                      timezone: 'Asia/Shanghai' },
  { code: 'PEK', city: 'Beijing',          name: 'Capital Intl',                     timezone: 'Asia/Shanghai' },
  { code: 'SIN', city: 'Singapore',        name: 'Changi',                           timezone: 'Asia/Singapore' },
  { code: 'BOM', city: 'Mumbai',           name: 'Chhatrapati Shivaji Maharaj Intl', timezone: 'Asia/Kolkata' },
  { code: 'DEL', city: 'Delhi',            name: 'Indira Gandhi Intl',               timezone: 'Asia/Kolkata' },
  { code: 'BLR', city: 'Bangalore',        name: 'Kempegowda Intl',                  timezone: 'Asia/Kolkata' },
];
