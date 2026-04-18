import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RetailApiService } from '../../../services/retail-api.service';
import { Order, Passenger } from '../../../models/order.model';
import { Seatmap, CabinSeatmap, SeatOffer } from '../../../models/flight.model';

interface SeatSelection {
  passengerId: string;
  segmentId: string;
  seatNumber: string;
  seatOfferId: string;
  inventoryId: string;
  cabinCode: string;
  price: number;
  currency: string;
}

@Component({
  selector: 'app-manage-seat',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './manage-seat.html',
  styleUrl: './manage-seat.css'
})
export class ManageSeatComponent implements OnInit {
  order = signal<Order | null>(null);
  seatmap = signal<Seatmap | null>(null);
  loading = signal(true);
  seatmapLoading = signal(false);
  errorMessage = signal('');
  successMessage = signal('');
  submitting = signal(false);
  submitted = signal(false);

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  activePassengerIndex = signal(0);
  selections = signal<SeatSelection[]>([]);

  // Payment form fields (shown only when paid seats are selected)
  cardholderName = signal('');
  cardNumber = signal('');
  expiryMonth = signal('');
  expiryYear = signal('');
  cvv = signal('');

  readonly activeCabin = computed((): CabinSeatmap | null => {
    const sm = this.seatmap();
    const o = this.order();
    if (!sm || !o) return null;
    const seg = o.flightSegments[0];
    if (!seg) return null;
    return sm.cabins.find(c => c.cabinCode === seg.cabinCode) ?? sm.cabins[0] ?? null;
  });

  readonly activePassenger = computed((): Passenger | null => {
    const o = this.order();
    if (!o) return null;
    return o.passengers[this.activePassengerIndex()] ?? null;
  });

  readonly totalSeatCost = computed((): number =>
    this.selections().reduce((sum, sel) => sum + sel.price, 0)
  );

  readonly currency = computed((): string => {
    const sel = this.selections().find(s => s.price > 0);
    if (sel) return sel.currency;
    const cabin = this.activeCabin();
    return cabin?.seats.find(s => s.price > 0)?.currency ?? 'GBP';
  });

  readonly hasPaidSeats = computed(() => this.totalSeatCost() > 0);

  readonly cardDisplayNumber = computed(() => {
    const raw = this.cardNumber().replace(/\D/g, '').substring(0, 16);
    return raw.replace(/(.{4})/g, '$1 ').trim();
  });

  readonly expiryYears = computed(() => {
    const current = new Date().getFullYear();
    return Array.from({ length: 12 }, (_, i) => current + i);
  });

  readonly expiryMonths = [
    { value: '01', label: '01 - Jan' }, { value: '02', label: '02 - Feb' },
    { value: '03', label: '03 - Mar' }, { value: '04', label: '04 - Apr' },
    { value: '05', label: '05 - May' }, { value: '06', label: '06 - Jun' },
    { value: '07', label: '07 - Jul' }, { value: '08', label: '08 - Aug' },
    { value: '09', label: '09 - Sep' }, { value: '10', label: '10 - Oct' },
    { value: '11', label: '11 - Nov' }, { value: '12', label: '12 - Dec' }
  ];

