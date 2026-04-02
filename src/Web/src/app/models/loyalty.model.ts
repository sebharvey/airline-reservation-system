export type LoyaltyTier = 'Blue' | 'Silver' | 'Gold' | 'Platinum';
export type TransactionType = 'Accrual' | 'Redemption' | 'Adjustment' | 'Expiry';
export type Gender = 'Male' | 'Female' | 'NonBinary' | 'Other' | 'PreferNotToSay';

export interface LoyaltyPreferences {
  marketingEnabled: boolean;
  analyticsEnabled: boolean;
  functionalEnabled: boolean;
  appNotificationsEnabled: boolean;
}

export interface LoyaltyTransaction {
  transactionId: string;
  type: TransactionType;
  points: number;
  description: string;
  referenceBooking?: string;
  transactionDate: string;
  runningBalance: number;
}

export interface TierInfo {
  tier: LoyaltyTier;
  label: string;
  color: string;
  pointsRequired: number;
  nextTier: LoyaltyTier | null;
  nextTierPointsRequired: number | null;
}

export interface LoyaltyCustomer {
  loyaltyNumber: string;
  givenName: string;
  surname: string;
  email: string;
  phone: string;
  dateOfBirth: string;
  gender: string;
  nationality: string;
  preferredLanguage: string;
  addressLine1: string;
  addressLine2: string;
  city: string;
  stateOrRegion: string;
  postalCode: string;
  countryCode: string;
  passportNumber: string;
  passportIssueDate: string;
  passportIssuer: string;
  passportExpiryDate: string;
  knownTravellerNumber: string;
  tier: LoyaltyTier;
  pointsBalance: number;
  tierProgressPoints: number;
  memberSince: string;
  transactions: LoyaltyTransaction[];
  preferences: LoyaltyPreferences | null;
}

export interface AuthSession {
  customer: LoyaltyCustomer;
  accessToken: string;
  refreshToken: string;
}

export interface CustomerOrderItem {
  customerOrderId: string;
  orderId: string;
  bookingReference: string;
  createdAt: string;
}

export const TIER_CONFIG: Record<LoyaltyTier, TierInfo> = {
  Blue: {
    tier: 'Blue',
    label: 'Blue',
    color: '#4a90d9',
    pointsRequired: 0,
    nextTier: 'Silver',
    nextTierPointsRequired: 10000
  },
  Silver: {
    tier: 'Silver',
    label: 'Silver',
    color: '#8e9eab',
    pointsRequired: 10000,
    nextTier: 'Gold',
    nextTierPointsRequired: 30000
  },
  Gold: {
    tier: 'Gold',
    label: 'Gold',
    color: '#f5a623',
    pointsRequired: 30000,
    nextTier: 'Platinum',
    nextTierPointsRequired: 75000
  },
  Platinum: {
    tier: 'Platinum',
    label: 'Platinum',
    color: '#9b59b6',
    pointsRequired: 75000,
    nextTier: null,
    nextTierPointsRequired: null
  }
};
