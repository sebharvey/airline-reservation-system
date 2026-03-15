import { Component } from '@angular/core';
import { RouterLink, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, FormsModule, CommonModule],
  templateUrl: './home.html',
  styleUrl: './home.css'
})
export class HomeComponent {
  tripType: 'one-way' | 'return' = 'one-way';
  origin = '';
  destination = '';
  departDate = '';
  returnDate = '';
  adults = 1;
  children = 0;

  today = new Date().toISOString().split('T')[0];

  constructor(private router: Router) {}

  get isReturn(): boolean {
    return this.tripType === 'return';
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
      origin: this.origin.trim().toUpperCase(),
      destination: this.destination.trim().toUpperCase(),
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
