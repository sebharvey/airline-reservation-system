import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RetailApiService } from '../../../services/retail-api.service';
import { Order, TravelDocument } from '../../../models/order.model';

interface TravelDocumentForm {
  type: 'PASSPORT' | 'ID_CARD';
  number: string;
  issuingCountry: string;
  expiryDate: string;
  nationality: string;
}

interface PassengerCheckInState {
  passengerId: string;
  givenName: string;
  surname: string;
  passengerType: 'ADT' | 'CHD';
  selected: boolean;
  travelDocument: TravelDocumentForm;
}

@Component({
  selector: 'app-check-in-details',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './check-in-details.html',
  styleUrl: './check-in-details.css'
})
export class CheckInDetailsComponent implements OnInit {
  order = signal<Order | null>(null);
  loading = signal(true);
  errorMessage = signal('');

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  passengerStates = signal<PassengerCheckInState[]>([]);

  readonly selectedPassengerIds = computed((): string[] =>
    this.passengerStates()
      .filter(s => s.selected)
      .map(s => s.passengerId)
  );

  readonly hasSeats = computed((): boolean => {
    const o = this.order();
    if (!o) return false;
    return o.orderItems.some(
      oi => oi.type === 'Flight' &&
        oi.seatAssignments &&
        oi.seatAssignments.some(sa => !!sa.seatNumber)
    );
  });

  readonly canProceed = computed((): boolean =>
    this.selectedPassengerIds().length > 0
  );

  readonly documentTypes: { value: string; label: string }[] = [
    { value: 'PASSPORT', label: 'Passport' },
    { value: 'ID_CARD', label: 'ID Card' }
  ];

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

      if (!ref) {
        this.router.navigate(['/check-in']);
        return;
      }
      this.loadOrder(ref, gn, sn);
    });
  }

  private loadOrder(ref: string, gn: string, sn: string): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.retailApi.retrieveForCheckIn({ bookingReference: ref, givenName: gn, surname: sn }).subscribe({
      next: (order) => {
        this.order.set(order);
        this.loading.set(false);
        const states: PassengerCheckInState[] = order.passengers.map(pax => ({
          passengerId: pax.passengerId,
          givenName: pax.givenName,
          surname: pax.surname,
          passengerType: pax.type,
          selected: true,
          travelDocument: pax.travelDocument
            ? { ...pax.travelDocument }
            : { type: 'PASSPORT', number: '', issuingCountry: '', expiryDate: '', nationality: '' }
        }));
        this.passengerStates.set(states);
      },
      error: (err: { message?: string }) => {
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking.');
        this.loading.set(false);
      }
    });
  }

  togglePassenger(index: number): void {
    const states = [...this.passengerStates()];
    states[index] = { ...states[index], selected: !states[index].selected };
    this.passengerStates.set(states);
  }

  updateDocField(index: number, field: keyof TravelDocumentForm, value: string): void {
    const states = [...this.passengerStates()];
    states[index] = {
      ...states[index],
      travelDocument: { ...states[index].travelDocument, [field]: value }
    };
    this.passengerStates.set(states);
  }

  proceedToSeats(): void {
    this.router.navigate(['/check-in/seats'], {
      queryParams: {
        bookingRef: this.bookingRef(),
        givenName: this.givenName(),
        surname: this.surname(),
        passengerIds: this.selectedPassengerIds().join(',')
      }
    });
  }

  proceedToBoardingPass(): void {
    this.router.navigate(['/check-in/boarding-pass'], {
      queryParams: {
        bookingRef: this.bookingRef(),
        givenName: this.givenName(),
        surname: this.surname(),
        passengerIds: this.selectedPassengerIds().join(',')
      }
    });
  }

  formatDateTime(dt: string): string {
    return new Date(dt).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit', timeZone: 'UTC'
    });
  }
}
