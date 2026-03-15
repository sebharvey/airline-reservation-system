import { BagPolicyResponse, CabinCode } from '../../models/flight.model';

export const MOCK_BAG_POLICIES: Record<CabinCode, BagPolicyResponse> = {
  F: {
    policy: { cabinCode: 'F', freeBagsIncluded: 2, maxWeightKgPerBag: 32 },
    additionalBagOffers: [
      { bagOfferId: 'bag-F-1', bagSequence: 1, price: 60.00, currency: 'GBP', label: '1st additional bag' },
      { bagOfferId: 'bag-F-2', bagSequence: 2, price: 80.00, currency: 'GBP', label: '2nd additional bag' },
      { bagOfferId: 'bag-F-3', bagSequence: 99, price: 100.00, currency: 'GBP', label: '3rd additional bag' }
    ]
  },
  J: {
    policy: { cabinCode: 'J', freeBagsIncluded: 2, maxWeightKgPerBag: 32 },
    additionalBagOffers: [
      { bagOfferId: 'bag-J-1', bagSequence: 1, price: 60.00, currency: 'GBP', label: '1st additional bag' },
      { bagOfferId: 'bag-J-2', bagSequence: 2, price: 80.00, currency: 'GBP', label: '2nd additional bag' },
      { bagOfferId: 'bag-J-3', bagSequence: 99, price: 100.00, currency: 'GBP', label: '3rd additional bag' }
    ]
  },
  W: {
    policy: { cabinCode: 'W', freeBagsIncluded: 2, maxWeightKgPerBag: 23 },
    additionalBagOffers: [
      { bagOfferId: 'bag-W-1', bagSequence: 1, price: 60.00, currency: 'GBP', label: '1st additional bag' },
      { bagOfferId: 'bag-W-2', bagSequence: 2, price: 80.00, currency: 'GBP', label: '2nd additional bag' },
      { bagOfferId: 'bag-W-3', bagSequence: 99, price: 100.00, currency: 'GBP', label: '3rd additional bag' }
    ]
  },
  Y: {
    policy: { cabinCode: 'Y', freeBagsIncluded: 1, maxWeightKgPerBag: 23 },
    additionalBagOffers: [
      { bagOfferId: 'bag-Y-1', bagSequence: 1, price: 60.00, currency: 'GBP', label: '1st additional bag' },
      { bagOfferId: 'bag-Y-2', bagSequence: 2, price: 80.00, currency: 'GBP', label: '2nd additional bag' },
      { bagOfferId: 'bag-Y-3', bagSequence: 99, price: 100.00, currency: 'GBP', label: '3rd additional bag' }
    ]
  }
};
