import { Component, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { CheckInStateService, CheckInBagSelection } from '../../../services/check-in-state.service';
import { OciOrder, OciFlightSegment } from '../../../models/order.model';
import { BagOffer, BagPolicy, CabinCode } from '../../../models/flight.model';

interface SegmentBagData {
  segmentRef: string;
  inventoryId: string;
  flightNumber: string;
  origin: string;
  destination: string;
  cabinCode: string;
  policy: BagPolicy | null;
  offers: BagOffer[];
  loading: boolean;
  currency: string;
}

interface PassengerBagState {
  key: string;
  passengerId: string;
  passengerName: string;
  segmentRef: string;
  alreadyPurchased: number;
  selectedAdditional: number;
}

@Component({
  selector: 'app-check-in-bags',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './check-in-bags.html',
  styleUrl: './check-in-bags.css'
})
export class CheckInBagsComponent implements OnInit {
  order = signal<OciOrder | null>(null);
  segmentBagData = signal<SegmentBagData[]>([]);
  passengerBagStates = signal<PassengerBagState[]>([]);

  readonly selectedPassengers = computed(() => {
    const o = this.order();
    const ids = this.checkInState.selectedPassengerIds();
    if (!o) return [];
    return o.passengers.filter(p => ids.includes(p.passengerId));
  });

  readonly totalBagCost = computed(() => {
    const states = this.passengerBagStates();
    const segs = this.segmentBagData();
    let total = 0;
    for (const state of states) {
      if (state.selectedAdditional === 0) continue;
      const seg = segs.find(s => s.segmentRef === state.segmentRef);
      if (seg) total += this.computePrice(seg.offers, state.alreadyPurchased, state.selectedAdditional);
    }
    return total;
  });

  readonly currency = computed(() => this.order()?.currencyCode ?? 'GBP');

  constructor(
    private router: Router,
    private retailApi: RetailApiService,
    private checkInState: CheckInStateService
  ) {}

  ngOnInit(): void {
    const order = this.checkInState.currentOrder();
    if (!order) {
      this.router.navigate(['/check-in']);
      return;
    }
    this.order.set(order);
    this.initBagData(order);
  }

  private initBagData(order: OciOrder): void {
    const paxIds = this.checkInState.selectedPassengerIds();
    const passengers = order.passengers.filter(p => paxIds.includes(p.passengerId));

    const segData: SegmentBagData[] = order.flightSegments.map(seg => ({
      segmentRef: seg.segmentRef,
      inventoryId: seg.inventoryId,
      flightNumber: seg.flightNumber,
      origin: seg.origin,
      destination: seg.destination,
      cabinCode: seg.cabinCode,
      policy: null,
      offers: [],
      loading: true,
      currency: order.currencyCode
    }));
    this.segmentBagData.set(segData);

    const states: PassengerBagState[] = [];
    for (const pax of passengers) {
      for (const seg of order.flightSegments) {
        states.push({
          key: `${pax.passengerId}__${seg.segmentRef}`,
          passengerId: pax.passengerId,
          passengerName: `${pax.givenName} ${pax.surname}`,
          segmentRef: seg.segmentRef,
          alreadyPurchased: 0,
          selectedAdditional: 0
        });
      }
    }
    this.passengerBagStates.set(states);

    order.flightSegments.forEach((seg, idx) => {
      this.retailApi.getBagOffers(seg.inventoryId, seg.cabinCode as CabinCode).subscribe({
        next: (response) => {
          this.segmentBagData.update(list => {
            const updated = [...list];
            updated[idx] = { ...updated[idx], policy: response.policy, offers: response.additionalBagOffers, loading: false };
            return updated;
          });
        },
        error: () => {
          this.segmentBagData.update(list => {
            const updated = [...list];
            updated[idx] = { ...updated[idx], loading: false };
            return updated;
          });
        }
      });
    });
  }

  getPaxStatesForSegment(segmentRef: string): PassengerBagState[] {
    return this.passengerBagStates().filter(s => s.segmentRef === segmentRef);
  }

  maxNewBags(alreadyPurchased: number): number {
    return Math.max(0, 3 - alreadyPurchased);
  }

  additionalOptions(alreadyPurchased: number): number[] {
    return Array.from({ length: this.maxNewBags(alreadyPurchased) + 1 }, (_, i) => i);
  }

  computePrice(offers: BagOffer[], alreadyPurchased: number, additional: number): number {
    if (additional === 0 || offers.length === 0) return 0;
    let total = 0;
    for (let i = 0; i < additional; i++) {
      const seq = alreadyPurchased + i + 1;
      const offer = offers.find(o => o.bagSequence === seq) ?? offers[offers.length - 1];
      if (offer) total += offer.price;
    }
    return total;
  }

  getBagOfferId(offers: BagOffer[], alreadyPurchased: number, additional: number): string {
    const seq = alreadyPurchased + additional;
    return (offers.find(o => o.bagSequence === seq) ?? offers[offers.length - 1])?.bagOfferId ?? '';
  }

  setAdditional(key: string, count: number): void {
    this.passengerBagStates.update(states =>
      states.map(s => s.key === key ? { ...s, selectedAdditional: count } : s)
    );
  }

  isSelected(key: string, count: number): boolean {
    return this.passengerBagStates().find(s => s.key === key)?.selectedAdditional === count;
  }

  optionLabel(offers: BagOffer[], alreadyPurchased: number, count: number, currency: string): string {
    if (count === 0) return 'None';
    const price = this.computePrice(offers, alreadyPurchased, count);
    return `+${count} bag${count > 1 ? 's' : ''} (${currency} ${price.toFixed(0)})`;
  }

  cabinLabel(cabinCode: string): string {
    const labels: Record<string, string> = { F: 'First', J: 'Business', W: 'Premium Economy', Y: 'Economy' };
    return labels[cabinCode] ?? cabinCode;
  }

  continueToPayment(): void {
    this.saveBagSelections();
    this.router.navigate(['/check-in/boarding-pass']);
  }

  skip(): void {
    this.checkInState.setBagSelections([]);
    this.router.navigate(['/check-in/seats']);
  }

  private saveBagSelections(): void {
    const segs = this.segmentBagData();
    const selections: CheckInBagSelection[] = [];
    for (const state of this.passengerBagStates()) {
      if (state.selectedAdditional === 0) continue;
      const seg = segs.find(s => s.segmentRef === state.segmentRef);
      if (!seg) continue;
      const price = this.computePrice(seg.offers, state.alreadyPurchased, state.selectedAdditional);
      const bagOfferId = this.getBagOfferId(seg.offers, state.alreadyPurchased, state.selectedAdditional);
      selections.push({
        passengerId: state.passengerId,
        segmentId: state.segmentRef,
        bagOfferId,
        additionalBags: state.selectedAdditional,
        price,
        currency: seg.currency
      });
    }
    this.checkInState.setBagSelections(selections);
  }
}
