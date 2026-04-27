import { Component } from '@angular/core';
import { RouterLink, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { BookingStateService } from '../../services/booking-state.service';
import { BookingType } from '../../models/order.model';
import { AirportComboboxComponent } from '../../components/airport-combobox/airport-combobox';
import { LucideAngularModule } from 'lucide-angular';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, FormsModule, CommonModule, AirportComboboxComponent, LucideAngularModule],
  templateUrl: './home.html',
  styleUrl: './home.css'
})
export class HomeComponent {
  private _tripType: 'one-way' | 'return' = 'one-way';

  get tripType(): 'one-way' | 'return' {
    return this._tripType;
  }

  set tripType(value: 'one-way' | 'return') {
    this._tripType = value;
    if (value === 'return') {
      this.setDefaultReturnDate();
    }
  }

  origin = 'LHR';
  destination = 'JFK';
  departDate = '';
  returnDate = '';
  adults = 1;
  children = 0;
  bookingType: BookingType = 'Revenue';

  today = new Date().toISOString().split('T')[0];

  constructor(private router: Router, private bookingState: BookingStateService) {
    const tomorrow = new Date();
    tomorrow.setDate(tomorrow.getDate() + 1);
    this.departDate = tomorrow.toISOString().split('T')[0];
  }

  onDepartDateChange(): void {
    if (this.isReturn) {
      this.setDefaultReturnDate();
    }
  }

  private setDefaultReturnDate(): void {
    if (!this.returnDate || this.returnDate <= this.departDate) {
      const depart = new Date(this.departDate);
      depart.setDate(depart.getDate() + 14);
      this.returnDate = depart.toISOString().split('T')[0];
    }
  }

  get isReturn(): boolean {
    return this.tripType === 'return';
  }

  swapAirports(): void {
    const tmp = this.origin;
    this.origin = this.destination;
    this.destination = tmp;
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
    this.bookingState.setBookingType(this.bookingType);
    const params: Record<string, string> = {
      origin: this.origin,
      destination: this.destination,
      tripType: this.tripType,
      departDate: this.departDate,
      adults: String(this.adults),
      children: String(this.children),
      bookingType: this.bookingType,
    };
    if (this.isReturn && this.returnDate) {
      params['returnDate'] = this.returnDate;
    }
    this.router.navigate(['/search'], { queryParams: params });
  }
}
