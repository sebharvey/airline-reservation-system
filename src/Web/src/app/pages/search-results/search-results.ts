import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../services/retail-api.service';
import { BookingStateService } from '../../services/booking-state.service';
import { FlightOffer, CabinCode } from '../../models/flight.model';
import { BookingType } from '../../models/order.model';
import { AIRPORTS } from '../../data/airports';
import { LoyaltyStateService } from '../../services/loyalty-state.service';

interface FlightRow {
  flightNumber: string;
  origin: string;
  destination: string;
  departureDateTime: string;
  arrivalDateTime: string;
  aircraftType: string;
  cabinOffers: Partial<Record<CabinCode, FlightOffer[]>>;
}

@Component({
  selector: 'app-search-results',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './search-results.html',
  styleUrl: './search-results.css'
})
export class SearchResultsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly retailApi = inject(RetailApiService);
  private readonly bookingState = inject(BookingStateService);
  private readonly loyaltyState = inject(LoyaltyStateService);

  origin = signal('');
  destination = signal('');
  departDate = signal('');
  returnDate = signal('');
  tripType = signal('one-way');
  adults = signal(1);
  children = signal(0);

  bookingType = signal<BookingType>('Revenue');

  readonly isRewardBooking = computed(() => this.bookingType() === 'Reward');

  outboundLoading = signal(false);
  returnLoading = signal(false);
  outboundError = signal('');
  returnError = signal('');

  outboundOffers = signal<FlightOffer[]>([]);
  returnOffers = signal<FlightOffer[]>([]);

  selectedOutbound = signal<FlightOffer | null>(null);
  selectedReturn = signal<FlightOffer | null>(null);

  showReturnSearch = signal(false);

  farePopup = signal<{ offers: FlightOffer[]; isReturn: boolean } | null>(null);

  basketLoading = signal(false);
  basketError = signal('');

  readonly airports = AIRPORTS;

  readonly isReturn = computed(() => this.tripType() === 'return');

  readonly totalPassengers = computed(() => this.adults() + this.children());

  readonly cabinOrder: CabinCode[] = ['F', 'J', 'W', 'Y'];

  readonly cabinDisplayNames: Record<CabinCode, string> = {
    F: 'First',
    J: 'Business',
    W: 'Premium Economy',
    Y: 'Economy'
  };

  readonly outboundRows = computed<FlightRow[]>(() =>
    this.groupByFlight(this.outboundOffers())
  );

  readonly returnRows = computed<FlightRow[]>(() =>
    this.groupByFlight(this.returnOffers())
  );

  readonly outboundCabins = computed<CabinCode[]>(() =>
    this.getAvailableCabins(this.outboundOffers())
  );

  readonly returnCabins = computed<CabinCode[]>(() =>
    this.getAvailableCabins(this.returnOffers())
  );

  ngOnInit(): void {
    const p = this.route.snapshot.queryParamMap;
    this.origin.set(p.get('origin') ?? '');
    this.destination.set(p.get('destination') ?? '');
    this.departDate.set(p.get('departDate') ?? '');
    this.returnDate.set(p.get('returnDate') ?? '');
    this.tripType.set(p.get('tripType') ?? 'one-way');
    this.adults.set(Number(p.get('adults') ?? 1));
    this.children.set(Number(p.get('children') ?? 0));
    const bt = (p.get('bookingType') ?? 'Revenue') as BookingType;
    this.bookingType.set(bt);
    this.bookingState.setBookingType(bt);

    this.bookingState.setSearchParams({
      origin: this.origin(),
      destination: this.destination(),
      departDate: this.departDate(),
      returnDate: this.returnDate() || undefined,
      tripType: this.tripType(),
      adults: this.adults(),
      children: this.children()
    });

    this.loadOutbound();
  }

  private loadOutbound(): void {
    this.outboundLoading.set(true);
    this.outboundError.set('');
    this.retailApi.searchSlice({
      origin: this.origin(),
      destination: this.destination(),
      departureDate: this.departDate(),
      adults: this.adults(),
      children: this.children(),
      bookingType: this.bookingType()
    }).subscribe({
      next: (offers) => {
        this.outboundOffers.set(offers);
        this.outboundLoading.set(false);
      },
      error: () => {
        this.outboundError.set('Failed to load flights. Please try again.');
        this.outboundLoading.set(false);
      }
    });
  }

  private loadReturn(): void {
    this.returnLoading.set(true);
    this.returnError.set('');
    this.retailApi.searchSlice({
      origin: this.destination(),
      destination: this.origin(),
      departureDate: this.returnDate(),
      adults: this.adults(),
      children: this.children(),
      bookingType: this.bookingType()
    }).subscribe({
      next: (offers) => {
        this.returnOffers.set(offers);
        this.returnLoading.set(false);
      },
      error: () => {
        this.returnError.set('Failed to load return flights. Please try again.');
        this.returnLoading.set(false);
      }
    });
  }

  selectOutbound(offer: FlightOffer): void {
    this.selectedOutbound.set(offer);
    if (this.isReturn()) {
      this.showReturnSearch.set(true);
      if (this.returnOffers().length === 0) {
        this.loadReturn();
      }
    } else {
      this.proceedToBooking();
    }
  }

  selectReturn(offer: FlightOffer): void {
    this.selectedReturn.set(offer);
    this.proceedToBooking();
  }

  private proceedToBooking(): void {
    const outbound = this.selectedOutbound();
    if (!outbound) return;
    const inbound = this.isReturn() ? this.selectedReturn() : null;

    this.basketLoading.set(true);
    this.basketError.set('');

    const loyaltyNumber = this.loyaltyState.currentCustomer()?.loyaltyNumber;

    this.retailApi.createBasket({
      outboundOfferId: outbound.offerId,
      inboundOfferId: inbound?.offerId,
      bookingType: this.bookingType(),
      loyaltyNumber
    }).subscribe({
      next: (basket) => {
        this.bookingState.startBasket(outbound, inbound, basket.basketId);
        this.basketLoading.set(false);
        if (this.isRewardBooking()) {
          this.router.navigate(['/booking/reward-login']);
        } else {
          this.router.navigate(['/booking/passengers']);
        }
      },
      error: () => {
        this.basketError.set('Unable to reserve your selection. Please try again.');
        this.basketLoading.set(false);
      }
    });
  }

  openFarePopup(offers: FlightOffer[], isReturn: boolean): void {
    if (offers.length === 1) {
      if (isReturn) {
        this.selectReturn(offers[0]);
      } else {
        this.selectOutbound(offers[0]);
      }
    } else {
      // Sort: cheapest first
      const sorted = [...offers].sort((a, b) => a.totalPrice - b.totalPrice);
      this.farePopup.set({ offers: sorted, isReturn });
    }
  }

  closeFarePopup(): void {
    this.farePopup.set(null);
  }

  selectFromPopup(offer: FlightOffer): void {
    const popup = this.farePopup();
    if (!popup) return;
    this.farePopup.set(null);
    if (popup.isReturn) {
      this.selectReturn(offer);
    } else {
      this.selectOutbound(offer);
    }
  }

  getCabinOffers(row: FlightRow, cabin: CabinCode): FlightOffer[] | undefined {
    return row.cabinOffers[cabin];
  }

  getLowestPrice(offers: FlightOffer[]): number {
    return Math.min(...offers.map(o => o.totalPrice));
  }

  getLowestPoints(offers: FlightOffer[]): number {
    return Math.min(...offers.map(o => o.pointsPrice ?? 0));
  }

  getLowestPointsTaxes(offers: FlightOffer[]): number {
    const lowestPointsOffer = offers.reduce((min, o) => (o.pointsPrice ?? 0) < (min.pointsPrice ?? 0) ? o : min);
    return lowestPointsOffer.pointsTaxes ?? lowestPointsOffer.taxes;
  }

  hasFlex(offers: FlightOffer[]): boolean {
    return offers.length > 1;
  }

  isCellSelectedOutbound(row: FlightRow, cabin: CabinCode): boolean {
    const sel = this.selectedOutbound();
    return sel !== null &&
      sel.flightNumber === row.flightNumber &&
      sel.departureDateTime === row.departureDateTime &&
      sel.cabinCode === cabin;
  }

  isCellSelectedReturn(row: FlightRow, cabin: CabinCode): boolean {
    const sel = this.selectedReturn();
    return sel !== null &&
      sel.flightNumber === row.flightNumber &&
      sel.departureDateTime === row.departureDateTime &&
      sel.cabinCode === cabin;
  }

  getCityName(code: string): string {
    return this.airports.find(a => a.code === code)?.city ?? code;
  }

  getDuration(dep: string, arr: string): string {
    const diffMs = new Date(arr).getTime() - new Date(dep).getTime();
    const totalMins = Math.round(diffMs / 60000);
    const h = Math.floor(totalMins / 60);
    const m = totalMins % 60;
    return `${h}h ${m}m`;
  }

  formatTime(dt: string): string {
    return new Date(dt).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  }

  formatDate(dt: string): string {
    return new Date(dt).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
  }

  private groupByFlight(offers: FlightOffer[]): FlightRow[] {
    const map = new Map<string, FlightRow>();
    for (const offer of offers) {
      const key = `${offer.flightNumber}|${offer.departureDateTime}`;
      if (!map.has(key)) {
        map.set(key, {
          flightNumber: offer.flightNumber,
          origin: offer.origin,
          destination: offer.destination,
          departureDateTime: offer.departureDateTime,
          arrivalDateTime: offer.arrivalDateTime,
          aircraftType: offer.aircraftType,
          cabinOffers: {}
        });
      }
      const row = map.get(key)!;
      if (!row.cabinOffers[offer.cabinCode]) {
        row.cabinOffers[offer.cabinCode] = [];
      }
      row.cabinOffers[offer.cabinCode]!.push(offer);
    }
    for (const row of map.values()) {
      for (const cabin of Object.keys(row.cabinOffers) as CabinCode[]) {
        row.cabinOffers[cabin]!.sort((a, b) => a.totalPrice - b.totalPrice);
      }
    }
    return Array.from(map.values());
  }

  private getAvailableCabins(offers: FlightOffer[]): CabinCode[] {
    const cabins = new Set(offers.map(o => o.cabinCode));
    return this.cabinOrder.filter(c => cabins.has(c));
  }
}
