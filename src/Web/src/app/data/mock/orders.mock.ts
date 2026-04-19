import { Order } from '../../models/order.model';

export const MOCK_ORDER_AB1234: Order = {
  orderId: 'ord-ab1234-0001',
  bookingReference: 'AB1234',
  orderStatus: 'Confirmed',
  bookingType: 'Revenue',
  channelCode: 'WEB',
  currency: 'GBP',
  fareTotal: 4431.00,
  seatTotal: 0.00,
  bagTotal: 0.00,
  productTotal: 0.00,
  totalAmount: 1749.00,
  createdAt: '2026-03-01T10:32:00Z',
  passengers: [
    {
      passengerId: 'PAX-1',
      type: 'ADT',
      givenName: 'Alex',
      surname: 'Taylor',
      dob:'1985-03-12',
      gender: 'Male',
      loyaltyNumber: 'AX9876543',
      contacts: { email: 'alex.taylor@example.com', phone: '+447700900100' },
      docs: [{
        type: 'PASSPORT',
        number: 'PA1234567',
        issuingCountry: 'GBR',
        expiryDate: '2030-01-01',
        nationality: 'GBR'
      }]
    },
    {
      passengerId: 'PAX-2',
      type: 'ADT',
      givenName: 'Jordan',
      surname: 'Taylor',
      dob:'1987-07-22',
      gender: 'Female',
      loyaltyNumber: null,
      contacts: null,
      docs: [{
        type: 'PASSPORT',
        number: 'PA7654321',
        issuingCountry: 'GBR',
        expiryDate: '2028-06-30',
        nationality: 'GBR'
      }]
    }
  ],
  flightSegments: [
    {
      segmentId: 'SEG-1',
      flightNumber: 'AX001',
      origin: 'LHR', destination: 'JFK',
      departureDateTime: '2026-08-15T08:00:00Z',
      arrivalDateTime: '2026-08-15T13:10:00Z',
      aircraftType: 'A351',
      operatingCarrier: 'AX', marketingCarrier: 'AX',
      cabinCode: 'J', bookingClass: 'J'
    },
    {
      segmentId: 'SEG-2',
      flightNumber: 'AX002',
      origin: 'JFK', destination: 'LHR',
      departureDateTime: '2026-08-25T13:00:00Z',
      arrivalDateTime: '2026-08-26T01:15:00Z',
      aircraftType: 'A351',
      operatingCarrier: 'AX', marketingCarrier: 'AX',
      cabinCode: 'J', bookingClass: 'J'
    }
  ],
  orderItems: [
    {
      orderItemId: 'OI-1',
      type: 'Flight',
      segmentRef: 'SEG-1',
      passengerRefs: ['PAX-1', 'PAX-2'],
      fareFamily: 'Business Flex',
      fareBasisCode: 'JFLEXGB',
      unitPrice: 1850.00, taxes: 312.50, totalPrice: 2162.50,
      isRefundable: true, isChangeable: true,
      paymentReference: 'AXPAY-0001',
      eTickets: [
        { passengerId: 'PAX-1', eTicketNumber: '932-1234567890' },
        { passengerId: 'PAX-2', eTicketNumber: '932-1234567891' }
      ]
    },
    {
      orderItemId: 'OI-2',
      type: 'Flight',
      segmentRef: 'SEG-2',
      passengerRefs: ['PAX-1', 'PAX-2'],
      fareFamily: 'Business Flex',
      fareBasisCode: 'JFLEXGB',
      unitPrice: 1950.00, taxes: 318.50, totalPrice: 2268.50,
      isRefundable: true, isChangeable: true,
      paymentReference: 'AXPAY-0001',
      eTickets: [
        { passengerId: 'PAX-1', eTicketNumber: '932-1234567892' },
        { passengerId: 'PAX-2', eTicketNumber: '932-1234567893' }
      ]
    },
    {
      orderItemId: 'OI-3',
      type: 'Seat',
      segmentRef: 'SEG-1',
      passengerRefs: ['PAX-1'],
      seatNumber: '2A',
      unitPrice: 0, taxes: 0, totalPrice: 0,
      paymentReference: 'AXPAY-0001'
    },
    {
      orderItemId: 'OI-4',
      type: 'Seat',
      segmentRef: 'SEG-1',
      passengerRefs: ['PAX-2'],
      seatNumber: '2K',
      unitPrice: 0, taxes: 0, totalPrice: 0,
      paymentReference: 'AXPAY-0001'
    },
    {
      orderItemId: 'OI-5',
      type: 'Seat',
      segmentRef: 'SEG-2',
      passengerRefs: ['PAX-1'],
      seatNumber: '3A',
      unitPrice: 0, taxes: 0, totalPrice: 0,
      paymentReference: 'AXPAY-0001'
    },
    {
      orderItemId: 'OI-6',
      type: 'Seat',
      segmentRef: 'SEG-2',
      passengerRefs: ['PAX-2'],
      seatNumber: '3K',
      unitPrice: 0, taxes: 0, totalPrice: 0,
      paymentReference: 'AXPAY-0001'
    }
  ],
  payments: [
    {
      paymentReference: 'AXPAY-0001',
      description: 'Fare — LHR-JFK-LHR, 2 PAX',
      method: 'CreditCard',
      cardLast4: '4242',
      cardType: 'Visa',
      authorisedAmount: 4431.00,
      settledAmount: 4431.00,
      currency: 'GBP',
      status: 'Settled',
      authorisedAt: '2026-03-01T10:31:00Z',
      settledAt: '2026-03-01T10:32:00Z'
    }
  ]
};

