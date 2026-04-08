import { Component, OnInit, ViewChild, ElementRef, signal, computed } from '@angular/core';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { CommonModule, NgTemplateOutlet } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { CheckInStateService } from '../../../services/check-in-state.service';
import { BoardingPass } from '../../../models/order.model';
import QRCode from 'qrcode';

@Component({
  selector: 'app-boarding-pass',
  standalone: true,
  imports: [CommonModule, RouterLink, NgTemplateOutlet],
  templateUrl: './boarding-pass.html',
  styleUrl: './boarding-pass.css'
})
export class BoardingPassComponent implements OnInit {
  boardingPasses = signal<BoardingPass[]>([]);
  loading = signal(true);
  errorMessage = signal('');
  qrCodeUrls = signal<Map<string, string>>(new Map());
  activeIndex = signal(0);

  @ViewChild('carouselTrack') carouselTrack?: ElementRef<HTMLElement>;

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

  constructor(
    private route: ActivatedRoute,
    private retailApi: RetailApiService,
    private checkInState: CheckInStateService
  ) {}

  ngOnInit(): void {
    const departureAirport = this.checkInState.departureAirport();
    const ticketNumbers = this.checkInState.checkedInTicketNumbers();

    if (!departureAirport || ticketNumbers.length === 0) {
      this.errorMessage.set('Check-in session expired. Please complete check-in again.');
      this.loading.set(false);
      return;
    }

    this.retailApi.getOciBoardingPasses(departureAirport, ticketNumbers).subscribe({
      next: (passes) => {
        if (!passes || passes.length === 0) {
          this.errorMessage.set('No boarding passes found. Please complete check-in again.');
          this.loading.set(false);
          return;
        }
        this.boardingPasses.set(passes);
        this.generateQrCodes(passes).then(() => this.loading.set(false));
      },
      error: (err: { message?: string }) => {
        this.errorMessage.set(err?.message ?? 'Unable to retrieve boarding passes. Please try again.');
        this.loading.set(false);
      }
    });
  }

  private async generateQrCodes(passes: BoardingPass[]): Promise<void> {
    const urls = new Map<string, string>();
    await Promise.all(passes.map(async (bp) => {
      const dataUrl = await QRCode.toDataURL(bp.bcbpBarcode, {
        errorCorrectionLevel: 'M',
        margin: 2,
        width: 300,
        color: { dark: '#000000', light: '#ffffff' }
      });
      urls.set(bp.sequenceNumber, dataUrl);
    }));
    this.qrCodeUrls.set(urls);
  }

  qrCodeUrl(bp: BoardingPass): string {
    return this.qrCodeUrls().get(bp.sequenceNumber) ?? '';
  }

  onCarouselScroll(): void {
    const el = this.carouselTrack?.nativeElement;
    if (!el) return;
    const index = Math.round(el.scrollLeft / el.clientWidth);
    this.activeIndex.set(index);
  }

  goToPass(index: number): void {
    const el = this.carouselTrack?.nativeElement;
    if (el) {
      el.scrollTo({ left: index * el.clientWidth, behavior: 'smooth' });
    }
    this.activeIndex.set(index);
  }

  prevPass(): void {
    this.goToPass(Math.max(0, this.activeIndex() - 1));
  }

  nextPass(): void {
    this.goToPass(Math.min(this.boardingPasses().length - 1, this.activeIndex() + 1));
  }

  formatTime(dt: string): string {
    if (!dt) return '—';
    const d = new Date(dt);
    if (isNaN(d.getTime())) return '—';
    return d.toLocaleTimeString('en-GB', {
      hour: '2-digit', minute: '2-digit', timeZone: 'UTC'
    });
  }

  formatDate(dt: string): string {
    if (!dt) return '—';
    const d = new Date(dt);
    if (isNaN(d.getTime())) return '—';
    return d.toLocaleDateString('en-GB', {
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
