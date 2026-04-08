import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { AuthService } from '../../services/auth.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.css',
})
export class LoginComponent {
  #auth = inject(AuthService);
  #router = inject(Router);

  username = signal('');
  password = signal('');
  loading = signal(false);
  error = signal('');

  async onSubmit(): Promise<void> {
    if (!this.username().trim()) {
      this.error.set('Please enter your agent ID or username.');
      return;
    }

    if (!this.password().trim()) {
      this.error.set('Please enter your password.');
      return;
    }

    this.loading.set(true);
    this.error.set('');

    try {
      await this.#auth.login(this.username(), this.password());
      this.#router.navigate(['/inventory']);
    } catch (err: any) {
      this.error.set(err?.message ?? 'Login failed. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  setUsername(val: string): void { this.username.set(val); }
  setPassword(val: string): void { this.password.set(val); }
}
