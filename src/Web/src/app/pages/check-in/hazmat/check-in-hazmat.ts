import { Component, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { CheckInStateService } from '../../../services/check-in-state.service';
import { RetailApiService } from '../../../services/retail-api.service';

@Component({
  selector: 'app-check-in-hazmat',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './check-in-hazmat.html',
  styleUrl: './check-in-hazmat.css'
})
export class CheckInHazmatComponent implements OnInit {
  submitting = signal(false);
  errorMessage = signal('');

  constructor(
    private router: Router,
    private checkInState: CheckInStateService,
    private retailApi: RetailApiService
  ) {}

  ngOnInit(): void {
    const order = this.checkInState.currentOrder();
    if (!order) {
      this.router.navigate(['/check-in']);
    }
  }

  confirm(): void {
    if (this.submitting()) return;

    const order = this.checkInState.currentOrder();
    if (!order) {
      this.router.navigate(['/check-in']);
      return;
    }

    const departureAirport = this.checkInState.departureAirport();
    if (!departureAirport) {
      this.router.navigate(['/check-in']);
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set('');

    this.retailApi.submitOciCheckIn(order.bookingReference, departureAirport).subscribe({
      next: (result) => {
        this.submitting.set(false);
        this.checkInState.setCheckedInTicketNumbers(result.checkedIn);
        if (this.checkInState.totalPaymentAmount() > 0) {
          this.router.navigate(['/check-in/payment']);
        } else {
          this.router.navigate(['/check-in/boarding-pass']);
        }
      },
      error: (err: { message?: string }) => {
        this.submitting.set(false);
        this.errorMessage.set(err?.message ?? 'Check-in failed. Please try again or visit the airport desk.');
      }
    });
  }
}