export const MOCK_ORDER_CD5678: Order = {
  orderId: 'ord-cd5678-0001',
  bookingReference: 'CD5678',
  orderStatus: 'Confirmed',
  bookingType: 'Revenue',
  channelCode: 'WEB',
  currency: 'GBP',
  fareTotal: 601.00,
  seatTotal: 70.00,
  bagTotal: 60.00,
  productTotal: 0.00,
  totalAmount: 857.00,
  createdAt: '2026-03-05T14:15:00Z',
  passengers: [
    {
      passengerId: 'PAX-1',
      type: 'ADT',
      givenName: 'Sam',
      surname: 'Morgan',
      dob:'1990-11-08',
      gender: 'Male',
      loyaltyNumber: 'AX1122334',
      contacts: { email: 'sam.morgan@example.com', phone: '+447711223344' },
      docs: [{
        type: 'PASSPORT',
        number: 'PB9876543',
        issuingCountry: 'GBR',
        expiryDate: '2031-05-15',
        nationality: 'GBR'
      }]
    }
  ],
  flightSegments: [
    {
      segmentId: 'SEG-1',
      flightNumber: 'AX301',
      origin: 'LHR', destination: 'SIN',
      departureDateTime: '2026-06-20T21:30:00Z',
      arrivalDateTime: '2026-06-21T17:45:00Z',
      aircraftType: 'A351',
      operatingCarrier: 'AX', marketingCarrier: 'AX',
      cabinCode: 'Y', bookingClass: 'V'
    }
  ],
  orderItems: [
    {
      orderItemId: 'OI-1',
      type: 'Flight',
      segmentRef: 'SEG-1',
      passengerRefs: ['PAX-1'],
      fareFamily: 'Economy Light',
      fareBasisCode: 'YLOWGB',
      unitPrice: 489.00, taxes: 112.00, totalPrice: 601.00,
      isRefundable: false, isChangeable: false,
      paymentReference: 'AXPAY-0002',
      eTickets: [{ passengerId: 'PAX-1', eTicketNumber: '932-9876543210' }]
    },
    {
      orderItemId: 'OI-2',
      type: 'Bag',
      segmentRef: 'SEG-1',
      passengerRefs: ['PAX-1'],
      additionalBags: 1,
      freeBagsIncluded: 1,
      unitPrice: 60.00, taxes: 0, totalPrice: 60.00,
      paymentReference: 'AXPAY-0003'
    },
    {
      orderItemId: 'OI-3',
      type: 'Seat',
      segmentRef: 'SEG-1',
      passengerRefs: ['PAX-1'],
      seatNumber: '22K',
      seatPosition: 'Window',
      unitPrice: 70.00, taxes: 0, totalPrice: 70.00,
      paymentReference: 'AXPAY-0004'
    }
  ],
  payments: [
    {
      paymentReference: 'AXPAY-0002',
      description: 'Fare — LHR-SIN, 1 PAX',
      method: 'CreditCard', cardLast4: '1337', cardType: 'Mastercard',
      authorisedAmount: 601.00, settledAmount: 601.00, currency: 'GBP',
      status: 'Settled', authorisedAt: '2026-03-05T14:14:00Z', settledAt: '2026-03-05T14:15:00Z'
    },
    {
      paymentReference: 'AXPAY-0003',
      description: 'Bag ancillary — SEG-1, PAX-1',
      method: 'CreditCard', cardLast4: '1337', cardType: 'Mastercard',
      authorisedAmount: 60.00, settledAmount: 60.00, currency: 'GBP',
      status: 'Settled', authorisedAt: '2026-03-05T14:14:30Z', settledAt: '2026-03-05T14:15:30Z'
    },
    {
      paymentReference: 'AXPAY-0004',
      description: 'Seat ancillary — SEG-1, PAX-1 seat 22K',
      method: 'CreditCard', cardLast4: '1337', cardType: 'Mastercard',
      authorisedAmount: 70.00, settledAmount: 70.00, currency: 'GBP',
      status: 'Settled', authorisedAt: '2026-03-05T14:14:45Z', settledAt: '2026-03-05T14:15:45Z'
    }
  ]
};

