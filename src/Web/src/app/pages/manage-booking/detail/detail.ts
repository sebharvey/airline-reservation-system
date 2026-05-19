import { Component, OnInit, signal, computed } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';
import { Order, OrderItem, Passenger, FlightSegment, BoardingPass, Ticket } from '../../../models/order.model';

interface PassengerSeatInfo {
  passenger: Passenger;
  seatNumber: string | null;
  eTicketNumber: string | null;
  isCheckedIn: boolean;
  fareAmount: number | null;
  totalAmount: number | null;
}

interface SegmentDisplay {
  segment: FlightSegment;
  passengerSeats: PassengerSeatInfo[];
  isTicketed: boolean;
  checkedInCount: number;
}

@Component({
  selector: 'app-manage-booking-detail',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './detail.html',
  styleUrl: './detail.css'
})
export class ManageBookingDetailComponent implements OnInit {
  order = signal<Order | null>(null);
  boardingPasses = signal<BoardingPass[]>([]);
  loading = signal(true);
  errorMessage = signal('');
  copiedText = signal<string | null>(null);

  selectedTicket = signal<Ticket | null>(null);
  pnrModalOpen = signal(false);
  copiedPnr = signal(false);

  readonly activeTickets = computed<Ticket[]>(() => (this.order()?.tickets ?? []).filter(t => !t.isVoided));
  readonly voidedTickets = computed<Ticket[]>(() => (this.order()?.tickets ?? []).filter(t => t.isVoided));

