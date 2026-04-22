import { Component, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../services/retail-api.service';
import { CheckInStateService } from '../../services/check-in-state.service';
import { AirportComboboxComponent } from '../../components/airport-combobox/airport-combobox';

@Component({
  selector: 'app-check-in',
  standalone: true,
  imports: [FormsModule, CommonModule, RouterLink, AirportComboboxComponent],
  templateUrl: './check-in.html',
  styleUrl: './check-in.css'
})
export class CheckInComponent {
  bookingReference = signal('');
  givenName = signal('');
  surname = signal('');
  departureAirport = signal('');
  loading = signal(false);
  errorMessage = signal('');

  constructor(
    private retailApi: RetailApiService,
    private checkInState: CheckInStateService,
    private router: Router
  ) {}

  onReferenceInput(value: string): void {
    this.bookingReference.set(value.toUpperCase());
  }

  get isFormValid(): boolean {
    return (
      this.bookingReference().trim().length >= 3 &&
      this.givenName().trim().length >= 1 &&
      this.surname().trim().length >= 1 &&
      this.departureAirport().trim().length === 3
    );
  }

  onSubmit(): void {
    if (!this.isFormValid || this.loading()) return;

    this.loading.set(true);
    this.errorMessage.set('');
    this.checkInState.clear();

    this.retailApi.retrieveOciOrder({
      bookingReference: this.bookingReference().trim(),
      givenName: this.givenName().trim(),
      surname: this.surname().trim(),
      departureAirport: this.departureAirport().trim()
    }).subscribe({
      next: (order) => {
        this.checkInState.setCurrentOrder(order);
        this.checkInState.setDepartureAirport(this.departureAirport().trim());

        this.retailApi.createCheckInBasket(
          order.bookingReference,
          order.passengers.length,
          order.currency
        ).subscribe({
          next: (res) => {
            this.checkInState.setBasketId(res.basketId);
            this.loading.set(false);
            this.router.navigate(['/check-in/details']);
          },
          error: () => {
            // Basket creation is best-effort — continue the journey without one
            this.loading.set(false);
            this.router.navigate(['/check-in/details']);
          }
        });
      },
      error: (err: { message?: string }) => {
        this.loading.set(false);
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking. Please check your details.');
      }
    });
  }
}
