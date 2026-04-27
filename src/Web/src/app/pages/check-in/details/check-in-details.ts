import { Component, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CheckInStateService, OciTravelDocument } from '../../../services/check-in-state.service';
import { RetailApiService } from '../../../services/retail-api.service';
import { OciOrder, OciFlightSegment } from '../../../models/order.model';
import { COUNTRIES, PRIORITY_COUNTRIES, Country } from '../../../data/countries';
import { LucideAngularModule } from 'lucide-angular';

interface TravelDocumentForm {
  type: 'PASSPORT' | 'ID_CARD';
  number: string;
  issuingCountry: string;
  issueDate: string;
  expiryDate: string;
  nationality: string;
}

interface PassengerCheckInState {
  passengerId: string;
  ticketNumber: string;
  givenName: string;
  surname: string;
  passengerType: string;
  selected: boolean;
  travelDocument: TravelDocumentForm;
}

@Component({
  selector: 'app-check-in-details',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LucideAngularModule],
  templateUrl: './check-in-details.html',
  styleUrl: './check-in-details.css'
})
export class CheckInDetailsComponent implements OnInit {
  order = signal<OciOrder | null>(null);
  passengerStates = signal<PassengerCheckInState[]>([]);

  readonly priorityCountries: Country[] = PRIORITY_COUNTRIES;
  readonly otherCountries: Country[] = COUNTRIES.filter(c => !['GBR', 'USA'].includes(c.code));

  readonly selectedPassengerIds = computed((): string[] =>
    this.passengerStates()
      .filter(s => s.selected)
      .map(s => s.passengerId)
  );

  readonly canProceed = computed((): boolean =>
    this.selectedPassengerIds().length > 0
  );

  readonly today = new Date().toISOString().split('T')[0];

  readonly documentTypes: { value: string; label: string }[] = [
    { value: 'PASSPORT', label: 'Passport' },
    { value: 'ID_CARD', label: 'ID Card' }
  ];

  saving = signal(false);
  saveError = signal('');
  copiedText = signal<string | null>(null);

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copiedText.set(text);
      setTimeout(() => this.copiedText.set(null), 2000);
    });
  }

  constructor(
    private checkInState: CheckInStateService,
    private retailApi: RetailApiService,
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
      ticketNumber: pax.ticketNumber,
      givenName: pax.givenName,
      surname: pax.surname,
      passengerType: pax.type,
      selected: true,
      travelDocument: pax.travelDocument
        ? {
            type: pax.travelDocument.type,
            number: pax.travelDocument.number,
            issuingCountry: pax.travelDocument.issuingCountry,
            issueDate: pax.travelDocument.issueDate ?? '',
            expiryDate: pax.travelDocument.expiryDate,
            nationality: pax.travelDocument.nationality
          }
        : { type: 'PASSPORT', number: '', issuingCountry: '', issueDate: '', expiryDate: '', nationality: '' }
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

  onIssueDateChange(index: number, value: string): void {
    this.updateDocField(index, 'issueDate', value);
    if (value) {
      const issue = new Date(value);
      const expiry = new Date(issue);
      expiry.setFullYear(expiry.getFullYear() + 10);
      this.updateDocField(index, 'expiryDate', expiry.toISOString().substring(0, 10));
    }
  }

  fillRandomPassportData(): void {
    const randInt = (min: number, max: number) => Math.floor(Math.random() * (max - min + 1)) + min;
    const letters = 'ABCDEFGHJKLMNPRSTUVWXYZ';
    const pickChar = () => letters[Math.floor(Math.random() * letters.length)];
    const randomPassportNumber = () => `${pickChar()}${pickChar()}${randInt(1000000, 9999999)}`;
    const randomIssueDate = (): string => {
      const d = new Date();
      d.setFullYear(d.getFullYear() - randInt(1, 9));
      return d.toISOString().substring(0, 10);
    };

    const states = this.passengerStates().map(s => {
      if (!s.selected) return s;
      const issueDate = randomIssueDate();
      const expiry = new Date(issueDate);
      expiry.setFullYear(expiry.getFullYear() + 10);
      return {
        ...s,
        travelDocument: {
          type: 'PASSPORT' as const,
          number: randomPassportNumber(),
          issuingCountry: 'GBR',
          nationality: 'GBR',
          issueDate,
          expiryDate: expiry.toISOString().substring(0, 10),
        }
      };
    });
    this.passengerStates.set(states);
  }

  proceedToSeats(): void {
    if (this.saving()) return;
    const selected = this.passengerStates().filter(s => s.selected);

    const today = new Date();
    today.setHours(0, 0, 0, 0);
    for (const s of selected) {
      const doc = s.travelDocument;
      if (doc.issueDate) {
        const issueDate = new Date(doc.issueDate);
        if (issueDate >= today) {
          this.saveError.set(`Document issue date for ${s.givenName} ${s.surname} must be in the past.`);
          return;
        }
      }
      if (doc.expiryDate) {
        const expiryDate = new Date(doc.expiryDate);
        if (expiryDate <= today) {
          this.saveError.set(`Document expiry date for ${s.givenName} ${s.surname} must be in the future.`);
          return;
        }
      }
    }

    const travelDocs: OciTravelDocument[] = selected.map(s => ({
      passengerId: s.passengerId,
      ticketNumber: s.ticketNumber,
      type: s.travelDocument.type,
      number: s.travelDocument.number,
      issuingCountry: s.travelDocument.issuingCountry,
      issueDate: s.travelDocument.issueDate,
      expiryDate: s.travelDocument.expiryDate,
      nationality: s.travelDocument.nationality,
    }));
    this.checkInState.setPassengerCheckInData(
      selected.map(s => s.passengerId),
      travelDocs
    );

    const order = this.order();
    if (!order) return;
    const departureAirport = this.checkInState.departureAirport();

    this.saving.set(true);
    this.saveError.set('');
    this.retailApi.saveOciPassengerDetails(order.bookingReference, departureAirport, travelDocs.map(d => ({
      ticketNumber: d.ticketNumber,
      type: d.type,
      number: d.number,
      issuingCountry: d.issuingCountry,
      issueDate: d.issueDate,
      expiryDate: d.expiryDate,
      nationality: d.nationality
    }))).subscribe({
      next: () => {
        this.saving.set(false);
        this.router.navigate(['/check-in/seats']);
      },
      error: () => {
        this.saving.set(false);
        this.saveError.set('Failed to save passenger details. Please try again.');
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