  readonly pnrText = computed((): string => {
    const o = this.order();
    return o ? this.buildPnrText(o) : '';
  });

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copiedText.set(text);
      setTimeout(() => this.copiedText.set(null), 2000);
    });
  }

  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  readonly canChangeSeat = computed(() => {
    const o = this.order();
    return o ? o.orderStatus === 'Confirmed' : false;
  });

  readonly canChangeFlight = computed(() => {
    const o = this.order();
    return o ? o.orderItems.some(i => i.isChangeable) : false;
  });

  readonly canCancel = computed(() => {
    const o = this.order();
    return o ? o.orderItems.some(i => i.isRefundable) : false;
  });

  readonly canAddBags = computed(() => {
    const o = this.order();
    return o ? o.orderStatus === 'Confirmed' : false;
  });

  readonly segmentDisplays = computed((): SegmentDisplay[] => {
    const o = this.order();
    if (!o) return [];
    const bps = this.boardingPasses();
    return o.flightSegments.map(seg => {
      const flightItems = o.orderItems.filter(
        oi => oi.type === 'Flight' && oi.segmentRef === seg.segmentId
      );
      const passengerSeats: PassengerSeatInfo[] = o.passengers.map(pax => {
        let seatNumber: string | null = null;
        let eTicketNumber: string | null = null;
        const seatItem = o.orderItems.find(
          oi => oi.type === 'Seat' && oi.segmentRef === seg.segmentId && oi.passengerRefs.includes(pax.passengerId)
        );
        if (seatItem) seatNumber = seatItem.seatNumber ?? null;
        for (const item of flightItems) {
          const ticket = item.eTickets?.find(t => t.passengerId === pax.passengerId);
          if (ticket) eTicketNumber = ticket.eTicketNumber;
        }
        const isCheckedIn = bps.some(
          bp => bp.passengerId === pax.passengerId && bp.flightNumber === seg.flightNumber
        );
        const flightItem = flightItems.find(oi => oi.passengerRefs.includes(pax.passengerId));
        const fareAmount = flightItem?.unitPrice ?? null;
        const totalAmount = flightItem?.totalPrice ?? null;
        return { passenger: pax, seatNumber, eTicketNumber, isCheckedIn, fareAmount, totalAmount };
      });
      const isTicketed = passengerSeats.some(ps => ps.eTicketNumber != null);
      const checkedInCount = passengerSeats.filter(ps => ps.isCheckedIn).length;
      return { segment: seg, passengerSeats, isTicketed, checkedInCount };
    });
  });

  readonly flightItemsOnly = computed((): OrderItem[] => {
    const o = this.order();
    return o ? o.orderItems.filter(i => i.type === 'Flight') : [];
  });

  readonly ancillaryItems = computed((): OrderItem[] => {
    const o = this.order();
    return o ? o.orderItems.filter(i => i.type !== 'Flight') : [];
  });

  constructor(
    private router: Router,
    private retailApi: RetailApiService
  ) {}

  ngOnInit(): void {
    const navState = (this.router.getCurrentNavigation()?.extras.state ?? history.state) as Record<string, string>;
    const gn = navState?.['givenName'] ?? '';
    const sn = navState?.['surname'] ?? '';

    if (!this.retailApi.hasActiveManageBookingSession()) {
      this.router.navigate(['/manage-booking']);
      return;
    }

    this.givenName.set(gn);
    this.surname.set(sn);
    this.fetchOrder();
  }

  private fetchOrder(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.boardingPasses.set([]);
    this.retailApi.retrieveOrder().subscribe({
      next: (order) => {
        this.bookingRef.set(order.bookingReference);
        this.order.set(order);
        this.loading.set(false);
      },
      error: (err: { status?: number; message?: string }) => {
        this.loading.set(false);
        if (err.status === 401) {
          this.router.navigate(['/manage-booking']);
          return;
        }
        this.errorMessage.set(err?.message ?? 'Unable to retrieve booking.');
      }
    });
  }

  findTicketByNumber(eTicketNumber: string): Ticket | null {
    return (this.order()?.tickets ?? []).find(t => t.eTicketNumber === eTicketNumber) ?? null;
  }

  openTicketModal(ticket: Ticket): void {
    this.selectedTicket.set(ticket);
  }

  closeTicketModal(): void {
    this.selectedTicket.set(null);
  }

  openPnrModal(): void {
    this.pnrModalOpen.set(true);
  }

  closePnrModal(): void {
    this.pnrModalOpen.set(false);
  }

  copyPnrToClipboard(): void {
    navigator.clipboard.writeText(this.pnrText()).then(() => {
      this.copiedPnr.set(true);
      setTimeout(() => this.copiedPnr.set(false), 2000);
    });
  }

  private buildPnrText(o: Order): string {
    const MONTHS = ['JAN','FEB','MAR','APR','MAY','JUN','JUL','AUG','SEP','OCT','NOV','DEC'];
    const DOW_MAP = [7,1,2,3,4,5,6]; // JS 0=Sun→7, 1=Mon→1 ...

    const fmtDate = (iso: string) => {
      const d = new Date(iso);
      return `${String(d.getUTCDate()).padStart(2,'0')}${MONTHS[d.getUTCMonth()]}`;
    };

    const fmtDateYY = (iso: string) => {
      const d = new Date(iso);
      return `${String(d.getUTCDate()).padStart(2,'0')}${MONTHS[d.getUTCMonth()]}${String(d.getUTCFullYear()).slice(2)}`;
    };

    const fmtTime = (iso: string) => {
      const d = new Date(iso);
      return `${String(d.getUTCHours()).padStart(2,'0')}${String(d.getUTCMinutes()).padStart(2,'0')}`;
    };

    const fmtDow = (iso: string) => String(DOW_MAP[new Date(iso).getUTCDay()]);

    const dayDiff = (dep: string, arr: string): string => {
      const a = new Date(arr), b = new Date(dep);
      const diff = Math.round((
        Date.UTC(a.getUTCFullYear(), a.getUTCMonth(), a.getUTCDate()) -
        Date.UTC(b.getUTCFullYear(), b.getUTCMonth(), b.getUTCDate())
      ) / 86400000);
      return diff > 0 ? `+${diff}` : diff < 0 ? String(diff) : '';
    };

    const lines: string[] = [];
    const cd = new Date(o.createdAt);
    const createdStr = `${cd.getUTCDate()}/${MONTHS[cd.getUTCMonth()]}/${String(cd.getUTCFullYear()).slice(2)}`;
    const mc0 = (o.flightSegments[0]?.marketingCarrier || 'AX').toUpperCase();

    // ── Header ──
    lines.push('--- RLR MSC ---');
    lines.push(`RP/APXWB0001/APXWB0001           ${o.channelCode.slice(0,2).padEnd(2)}/SU  ${createdStr}`);
    lines.push(o.bookingReference);
    lines.push('');

    // ── Name elements ──
    const names = o.passengers.map((p, i) => {
      const title = p.type === 'ADT' ? (p.gender === 'F' ? 'MRS' : 'MR') :
                    p.type === 'CHD' ? (p.gender === 'F' ? 'MISS' : 'MSTR') :
                    p.type === 'INF' ? 'INF' : '';
      return `${i+1}.${p.surname.toUpperCase()}/${p.givenName.toUpperCase()}${title ? ' '+title : ''}`;
    });
    let nameLine = '';
    for (const n of names) {
      const sep = nameLine ? '  ' : ' ';
      if (nameLine && (nameLine.length + sep.length + n.length) > 72) {
        lines.push(nameLine);
        nameLine = ' ' + n;
      } else {
        nameLine = nameLine ? nameLine + sep + n : ' ' + n;
      }
    }
    if (nameLine) lines.push(nameLine);
    lines.push('');

    // ── Itinerary ──
    o.flightSegments.forEach((s, i) => {
      const mc = (s.marketingCarrier || s.operatingCarrier || 'AX').toUpperCase();
      const fn = s.flightNumber.replace(/^[A-Z]{2}/i, '').padStart(3, '0');
      const dc = dayDiff(s.departureDateTime, s.arrivalDateTime);
      const n = o.passengers.length;
      const dcPad = dc ? ` ${dc}` : '   ';
      lines.push(` ${String(i+1).padStart(2)}  ${mc} ${fn} ${s.bookingClass} ${fmtDate(s.departureDateTime)} ${fmtDow(s.departureDateTime)} ${s.origin}${s.destination} HK${n}  ${fmtTime(s.departureDateTime)}  ${fmtTime(s.arrivalDateTime)}${dcPad}  E  /DC${mc} /E`);
    });
    lines.push('');

    // ── AP (contact) ──
    const contactPax = o.passengers.find(p => p.contacts?.phone || p.contacts?.email);
    if (contactPax?.contacts?.phone) {
      lines.push(`AP ${contactPax.contacts.phone}`);
    }
    if (contactPax?.contacts?.email) {
      lines.push(`AP ${contactPax.contacts.email.toUpperCase()}`);
    }
    if (contactPax) lines.push('');

    // ── TK (ticketing) ──
    lines.push(`TK OK${fmtDateYY(o.createdAt)}/APXWB0001//ET${mc0}`);
    lines.push('');

    // ── FP (form of payment) ──
    o.payments.forEach((pay, i) => {
      let fp: string;
      if (pay.method === 'CreditCard' || pay.method === 'DebitCard') {
        const brand = (pay.cardType || '').toUpperCase();
        const code = brand.startsWith('VI') ? 'VI' : brand.startsWith('MA') ? 'MC' :
                     brand.startsWith('AM') ? 'AX' : brand.startsWith('DI') ? 'DC' : 'CA';
        fp = `CC${code}${pay.cardLast4}/0000/${pay.currency}${pay.authorisedAmount.toFixed(2)}`;
      } else if (pay.method === 'ApplePay') {
        fp = 'APPL';
      } else if (pay.method === 'GooglePay') {
        fp = 'GPAY';
      } else {
        fp = pay.method.toUpperCase().slice(0, 8);
      }
      lines.push(` ${String(i+1).padStart(2)} FP ${fp}`);
    });
    lines.push('');

    // ── SSR ──
    o.passengers.forEach((p, i) => {
      const pn = i + 1;
      const carrier = mc0;
      p.docs?.forEach(doc => {
        const docType = doc.type === 'PASSPORT' ? 'P' : 'I';
        const dob = fmtDateYY(p.dob);
        const exp = fmtDateYY(doc.expiryDate);
        const gen = p.gender === 'F' ? 'F' : p.gender === 'M' ? 'M' : 'U';
        lines.push(`SSR DOCS ${carrier} HK1 ${docType}/${doc.issuingCountry}/${doc.number}/${doc.nationality}/${dob}/${gen}/${exp}/${p.surname.toUpperCase()}/${p.givenName.toUpperCase()}-${pn}`);
      });
      if (p.contacts?.email) {
        lines.push(`SSR CTCE ${carrier} HK1 ${p.contacts.email.replace('@', '//').toUpperCase()}-${pn}`);
      }
      if (p.contacts?.phone) {
        lines.push(`SSR CTCM ${carrier} HK1 ${p.contacts.phone}-${pn}`);
      }
      if (p.loyaltyNumber) {
        lines.push(`SSR FQTV ${carrier} HK1 ${carrier}${p.loyaltyNumber}/${carrier}-${pn}`);
      }
    });

    o.orderItems.filter(oi => oi.type === 'SSR').forEach(oi => {
      if (!oi.ssrCode) return;
      const pn = o.passengers.findIndex(p => oi.passengerRefs.includes(p.passengerId)) + 1;
      const seg = o.flightSegments.find(s => s.segmentId === oi.segmentRef);
      const carrier = (seg?.marketingCarrier || mc0).toUpperCase();
      lines.push(`SSR ${oi.ssrCode} ${carrier} HK1-${pn}`);
    });

    lines.push('');

    // ── FA (e-ticket numbers) ──
    let faSeq = 1;
    const faLines: string[] = [];
    o.flightSegments.forEach(seg => {
      const fi = o.orderItems.find(oi => oi.type === 'Flight' && oi.segmentRef === seg.segmentId);
      if (!fi?.eTickets?.length) return;
      const carrier = (seg.marketingCarrier || 'AX').toUpperCase();
      const fn = seg.flightNumber.replace(/^[A-Z]{2}/i, '').padStart(3, '0');
      const date = fmtDate(seg.departureDateTime);
      fi.eTickets.forEach(et => {
        const pn = o.passengers.findIndex(p => p.passengerId === et.passengerId) + 1;
        const fare = (fi.totalPrice ?? 0).toFixed(2);
        const fb = fi.fareBasisCode ?? '';
        faLines.push(` ${String(faSeq++).padStart(2)}  FA PAX ${pn}.${et.eTicketNumber}/${carrier} ${carrier}${fn}${seg.bookingClass}/${date}/${seg.origin}${seg.destination}/${o.currency}${fare}/${fb}`);
      });
    });
    if (faLines.length) {
      lines.push(...faLines);
      lines.push('');
    }

    // ── FE (endorsements/restrictions) ──
    const seenFe = new Set<string>();
    o.tickets?.filter(t => !t.isVoided && t.ticketData?.endorsementsRestrictions).forEach(t => {
      const end = t.ticketData!.endorsementsRestrictions!;
      if (!seenFe.has(end)) {
        seenFe.add(end);
        const pn = o.passengers.findIndex(p => p.passengerId === t.passengerId) + 1;
        lines.push(`FE ${end}-${pn}`);
      }
    });

    return lines.join('\n');
  }

  couponStatusLabel(status: string): string {
    const labels: Record<string, string> = {
      OPEN: 'Open', CHECKED_IN: 'Checked in', LIFTED: 'Lifted',
      FLOWN: 'Flown', REFUNDED: 'Refunded', VOID: 'Void',
      EXCHANGED: 'Exchanged', PRINT_EXCHANGE: 'Print exchange',
    };
    return labels[status] ?? status;
  }

  passengerTypeLabel(type: string): string {
    return ({ ADT: 'Adult', CHD: 'Child', INF: 'Infant', YTH: 'Youth' } as Record<string, string>)[type] ?? type;
  }

  formatDateTime(dt: string): string {
    return new Date(dt).toLocaleString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit', timeZone: 'UTC'
    });
  }

  formatDate(dt: string): string {
    return new Date(dt).toLocaleDateString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC'
    });
  }

  formatCurrency(amount: number, currency: string): string {
    return new Intl.NumberFormat('en-GB', { style: 'currency', currency }).format(amount);
  }

  navigateToAddBags(): void {
    this.router.navigate(['/manage-booking/bags'], {
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }

  navigateToSeat(): void {
    this.router.navigate(['/manage-booking/seat'], {
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }

  navigateToChangeFlight(): void {
    this.router.navigate(['/manage-booking/change-flight'], {
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }

  navigateToCancel(): void {
    this.router.navigate(['/manage-booking/cancel'], {
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }

  statusClass(status: string): string {
    switch (status) {
      case 'Confirmed': return 'badge-confirmed';
      case 'Cancelled': return 'badge-cancelled';
      case 'Changed': return 'badge-changed';
      default: return 'badge-default';
    }
  }

  formatPaymentMethod(method: string): string {
    switch (method) {
      case 'CreditCard': return 'Credit card';
      case 'DebitCard': return 'Debit card';
      case 'ApplePay': return 'Apple Pay';
      case 'GooglePay': return 'Google Pay';
      case 'PayPal': return 'PayPal';
      default: return method;
    }
  }

  itemTypeLabel(type: string): string {
    switch (type) {
      case 'Seat': return 'Seat Ancillary';
      case 'Bag': return 'Baggage Ancillary';
      case 'SSR': return 'Service';
      default: return type;
    }
  }
}
