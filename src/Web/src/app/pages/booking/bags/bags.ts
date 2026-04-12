import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { BookingStateService } from '../../../services/booking-state.service';
import { RetailApiService } from '../../../services/retail-api.service';
import { BasketFlightOffer, BasketBagSelection } from '../../../models/order.model';
import { BagPolicyResponse, BagOffer } from '../../../models/flight.model';

interface FlightBagData {
  flightOffer: BasketFlightOffer;
  policyResponse: BagPolicyResponse | null;
  loading: boolean;
  error: string;
}

interface PassengerBagSelection {
  passengerId: string;
  passengerName: string;
  basketItemId: string;
  selectedBagOffer: BagOffer | null;
  additionalBags: number;
}

@Component({
  selector: 'app-bags',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './bags.html',
  styleUrl: './bags.css'
})
export class BagsComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly bookingState = inject(BookingStateService);
  private readonly retailApi = inject(RetailApiService);

  readonly basket = this.bookingState.basket;

  flightBagData = signal<FlightBagData[]>([]);
  saving = signal(false);
  saveError = signal('');

  // Map of `${passengerId}__${basketItemId}` -> PassengerBagSelection
  selections = signal<Map<string, PassengerBagSelection>>(new Map());

  readonly passengers = computed(() => this.basket()?.passengers ?? []);

  readonly totalBagCost = computed(() => {
    let total = 0;
    for (const sel of this.selections().values()) {
      if (sel.selectedBagOffer) {
        total += sel.selectedBagOffer.price * sel.additionalBags;
      }
    }
    return total;
  });

  readonly currency = computed(() => this.basket()?.currency ?? 'GBP');

  ngOnInit(): void {
    if (!this.basket()) {
      this.router.navigate(['/']);
      return;
    }
    this.loadBagPolicies();
    this.initSelections();
  }

  private loadBagPolicies(): void {
    const basket = this.basket();
    if (!basket) return;

    const entries: FlightBagData[] = basket.flightOffers.map(fo => ({
      flightOffer: fo,
      policyResponse: null,
      loading: true,
      error: ''
    }));
    this.flightBagData.set(entries);

    basket.flightOffers.forEach((fo, idx) => {
      this.retailApi.getBagOffers(fo.inventoryId, fo.cabinCode).subscribe({
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
    const basket = this.basket();
    if (!basket) return;

    const map = new Map<string, PassengerBagSelection>();
    for (const pax of basket.passengers) {
      const name = `${pax.givenName} ${pax.surname}`.trim() ||
        (pax.type === 'ADT' ? 'Adult' : 'Child');
      for (const fo of basket.flightOffers) {
        const key = `${pax.passengerId}__${fo.basketItemId}`;
        map.set(key, {
          passengerId: pax.passengerId,
          passengerName: name,
          basketItemId: fo.basketItemId,
          selectedBagOffer: null,
          additionalBags: 0
        });
      }
    }
    this.selections.set(map);
  }

  getSelectionKey(passengerId: string, basketItemId: string): string {
    return `${passengerId}__${basketItemId}`;
  }

  getSelection(passengerId: string, basketItemId: string): PassengerBagSelection | undefined {
    return this.selections().get(this.getSelectionKey(passengerId, basketItemId));
  }

  setBagCount(passengerId: string, basketItemId: string, offer: BagOffer | null, count: number): void {
    const key = this.getSelectionKey(passengerId, basketItemId);
    const newMap = new Map(this.selections());
    const existing = newMap.get(key);
    if (!existing) return;
    newMap.set(key, {
      ...existing,
      selectedBagOffer: count > 0 ? offer : null,
      additionalBags: count
    });
    this.selections.set(newMap);
  }

  selectBagCount(passengerId: string, basketItemId: string, offers: BagOffer[], count: number): void {
    // For count bags: find the offer matching that sequence or use the cheapest per-bag
    const offer = count === 0 ? null : (offers.find(o => o.bagSequence === count) ?? offers[0] ?? null);
    this.setBagCount(passengerId, basketItemId, offer, count);
  }

  getAdditionalBagOptions(offers: BagOffer[]): number[] {
    // 0 = no additional bags, plus up to 3 additional bags
    return [0, 1, 2, 3];
  }

  getBagOptionPrice(offers: BagOffer[], count: number): string {
    if (count === 0) return 'No additional bags';
    const offer = offers.find(o => o.bagSequence === count) ?? offers[0];
    if (!offer) return `${count} bag(s)`;
    return `${count} bag(s) · ${offer.currency} ${(offer.price * count).toFixed(2)}`;
  }

  isSelected(passengerId: string, basketItemId: string, count: number): boolean {
    const sel = this.getSelection(passengerId, basketItemId);
    return (sel?.additionalBags ?? 0) === count;
  }

  onSkip(): void {
    const basketId = this.basket()?.basketId;
    if (!basketId) return;
    this.saving.set(true);
    this.saveError.set('');
    this.retailApi.updateBasketBags(basketId, []).subscribe({
      next: () => {
        this.bookingState.setBagSelections([]);
        this.saving.set(false);
        this.router.navigate(['/booking/products']);
      },
      error: () => {
        this.saving.set(false);
        this.saveError.set('Failed to update bag selections. Please try again.');
      }
    });
  }

  onContinue(): void {
    const basketId = this.basket()?.basketId;
    if (!basketId) return;

    const selectionsList: BasketBagSelection[] = [];

    for (const sel of this.selections().values()) {
      if (sel.selectedBagOffer && sel.additionalBags > 0) {
        selectionsList.push({
          passengerId: sel.passengerId,
          segmentId: sel.basketItemId,
          basketItemRef: sel.basketItemId,
          bagOfferId: sel.selectedBagOffer.bagOfferId,
          additionalBags: sel.additionalBags,
          price: sel.selectedBagOffer.price * sel.additionalBags,
          currency: sel.selectedBagOffer.currency
        });
      }
    }

    this.saving.set(true);
    this.saveError.set('');
    this.retailApi.updateBasketBags(basketId, selectionsList).subscribe({
      next: () => {
        this.bookingState.setBagSelections(selectionsList);
        this.saving.set(false);
        this.router.navigate(['/booking/products']);
      },
      error: () => {
        this.saving.set(false);
        this.saveError.set('Failed to save bag selections. Please try again.');
      }
    });
  }
}
