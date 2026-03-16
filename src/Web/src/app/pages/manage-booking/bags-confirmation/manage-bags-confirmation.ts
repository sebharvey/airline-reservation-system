import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
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

  constructor(private route: ActivatedRoute, private router: Router) {}

  ngOnInit(): void {
    this.route.queryParams.subscribe(params => {
      this.bookingRef.set(params['bookingRef'] ?? '');
      this.givenName.set(params['givenName'] ?? '');
      this.surname.set(params['surname'] ?? '');
    });
  }

  goToBooking(): void {
    this.router.navigate(['/manage-booking/detail'], {
      queryParams: {
        bookingRef: this.bookingRef(),
        givenName: this.givenName(),
        surname: this.surname()
      }
    });
  }
}
