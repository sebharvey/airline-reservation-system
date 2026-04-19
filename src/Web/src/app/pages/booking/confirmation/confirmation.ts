import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { BookingStateService } from '../../../services/booking-state.service';
import { Order, FlightSegment, Passenger, OrderItem, ETicket } from '../../../models/order.model';

export interface ItinerarySegment {
  segment: FlightSegment;
  passengerTickets: Array<{
    passenger: Passenger;
    eTicketNumber: string;
    seatNumber: string;
  }>;
}

@Component({
  selector: 'app-booking-confirmation',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './confirmation.html',
  styleUrl: './confirmation.css'
})
export class ConfirmationComponent implements OnInit {
  private router = inject(Router);
  private bookingState = inject(BookingStateService);

  order: Order | null = null;
  copiedText = signal<string | null>(null);

  copyToClipboard(text: string): void {
    navigator.clipboard.writeText(text).then(() => {
      this.copiedText.set(text);
      setTimeout(() => this.copiedText.set(null), 2000);
    });
  }

  get isRewardBooking(): boolean {
    return this.order?.bookingType === 'Reward';
  }

  get pointsRedemption() {
    return this.order?.pointsRedemption ?? null;
  }

  ngOnInit(): void {
    this.order = this.bookingState.confirmedOrder();
    if (!this.order) {
      this.router.navigate(['/']);
    }
  }

  get itinerary(): ItinerarySegment[] {
    const o = this.order;
    if (!o) return [];
    return o.flightSegments.map(seg => {
      const flightItem = o.orderItems.find(
        oi => oi.type === 'Flight' && oi.segmentRef === seg.segmentId
      );
      const passengerTickets = o.passengers.map(pax => {
        const seatItem = o.orderItems.find(
          oi => oi.type === 'Seat' && oi.segmentRef === seg.segmentId && oi.passengerRefs.includes(pax.passengerId)
        );
        return {
          passenger: pax,
          eTicketNumber: flightItem?.eTickets?.find(t => t.passengerId === pax.passengerId)?.eTicketNumber ?? 'N/A',
          seatNumber: seatItem?.seatNumber ?? 'Not assigned'
        };
      });
      return { segment: seg, passengerTickets };
    });
  }

  get seatItems(): OrderItem[] {
    return this.order?.orderItems.filter(oi => oi.type === 'Seat') ?? [];
  }

  get bagItems(): OrderItem[] {
    return this.order?.orderItems.filter(oi => oi.type === 'Bag') ?? [];
  }

  get productItems(): OrderItem[] {
    return this.order?.orderItems.filter(oi => oi.type === 'Product') ?? [];
  }

  get payment() {
    return this.order?.payments[0] ?? null;
  }

  get fareTotal(): number {
    return this.order?.orderItems
      .filter(oi => oi.type === 'Flight')
      .reduce((sum, oi) => sum + oi.totalPrice, 0) ?? 0;
  }

  get seatTotal(): number {
    return this.order?.orderItems
      .filter(oi => oi.type === 'Seat')
      .reduce((sum, oi) => sum + oi.totalPrice, 0) ?? 0;
  }

  get bagTotal(): number {
    return this.order?.orderItems
      .filter(oi => oi.type === 'Bag')
      .reduce((sum, oi) => sum + oi.totalPrice, 0) ?? 0;
  }

  get productTotal(): number {
    return this.order?.orderItems
      .filter(oi => oi.type === 'Product')
      .reduce((sum, oi) => sum + oi.totalPrice, 0) ?? 0;
  }

  // Sum of all priced order items — the confirmation page source of truth.
  // Reward bookings fall back to order.totalAmount because their flight items
  // carry the full cash fare price, not the taxes-only amount paid in cash.
  get grandTotal(): number {
    if (this.isRewardBooking) return this.order?.totalAmount ?? 0;
    return this.fareTotal + this.seatTotal + this.bagTotal + this.productTotal;
  }

  getPassengerName(passengerId: string): string {
    const pax = this.order?.passengers.find(p => p.passengerId === passengerId);
    return pax ? `${pax.givenName} ${pax.surname}` : passengerId;
  }

  getPassengerETickets(passengerId: string): ETicket[] {
    if (!this.order) return [];
    const seen = new Set<string>();
    return this.order.orderItems
      .filter(oi => oi.type === 'Flight')
      .flatMap(oi => oi.eTickets?.filter(et => et.passengerId === passengerId) ?? [])
      .filter(et => {
        if (seen.has(et.eTicketNumber)) return false;
        seen.add(et.eTicketNumber);
        return true;
      });
  }

  getSegment(segmentId: string): FlightSegment | undefined {
    return this.order?.flightSegments.find(s => s.segmentId === segmentId);
  }

  getSegmentLabel(segmentRef: string): string {
    const seg = this.getSegment(segmentRef);
    return seg ? `${seg.origin} \u2192 ${seg.destination}` : segmentRef;
  }

  getFlightItems(): OrderItem[] {
    return this.order?.orderItems.filter(i => i.type === 'Flight') ?? [];
  }

  getSeatsForSegment(segmentRef: string): OrderItem[] {
    return this.order?.orderItems.filter(i => i.type === 'Seat' && i.segmentRef === segmentRef) ?? [];
  }

  formatDateTime(dt: string): string {
    if (!dt) return '';
    return new Date(dt).toLocaleString('en-GB', {
      weekday: 'short', day: 'numeric', month: 'short', year: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  }

  formatDate(dt: string): string {
    if (!dt) return '';
    return new Date(dt).toLocaleDateString('en-GB', {
      day: 'numeric', month: 'short', year: 'numeric'
    });
  }

  formatTime(dt: string): string {
    if (!dt) return '';
    return new Date(dt).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
  }

  getDuration(dep: string, arr: string): string {
    const diffMs = new Date(arr).getTime() - new Date(dep).getTime();
    const totalMins = Math.round(diffMs / 60000);
    const h = Math.floor(totalMins / 60);
    const m = totalMins % 60;
    return `${h}h ${m}m`;
  }

  formatPrice(amount: number): string {
    return `${this.order?.currency ?? ''} ${amount.toFixed(2)}`;
  }

  cabinLabel(code: string): string {
    const labels: Record<string, string> = {
      F: 'First Class', J: 'Business Class', W: 'Premium Economy', Y: 'Economy'
    };
    return labels[code] ?? code;
  }
}