  readonly seatRows = computed((): { rowNumber: number; seats: (SeatOffer | null)[] }[] => {
    const cabin = this.activeCabin();
    if (!cabin) return [];
    const rows: Map<number, (SeatOffer | null)[]> = new Map();
    for (let r = cabin.startRow; r <= cabin.endRow; r++) {
      rows.set(r, cabin.columns.map(() => null));
    }
    for (const seat of cabin.seats) {
      const row = rows.get(seat.rowNumber);
      if (row) {
        const idx = cabin.columns.indexOf(seat.column);
        if (idx !== -1) row[idx] = seat;
      }
    }
    return Array.from(rows.entries())
      .sort((a, b) => a[0] - b[0])
      .map(([rowNumber, seats]) => ({ rowNumber, seats }));
  });

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private retailApi: RetailApiService
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
        const seg = order.flightSegments[0];
        if (seg) {
          this.loadSeatmap(seg.segmentId, seg.flightNumber, seg.aircraftType, seg.cabinCode);
        }
        // Pre-populate existing seat assignments (free — no offer ID needed)
        const existing: SeatSelection[] = [];
        for (const pax of order.passengers) {
          for (const seg2 of order.flightSegments) {
            const seatItem = order.orderItems.find(
              oi => oi.type === 'Seat' && oi.segmentRef === seg2.segmentId && oi.passengerRefs.includes(pax.passengerId)
            );
            if (seatItem?.seatNumber) {
              existing.push({
                passengerId: pax.passengerId,
                segmentId: seg2.segmentId,
                seatNumber: seatItem.seatNumber,
                seatOfferId: '',
                inventoryId: seg2.segmentId,
                cabinCode: seg2.cabinCode,
                price: 0,
                currency: order.currency ?? 'GBP'
              });
            }
          }
        }
        this.selections.set(existing);
      },
      error: (err: { message?: string }) => {
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking.');
        this.loading.set(false);
      }
    });
  }

  private loadSeatmap(flightId: string, flightNumber: string, aircraftType: string, cabinCode: string): void {
    this.seatmapLoading.set(true);
    this.retailApi.getFlightSeatmap(flightId, flightNumber, aircraftType, cabinCode).subscribe({
      next: (sm) => {
        this.seatmap.set(sm);
        this.seatmapLoading.set(false);
      },
      error: () => {
        this.seatmapLoading.set(false);
      }
    });
  }

  selectPassenger(index: number): void {
    this.activePassengerIndex.set(index);
  }

  isSeatSelected(seat: SeatOffer): boolean {
    const pax = this.activePassenger();
    if (!pax) return false;
    const seg = this.order()?.flightSegments[0];
    return this.selections().some(
      s => s.passengerId === pax.passengerId && s.segmentId === (seg?.segmentId ?? '') && s.seatNumber === seat.seatNumber
    );
  }

  isSeatTakenByOther(seat: SeatOffer): boolean {
    const pax = this.activePassenger();
    const seg = this.order()?.flightSegments[0];
    return this.selections().some(
      s => s.seatNumber === seat.seatNumber && s.segmentId === (seg?.segmentId ?? '') && s.passengerId !== pax?.passengerId
    );
  }

  selectSeat(seat: SeatOffer): void {
    if (seat.availability !== 'available' || this.isSeatTakenByOther(seat)) return;
    const pax = this.activePassenger();
    const seg = this.order()?.flightSegments[0];
    const sm = this.seatmap();
    if (!pax || !seg || !sm) return;

    const updated = this.selections().filter(
      s => !(s.passengerId === pax.passengerId && s.segmentId === seg.segmentId)
    );
    if (!this.isSeatSelected(seat)) {
      updated.push({
        passengerId: pax.passengerId,
        segmentId: seg.segmentId,
        seatNumber: seat.seatNumber,
        seatOfferId: seat.price > 0 ? seat.seatOfferId : '',
        inventoryId: sm.flightId,
        cabinCode: seat.cabinCode,
        price: seat.price,
        currency: seat.currency
      });
    }
    this.selections.set(updated);
  }

  getSeatLabel(passengerId: string): string {
    const seg = this.order()?.flightSegments[0];
    const sel = this.selections().find(s => s.passengerId === passengerId && s.segmentId === (seg?.segmentId ?? ''));
    return sel?.seatNumber ?? 'Not selected';
  }

  onCardNumberInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const digits = input.value.replace(/\D/g, '').substring(0, 16);
    this.cardNumber.set(digits);
    input.value = digits.replace(/(.{4})/g, '$1 ').trim();
  }

  isPaymentFormValid(): boolean {
    if (!this.hasPaidSeats()) return true;
    const name = this.cardholderName().trim();
    const num = this.cardNumber().replace(/\D/g, '');
    const month = this.expiryMonth();
    const year = this.expiryYear();
    const cvv = this.cvv().trim();
    return !!(name && num.length === 16 && month && year && cvv.length >= 3);
  }

  confirmSelection(): void {
    this.submitted.set(true);
    if (this.submitting()) return;
    if (!this.isPaymentFormValid()) return;

    this.submitting.set(true);
    this.errorMessage.set('');

    const seatSelections = this.selections().map(s => ({
      passengerId: s.passengerId,
      segmentId: s.segmentId,
      seatNumber: s.seatNumber,
      seatOfferId: s.seatOfferId || undefined,
      inventoryId: s.inventoryId || undefined,
      cabinCode: s.cabinCode || undefined
    }));

    const payment = this.hasPaidSeats() ? {
      method: 'CreditCard',
      cardNumber: this.cardNumber().replace(/\D/g, ''),
      expiryDate: `${this.expiryMonth()}/${this.expiryYear()}`,
      cvv: this.cvv().trim(),
      cardholderName: this.cardholderName().trim()
    } : undefined;

    this.retailApi.updateSeats(this.bookingRef(), seatSelections, payment).subscribe({
      next: (res) => {
        this.submitting.set(false);
        if (res.success) {
          this.successMessage.set('Seat selection updated successfully!');
        } else {
          this.errorMessage.set('Seat update failed. Please try again.');
        }
      },
      error: (err: { message?: string }) => {
        this.submitting.set(false);
        this.errorMessage.set(err?.message ?? 'Seat update failed. Please try again.');
      }
    });
  }

  formatCurrency(amount: number, currency: string): string {
    return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(amount);
  }

  get detailQueryParams() {
    return { bookingRef: this.bookingRef() };
  }

  get detailState() {
    return { givenName: this.givenName(), surname: this.surname() };
  }
}
