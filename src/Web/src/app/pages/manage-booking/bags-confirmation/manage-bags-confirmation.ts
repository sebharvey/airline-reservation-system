import { Component, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { RetailApiService } from '../../../services/retail-api.service';

@Component({
  selector: 'app-manage-bags-confirmation',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './manage-bags-confirmation.html',
  styleUrl: './manage-bags-confirmation.css'
})
export class ManageBagsConfirmationComponent implements OnInit {
  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  constructor(private router: Router, private retailApi: RetailApiService) {}

  ngOnInit(): void {
    const navState = (this.router.getCurrentNavigation()?.extras.state ?? history.state) as Record<string, string>;
    this.givenName.set(navState?.['givenName'] ?? '');
    this.surname.set(navState?.['surname'] ?? '');

    const ref = this.retailApi.getManageBookingRef();
    this.bookingRef.set(ref ?? '');
  }

  goToBooking(): void {
    this.router.navigate(['/manage-booking/detail'], {
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }
}
