import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { switchMap } from 'rxjs/operators';
import { of } from 'rxjs';
import { BookingStateService } from '../../../services/booking-state.service';
import { LoyaltyStateService } from '../../../services/loyalty-state.service';
import { RetailApiService } from '../../../services/retail-api.service';
import { Passenger, BasketSsrSelection } from '../../../models/order.model';

interface PassengerForm {
  passengerId: string;
  type: 'ADT' | 'CHD';
  label: string;
  givenName: string;
  surname: string;
  dateOfBirth: string;
  gender: 'Male' | 'Female' | 'Other' | '';
  loyaltyNumber: string;
  email: string;
  phone: string;
  wheelchairRequested: boolean;
}

@Component({
  selector: 'app-passengers',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './passengers.html',
  styleUrl: './passengers.css'
})
export class PassengersComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly bookingState = inject(BookingStateService);
  private readonly loyaltyState = inject(LoyaltyStateService);
  private readonly retailApi = inject(RetailApiService);

  readonly basket = this.bookingState.basket;
  readonly isRewardBooking = this.bookingState.isRewardBooking;
  readonly adultCount = this.bookingState.adultCount;
  readonly childCount = this.bookingState.childCount;

  forms = signal<PassengerForm[]>([]);
  submitted = signal(false);
  countdown = signal('');
  saving = signal(false);
  saveError = signal('');
  prefilled = signal(false);

  private countdownInterval: ReturnType<typeof setInterval> | null = null;

  readonly genderOptions: Array<{ value: 'Male' | 'Female' | 'Other'; label: string }> = [
    { value: 'Male', label: 'Male' },
    { value: 'Female', label: 'Female' },
    { value: 'Other', label: 'Other' }
  ];

  readonly todayStr = new Date().toISOString().split('T')[0];

  readonly maxChildDob = computed(() => {
    const d = new Date();
    d.setFullYear(d.getFullYear() - 2);
    return d.toISOString().split('T')[0];
  });

  readonly minAdultDob = computed(() => {
    const d = new Date();
    d.setFullYear(d.getFullYear() - 120);
    return d.toISOString().split('T')[0];
  });

  readonly maxAdultDob = computed(() => {
    const d = new Date();
    d.setFullYear(d.getFullYear() - 16);
    return d.toISOString().split('T')[0];
  });

  ngOnInit(): void {
    if (!this.basket()) {
      this.router.navigate(['/']);
      return;
    }
    this.buildForms();
    this.autofillFromLoyalty();
    this.startCountdown();
  }

  ngOnDestroy(): void {
    if (this.countdownInterval) clearInterval(this.countdownInterval);
  }

  private buildForms(): void {
    const passengers: PassengerForm[] = [];
    const adults = this.adultCount();
    const children = this.childCount();

    for (let i = 0; i < adults; i++) {
      passengers.push({
        passengerId: `PAX-${i + 1}`,
        type: 'ADT',
        label: adults === 1 ? 'Adult' : `Adult ${i + 1}`,
        givenName: '',
        surname: '',
        dateOfBirth: '',
        gender: '',
        loyaltyNumber: '',
        email: '',
        phone: '',
        wheelchairRequested: false
      });
    }

    for (let i = 0; i < children; i++) {
      passengers.push({
        passengerId: `PAX-${adults + i + 1}`,
        type: 'CHD',
        label: children === 1 ? 'Child' : `Child ${i + 1}`,
        givenName: '',
        surname: '',
        dateOfBirth: '',
        gender: '',
        loyaltyNumber: '',
        email: '',
        phone: '',
        wheelchairRequested: false
      });
    }

    this.forms.set(passengers);
  }

  /** Autofill the lead passenger from loyalty account details when the user is logged in. */
  private autofillFromLoyalty(): void {
    if (!this.loyaltyState.isLoggedIn()) return;
    const customer = this.loyaltyState.currentCustomer();
    if (!customer) return;

    const forms = this.forms();
    if (forms.length === 0) return;

    const leadPax = forms[0];
    leadPax.givenName = customer.givenName;
    leadPax.surname = customer.surname;
    leadPax.dateOfBirth = customer.dateOfBirth;
    leadPax.gender = (customer.gender as 'Male' | 'Female' | 'Other') || '';
    leadPax.email = customer.email;
    leadPax.phone = customer.phone;
    leadPax.loyaltyNumber = customer.loyaltyNumber;
    this.forms.set([...forms]);
    this.prefilled.set(true);
  }

  private startCountdown(): void {
    const basket = this.basket();
    if (!basket?.ticketingTimeLimit) return;
    const limit = new Date(basket.ticketingTimeLimit).getTime();

    const update = () => {
      const remaining = limit - Date.now();
      if (remaining <= 0) {
        this.countdown.set('Expired');
        if (this.countdownInterval) clearInterval(this.countdownInterval);
        return;
      }
      const h = Math.floor(remaining / 3600000);
      const m = Math.floor((remaining % 3600000) / 60000);
      const s = Math.floor((remaining % 60000) / 1000);
      this.countdown.set(
        `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`
      );
    };

    update();
    this.countdownInterval = setInterval(update, 1000);
  }

  isFirstAdult(index: number): boolean {
    return index === 0 && this.forms()[index]?.type === 'ADT';
  }

  isFormValid(): boolean {
    return this.forms().every(f => {
      const base = f.givenName.trim() && f.surname.trim() && f.dateOfBirth && f.gender;
      if (!base) return false;
      if (this.isFirstAdult(this.forms().indexOf(f))) {
        return !!(f.email.trim() && f.phone.trim());
      }
      return true;
    });
  }

  onContinue(): void {
    this.submitted.set(true);
    if (!this.isFormValid()) return;

    const passengers: Passenger[] = this.forms().map((f, i) => ({
      passengerId: f.passengerId,
      type: f.type,
      givenName: f.givenName.trim(),
      surname: f.surname.trim(),
      dateOfBirth: f.dateOfBirth,
      gender: f.gender,
      loyaltyNumber: f.loyaltyNumber.trim() || null,
      contacts: this.isFirstAdult(i)
        ? { email: f.email.trim(), phone: f.phone.trim() }
        : null,
      travelDocument: null
    }));

    const basket = this.basket();
    const basketId = basket?.basketId;
    if (!basketId) return;

    const ssrSelections: BasketSsrSelection[] = [];
    for (const f of this.forms()) {
      if (f.wheelchairRequested) {
        for (const offer of (basket?.flightOffers ?? [])) {
          ssrSelections.push({ ssrCode: 'WCHR', passengerRef: f.passengerId, segmentRef: offer.inventoryId });
        }
      }
    }

    this.saving.set(true);
    this.saveError.set('');

    this.retailApi.updateBasketPassengers(basketId, passengers).pipe(
      switchMap(() => ssrSelections.length > 0
        ? this.retailApi.updateBasketSsrs(basketId, ssrSelections)
        : of(undefined))
    ).subscribe({
      next: () => {
        this.bookingState.setPassengers(passengers);
        this.bookingState.setSsrSelections(ssrSelections);
        this.saving.set(false);
        this.router.navigate(['/booking/seats']);
      },
      error: () => {
        this.saving.set(false);
        this.saveError.set('Failed to save passenger details. Please try again.');
      }
    });
  }

  formatDateTime(dt: string): string {
    if (!dt) return '';
    return new Date(dt).toLocaleString('en-GB', {
      day: 'numeric', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  }

  formatPrice(amount: number, currency: string): string {
    return `${currency} ${amount.toFixed(2)}`;
  }

  trackByIndex(index: number): number {
    return index;
  }
}
