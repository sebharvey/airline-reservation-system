import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { Order, OrderItem, Passenger, FlightSegment } from '../../../models/order.model';

interface PassengerSeatInfo {
  passenger: Passenger;
  seatNumber: string | null;
  eTicketNumber: string | null;
}

interface SegmentDisplay {
  segment: FlightSegment;
  passengerSeats: PassengerSeatInfo[];
}

@Component({
  selector: 'app-manage-booking-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './detail.html',
  styleUrl: './detail.css'
})
export class ManageBookingDetailComponent implements OnInit {
  order = signal<Order | null>(null);
  loading = signal(true);
  errorMessage = signal('');

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  readonly canChangeSeat = computed(() => {
    const o = this.order();
    return o ? o.orderStatus === 'Confirmed' : false;
  });

  readonly canChangeFlight = computed(() => {
    const o = this.order();
    return o ? o.orderItems.some(i => i.isChangeable) : false;
  });

  readonly canCancel = computed(() => {
    const o = this.order();
    return o ? o.orderItems.some(i => i.isRefundable) : false;
  });

  readonly canAddBags = computed(() => {
    const o = this.order();
    return o ? o.orderStatus === 'Confirmed' : false;
  });

  readonly segmentDisplays = computed((): SegmentDisplay[] => {
    const o = this.order();
    if (!o) return [];
    return o.flightSegments.map(seg => {
      const flightItems = o.orderItems.filter(
        oi => oi.type === 'Flight' && oi.segmentRef === seg.segmentId
      );
      const passengerSeats: PassengerSeatInfo[] = o.passengers.map(pax => {
        let seatNumber: string | null = null;
        let eTicketNumber: string | null = null;
        for (const item of flightItems) {
          const seat = item.seatAssignments?.find(s => s.passengerId === pax.passengerId);
          if (seat) seatNumber = seat.seatNumber;
          const ticket = item.eTickets?.find(t => t.passengerId === pax.passengerId);
          if (ticket) eTicketNumber = ticket.eTicketNumber;
        }
        return { passenger: pax, seatNumber, eTicketNumber };
      });
      return { segment: seg, passengerSeats };
    });
  });

  readonly flightItemsOnly = computed((): OrderItem[] => {
    const o = this.order();
    return o ? o.orderItems.filter(i => i.type === 'Flight') : [];
  });

  readonly ancillaryItems = computed((): OrderItem[] => {
    const o = this.order();
    return o ? o.orderItems.filter(i => i.type !== 'Flight') : [];
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
      this.fetchOrder(ref, gn, sn);
    });
  }

  private fetchOrder(ref: string, givenName: string, surname: string): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.retailApi.retrieveOrder({ bookingReference: ref, givenName, surname }).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
      },
      error: (err: { message?: string }) => {
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking.');
        this.loading.set(false);
      }
    });
  }

  formatDateTime(dt: string): string {
    return new Date(dt).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit', timeZone: 'UTC'
    });
  }

  formatDate(dt: string): string {
    return new Date(dt).toLocaleDateString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC'
    });
  }

  formatCurrency(amount: number, currency: string): string {
    return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(amount);
  }

  navigateToAddBags(): void {
    this.router.navigate(['/manage-booking/bags'], {
      queryParams: { bookingRef: this.bookingRef(), givenName: this.givenName(), surname: this.surname() }
    });
  }

  navigateToSeat(): void {
    this.router.navigate(['/manage-booking/seat'], {
      queryParams: { bookingRef: this.bookingRef(), givenName: this.givenName(), surname: this.surname() }
    });
  }

  navigateToChangeFlight(): void {
    this.router.navigate(['/manage-booking/change-flight'], {
      queryParams: { bookingRef: this.bookingRef(), givenName: this.givenName(), surname: this.surname() }
    });
  }

  navigateToCancel(): void {
    this.router.navigate(['/manage-booking/cancel'], {
      queryParams: { bookingRef: this.bookingRef(), givenName: this.givenName(), surname: this.surname() }
    });
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Confirmed': return 'badge-confirmed';
      case 'Cancelled': return 'badge-cancelled';
      case 'Changed': return 'badge-changed';
      default: return 'badge-default';
    }
  }

  itemTypeLabel(type: string): string {
    switch (type) {
      case 'Seat': return 'Seat Ancillary';
      case 'Bag': return 'Baggage Ancillary';
      default: return type;
    }
  }
}
