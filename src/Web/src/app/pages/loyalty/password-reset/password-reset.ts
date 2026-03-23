import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { LoyaltyApiService } from '../../../services/loyalty-api.service';

export type ResetStep = 'request' | 'reset' | 'done';

@Component({
  selector: 'app-password-reset',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './password-reset.html',
  styleUrl: './password-reset.css'
})
export class PasswordResetComponent {
  private readonly loyaltyApi = inject(LoyaltyApiService);

  step = signal<ResetStep>('request');

  // Step 1
  email = signal('');
  requestLoading = signal(false);
  requestError = signal<string | null>(null);

  // Step 2
  token = signal('');
  newPassword = signal('');
  confirmNewPassword = signal('');
  showNewPassword = signal(false);
  showConfirmNewPassword = signal(false);
  resetLoading = signal(false);
  resetError = signal<string | null>(null);

  setEmail(v: string): void { this.email.set(v); }
  setToken(v: string): void { this.token.set(v.replace(/\D/g, '').substring(0, 6)); }
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
        // Always move to step 2 (security: don't reveal if email exists)
        this.step.set('reset');
      },
      error: () => {
        this.requestLoading.set(false);
        // Still move forward — don't leak account existence
        this.step.set('reset');
      }
    });
  }

  submitReset(): void {
    this.resetError.set(null);

    if (this.token().length !== 6) {
      this.resetError.set('Please enter the 6-digit code from your email.');
      return;
    }

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
    this.loyaltyApi.resetPassword(this.token(), this.newPassword()).subscribe({
      next: () => {
        this.resetLoading.set(false);
        this.step.set('done');
      },
      error: (err: { message?: string }) => {
        this.resetLoading.set(false);
        this.resetError.set(err?.message ?? 'Reset failed. Please try again.');
      }
    });
  }
}
