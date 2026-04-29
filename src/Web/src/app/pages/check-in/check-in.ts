import { Component, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { RetailApiService } from '../../services/retail-api.service';
import { CheckInStateService } from '../../services/check-in-state.service';
import { OciFlightSegment } from '../../models/order.model';
import { AirportComboboxComponent } from '../../components/airport-combobox/airport-combobox';

@Component({
  selector: 'app-check-in',
  standalone: true,
  imports: [FormsModule, CommonModule, AirportComboboxComponent],
  templateUrl: './check-in.html',
  styleUrl: './check-in.css'
})
export class CheckInComponent {
  bookingReference = signal('');
  givenName = signal('');
  surname = signal('');
  departureAirport = signal('');
  loading = signal(false);
  errorMessage = signal('');

  constructor(
    private retailApi: RetailApiService,
    private checkInState: CheckInStateService,
    private router: Router
  ) {}

  onReferenceInput(value: string): void {
    this.bookingReference.set(value.toUpperCase());
  }

  get isFormValid(): boolean {
    return (
      this.bookingReference().trim().length >= 3 &&
      this.givenName().trim().length >= 1 &&
      this.surname().trim().length >= 1 &&
      this.departureAirport().trim().length === 3
    );
  }

  onSubmit(): void {
    if (!this.isFormValid || this.loading()) return;

    this.loading.set(true);
    this.errorMessage.set('');
    this.checkInState.clear();

    const ref = this.bookingReference().trim();
    const gn = this.givenName().trim();
    const sn = this.surname().trim();
    const airport = this.departureAirport().trim();

    this.retailApi.retrieveOciOrder({ bookingReference: ref, givenName: gn, surname: sn, departureAirport: airport })
      .subscribe({
        next: (ociOrder) => {
          this.checkInState.setDepartureAirport(airport);

          // Retrieve endpoint sets this when the order already has a completed check-in for this airport.
          if (ociOrder.alreadyCheckedIn) {
            this.checkInState.setCheckedInTicketNumbers(ociOrder.passengers.map(p => p.ticketNumber));
            this.loading.set(false);
            this.router.navigate(['/check-in/boarding-pass']);
            return;
          }

          // Fetch full order and create basket in parallel; both are best-effort
          forkJoin([
            this.retailApi.retrieveOrder(ref)
              .pipe(catchError(() => of(null))),
            this.retailApi.createCheckInBasket(ociOrder.bookingReference, ociOrder.passengers.length, ociOrder.currency)
              .pipe(catchError(() => of(null)))
          ]).subscribe(([fullOrder, basketRes]) => {
            if (basketRes?.basketId) {
              this.checkInState.setBasketId(basketRes.basketId);
            }

            // Merge flight segments from the full order into the OCI order, keeping only
            // the segment(s) departing from the airport the passenger is checking in at.
            // FlightSegment.segmentId serves as the inventoryId for seatmap/bag API calls
            // (consistent with how manage-booking seat selection works).
            const segments: OciFlightSegment[] = (fullOrder?.flightSegments ?? [])
              .filter(seg => seg.origin.toUpperCase() === airport.toUpperCase())
              .map(seg => ({
                segmentRef: seg.segmentId,
                inventoryId: seg.segmentId,
                flightNumber: seg.flightNumber,
                origin: seg.origin,
                destination: seg.destination,
                departureDateTime: seg.departureDateTime,
                arrivalDateTime: seg.arrivalDateTime,
                cabinCode: seg.cabinCode,
                aircraftType: seg.aircraftType,
                seatAssignments: ociOrder.passengers.flatMap(pax => {
                  const seatItem = fullOrder?.orderItems.find(
                    oi => oi.type === 'Seat'
                      && oi.segmentRef === seg.segmentId
                      && oi.passengerRefs.includes(pax.passengerId)
                  );
                  return seatItem?.seatNumber
                    ? [{ passengerId: pax.passengerId, seatNumber: seatItem.seatNumber }]
                    : [];
                })
              }));

            if (fullOrder && segments.length === 0) {
              this.loading.set(false);
              this.errorMessage.set('The departure airport is not on your itinerary. Please check your details and try again.');
              return;
            }

            this.checkInState.setCurrentOrder({ ...ociOrder, flightSegments: segments });
            this.loading.set(false);
            this.router.navigate(['/check-in/details']);
          });
        },
        error: (err: { message?: string }) => {
          this.loading.set(false);
          this.errorMessage.set(err?.message ?? 'Unable to retrieve booking. Please check your details.');
        }
      });
  }
}
