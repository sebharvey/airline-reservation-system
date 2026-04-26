import { Component, computed, inject, signal } from '@angular/core';
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

type PaxStatus = 'pending' | 'checked-in' | 'failed';

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
  paxStatuses = signal<PaxStatus[]>([]);

  selectedPaxIndex = signal<number | null>(null);
  checkingInIndex = signal<number | null>(null);
  checkInError = signal('');

  boardingCards = signal<BoardingCard[]>([]);
  copiedBcbp = signal<string | null>(null);

  selectedPax = computed(() => {
    const i = this.selectedPaxIndex();
    return i !== null ? (this.paxForms()[i] ?? null) : null;
  });

  allAttempted = computed(() => {
    const s = this.paxStatuses();
    return s.length > 0 && s.every(st => st !== 'pending');
  });

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
    this.paxStatuses.set([]);
    this.selectedPaxIndex.set(null);
    this.boardingCards.set([]);
    this.checkInError.set('');

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
    this.checkInError.set('');
    this.paxForms.set([]);
    this.paxStatuses.set([]);
    this.selectedPaxIndex.set(null);
    this.boardingCards.set([]);

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
    this.paxStatuses.set(passengers.map(() => 'pending'));
    this.selectedPaxIndex.set(0);
  }

  selectPax(index: number): void {
    this.selectedPaxIndex.set(index);
    this.checkInError.set('');
  }

  updateDoc(index: number, field: keyof PaxFormData, value: string): void {
    const forms = this.paxForms().slice();
    forms[index] = { ...forms[index], [field]: value };
    this.paxForms.set(forms);
  }

  async checkInPax(index: number): Promise<void> {
    const pax = this.paxForms()[index];
    if (!pax) return;

    if (!pax.docNumber || !pax.issuingCountry || !pax.nationality || !pax.issueDate || !pax.expiryDate) {
      this.checkInError.set('Please complete all travel document fields before checking in.');
      return;
    }

    this.checkingInIndex.set(index);
    this.checkInError.set('');

    const submission: PaxSubmission = {
      ticketNumber: pax.ticketNumber,
      travelDocument: {
        type: pax.docType,
        number: pax.docNumber.toUpperCase(),
        issuingCountry: pax.issuingCountry.toUpperCase(),
        nationality: pax.nationality.toUpperCase(),
        issueDate: pax.issueDate,
        expiryDate: pax.expiryDate,
      },
    };

    try {
      const result = await this.#svc.adminCheckIn(
        this.booking()!.bookingReference,
        this.departureAirport(),
        [submission],
      );

      const statuses = this.paxStatuses().slice();
      statuses[index] = 'checked-in';
      this.paxStatuses.set(statuses);

      if (result.boardingCards.length > 0) {
        this.boardingCards.update(cards => [...cards, ...result.boardingCards]);
      }

      // Auto-advance to next pending passenger
      const nextPending = this.paxForms().findIndex((_, i) => i !== index && this.paxStatuses()[i] === 'pending');
      this.selectedPaxIndex.set(nextPending >= 0 ? nextPending : null);
    } catch {
      const statuses = this.paxStatuses().slice();
      statuses[index] = 'failed';
      this.paxStatuses.set(statuses);
      this.checkInError.set('Check-in failed. Please verify the document details and try again.');
    } finally {
      this.checkingInIndex.set(null);
    }
  }

  reset(): void {
    this.booking.set(null);
    this.paxForms.set([]);
    this.paxStatuses.set([]);
    this.selectedPaxIndex.set(null);
    this.boardingCards.set([]);
    this.departureAirports.set([]);
    this.departureAirport.set('');
    this.bookingRef.set('');
    this.error.set('');
    this.checkInError.set('');
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
