import { Component, OnInit, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { ManageBookingStateService, ManageBookingBagSelection } from '../../../services/manage-booking-state.service';
import { Order, FlightSegment } from '../../../models/order.model';
import { BagOffer, BagPolicy } from '../../../models/flight.model';

interface PassengerBagState {
  passengerId: string;
  passengerName: string;
  alreadyPurchased: number;
  selectedAdditional: number;
}

@Component({
  selector: 'app-manage-bags',
  standalone: true,
  imports: [CommonModule],
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
  segmentId = signal('');

  segment = signal<FlightSegment | null>(null);
  policy = signal<BagPolicy | null>(null);
  offers = signal<BagOffer[]>([]);
  offersLoading = signal(true);

  passengerBagStates = signal<PassengerBagState[]>([]);

  readonly currency = computed(() => this.order()?.currency ?? 'GBP');

  readonly totalBagCost = computed(() => {
    const states = this.passengerBagStates();
    const offersList = this.offers();
    let total = 0;
    for (const state of states) {
      if (state.selectedAdditional === 0) continue;
      total += this.computePrice(offersList, state.alreadyPurchased, state.selectedAdditional);
    }
    return total;
  });

  readonly hasAnySelection = computed(() =>
    this.passengerBagStates().some(s => s.selectedAdditional > 0)
  );

  constructor(
    private router: Router,
    private retailApi: RetailApiService,
    private manageBookingState: ManageBookingStateService
  ) {}

  ngOnInit(): void {
    const navState = (this.router.getCurrentNavigation()?.extras.state ?? history.state) as Record<string, string>;
    const gn = navState?.['givenName'] ?? '';
    const sn = navState?.['surname'] ?? '';
    const sid = navState?.['segmentId'] ?? '';

    if (!this.retailApi.hasActiveManageBookingSession() || !gn || !sn || !sid) {
      this.router.navigate(['/manage-booking']);
      return;
    }

    this.givenName.set(gn);
    this.surname.set(sn);
    this.segmentId.set(sid);
    this.loadOrder();
  }

  private loadOrder(): void {
    this.loading.set(true);
    this.retailApi.retrieveOrder().subscribe({
      next: (order) => {
        this.bookingRef.set(order.bookingReference);
        this.order.set(order);

        const seg = order.flightSegments.find(s => s.segmentId === this.segmentId());
        if (!seg) {
          this.errorMessage.set('Flight segment not found.');
          this.loading.set(false);
          return;
        }
        this.segment.set(seg);
        this.loading.set(false);
        this.initPassengerStates(order, seg);
        this.loadBagOffers(seg);
      },
      error: (err: { status?: number; message?: string }) => {
        if (err.status === 401) { this.router.navigate(['/manage-booking']); return; }
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking.');
        this.loading.set(false);
      }
    });
  }

  private initPassengerStates(order: Order, seg: FlightSegment): void {
    // Bag items store inventory IDs as segmentRef — map by position to segment UUIDs.
    const bagInventoryRefs = [...new Set(
      order.orderItems.filter(oi => oi.type === 'Bag').map(oi => oi.segmentRef)
    )];
    const bagRefToSegmentId = new Map<string, string>(
      bagInventoryRefs.map((ref, idx) => [ref, order.flightSegments[idx]?.segmentId ?? ''])
    );

    const states: PassengerBagState[] = order.passengers.map(pax => {
      const bagItem = order.orderItems.find(
        oi => oi.type === 'Bag' &&
              bagRefToSegmentId.get(oi.segmentRef) === seg.segmentId &&
              oi.passengerRefs.includes(pax.passengerId)
      );
      return {
        passengerId: pax.passengerId,
        passengerName: `${pax.givenName} ${pax.surname}`,
        alreadyPurchased: bagItem?.additionalBags ?? 0,
        selectedAdditional: 0
      };
    });
    this.passengerBagStates.set(states);
  }

  private loadBagOffers(seg: FlightSegment): void {
    this.offersLoading.set(true);
    this.retailApi.getBagOffers(seg.segmentId, seg.cabinCode as 'F' | 'J' | 'W' | 'Y').subscribe({
      next: (response) => {
        this.policy.set(response.policy);
        this.offers.set(response.additionalBagOffers);
        this.offersLoading.set(false);
      },
      error: () => {
        this.offersLoading.set(false);
      }
    });
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

  setAdditional(passengerId: string, count: number): void {
    this.passengerBagStates.update(states =>
      states.map(s => s.passengerId === passengerId ? { ...s, selectedAdditional: count } : s)
    );
  }

  isSelected(passengerId: string, count: number): boolean {
    return this.passengerBagStates().find(s => s.passengerId === passengerId)?.selectedAdditional === count;
  }

  optionLabel(alreadyPurchased: number, count: number): string {
    if (count === 0) return 'None';
    const price = this.computePrice(this.offers(), alreadyPurchased, count);
    return `+${count} bag${count > 1 ? 's' : ''} (${this.currency()} ${price.toFixed(0)})`;
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

  formatDateTime(dt: string): string {
    return new Date(dt).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit', timeZone: 'UTC'
    });
  }

  onContinue(): void {
    const offersList = this.offers();
    const segId = this.segmentId();
    const selections: ManageBookingBagSelection[] = [];

    for (const state of this.passengerBagStates()) {
      if (state.selectedAdditional === 0) continue;
      const price = this.computePrice(offersList, state.alreadyPurchased, state.selectedAdditional);
      const bagOfferId = this.getBagOfferId(offersList, state.alreadyPurchased, state.selectedAdditional);
      selections.push({
        passengerId: state.passengerId,
        segmentId: segId,
        bagOfferId,
        additionalBags: state.selectedAdditional,
        price,
        currency: this.currency()
      });
    }

    this.manageBookingState.setBagSelections(selections, segId);
    this.router.navigate(['/manage-booking/bags-payment'], {
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }

  onBack(): void {
    this.router.navigate(['/manage-booking/detail'], {
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }
}
