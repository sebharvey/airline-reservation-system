import { Component, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../services/retail-api.service';

interface DemoHint {
  ref: string;
  name: string;
  givenName: string;
  surname: string;
}

@Component({
  selector: 'app-manage-booking',
  standalone: true,
  imports: [FormsModule, CommonModule, RouterLink],
  templateUrl: './manage-booking.html',
  styleUrl: './manage-booking.css'
})
export class ManageBookingComponent {
  bookingReference = signal('');
  givenName = signal('');
  surname = signal('');
  loading = signal(false);
  errorMessage = signal('');

  readonly demoHints: DemoHint[] = [
    { ref: 'AB1234', name: 'Alex Taylor', givenName: 'Alex', surname: 'Taylor' },
    { ref: 'CD5678', name: 'Sam Morgan', givenName: 'Sam', surname: 'Morgan' }
  ];

  constructor(
    private retailApi: RetailApiService,
    private router: Router
  ) {}

  onReferenceInput(value: string): void {
    this.bookingReference.set(value.toUpperCase());
  }

  fillDemo(hint: DemoHint): void {
    this.bookingReference.set(hint.ref);
    this.givenName.set(hint.givenName);
    this.surname.set(hint.surname);
    this.errorMessage.set('');
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

    this.retailApi.retrieveOrder({
      bookingReference: this.bookingReference().trim(),
      givenName: this.givenName().trim(),
      surname: this.surname().trim()
    }).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/manage-booking/detail'], {
          queryParams: {
            bookingRef: this.bookingReference().trim(),
            givenName: this.givenName().trim(),
            surname: this.surname().trim()
          }
        });
      },
      error: (err: { message?: string }) => {
        this.loading.set(false);
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking. Please try again.');
      }
    });
  }
}
