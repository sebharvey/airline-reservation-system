import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { ManageBookingStateService, ManageBookingBagSelection } from '../../../services/manage-booking-state.service';
import { Order } from '../../../models/order.model';
import { BagOffer, BagPolicy } from '../../../models/flight.model';
import { LucideAngularModule } from 'lucide-angular';

interface SegmentBagData {
  segmentId: string;
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
  segmentId: string;
  alreadyPurchased: number;
  selectedAdditional: number;
}

@Component({
  selector: 'app-manage-bags',
  standalone: true,
  imports: [CommonModule, RouterLink, LucideAngularModule],
  templateUrl: './manage-bags.html',
  styleUrl: './manage-bags.css'
})
export class ManageBagsComponent implements OnInit {
  order = signal<Order | null>(null);
  loading = signal(true);
  errorMessage = signal('');

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  segmentBagData = signal<SegmentBagData[]>([]);
  passengerBagStates = signal<PassengerBagState[]>([]);

  readonly totalBagCost = computed(() => {
    const states = this.passengerBagStates();
    const segs = this.segmentBagData();
    let total = 0;
    for (const state of states) {
      if (state.selectedAdditional === 0) continue;
      const seg = segs.find(s => s.segmentId === state.segmentId);
      if (seg) {
        total += this.computePrice(seg.offers, state.alreadyPurchased, state.selectedAdditional);
      }
    }
    return total;
  });

  readonly currency = computed(() => this.order()?.currency ?? 'GBP');

  readonly hasAnySelection = computed(() =>
    this.passengerBagStates().some(s => s.selectedAdditional > 0)
  );

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private retailApi: RetailApiService,
    private manageBookingState: ManageBookingStateService
  ) {}

  ngOnInit(): void {
    const navState = (this.router.getCurrentNavigation()?.extras.state ?? history.state) as Record<string, string>;
    const gn = navState?.['givenName'] ?? '';
    const sn = navState?.['surname'] ?? '';

    this.route.queryParams.subscribe(params => {
      const ref = params['bookingRef'] ?? '';
      this.bookingRef.set(ref);
      this.givenName.set(gn);
      this.surname.set(sn);

      if (!ref || !gn || !sn) {
        this.router.navigate(['/manage-booking']);
        return;
      }
      this.loadOrder(ref, gn, sn);
    });
  }

  private loadOrder(ref: string, gn: string, sn: string): void {
    this.loading.set(true);
    this.retailApi.retrieveOrder({ bookingReference: ref, givenName: gn, surname: sn }).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
        this.initBagData(order);
      },
      error: (err: { message?: string }) => {
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking.');
        this.loading.set(false);
      }
    });
  }

  private initBagData(order: Order): void {
    const segData: SegmentBagData[] = order.flightSegments.map(seg => ({
      segmentId: seg.segmentId,
      flightNumber: seg.flightNumber,
      origin: seg.origin,
      destination: seg.destination,
      cabinCode: seg.cabinCode,
      policy: null,
      offers: [],
      loading: true,
      currency: order.currency
    }));
    this.segmentBagData.set(segData);

    const states: PassengerBagState[] = [];
    for (const pax of order.passengers) {
      for (const seg of order.flightSegments) {
        const bagItem = order.orderItems.find(
          oi => oi.type === 'Bag' &&
                oi.segmentRef === seg.segmentId &&
                oi.passengerRefs.includes(pax.passengerId)
        );
        states.push({
          key: `${pax.passengerId}__${seg.segmentId}`,
          passengerId: pax.passengerId,
          passengerName: `${pax.givenName} ${pax.surname}`,
          segmentId: seg.segmentId,
          alreadyPurchased: bagItem?.additionalBags ?? 0,
          selectedAdditional: 0
        });
      }
    }
    this.passengerBagStates.set(states);

    order.flightSegments.forEach((seg, idx) => {
      this.retailApi.getBagOffers(seg.segmentId, seg.cabinCode as 'F' | 'J' | 'W' | 'Y').subscribe({
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

  getPaxStatesForSegment(segmentId: string): PassengerBagState[] {
    return this.passengerBagStates().filter(s => s.segmentId === segmentId);
  }

  maxNewBags(alreadyPurchased: number): number {
    return Math.max(0, 3 - alreadyPurchased);
  }

  additionalOptions(alreadyPurchased: number): number[] {
    const max = this.maxNewBags(alreadyPurchased);
    return Array.from({ length: max + 1 }, (_, i) => i);
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
    switch (cabinCode) {
      case 'F': return 'First';
      case 'J': return 'Business';
      case 'W': return 'Premium Economy';
      case 'Y': return 'Economy';
      default: return cabinCode;
    }
  }

  onContinue(): void {
    const segs = this.segmentBagData();
    const selections: ManageBookingBagSelection[] = [];

    for (const state of this.passengerBagStates()) {
      if (state.selectedAdditional === 0) continue;
      const seg = segs.find(s => s.segmentId === state.segmentId);
      if (!seg) continue;
      const price = this.computePrice(seg.offers, state.alreadyPurchased, state.selectedAdditional);
      const bagOfferId = this.getBagOfferId(seg.offers, state.alreadyPurchased, state.selectedAdditional);
      selections.push({
        passengerId: state.passengerId,
        segmentId: state.segmentId,
        bagOfferId,
        additionalBags: state.selectedAdditional,
        price,
        currency: seg.currency
      });
    }

    this.manageBookingState.setBagSelections(selections);
    this.router.navigate(['/manage-booking/bags-payment'], {
      queryParams: { bookingRef: this.bookingRef() },
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }

  onBack(): void {
    this.router.navigate(['/manage-booking/detail'], {
      queryParams: { bookingRef: this.bookingRef() },
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }
}
