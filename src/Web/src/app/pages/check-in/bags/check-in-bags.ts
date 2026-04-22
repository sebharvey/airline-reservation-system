import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { CheckInStateService } from '../../../services/check-in-state.service';
import { RetailApiService } from '../../../services/retail-api.service';
import { OciFlightSegment, OciPassenger, BasketBagSelection } from '../../../models/order.model';
import { CheckInBagSelection } from '../../../services/check-in-state.service';
import { BagPolicyResponse, BagOffer, CabinCode } from '../../../models/flight.model';

interface FlightBagData {
  segment: OciFlightSegment;
  policyResponse: BagPolicyResponse | null;
  loading: boolean;
  error: string;
}

interface PassengerBagSelection {
  passengerId: string;
  passengerName: string;
  segmentRef: string;
  inventoryId: string;
  selectedBagOffer: BagOffer | null;
  additionalBags: number;
}

@Component({
  selector: 'app-check-in-bags',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './check-in-bags.html',
  styleUrl: './check-in-bags.css'
})
export class CheckInBagsComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly checkInState = inject(CheckInStateService);
  private readonly retailApi = inject(RetailApiService);

  flightBagData = signal<FlightBagData[]>([]);
  saving = signal(false);
  saveError = signal('');

  // Key: `${passengerId}__${segmentRef}` -> PassengerBagSelection
  selections = signal<Map<string, PassengerBagSelection>>(new Map());

  readonly passengers = computed((): OciPassenger[] =>
    this.checkInState.currentOrder()?.passengers ?? []
  );

  readonly segments = computed((): OciFlightSegment[] =>
    this.checkInState.currentOrder()?.flightSegments ?? []
  );

  readonly currency = computed(() =>
    this.checkInState.currentOrder()?.currency ?? 'GBP'
  );

  readonly totalBagCost = computed(() => {
    let total = 0;
    for (const sel of this.selections().values()) {
      if (sel.selectedBagOffer && sel.additionalBags > 0) {
        total += sel.selectedBagOffer.price * sel.additionalBags;
      }
    }
    return total;
  });

  ngOnInit(): void {
    if (!this.checkInState.currentOrder()) {
      this.router.navigate(['/check-in']);
      return;
    }
    this.loadBagPolicies();
    this.initSelections();
  }

  private loadBagPolicies(): void {
    const segments = this.segments();
    if (segments.length === 0) return;

    const entries: FlightBagData[] = segments.map(seg => ({
      segment: seg,
      policyResponse: null,
      loading: true,
      error: ''
    }));
    this.flightBagData.set(entries);

    segments.forEach((seg, idx) => {
      this.retailApi.getBagOffers(seg.inventoryId, seg.cabinCode as CabinCode).subscribe({
        next: (response) => {
          this.flightBagData.update(list => {
            const updated = [...list];
            updated[idx] = { ...updated[idx], policyResponse: response, loading: false };
            return updated;
          });
        },
        error: () => {
          this.flightBagData.update(list => {
            const updated = [...list];
            updated[idx] = { ...updated[idx], loading: false, error: 'Failed to load bag policy' };
            return updated;
          });
        }
      });
    });
  }

  private initSelections(): void {
    const map = new Map<string, PassengerBagSelection>();
    for (const pax of this.passengers()) {
      const name = `${pax.givenName} ${pax.surname}`.trim() || 'Passenger';
      for (const seg of this.segments()) {
        const key = this.selectionKey(pax.passengerId, seg.segmentRef);
        map.set(key, {
          passengerId: pax.passengerId,
          passengerName: name,
          segmentRef: seg.segmentRef,
          inventoryId: seg.inventoryId,
          selectedBagOffer: null,
          additionalBags: 0
        });
      }
    }
    this.selections.set(map);
  }

  selectionKey(passengerId: string, segmentRef: string): string {
    return `${passengerId}__${segmentRef}`;
  }

  getSelection(passengerId: string, segmentRef: string): PassengerBagSelection | undefined {
    return this.selections().get(this.selectionKey(passengerId, segmentRef));
  }

  isSelected(passengerId: string, segmentRef: string, count: number): boolean {
    return (this.getSelection(passengerId, segmentRef)?.additionalBags ?? 0) === count;
  }

  selectBagCount(passengerId: string, seg: OciFlightSegment, offers: BagOffer[], count: number): void {
    const key = this.selectionKey(passengerId, seg.segmentRef);
    const newMap = new Map(this.selections());
    const existing = newMap.get(key);
    if (!existing) return;
    const offer = count === 0 ? null : (offers.find(o => o.bagSequence === count) ?? offers[0] ?? null);
    newMap.set(key, { ...existing, selectedBagOffer: offer, additionalBags: count });
    this.selections.set(newMap);
  }

  bagOptions(): number[] {
    return [0, 1, 2, 3];
  }

  getBagOptionLabel(offers: BagOffer[], count: number): string {
    if (count === 0) return 'No extras';
    const offer = offers.find(o => o.bagSequence === count) ?? offers[0];
    if (!offer) return `+${count} bag${count > 1 ? 's' : ''}`;
    const price = offer.price * count;
    return `+${count} bag${count > 1 ? 's' : ''} · ${offer.currency} ${price.toFixed(2)}`;
  }

  onSkip(): void {
    this.checkInState.setBagSelections([]);
    const basketId = this.checkInState.basketId();
    if (basketId) {
      this.retailApi.updateBasketBags(basketId, []).subscribe();
    }
    this.router.navigate(['/check-in/hazmat']);
  }

  onContinue(): void {
    const stateSels: CheckInBagSelection[] = [];
    const basketSels: BasketBagSelection[] = [];

    for (const sel of this.selections().values()) {
      if (!sel.selectedBagOffer || sel.additionalBags === 0) continue;
      stateSels.push({
        passengerId: sel.passengerId,
        segmentId: sel.inventoryId,
        bagOfferId: sel.selectedBagOffer.bagOfferId,
        additionalBags: sel.additionalBags,
        price: sel.selectedBagOffer.price * sel.additionalBags,
        currency: sel.selectedBagOffer.currency
      });
      basketSels.push({
        passengerId: sel.passengerId,
        segmentId: sel.inventoryId,
        basketItemRef: sel.segmentRef,
        bagOfferId: sel.selectedBagOffer.bagOfferId,
        additionalBags: sel.additionalBags,
        price: sel.selectedBagOffer.price * sel.additionalBags,
        tax: (sel.selectedBagOffer.tax ?? 0) * sel.additionalBags,
        currency: sel.selectedBagOffer.currency
      });
    }

    this.checkInState.setBagSelections(stateSels);

    const basketId = this.checkInState.basketId();
    this.saving.set(true);
    this.saveError.set('');

    const proceed = () => {
      this.saving.set(false);
      this.router.navigate(['/check-in/hazmat']);
    };

    if (basketId && basketSels.length > 0) {
      this.retailApi.updateBasketBags(basketId, basketSels).subscribe({
        next: proceed,
        error: () => {
          this.saving.set(false);
          this.saveError.set('Failed to save bag selections. Please try again.');
        }
      });
    } else {
      proceed();
    }
  }
}
