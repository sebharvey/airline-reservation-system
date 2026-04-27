import { Component, OnInit, signal, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BookingStateService } from '../../../services/booking-state.service';
import { LoyaltyApiService } from '../../../services/loyalty-api.service';
import { LoyaltyStateService } from '../../../services/loyalty-state.service';
import { LucideAngularModule } from 'lucide-angular';

@Component({
  selector: 'app-reward-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, LucideAngularModule],
  templateUrl: './reward-login.html',
  styleUrl: './reward-login.css'
})
export class RewardLoginComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly bookingState = inject(BookingStateService);
  private readonly loyaltyApi = inject(LoyaltyApiService);
  private readonly loyaltyState = inject(LoyaltyStateService);

  readonly basket = this.bookingState.basket;

  email = signal('');
  password = signal('');
  submitted = signal(false);
  loading = signal(false);
  errorMessage = signal('');
  insufficientPoints = signal(false);

  ngOnInit(): void {
    if (!this.basket()) {
      this.router.navigate(['/']);
      return;
    }
    // If already logged in, check points and proceed
    if (this.loyaltyState.isLoggedIn()) {
      this.checkPointsAndProceed();
    }
  }

  onLogin(): void {
    this.submitted.set(true);
    this.errorMessage.set('');
    this.insufficientPoints.set(false);

    if (!this.email().trim() || !this.password().trim()) return;

    this.loading.set(true);

    this.loyaltyApi.login({
      email: this.email().trim(),
      password: this.password()
    }).subscribe({
      next: (session) => {
        this.loyaltyState.setSession(session);
        this.loading.set(false);
        this.checkPointsAndProceed();
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.message || 'Invalid email address or password.');
      }
    });
  }

  private checkPointsAndProceed(): void {
    const customer = this.loyaltyState.currentCustomer();
    const b = this.basket();
    if (!customer || !b) return;

    const requiredPoints = b.totalPointsAmount;
    if (customer.pointsBalance < requiredPoints) {
      this.insufficientPoints.set(true);
      this.errorMessage.set(
        `Insufficient points. You need ${requiredPoints.toLocaleString()} pts but your balance is ${customer.pointsBalance.toLocaleString()} pts.`
      );
      return;
    }

    // Set loyalty number on basket and proceed
    this.bookingState.setLoyaltyNumber(customer.loyaltyNumber);
    this.router.navigate(['/booking/flight-summary']);
  }

  get requiredPoints(): number {
    return this.basket()?.totalPointsAmount ?? 0;
  }

  formatPrice(amount: number, currency: string): string {
    return `${currency} ${amount.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  formatDateTime(dt: string): string {
    if (!dt) return '';
    return new Date(dt).toLocaleString('en-GB', {
      day: 'numeric', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  }
}
