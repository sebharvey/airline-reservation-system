import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';

interface NdcEndpoint {
  name: string;
  method: string;
  description: string;
  version: string;
}

const NDC_ENDPOINTS: NdcEndpoint[] = [
  {
    name: 'AirShopping',
    method: 'POST',
    description: 'Search for available flights and return priced offers for a given itinerary.',
    version: '21.3'
  },
  {
    name: 'OfferPrice',
    method: 'POST',
    description: 'Reprice and confirm availability of a selected offer before order creation.',
    version: '21.3'
  },
  {
    name: 'OrderCreate',
    method: 'POST',
    description: 'Create a new order from a priced offer, capturing passenger details and payment.',
    version: '21.3'
  },
  {
    name: 'OrderRetrieve',
    method: 'POST',
    description: 'Retrieve the full details of an existing order by order ID.',
    version: '21.3'
  },
  {
    name: 'OrderChange',
    method: 'POST',
    description: 'Modify an existing order — change flight, add services, or update passenger data.',
    version: '21.3'
  },
  {
    name: 'OrderCancel',
    method: 'POST',
    description: 'Cancel an order or individual order items and initiate any applicable refunds.',
    version: '21.3'
  },
  {
    name: 'SeatAvailability',
    method: 'POST',
    description: 'Return the seat map and availability for a given flight and cabin class.',
    version: '21.3'
  },
  {
    name: 'ServiceList',
    method: 'POST',
    description: 'Return the catalogue of ancillary services available for an offer or order.',
    version: '21.3'
  }
];

@Component({
  selector: 'app-ndc',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './ndc.html',
  styleUrl: './ndc.css'
})
export class NdcComponent {
  readonly endpoints = NDC_ENDPOINTS;
}
