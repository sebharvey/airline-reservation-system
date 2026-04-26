import { Component, computed, inject, signal } from '@angular/core';
import {
  BoardingCard,
  CheckInService,
  LookupResponse,
  PaxSubmission,
  TimaticNote,
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

interface CheckInResult {
  success: boolean;
  errorMessage: string;
  givenName: string;
  surname: string;
  passengerTypeCode: string;
  ticketNumber: string;
  boardingCard: BoardingCard | null;
  timaticNotes: TimaticNote[];
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

  searchMode = signal<'bookingRef' | 'eTicket'>('bookingRef');
  bookingRef = signal('');
  eTicketNumber = signal('');
  departureAirports = signal<string[]>([]);
  departureAirport = signal('');

  booking = signal<LookupResponse | null>(null);
  paxForms = signal<PaxFormData[]>([]);
  paxStatuses = signal<PaxStatus[]>([]);

  selectedPaxIndex = signal<number | null>(null);
  checkingInIndex = signal<number | null>(null);
  checkInResult = signal<CheckInResult | null>(null);

  selectedPax = computed(() => {
    const i = this.selectedPaxIndex();
    return i !== null ? (this.paxForms()[i] ?? null) : null;
  });

  allAttempted = computed(() => {
    const s = this.paxStatuses();
    return s.length > 0 && s.every(st => st !== 'pending');
  });

  docComplete = computed(() => {
    const p = this.selectedPax();
    return !!p && !!p.docNumber && !!p.issuingCountry && !!p.nationality && !!p.issueDate && !!p.expiryDate;
  });

  async findBooking(): Promise<void> {
    const mode = this.searchMode();
    const ref = this.bookingRef().trim().toUpperCase();
    const ticket = this.eTicketNumber().trim();

    if (mode === 'bookingRef' && !ref) {
      this.error.set('Booking reference is required.');
      return;
    }
    if (mode === 'eTicket' && !ticket) {
      this.error.set('E-ticket number is required.');
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

    try {
      const result = mode === 'eTicket'
        ? await this.#svc.lookupByETicket(ticket)
        : await this.#svc.lookup(ref);
      if (!result.departureAirports.length) {
        this.error.set('No eligible departures found for this booking.');
        return;
      }
      this.booking.set(result);
      this.departureAirports.set(result.departureAirports);
    } catch {
      this.error.set('Booking not found. Please check the details and try again.');
    } finally {
      this.loading.set(false);
    }
  }

  selectDeparture(airport: string): void {
    if (!airport) return;
    this.departureAirport.set(airport);
    this.error.set('');
    this.paxForms.set([]);
    this.paxStatuses.set([]);
    this.selectedPaxIndex.set(null);

    const lookupResult = this.booking();
    if (!lookupResult) return;

    const passengers = this.#svc.extractPassengers(
      lookupResult.orderDetail,
      lookupResult.tickets,
      airport,
    );
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

    const statuses: PaxStatus[] = passengers.map(p => p.alreadyCheckedIn ? 'checked-in' : 'pending');
    this.paxStatuses.set(statuses);

    const firstPending = statuses.findIndex(s => s === 'pending');
    this.selectedPaxIndex.set(firstPending >= 0 ? firstPending : 0);
  }

  selectPax(index: number): void {
    this.selectedPaxIndex.set(index);
  }

  updateDoc(index: number, field: keyof PaxFormData, value: string): void {
    const forms = this.paxForms().slice();
    forms[index] = { ...forms[index], [field]: value };
    this.paxForms.set(forms);
  }

  async checkInPax(index: number): Promise<void> {
    const pax = this.paxForms()[index];
    if (!pax) return;

    if (!pax.docNumber || !pax.issuingCountry || !pax.nationality || !pax.issueDate || !pax.expiryDate) return;

    this.checkingInIndex.set(index);

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
      const response = await this.#svc.adminCheckIn(
        this.booking()!.bookingReference,
        this.departureAirport(),
        [submission],
      );

      const statuses = this.paxStatuses().slice();
      statuses[index] = 'checked-in';
      this.paxStatuses.set(statuses);

      const boardingCard = response.boardingCards.find(c => c.ticketNumber === pax.ticketNumber) ?? response.boardingCards[0] ?? null;
      const timaticNotes = (response.timaticNotes ?? []).filter(n => n.ticketNumber === pax.ticketNumber);
      this.checkInResult.set({ success: true, errorMessage: '', givenName: pax.givenName, surname: pax.surname, passengerTypeCode: pax.passengerTypeCode, ticketNumber: pax.ticketNumber, boardingCard, timaticNotes });

      const nextPending = this.paxForms().findIndex((_, i) => i !== index && this.paxStatuses()[i] === 'pending');
      this.selectedPaxIndex.set(nextPending >= 0 ? nextPending : null);
    } catch (err: unknown) {
      const statuses = this.paxStatuses().slice();
      statuses[index] = 'failed';
      this.paxStatuses.set(statuses);

      const message = this.#extractErrorMessage(err);
      const timaticNotes = this.#extractTimaticNotes(err).filter(n => n.ticketNumber === pax.ticketNumber);
      const errorMessage = message || 'Check-in failed. Please verify the document details and try again.';
      this.checkInResult.set({ success: false, errorMessage, givenName: pax.givenName, surname: pax.surname, passengerTypeCode: pax.passengerTypeCode, ticketNumber: pax.ticketNumber, boardingCard: null, timaticNotes });
    } finally {
      this.checkingInIndex.set(null);
    }
  }

  #extractTimaticNotes(err: unknown): TimaticNote[] {
    if (err && typeof err === 'object') {
      const body = (err as Record<string, unknown>)['error'];
      if (body && typeof body === 'object') {
        const notes = (body as Record<string, unknown>)['timaticNotes'];
        if (Array.isArray(notes)) return notes as TimaticNote[];
      }
    }
    return [];
  }

  #extractErrorMessage(err: unknown): string {
    if (err && typeof err === 'object') {
      const e = err as Record<string, unknown>;
      // Angular HttpErrorResponse
      if (e['error'] && typeof e['error'] === 'object') {
        const body = e['error'] as Record<string, unknown>;
        if (typeof body['error'] === 'string') return body['error'];
        if (typeof body['message'] === 'string') return body['message'];
      }
      if (typeof e['message'] === 'string') return e['message'];
    }
    return '';
  }

  fillTestData(index: number): void {
    const pax = this.paxForms()[index];
    if (!pax) return;

    const countries = ['GBR', 'USA', 'FRA', 'DEU', 'ESP', 'ITA', 'AUS', 'CAN', 'NLD', 'SWE'];
    const rand = <T>(arr: T[]): T => arr[Math.floor(Math.random() * arr.length)];
    const country = rand(countries);

    const alpha = 'ABCDEFGHJKLMNPRSTUVWXYZ';
    const digits = '0123456789';
    const pick = (s: string) => s[Math.floor(Math.random() * s.length)];
    const docNumber =
      pick(alpha) + pick(alpha) +
      pick(digits) + pick(digits) + pick(digits) + pick(digits) +
      pick(digits) + pick(digits) + pick(digits);

    const pad2 = (n: number) => String(n).padStart(2, '0');
    const issueYear = 2021 + Math.floor(Math.random() * 4);
    const expiryYear = 2031 + Math.floor(Math.random() * 5);
    const issueDate = `${issueYear}-${pad2(Math.floor(Math.random() * 12) + 1)}-${pad2(Math.floor(Math.random() * 28) + 1)}`;
    const expiryDate = `${expiryYear}-${pad2(Math.floor(Math.random() * 12) + 1)}-${pad2(Math.floor(Math.random() * 28) + 1)}`;

    const forms = this.paxForms().slice();
    forms[index] = {
      ...pax,
      docNumber: pax.docNumber || docNumber,
      issuingCountry: pax.issuingCountry || country,
      nationality: pax.nationality || country,
      issueDate: pax.issueDate || issueDate,
      expiryDate: pax.expiryDate || expiryDate,
    };
    this.paxForms.set(forms);
  }

  reset(): void {
    this.booking.set(null);
    this.paxForms.set([]);
    this.paxStatuses.set([]);
    this.selectedPaxIndex.set(null);
    this.departureAirports.set([]);
    this.departureAirport.set('');
    this.bookingRef.set('');
    this.eTicketNumber.set('');
    this.searchMode.set('bookingRef');
    this.error.set('');
    this.checkInResult.set(null);
  }

  closeModal(): void {
    this.checkInResult.set(null);
  }

  paxTypeLabel(code: string): string {
    const labels: Record<string, string> = { ADT: 'Adult', CHD: 'Child', INF: 'Infant', YTH: 'Youth' };
    return labels[code] ?? code;
  }

  cabinLabel(code: string): string {
    const labels: Record<string, string> = { F: 'First', J: 'Business', W: 'Premium Economy', Y: 'Economy' };
    return labels[code] ?? code;
  }
}
