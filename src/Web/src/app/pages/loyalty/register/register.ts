import { Component, inject, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { LoyaltyApiService } from '../../../services/loyalty-api.service';
import { LoyaltyStateService } from '../../../services/loyalty-state.service';

export interface Country {
  code: string;
  name: string;
}

export const COUNTRIES: Country[] = [
  { code: 'GB', name: 'United Kingdom' },
  { code: 'US', name: 'United States' },
  { code: 'CA', name: 'Canada' },
  { code: 'AU', name: 'Australia' },
  { code: 'DE', name: 'Germany' },
  { code: 'FR', name: 'France' },
  { code: 'IT', name: 'Italy' },
  { code: 'ES', name: 'Spain' },
  { code: 'NL', name: 'Netherlands' },
  { code: 'SE', name: 'Sweden' },
  { code: 'NO', name: 'Norway' },
  { code: 'DK', name: 'Denmark' },
  { code: 'CH', name: 'Switzerland' },
  { code: 'AT', name: 'Austria' },
  { code: 'BE', name: 'Belgium' },
  { code: 'PT', name: 'Portugal' },
  { code: 'IE', name: 'Ireland' },
  { code: 'PL', name: 'Poland' },
  { code: 'CZ', name: 'Czech Republic' },
  { code: 'HU', name: 'Hungary' },
  { code: 'RO', name: 'Romania' },
  { code: 'GR', name: 'Greece' },
  { code: 'TR', name: 'Turkey' },
  { code: 'RU', name: 'Russia' },
  { code: 'CN', name: 'China' },
  { code: 'JP', name: 'Japan' },
  { code: 'KR', name: 'South Korea' },
  { code: 'IN', name: 'India' },
  { code: 'SG', name: 'Singapore' },
  { code: 'HK', name: 'Hong Kong' },
  { code: 'AE', name: 'United Arab Emirates' },
  { code: 'SA', name: 'Saudi Arabia' },
  { code: 'ZA', name: 'South Africa' },
  { code: 'NG', name: 'Nigeria' },
  { code: 'EG', name: 'Egypt' },
  { code: 'BR', name: 'Brazil' },
  { code: 'MX', name: 'Mexico' },
  { code: 'AR', name: 'Argentina' },
  { code: 'CO', name: 'Colombia' },
  { code: 'NZ', name: 'New Zealand' },
];

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [FormsModule, CommonModule, RouterLink],
  templateUrl: './register.html',
  styleUrl: './register.css'
})
export class LoyaltyRegisterComponent {
  private readonly loyaltyApi = inject(LoyaltyApiService);
  private readonly loyaltyState = inject(LoyaltyStateService);
  private readonly router = inject(Router);

  readonly countries = COUNTRIES;

  givenName = signal('');
  surname = signal('');
  email = signal('');
  password = signal('');
  confirmPassword = signal('');
  dateOfBirth = signal('');
  nationality = signal('');
  phone = signal('');
  showPassword = signal(false);
  showConfirmPassword = signal(false);
  loading = signal(false);
  errorMessage = signal<string | null>(null);

  readonly today = new Date().toISOString().split('T')[0];

  readonly passwordStrength = computed<'weak' | 'fair' | 'strong'>(() => {
    const pw = this.password();
    if (pw.length === 0) return 'weak';
    const hasLength = pw.length >= 8;
    const hasUpper = /[A-Z]/.test(pw);
    const hasNumber = /[0-9]/.test(pw);
    const hasSpecial = /[^A-Za-z0-9]/.test(pw);
    const score = [hasLength, hasUpper, hasNumber, hasSpecial].filter(Boolean).length;
    if (score <= 2) return 'weak';
    if (score === 3) return 'fair';
    return 'strong';
  });

  readonly passwordStrengthLabel = computed<string>(() => {
    const s = this.passwordStrength();
    if (s === 'weak') return 'Weak';
    if (s === 'fair') return 'Fair';
    return 'Strong';
  });

  readonly passwordsMatch = computed(() =>
    this.password().length > 0 && this.confirmPassword().length > 0
      ? this.password() === this.confirmPassword()
      : null
  );

  setGivenName(v: string): void { this.givenName.set(v); }
  setSurname(v: string): void { this.surname.set(v); }
  setEmail(v: string): void { this.email.set(v); }
  setPassword(v: string): void { this.password.set(v); }
  setConfirmPassword(v: string): void { this.confirmPassword.set(v); }
  setDateOfBirth(v: string): void { this.dateOfBirth.set(v); }
  setNationality(v: string): void { this.nationality.set(v); }
  setPhone(v: string): void { this.phone.set(v); }
  toggleShowPassword(): void { this.showPassword.update(v => !v); }
  toggleShowConfirmPassword(): void { this.showConfirmPassword.update(v => !v); }

  onSubmit(): void {
    this.errorMessage.set(null);

    if (!this.givenName() || !this.surname() || !this.email() || !this.password() ||
        !this.confirmPassword() || !this.dateOfBirth() || !this.nationality() || !this.phone()) {
      this.errorMessage.set('Please fill in all required fields.');
      return;
    }

    if (this.password() !== this.confirmPassword()) {
      this.errorMessage.set('Passwords do not match.');
      return;
    }

    if (this.passwordStrength() === 'weak') {
      this.errorMessage.set('Please choose a stronger password (min 8 characters with uppercase and a number).');
      return;
    }

    this.loading.set(true);
    this.loyaltyApi.register({
      givenName: this.givenName(),
      surname: this.surname(),
      email: this.email(),
      password: this.password(),
      dateOfBirth: this.dateOfBirth(),
      nationality: this.nationality(),
      phoneNumber: this.phone()
    }).subscribe({
      next: (session) => {
        this.loyaltyState.setSession(session);
        this.loading.set(false);
        this.router.navigate(['/loyalty/account']);
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err?.message ?? 'Registration failed. Please try again.');
      }
    });
  }
}
