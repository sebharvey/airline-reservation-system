import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../services/retail-api.service';
import { BookingStateService } from '../../services/booking-state.service';
import { FlightOffer, CabinCode } from '../../models/flight.model';
import { AIRPORTS } from '../../data/airports';

interface CabinGroup {
  cabinCode: CabinCode;
  cabinName: string;
  offers: FlightOffer[];
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

  origin = signal('');
  destination = signal('');
  departDate = signal('');
  returnDate = signal('');
  tripType = signal('one-way');
  adults = signal(1);
  children = signal(0);

  outboundLoading = signal(false);
  returnLoading = signal(false);
  outboundError = signal('');
  returnError = signal('');

  outboundOffers = signal<FlightOffer[]>([]);
  returnOffers = signal<FlightOffer[]>([]);

  selectedOutbound = signal<FlightOffer | null>(null);
  selectedReturn = signal<FlightOffer | null>(null);

  showReturnSearch = signal(false);

  readonly airports = AIRPORTS;

  readonly isReturn = computed(() => this.tripType() === 'return');

  readonly totalPassengers = computed(() => this.adults() + this.children());

  readonly outboundGroups = computed<CabinGroup[]>(() =>
    this.groupByCabin(this.outboundOffers())
  );

  readonly returnGroups = computed<CabinGroup[]>(() =>
    this.groupByCabin(this.returnOffers())
  );

  readonly cabinOrder: CabinCode[] = ['F', 'J', 'W', 'Y'];

  readonly cabinDisplayNames: Record<CabinCode, string> = {
    F: 'First Class',
    J: 'Business Class',
    W: 'Premium Economy',
    Y: 'Economy'
  };

  ngOnInit(): void {
    const p = this.route.snapshot.queryParamMap;
    this.origin.set(p.get('origin') ?? '');
    this.destination.set(p.get('destination') ?? '');
    this.departDate.set(p.get('departDate') ?? '');
    this.returnDate.set(p.get('returnDate') ?? '');
    this.tripType.set(p.get('tripType') ?? 'one-way');
    this.adults.set(Number(p.get('adults') ?? 1));
    this.children.set(Number(p.get('children') ?? 0));

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
      departDate: this.departDate(),
      adults: this.adults(),
      children: this.children()
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
      departDate: this.returnDate(),
      adults: this.adults(),
      children: this.children()
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
    this.bookingState.startBasket(outbound, inbound);
    this.router.navigate(['/booking/passengers']);
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

  private groupByCabin(offers: FlightOffer[]): CabinGroup[] {
    const map = new Map<CabinCode, FlightOffer[]>();
    for (const o of offers) {
      if (!map.has(o.cabinCode)) map.set(o.cabinCode, []);
      map.get(o.cabinCode)!.push(o);
    }
    return this.cabinOrder
      .filter(code => map.has(code))
      .map(code => ({
        cabinCode: code,
        cabinName: this.cabinDisplayNames[code],
        offers: map.get(code)!
      }));
  }

  isSelectedOutbound(offer: FlightOffer): boolean {
    return this.selectedOutbound()?.offerId === offer.offerId;
  }

  isSelectedReturn(offer: FlightOffer): boolean {
    return this.selectedReturn()?.offerId === offer.offerId;
  }

  formatTime(dt: string): string {
    return new Date(dt).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  }

  formatDate(dt: string): string {
    return new Date(dt).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' });
  }
}
