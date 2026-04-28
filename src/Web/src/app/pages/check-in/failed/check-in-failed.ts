import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CheckInStateService } from '../../../services/check-in-state.service';

@Component({
  selector: 'app-check-in-failed',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './check-in-failed.html',
  styleUrl: './check-in-failed.css'
})
export class CheckInFailedComponent {
  readonly #state = inject(CheckInStateService);
  readonly failureReason = this.#state.checkInFailureReason;
}
