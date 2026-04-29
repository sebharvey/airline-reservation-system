import { Component, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../services/retail-api.service';

@Component({
  selector: 'app-manage-booking',
  standalone: true,
  imports: [FormsModule, CommonModule],
  templateUrl: './manage-booking.html',
  styleUrl: './manage-booking.css'
})
export class ManageBookingComponent {
  bookingReference = signal('');
  givenName = signal('');
  surname = signal('');
  loading = signal(false);
  errorMessage = signal('');

  constructor(
    private retailApi: RetailApiService,
    private router: Router
  ) {}

  onReferenceInput(value: string): void {
    this.bookingReference.set(value.toUpperCase());
  }

  get isFormValid(): boolean {
    return (
      this.bookingReference().trim().length >= 3 &&
      this.givenName().trim().length >= 1 &&
      this.surname().trim().length >= 1
    );
  }

  onSubmit(): void {
    if (!this.isFormValid || this.loading()) return;

    this.loading.set(true);
    this.errorMessage.set('');

    this.retailApi.validateOrder({
      bookingReference: this.bookingReference().trim(),
      givenName: this.givenName().trim(),
      surname: this.surname().trim()
    }).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/manage-booking/detail'], {
          state: { givenName: this.givenName().trim(), surname: this.surname().trim() }
        });
      },
      error: (err: { message?: string }) => {
        this.loading.set(false);
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking. Please try again.');
      }
    });
  }
}
