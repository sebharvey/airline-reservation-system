import { Component, OnInit, inject } from '@angular/core';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { BoardingPass } from '../../../models/order.model';

@Component({
  selector: 'app-boarding-pass',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './boarding-pass.html',
  styleUrl: './boarding-pass.css'
})
export class BoardingPassComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private retailApi = inject(RetailApiService);

  boardingPasses: BoardingPass[] = [];
  loading = false;
  error = '';
  bookingRef = '';

  ngOnInit(): void {
    const p = this.route.snapshot.queryParamMap;
    this.bookingRef = p.get('bookingRef') ?? '';
    const givenName = p.get('givenName') ?? '';
    const surname = p.get('surname') ?? '';
    const paxIds = (p.get('paxIds') ?? '').split(',').filter(Boolean);

    if (!this.bookingRef) { this.router.navigate(['/check-in']); return; }

    this.loading = true;
    this.retailApi.submitCheckIn(this.bookingRef, paxIds).subscribe({
      next: (passes) => {
        this.boardingPasses = passes;
        this.loading = false;
      },
      error: (err) => {
        this.error = err.message ?? 'Failed to generate boarding passes';
        this.loading = false;
      }
    });
  }

  formatTime(dt: string): string {
    return new Date(dt).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit', timeZone: 'UTC' });
  }

  formatDate(dt: string): string {
    return new Date(dt).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' });
  }

  cabinLabel(code: string): string {
    const labels: Record<string, string> = { F: 'First', J: 'Business', W: 'Prem. Economy', Y: 'Economy' };
    return labels[code] ?? code;
  }
}
