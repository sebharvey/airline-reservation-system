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

    const passengerIds = this.checkInState.selectedPassengerIds();
    const inventoryIds = order.flightSegments.map(s => s.inventoryId);
    const passengers = passengerIds.map(id => ({ passengerId: id, inventoryIds }));

    this.submitting.set(true);
    this.errorMessage.set('');

    this.retailApi.submitOciCheckIn(order.bookingReference, passengers).subscribe({
      next: (passes) => {
        this.checkInState.setBoardingPasses(passes);
        this.submitting.set(false);
        this.router.navigate(['/check-in/boarding-pass']);
      },
      error: (err: { message?: string }) => {
        this.submitting.set(false);
        this.errorMessage.set(err?.message ?? 'Check-in failed. Please try again or visit the airport desk.');
      }
    });
  }
}
