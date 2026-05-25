import { Component, OnInit, signal, computed } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { Order, Passenger, CardDetails, FlightSegment } from '../../../models/order.model';
import { Seatmap, CabinSeatmap, SeatOffer } from '../../../models/flight.model';
import { PaymentFormComponent } from '../../../components/payment-form/payment-form';

interface SeatSelection {
  passengerId: string;
  segmentId: string;
  seatNumber: string;
  seatOfferId: string;
  inventoryId: string;
  cabinCode: string;
  price: number;
  tax: number;
  currency: string;
}

@Component({
  selector: 'app-manage-seat',
  standalone: true,
  imports: [CommonModule, PaymentFormComponent],
  templateUrl: './manage-seat.html',
  styleUrl: './manage-seat.css'
})
export class ManageSeatComponent implements OnInit {
  order = signal<Order | null>(null);
  seatmap = signal<Seatmap | null>(null);
  loading = signal(true);
  seatmapLoading = signal(false);
  errorMessage = signal('');
  submitting = signal(false);
  paying = signal(false);
  paymentError = signal('');

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');
  segmentId = signal('');

  segment = signal<FlightSegment | null>(null);
  activePassengerIndex = signal(0);
  selections = signal<SeatSelection[]>([]);

  readonly activeCabin = computed((): CabinSeatmap | null => {
    const sm = this.seatmap();
    const seg = this.segment();
    if (!sm || !seg) return null;
    return sm.cabins.find(c => c.cabinCode === seg.cabinCode) ?? sm.cabins[0] ?? null;
  });

  readonly activePassenger = computed((): Passenger | null => {
    const o = this.order();
    if (!o) return null;
    return o.passengers[this.activePassengerIndex()] ?? null;
  });

  readonly totalSeatCost = computed((): number =>
    this.selections()
      .filter(s => s.segmentId === this.segmentId())
      .reduce((sum, sel) => sum + sel.price, 0)
  );

  readonly currency = computed((): string => {
    const sel = this.selections().find(s => s.price > 0);
    if (sel) return sel.currency;
    const cabin = this.activeCabin();
    return cabin?.seats.find(s => s.price > 0)?.currency ?? 'GBP';
  });

  readonly hasPaidSeats = computed(() => this.totalSeatCost() > 0);

  readonly hasAnySelection = computed(() =>
    this.selections().some(s => s.segmentId === this.segmentId())
  );

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
    private router: Router,
    private retailApi: RetailApiService
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
        this.loadSeatmap(seg.segmentId, seg.flightNumber, seg.aircraftType, seg.cabinCode);

        const existing: SeatSelection[] = [];
        for (const pax of order.passengers) {
          const seatItem = order.orderItems.find(
            oi => oi.type === 'Seat' && oi.segmentRef === seg.segmentId && oi.passengerRefs.includes(pax.passengerId)
          );
          if (seatItem?.seatNumber) {
            existing.push({
              passengerId: pax.passengerId,
              segmentId: seg.segmentId,
              seatNumber: seatItem.seatNumber,
              seatOfferId: '',
              inventoryId: seg.segmentId,
              cabinCode: seg.cabinCode,
              price: 0,
              tax: 0,
              currency: order.currency ?? 'GBP'
            });
          }
        }
        this.selections.set(existing);
      },
      error: (err: { status?: number; message?: string }) => {
        if (err.status === 401) { this.router.navigate(['/manage-booking']); return; }
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
    const segId = this.segmentId();
    return this.selections().some(
      s => s.passengerId === pax.passengerId && s.segmentId === segId && s.seatNumber === seat.seatNumber
    );
  }

  isSeatTakenByOther(seat: SeatOffer): boolean {
    const pax = this.activePassenger();
    const segId = this.segmentId();
    return this.selections().some(
      s => s.seatNumber === seat.seatNumber && s.segmentId === segId && s.passengerId !== pax?.passengerId
    );
  }

  selectSeat(seat: SeatOffer): void {
    if (seat.availability !== 'available' || this.isSeatTakenByOther(seat)) return;
    const pax = this.activePassenger();
    const seg = this.segment();
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
        tax: seat.tax,
        currency: seat.currency
      });
    }
    this.selections.set(updated);
  }

  getSeatLabel(passengerId: string): string {
    const segId = this.segmentId();
    const sel = this.selections().find(s => s.passengerId === passengerId && s.segmentId === segId);
    return sel?.seatNumber ?? 'Not selected';
  }

  formatCurrency(amount: number, currency: string): string {
    return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(amount);
  }

  onPay(card: CardDetails): void {
    this.paying.set(true);
    this.paymentError.set('');
    this.submitSeats({
      method: 'CreditCard',
      cardNumber: card.cardNumber,
      expiryDate: `${card.expiryMonth}/${card.expiryYear}`,
      cvv: card.cvv,
      cardholderName: card.cardholderName
    });
  }

  confirmSelection(): void {
    if (this.submitting()) return;
    this.submitting.set(true);
    this.errorMessage.set('');
    this.submitSeats(undefined);
  }

  private submitSeats(payment?: { method: string; cardNumber: string; expiryDate: string; cvv: string; cardholderName: string }): void {
    const segSelections = this.selections()
      .filter(s => s.segmentId === this.segmentId())
      .map(s => ({
        passengerId: s.passengerId,
        segmentId: s.segmentId,
        seatNumber: s.seatNumber,
        seatOfferId: s.seatOfferId || undefined,
        inventoryId: s.inventoryId || undefined,
        cabinCode: s.cabinCode || undefined,
        price: s.price,
        tax: s.tax,
        currency: s.currency
      }));

    this.retailApi.updateSeats(this.bookingRef(), segSelections, payment).subscribe({
      next: (res) => {
        this.paying.set(false);
        this.submitting.set(false);
        if (res.success) {
          this.router.navigate(['/manage-booking/detail'], {
            state: { givenName: this.givenName(), surname: this.surname() }
          });
        } else {
          this.errorMessage.set('Seat update failed. Please try again.');
        }
      },
      error: (err: { message?: string }) => {
        this.paying.set(false);
        this.submitting.set(false);
        if (this.hasPaidSeats()) {
          this.paymentError.set(err?.message ?? 'Seat update failed. Please try again.');
        } else {
          this.errorMessage.set(err?.message ?? 'Seat update failed. Please try again.');
        }
      }
    });
  }

  onBack(): void {
    this.router.navigate(['/manage-booking/detail'], {
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }
}
