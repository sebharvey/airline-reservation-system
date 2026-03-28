import { Component, inject, signal, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { LoyaltyApiService } from '../../services/loyalty-api.service';
import { LoyaltyStateService } from '../../services/loyalty-state.service';

@Component({
  selector: 'app-loyalty-login',
  standalone: true,
  imports: [FormsModule, CommonModule, RouterLink],
  templateUrl: './loyalty-login.html',
  styleUrl: './loyalty-login.css'
})
export class LoyaltyLoginComponent implements OnInit {
  private readonly loyaltyApi = inject(LoyaltyApiService);
  private readonly loyaltyState = inject(LoyaltyStateService);
  private readonly router = inject(Router);

  email = signal('');
  password = signal('');
  showPassword = signal(false);
  loading = signal(false);
  errorMessage = signal<string | null>(null);

  ngOnInit(): void {
    if (this.loyaltyState.isLoggedIn()) {
      this.router.navigate(['/loyalty/account']);
    }
  }

  setEmail(value: string): void {
    this.email.set(value);
  }

  setPassword(value: string): void {
    this.password.set(value);
  }

  toggleShowPassword(): void {
    this.showPassword.update(v => !v);
  }

  onSubmit(): void {
    this.errorMessage.set(null);
    if (!this.email() || !this.password()) {
      this.errorMessage.set('Please enter your email and password.');
      return;
    }
    this.loading.set(true);
    this.loyaltyApi.login({ email: this.email(), password: this.password() }).subscribe({
      next: (session) => {
        this.loyaltyState.setSession(session);
        this.loading.set(false);
        this.router.navigate(['/loyalty/account']);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.message ?? 'Login failed. Please try again.');
      }
    });
  }
}
