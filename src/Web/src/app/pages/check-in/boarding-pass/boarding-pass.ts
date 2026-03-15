import { Component, OnInit, signal, computed } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
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
  boardingPasses = signal<BoardingPass[]>([]);
  loading = signal(true);
  errorMessage = signal('');

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');
  passengerIds = signal<string[]>([]);

  readonly groupedByPassenger = computed((): { name: string; passes: BoardingPass[] }[] => {
    const passes = this.boardingPasses();
    const map = new Map<string, { name: string; passes: BoardingPass[] }>();
    for (const bp of passes) {
      const key = bp.passengerId;
      if (!map.has(key)) {
        map.set(key, { name: `${bp.givenName} ${bp.surname}`, passes: [] });
      }
      map.get(key)!.passes.push(bp);
    }
    return Array.from(map.values());
  });

  readonly barcodeSegments = Array.from({ length: 40 }, (_, i) => i);

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private retailApi: RetailApiService
  ) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      const ref = params['bookingRef'] ?? '';
      const gn = params['givenName'] ?? '';
      const sn = params['surname'] ?? '';
      const paxIds = (params['passengerIds'] ?? params['paxIds'] ?? '').split(',').filter(Boolean);
      this.bookingRef.set(ref);
      this.givenName.set(gn);
      this.surname.set(sn);
      this.passengerIds.set(paxIds);

      if (!ref) {
        this.router.navigate(['/check-in']);
        return;
      }
      this.submitCheckIn(ref, paxIds);
    });
  }

  private submitCheckIn(ref: string, paxIds: string[]): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.retailApi.submitCheckIn(ref, paxIds).subscribe({
      next: (passes) => {
        this.boardingPasses.set(passes);
        this.loading.set(false);
      },
      error: (err: { message?: string }) => {
        this.errorMessage.set(err?.message ?? 'Check-in failed. Please try again or visit the airport desk.');
        this.loading.set(false);
      }
    });
  }

  formatTime(dt: string): string {
    return new Date(dt).toLocaleTimeString('en-GB', {
      hour: '2-digit', minute: '2-digit', timeZone: 'UTC'
    });
  }

  formatDate(dt: string): string {
    return new Date(dt).toLocaleDateString('en-GB', {
      weekday: 'short', day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC'
    });
  }

  cabinLabel(code: string): string {
    switch (code) {
      case 'F': return 'First';
      case 'J': return 'Business';
      case 'W': return 'Premium Economy';
      case 'Y': return 'Economy';
      default: return code;
    }
  }

  print(): void {
    window.print();
  }
}
