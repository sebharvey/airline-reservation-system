import { Component, HostListener } from '@angular/core';
import { RouterLink, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { AIRPORTS, Airport } from '../../data/airports';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, FormsModule, CommonModule],
  templateUrl: './home.html',
  styleUrl: './home.css'
})
export class HomeComponent {
  tripType: 'one-way' | 'return' = 'one-way';
  origin = 'LHR';
  destination = '';
  departDate = '';
  returnDate = '';
  adults = 1;
  children = 0;

  today = new Date().toISOString().split('T')[0];

  readonly airports = AIRPORTS;

  // Combobox state
  originQuery = 'LHR';
  destinationQuery = '';
  showOriginDropdown = false;
  showDestinationDropdown = false;

  constructor(private router: Router) {}

  get isReturn(): boolean {
    return this.tripType === 'return';
  }

  // Filter airports by query (matches code or city, case-insensitive)
  filteredAirports(query: string): Airport[] {
    const q = query.trim().toLowerCase();
    if (!q) return this.airports;
    return this.airports.filter(a =>
      a.code.toLowerCase().includes(q) ||
      a.city.toLowerCase().includes(q) ||
      a.name.toLowerCase().includes(q)
    );
  }

  get originSuggestions(): Airport[] {
    return this.filteredAirports(this.originQuery);
  }

  get destinationSuggestions(): Airport[] {
    return this.filteredAirports(this.destinationQuery);
  }

  onOriginInput(): void {
    this.origin = '';
    this.showOriginDropdown = true;
  }

  onDestinationInput(): void {
    this.destination = '';
    this.showDestinationDropdown = true;
  }

  selectOrigin(airport: Airport): void {
    this.origin = airport.code;
    this.originQuery = airport.code;
    this.showOriginDropdown = false;
  }

  selectDestination(airport: Airport): void {
    this.destination = airport.code;
    this.destinationQuery = airport.code;
    this.showDestinationDropdown = false;
  }

  toggleOriginDropdown(): void {
    this.showOriginDropdown = !this.showOriginDropdown;
    if (this.showOriginDropdown) this.showDestinationDropdown = false;
  }

  toggleDestinationDropdown(): void {
    this.showDestinationDropdown = !this.showDestinationDropdown;
    if (this.showDestinationDropdown) this.showOriginDropdown = false;
  }

  swapAirports(): void {
    const tmpCode = this.origin;
    const tmpQuery = this.originQuery;
    this.origin = this.destination;
    this.originQuery = this.destinationQuery;
    this.destination = tmpCode;
    this.destinationQuery = tmpQuery;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.airport-combobox')) {
      this.showOriginDropdown = false;
      this.showDestinationDropdown = false;
    }
  }

  incrementAdults(): void {
    if (this.adults < 9) this.adults++;
  }

  decrementAdults(): void {
    if (this.adults > 1) this.adults--;
  }

  incrementChildren(): void {
    if (this.children < 9) this.children++;
  }

  decrementChildren(): void {
    if (this.children > 0) this.children--;
  }

  onSearch(): void {
    const params: Record<string, string> = {
      origin: this.origin,
      destination: this.destination,
      tripType: this.tripType,
      departDate: this.departDate,
      adults: String(this.adults),
      children: String(this.children),
    };
    if (this.isReturn && this.returnDate) {
      params['returnDate'] = this.returnDate;
    }
    this.router.navigate(['/search'], { queryParams: params });
  }
}
