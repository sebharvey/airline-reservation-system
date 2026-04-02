import { Component, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CheckInStateService, OciTravelDocument } from '../../../services/check-in-state.service';
import { OciOrder, OciFlightSegment } from '../../../models/order.model';
import { COUNTRIES, Country } from '../../../data/countries';

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
  passengerType: string;
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
  order = signal<OciOrder | null>(null);
  passengerStates = signal<PassengerCheckInState[]>([]);

  readonly countries: Country[] = COUNTRIES;

  readonly selectedPassengerIds = computed((): string[] =>
    this.passengerStates()
      .filter(s => s.selected)
      .map(s => s.passengerId)
  );

  readonly canProceed = computed((): boolean =>
    this.selectedPassengerIds().length > 0
  );

  readonly documentTypes: { value: string; label: string }[] = [
    { value: 'PASSPORT', label: 'Passport' },
    { value: 'ID_CARD', label: 'ID Card' }
  ];

  constructor(
    private checkInState: CheckInStateService,
    private router: Router
  ) {}

  ngOnInit(): void {
    const order = this.checkInState.currentOrder();
    if (!order) {
      this.router.navigate(['/check-in']);
      return;
    }
    this.order.set(order);
    this.passengerStates.set(order.passengers.map(pax => ({
      passengerId: pax.passengerId,
      givenName: pax.givenName,
      surname: pax.surname,
      passengerType: pax.type,
      selected: true,
      travelDocument: { type: 'PASSPORT', number: '', issuingCountry: '', expiryDate: '', nationality: '' }
    })));
  }

  getSeatNumber(passengerId: string, seg: OciFlightSegment): string | null {
    return seg.seatAssignments.find(s => s.passengerId === passengerId)?.seatNumber ?? null;
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

  proceedToBags(): void {
    const selected = this.passengerStates().filter(s => s.selected);
    const travelDocs: OciTravelDocument[] = selected.map(s => ({
      passengerId: s.passengerId,
      type: s.travelDocument.type,
      number: s.travelDocument.number,
      issuingCountry: s.travelDocument.issuingCountry,
      expiryDate: s.travelDocument.expiryDate,
      nationality: s.travelDocument.nationality,
    }));
    this.checkInState.setPassengerCheckInData(
      selected.map(s => s.passengerId),
      travelDocs
    );
    this.router.navigate(['/check-in/bags']);
  }

  formatDateTime(dt: string): string {
    return new Date(dt).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit', timeZone: 'UTC'
    });
  }
}