export const MOCK_ORDER_EF9012: Order = {
  orderId: 'ord-ef9012-0001',
  bookingReference: 'EF9012',
  orderStatus: 'Confirmed',
  bookingType: 'Revenue',
  channelCode: 'WEB',
  currency: 'GBP',
  fareTotal: 489.00,
  seatTotal: 0.00,
  bagTotal: 0.00,
  productTotal: 0.00,
  totalAmount: 489.00,
  createdAt: '2026-03-10T09:00:00Z',
  passengers: [
    {
      passengerId: 'PAX-1',
      type: 'ADT',
      givenName: 'Jamie',
      surname: 'Patel',
      dob:'1995-04-20',
      gender: 'Other',
      loyaltyNumber: null,
      contacts: { email: 'jamie.patel@example.com', phone: '+447788990011' },
      docs: []
    }
  ],
  flightSegments: [
    {
      segmentId: 'SEG-1',
      flightNumber: 'AX205',
      origin: 'LHR',
      destination: 'DXB',
      departureDateTime: '2026-09-05T14:00:00Z',
      arrivalDateTime: '2026-09-05T23:30:00Z',
      aircraftType: 'A351',
      operatingCarrier: 'AX',
      marketingCarrier: 'AX',
      cabinCode: 'Y',
      bookingClass: 'V'
    }
  ],
  orderItems: [
    {
      orderItemId: 'OI-1',
      type: 'Flight',
      segmentRef: 'SEG-1',
      passengerRefs: ['PAX-1'],
      fareFamily: 'Economy Light',
      fareBasisCode: 'YLOWGB',
      unitPrice: 379.00,
      taxes: 110.00,
      totalPrice: 489.00,
      isRefundable: false,
      isChangeable: false,
      paymentReference: 'AXPAY-0005',
      eTickets: [{ passengerId: 'PAX-1', eTicketNumber: '932-1111222333' }]
    }
  ],
  payments: [
    {
      paymentReference: 'AXPAY-0005',
      description: 'Fare — LHR-DXB, 1 PAX',
      method: 'CreditCard',
      cardLast4: '9876',
      cardType: 'Visa',
      authorisedAmount: 489.00,
      settledAmount: 489.00,
      currency: 'GBP',
      status: 'Settled',
      authorisedAt: '2026-03-10T08:59:00Z',
      settledAt: '2026-03-10T09:00:00Z'
    }
  ]
};

export const MOCK_ORDERS: Record<string, Order> = {
  'AB1234': MOCK_ORDER_AB1234,
  'CD5678': MOCK_ORDER_CD5678,
  'EF9012': MOCK_ORDER_EF9012
};
