import { Component, inject, signal, OnInit } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { LoyaltyApiService } from '../../../services/loyalty-api.service';
import { LucideAngularModule } from 'lucide-angular';

export type ResetStep = 'request' | 'check-email' | 'reset' | 'done';

@Component({
  selector: 'app-password-reset',
  standalone: true,
  imports: [CommonModule, RouterLink, LucideAngularModule],
  templateUrl: './password-reset.html',
  styleUrl: './password-reset.css'
})
export class PasswordResetComponent implements OnInit {
  private readonly loyaltyApi = inject(LoyaltyApiService);
  private readonly route = inject(ActivatedRoute);

  step = signal<ResetStep>('request');

  // Step 'request'
  email = signal('');
  requestLoading = signal(false);
  requestError = signal<string | null>(null);

  // Step 'reset' — token supplied via URL query param
  resetToken = signal('');
  newPassword = signal('');
  confirmNewPassword = signal('');
  showNewPassword = signal(false);
  showConfirmNewPassword = signal(false);
  resetLoading = signal(false);
  resetError = signal<string | null>(null);

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token');
    if (token) {
      this.resetToken.set(token);
      this.step.set('reset');
    }
  }

  setEmail(v: string): void { this.email.set(v); }
  setNewPassword(v: string): void { this.newPassword.set(v); }
  setConfirmNewPassword(v: string): void { this.confirmNewPassword.set(v); }
  toggleShowNewPassword(): void { this.showNewPassword.update(v => !v); }
  toggleShowConfirmNewPassword(): void { this.showConfirmNewPassword.update(v => !v); }

  submitRequest(): void {
    this.requestError.set(null);
    if (!this.email()) {
      this.requestError.set('Please enter your email address.');
      return;
    }
    this.requestLoading.set(true);
    this.loyaltyApi.requestPasswordReset(this.email()).subscribe({
      next: () => {
        this.requestLoading.set(false);
        // Always advance — security: don't reveal whether the email is registered
        this.step.set('check-email');
      },
      error: () => {
        this.requestLoading.set(false);
        this.step.set('check-email');
      }
    });
  }

  submitReset(): void {
    this.resetError.set(null);

    if (!this.newPassword()) {
      this.resetError.set('Please enter a new password.');
      return;
    }

    if (this.newPassword().length < 8) {
      this.resetError.set('Password must be at least 8 characters long.');
      return;
    }

    if (this.newPassword() !== this.confirmNewPassword()) {
      this.resetError.set('Passwords do not match.');
      return;
    }

    this.resetLoading.set(true);
    this.loyaltyApi.resetPassword(this.resetToken(), this.newPassword()).subscribe({
      next: () => {
        this.resetLoading.set(false);
        this.step.set('done');
      },
      error: (err: { message?: string }) => {
        this.resetLoading.set(false);
        this.resetError.set(
          err?.message ?? 'Reset failed. The link may have expired or already been used. Please request a new one.'
        );
      }
    });
  }
}
