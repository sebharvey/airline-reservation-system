import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { CommonModule } from '@angular/common';
import { LucideAngularModule } from 'lucide-angular';

@Component({
  selector: 'app-manage-bags-confirmation',
  standalone: true,
  imports: [CommonModule, LucideAngularModule],
  templateUrl: './manage-bags-confirmation.html',
  styleUrl: './manage-bags-confirmation.css'
})
export class ManageBagsConfirmationComponent implements OnInit {
  bookingRef = signal('');
  givenName = signal('');
  surname = signal('');

  constructor(private route: ActivatedRoute, private router: Router) {}

  ngOnInit(): void {
    const navState = (this.router.getCurrentNavigation()?.extras.state ?? history.state) as Record<string, string>;
    this.givenName.set(navState?.['givenName'] ?? '');
    this.surname.set(navState?.['surname'] ?? '');

    this.route.queryParams.subscribe(params => {
      this.bookingRef.set(params['bookingRef'] ?? '');
    });
  }

  goToBooking(): void {
    this.router.navigate(['/manage-booking/detail'], {
      queryParams: { bookingRef: this.bookingRef() },
      state: { givenName: this.givenName(), surname: this.surname() }
    });
  }
}
