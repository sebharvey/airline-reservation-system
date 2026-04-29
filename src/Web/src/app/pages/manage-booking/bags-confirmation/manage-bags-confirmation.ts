import { Component, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CommonModule } from '@angular/common';

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

  constructor(private router: Router) {}

  ngOnInit(): void {
    const navState = (this.router.getCurrentNavigation()?.extras.state ?? history.state) as Record<string, string>;
    this.givenName.set(navState?.['givenName'] ?? '');
    this.surname.set(navState?.['surname'] ?? '');
    this.bookingRef.set(navState?.['bookingRef'] ?? '');
  }

  goToBooking(): void {
    this.router.navigate(['/manage-booking/detail'], {
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }
}
