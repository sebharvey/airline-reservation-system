import { Component, HostListener, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../services/retail-api.service';
import { CheckInStateService } from '../../services/check-in-state.service';
import { Airport, AIRPORTS } from '../../data/airports';

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

  airports = AIRPORTS;
  airportQuery = '';
  showAirportDropdown = false;

  constructor(
    private retailApi: RetailApiService,
    private checkInState: CheckInStateService,
    private router: Router
  ) {}

  get airportSuggestions(): Airport[] {
    const q = this.airportQuery.trim().toLowerCase();
    if (!q) return this.airports;
    return this.airports.filter(a =>
      a.code.toLowerCase().includes(q) ||
      a.city.toLowerCase().includes(q) ||
      a.name.toLowerCase().includes(q)
    );
  }

  onAirportQueryInput(): void {
    this.departureAirport.set('');
    this.showAirportDropdown = true;
  }

  selectAirport(airport: Airport): void {
    this.departureAirport.set(airport.code);
    this.airportQuery = airport.code;
    this.showAirportDropdown = false;
  }

  toggleAirportDropdown(): void {
    this.showAirportDropdown = !this.showAirportDropdown;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.airport-combobox')) {
      this.showAirportDropdown = false;
    }
  }

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
