import { LoyaltyCustomer } from '../../models/loyalty.model';

export const MOCK_LOYALTY_CUSTOMERS: Record<string, LoyaltyCustomer> = {
  'demo@apexair.com': {
    loyaltyNumber: 'AX9876543',
    givenName: 'Alex',
    surname: 'Taylor',
    email: 'demo@apexair.com',
    phone: '+447700900100',
    dateOfBirth: '1985-03-12',
    nationality: 'GBR',
    preferredLanguage: 'en',
    tier: 'Gold',
    pointsBalance: 42500,
    tierProgressPoints: 42500,
    memberSince: '2019-06-15',
    transactions: [
      {
        transactionId: 'TXN-001',
        type: 'Accrual',
        points: 8750,
        description: 'Flight LHR-JFK-LHR — Booking AB1234',
        referenceBooking: 'AB1234',
        transactionDate: '2026-03-01T10:32:00Z',
        runningBalance: 42500
      },
      {
        transactionId: 'TXN-002',
        type: 'Accrual',
        points: 4200,
        description: 'Flight LHR-SIN — Booking XY9900',
        referenceBooking: 'XY9900',
        transactionDate: '2026-02-10T14:00:00Z',
        runningBalance: 33750
      },
      {
        transactionId: 'TXN-003',
        type: 'Redemption',
        points: -5000,
        description: 'Points redemption — upgrade to Business Class',
        transactionDate: '2026-01-22T09:15:00Z',
        runningBalance: 29550
      },
      {
        transactionId: 'TXN-004',
        type: 'Accrual',
        points: 6300,
        description: 'Flight LHR-DEL-LHR — Booking MN4567',
        referenceBooking: 'MN4567',
        transactionDate: '2025-11-18T11:45:00Z',
        runningBalance: 34550
      },
      {
        transactionId: 'TXN-005',
        type: 'Accrual',
        points: 12000,
        description: 'Flight LHR-HKG-LHR — Booking PQ2233',
        referenceBooking: 'PQ2233',
        transactionDate: '2025-09-05T08:20:00Z',
        runningBalance: 28250
      },
      {
        transactionId: 'TXN-006',
        type: 'Adjustment',
        points: 2500,
        description: 'Customer service goodwill gesture — disruption on AX301',
        transactionDate: '2025-08-15T16:00:00Z',
        runningBalance: 16250
      },
      {
        transactionId: 'TXN-007',
        type: 'Accrual',
        points: 3800,
        description: 'Flight LHR-MIA — Booking LM7788',
        referenceBooking: 'LM7788',
        transactionDate: '2025-07-04T10:00:00Z',
        runningBalance: 13750
      },
      {
        transactionId: 'TXN-008',
        type: 'Accrual',
        points: 2450,
        description: 'Flight LHR-BGI — Booking KJ3344',
        referenceBooking: 'KJ3344',
        transactionDate: '2025-05-20T09:30:00Z',
        runningBalance: 9950
      },
      {
        transactionId: 'TXN-009',
        type: 'Expiry',
        points: -1500,
        description: 'Points expiry — activity threshold not met',
        transactionDate: '2025-03-01T00:00:00Z',
        runningBalance: 7500
      },
      {
        transactionId: 'TXN-010',
        type: 'Accrual',
        points: 9000,
        description: 'Welcome bonus — new member registration',
        transactionDate: '2019-06-15T10:00:00Z',
        runningBalance: 9000
      }
    ]
  },
  'silver@apexair.com': {
    loyaltyNumber: 'AX1122334',
    givenName: 'Sam',
    surname: 'Morgan',
    email: 'silver@apexair.com',
    phone: '+447711223344',
    dateOfBirth: '1990-11-08',
    nationality: 'GBR',
    preferredLanguage: 'en',
    tier: 'Silver',
    pointsBalance: 18200,
    tierProgressPoints: 18200,
    memberSince: '2022-03-10',
    transactions: [
      {
        transactionId: 'TXN-S001',
        type: 'Accrual',
        points: 3200,
        description: 'Flight LHR-SIN — Booking CD5678',
        referenceBooking: 'CD5678',
        transactionDate: '2026-03-05T14:15:00Z',
        runningBalance: 18200
      },
      {
        transactionId: 'TXN-S002',
        type: 'Accrual',
        points: 5000,
        description: 'Flight LHR-BOM-LHR — Booking GH1122',
        referenceBooking: 'GH1122',
        transactionDate: '2025-12-20T09:00:00Z',
        runningBalance: 15000
      },
      {
        transactionId: 'TXN-S003',
        type: 'Accrual',
        points: 2500,
        description: 'Welcome bonus — new member registration',
        transactionDate: '2022-03-10T00:00:00Z',
        runningBalance: 2500
      }
    ]
  }
};

// Demo passwords for mock auth
export const MOCK_PASSWORDS: Record<string, string> = {
  'demo@apexair.com': 'Password1',
  'silver@apexair.com': 'Password1'
};
