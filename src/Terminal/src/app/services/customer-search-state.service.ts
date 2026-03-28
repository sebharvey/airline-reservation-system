import { Injectable, signal } from '@angular/core';
import { CustomerSummary } from './customer.service';

@Injectable({ providedIn: 'root' })
export class CustomerSearchStateService {
  query = signal('');
  results = signal<CustomerSummary[]>([]);
  loaded = signal(false);
}
