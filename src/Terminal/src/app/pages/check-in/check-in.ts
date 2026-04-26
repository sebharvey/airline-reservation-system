import { Component, inject, signal } from '@angular/core';
import {
  CheckInService,
  LookupResponse,
  BoardingCard,
  PaxSubmission,
} from '../../services/check-in.service';

interface PaxFormData {
  passengerId: string;
  ticketNumber: string;
  givenName: string;
  surname: string;
  passengerTypeCode: string;
  docType: string;
  docNumber: string;
  issuingCountry: string;
  nationality: string;
  issueDate: string;
  expiryDate: string;
}

@Component({
  selector: 'app-check-in',
  templateUrl: './check-in.html',
  styleUrl: './check-in.css',
})
export class CheckInComponent {
  #svc = inject(CheckInService);

  loading = signal(false);
  error = signal('');

  bookingRef = signal('');
  departureAirports = signal<string[]>([]);
  departureAirport = signal('');

  booking = signal<LookupResponse | null>(null);
  paxForms = signal<PaxFormData[]>([]);

  boardingCards = signal<BoardingCard[]>([]);
  copiedBcbp = signal<string | null>(null);
  checkInComplete = signal(false);

  async findBooking(): Promise<void> {
    const ref = this.bookingRef().trim().toUpperCase();
    if (!ref) {
      this.error.set('Booking reference is required.');
      return;
    }

    this.loading.set(true);
    this.error.set('');
    this.departureAirports.set([]);
    this.departureAirport.set('');
    this.booking.set(null);
    this.paxForms.set([]);
    this.boardingCards.set([]);
    this.checkInComplete.set(false);

    try {
      const result = await this.#svc.lookup(ref);
      if (!result.departureAirports.length) {
        this.error.set('No eligible departures found for this booking reference.');
        return;
      }
      this.booking.set(result);
      this.departureAirports.set(result.departureAirports);
    } catch {
      this.error.set('Booking not found. Please check the reference.');
    } finally {
      this.loading.set(false);
    }
  }

  selectDeparture(airport: string): void {
    if (!airport) return;
    this.departureAirport.set(airport);
    this.error.set('');
    this.paxForms.set([]);
    this.boardingCards.set([]);
    this.checkInComplete.set(false);

    const orderDetail = this.booking()?.orderDetail;
    if (!orderDetail) return;

    const passengers = this.#svc.extractPassengers(orderDetail, airport);
    if (!passengers.length) {
      this.error.set('No passengers found for the selected departure airport.');
      return;
    }

    this.paxForms.set(
      passengers.map(p => ({
        passengerId: p.passengerId,
        ticketNumber: p.ticketNumber,
        givenName: p.givenName,
        surname: p.surname,
        passengerTypeCode: p.passengerTypeCode,
        docType: p.existingDoc?.type ?? 'PASSPORT',
        docNumber: p.existingDoc?.number ?? '',
        issuingCountry: p.existingDoc?.issuingCountry ?? '',
        nationality: p.existingDoc?.nationality ?? '',
        issueDate: p.existingDoc?.issueDate ?? '',
        expiryDate: p.existingDoc?.expiryDate ?? '',
      })),
    );
  }

  updateDoc(index: number, field: keyof PaxFormData, value: string): void {
    const forms = this.paxForms().slice();
    forms[index] = { ...forms[index], [field]: value };
    this.paxForms.set(forms);
  }

  async completeCheckIn(): Promise<void> {
    const forms = this.paxForms();
    const incomplete = forms.find(
      f => !f.docNumber || !f.issuingCountry || !f.nationality || !f.issueDate || !f.expiryDate,
    );
    if (incomplete) {
      this.error.set(
        `Please complete all travel document fields for ${incomplete.givenName} ${incomplete.surname}.`,
      );
      return;
    }

    const bookingRef = this.booking()!.bookingReference;
    const airport = this.departureAirport();

    this.loading.set(true);
    this.error.set('');

    try {
      const submissions: PaxSubmission[] = forms.map(f => ({
        ticketNumber: f.ticketNumber,
        travelDocument: {
          type: f.docType,
          number: f.docNumber.toUpperCase(),
          issuingCountry: f.issuingCountry.toUpperCase(),
          nationality: f.nationality.toUpperCase(),
          issueDate: f.issueDate,
          expiryDate: f.expiryDate,
        },
      }));

      const result = await this.#svc.adminCheckIn(bookingRef, airport, submissions);
      this.boardingCards.set(result.boardingCards);
      this.checkInComplete.set(true);
    } catch {
      this.error.set('Failed to complete check-in. Please try again or contact a supervisor.');
    } finally {
      this.loading.set(false);
    }
  }

  reset(): void {
    this.booking.set(null);
    this.paxForms.set([]);
    this.boardingCards.set([]);
    this.departureAirports.set([]);
    this.departureAirport.set('');
    this.bookingRef.set('');
    this.error.set('');
    this.checkInComplete.set(false);
  }

  copyBcbp(bcbp: string): void {
    navigator.clipboard.writeText(bcbp).then(() => {
      this.copiedBcbp.set(bcbp);
      setTimeout(() => this.copiedBcbp.set(null), 2000);
    });
  }

  paxTypeLabel(code: string): string {
    const labels: Record<string, string> = { ADT: 'Adult', CHD: 'Child', INF: 'Infant', YTH: 'Youth' };
    return labels[code] ?? code;
  }

  formatDate(iso: string): string {
    if (!iso) return '—';
    return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }
}
