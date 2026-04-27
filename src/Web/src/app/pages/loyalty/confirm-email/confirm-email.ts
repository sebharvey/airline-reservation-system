import { Component, inject, signal, OnInit } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { CommonModule } from '@angular/common';
import { LoyaltyApiService } from '../../../services/loyalty-api.service';
import { LucideAngularModule } from 'lucide-angular';

@Component({
  selector: 'app-confirm-email',
  standalone: true,
  imports: [CommonModule, RouterLink, LucideAngularModule],
  templateUrl: './confirm-email.html',
  styleUrl: './confirm-email.css'
})
export class ConfirmEmailComponent implements OnInit {
  private readonly loyaltyApi = inject(LoyaltyApiService);
  private readonly route = inject(ActivatedRoute);

  loading = signal(true);
  success = signal(false);
  error = signal<string | null>(null);

  ngOnInit(): void {
    const token = this.route.snapshot.queryParamMap.get('token');

    if (!token) {
      this.loading.set(false);
      this.error.set('No confirmation token found in the link. Please use the link from your email.');
      return;
    }

    this.loyaltyApi.confirmEmailChange(token).subscribe({
      next: () => {
        this.loading.set(false);
        this.success.set(true);
      },
      error: (err: { message?: string }) => {
        this.loading.set(false);
        this.error.set(
          err?.message ?? 'Email confirmation failed. The link may have expired or already been used.'
        );
      }
    });
  }
}
