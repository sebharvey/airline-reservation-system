import { Component, inject, signal } from '@angular/core';
import {
  CheckInService,
  RetrieveResponse,
  BoardingCard,
  PaxSubmission,
} from '../../services/check-in.service';

type Step = 'search' | 'pax' | 'confirm' | 'boarding';

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

  step = signal<Step>('search');
  loading = signal(false);
  error = signal('');

  bookingRef = signal('');
  firstName = signal('');
  lastName = signal('');
  departureAirport = signal('');

  booking = signal<RetrieveResponse | null>(null);
  paxForms = signal<PaxFormData[]>([]);

  boardingCards = signal<BoardingCard[]>([]);
  copiedBcbp = signal<string | null>(null);

  async findBooking(): Promise<void> {
    const ref = this.bookingRef().trim().toUpperCase();
    const first = this.firstName().trim();
    const last = this.lastName().trim();
    const airport = this.departureAirport().trim().toUpperCase();

    if (!ref || !first || !last || !airport) {
      this.error.set('All fields are required.');
      return;
    }

    this.loading.set(true);
    this.error.set('');

    try {
      const result = await this.#svc.retrieve(ref, first, last, airport);

      if (!result.checkInEligible) {
        this.error.set(
          'This booking is not eligible for check-in. Please verify the details or confirm the departure window is open.',
        );
        return;
      }

      this.booking.set(result);
      this.paxForms.set(
        result.passengers.map(p => ({
          passengerId: p.passengerId,
          ticketNumber: p.ticketNumber,
          givenName: p.givenName,
          surname: p.surname,
          passengerTypeCode: p.passengerTypeCode,
          docType: p.travelDocument?.type ?? 'PASSPORT',
          docNumber: p.travelDocument?.number ?? '',
          issuingCountry: p.travelDocument?.issuingCountry ?? '',
          nationality: p.travelDocument?.nationality ?? '',
          issueDate: p.travelDocument?.issueDate ?? '',
          expiryDate: p.travelDocument?.expiryDate ?? '',
        })),
      );
      this.step.set('pax');
    } catch {
      this.error.set('Booking not found. Please check the reference and passenger details.');
    } finally {
      this.loading.set(false);
    }
  }

  updateDoc(index: number, field: keyof PaxFormData, value: string): void {
    const forms = this.paxForms().slice();
    forms[index] = { ...forms[index], [field]: value };
    this.paxForms.set(forms);
  }

  async savePaxDocs(): Promise<void> {
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

      await this.#svc.submitPax(this.booking()!.bookingReference, submissions);
      this.step.set('confirm');
    } catch {
      this.error.set('Failed to save travel documents. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  async completeCheckIn(): Promise<void> {
    const bookingRef = this.booking()!.bookingReference;
    const airport = this.departureAirport();

    this.loading.set(true);
    this.error.set('');

    try {
      const checkInResult = await this.#svc.completeCheckIn(bookingRef, airport);
      const docsResult = await this.#svc.getBoardingDocs(airport, checkInResult.checkedIn);
      this.boardingCards.set(docsResult.boardingCards);
      this.step.set('boarding');
    } catch {
      this.error.set('Failed to complete check-in. Please try again or contact a supervisor.');
    } finally {
      this.loading.set(false);
    }
  }

  reset(): void {
    this.step.set('search');
    this.booking.set(null);
    this.paxForms.set([]);
    this.boardingCards.set([]);
    this.error.set('');
    this.bookingRef.set('');
    this.firstName.set('');
    this.lastName.set('');
    this.departureAirport.set('');
  }

  goBack(): void {
    const s = this.step();
    if (s === 'pax') this.step.set('search');
    else if (s === 'confirm') this.step.set('pax');
    this.error.set('');
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
