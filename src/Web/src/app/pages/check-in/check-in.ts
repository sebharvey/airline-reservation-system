import { Component, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../services/retail-api.service';
import { CheckInStateService } from '../../services/check-in-state.service';

@Component({
  selector: 'app-check-in',
  standalone: true,
  imports: [FormsModule, CommonModule, RouterLink],
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

  onAirportInput(value: string): void {
    this.departureAirport.set(value.toUpperCase().replace(/[^A-Z]/g, '').substring(0, 3));
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
        this.loading.set(false);
        this.checkInState.setCurrentOrder(order);
        this.checkInState.setDepartureAirport(this.departureAirport().trim());
        this.router.navigate(['/check-in/details']);
      },
      error: (err: { message?: string }) => {
        this.loading.set(false);
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking. Please check your details.');
      }
    });
  }
}
