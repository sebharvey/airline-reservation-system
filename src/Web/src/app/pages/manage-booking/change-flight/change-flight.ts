import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RetailApiService } from '../../../services/retail-api.service';
import { Order, FlightSegment } from '../../../models/order.model';
import { FlightOffer } from '../../../models/flight.model';

@Component({
  selector: 'app-change-flight',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './change-flight.html',
  styleUrl: './change-flight.css'
})
export class ChangeFlightComponent implements OnInit {
  order = signal<Order | null>(null);
  loading = signal(true);
  searching = signal(false);
  confirming = signal(false);
  errorMessage = signal('');
  successMessage = signal('');

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  newDate = signal('');
  selectedSegment = signal<FlightSegment | null>(null);
  flightOffers = signal<FlightOffer[]>([]);
  selectedOffer = signal<FlightOffer | null>(null);

  readonly changeableSegments = computed((): FlightSegment[] => {
    const o = this.order();
    if (!o) return [];
    return o.flightSegments.filter(seg =>
      o.orderItems.some(oi => oi.type === 'Flight' && oi.segmentRef === seg.segmentId && oi.isChangeable)
    );
  });

  readonly addCollectEstimate = computed((): string => {
    const offer = this.selectedOffer();
    const o = this.order();
    if (!offer || !o) return '';
    const seg = this.selectedSegment();
    if (!seg) return '';
    const existingItem = o.orderItems.find(oi => oi.type === 'Flight' && oi.segmentRef === seg.segmentId);
    if (!existingItem) return '';
    const diff = offer.totalPrice - existingItem.totalPrice;
    if (diff <= 0) return 'No additional charge';
    return `Add collect: ${this.formatCurrency(diff, offer.currency)}`;
  });

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private retailApi: RetailApiService
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const ref = params['bookingRef'] ?? '';
      const gn = params['givenName'] ?? '';
      const sn = params['surname'] ?? '';
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
        const segs = this.changeableSegments();
        if (segs.length > 0) {
          this.selectedSegment.set(segs[0]);
        }
      },
      error: (err: { message?: string }) => {
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking.');
        this.loading.set(false);
      }
    });
  }

  selectSegment(seg: FlightSegment): void {
    this.selectedSegment.set(seg);
    this.flightOffers.set([]);
    this.selectedOffer.set(null);
  }

  searchFlights(): void {
    const seg = this.selectedSegment();
    const date = this.newDate();
    const o = this.order();
    if (!seg || !date || !o) return;

    this.searching.set(true);
    this.flightOffers.set([]);
    this.selectedOffer.set(null);
    this.errorMessage.set('');

    this.retailApi.searchSlice({
      origin: seg.origin,
      destination: seg.destination,
      departureDate: date,
      adults: o.passengers.filter(p => p.type === 'ADT').length,
      children: o.passengers.filter(p => p.type === 'CHD').length
    }).subscribe({
      next: (offers) => {
        this.flightOffers.set(offers);
        this.searching.set(false);
        if (offers.length === 0) {
          this.errorMessage.set('No flights found for the selected date. Try a different date.');
        }
      },
      error: (err: { message?: string }) => {
        this.errorMessage.set(err?.message ?? 'Search failed. Please try again.');
        this.searching.set(false);
      }
    });
  }

  selectOffer(offer: FlightOffer): void {
    this.selectedOffer.set(this.selectedOffer()?.offerId === offer.offerId ? null : offer);
  }

  confirmChange(): void {
    const offer = this.selectedOffer();
    if (!offer || this.confirming()) return;

    this.confirming.set(true);
    this.errorMessage.set('');

    this.retailApi.changeOrder(this.bookingRef(), offer.offerId).subscribe({
      next: (res) => {
        this.confirming.set(false);
        if (res.success) {
          const chargeMsg = res.addCollect > 0
            ? ` An additional charge of ${this.formatCurrency(res.addCollect, res.currency)} has been applied.`
            : '';
          this.successMessage.set(`Flight changed successfully.${chargeMsg}`);
        } else {
          this.errorMessage.set('Flight change failed. Please try again.');
        }
      },
      error: (err: { message?: string }) => {
        this.confirming.set(false);
        this.errorMessage.set(err?.message ?? 'Flight change failed. Please try again.');
      }
    });
  }

  formatDateTime(dt: string): string {
    return new Date(dt).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit', timeZone: 'UTC'
    });
  }

  formatCurrency(amount: number, currency: string): string {
    return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(amount);
  }

  formatDuration(dep: string, arr: string): string {
    const mins = (new Date(arr).getTime() - new Date(dep).getTime()) / 60000;
    const h = Math.floor(mins / 60);
    const m = Math.round(mins % 60);
    return `${h}h ${m}m`;
  }

  get minDate(): string {
    return new Date().toISOString().split('T')[0];
  }

  get detailQueryParams() {
    return { bookingRef: this.bookingRef(), givenName: this.givenName(), surname: this.surname() };
  }
}
