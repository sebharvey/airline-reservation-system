import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { CheckInStateService } from '../../../services/check-in-state.service';

@Component({
  selector: 'app-check-in-seats',
  standalone: true,
  imports: [],
  templateUrl: './check-in-seats.html',
  styleUrl: './check-in-seats.css'
})
export class CheckInSeatsComponent implements OnInit {
  constructor(
    private router: Router,
    private checkInState: CheckInStateService
  ) {}

  ngOnInit(): void {
    if (!this.checkInState.currentOrder()) {
      this.router.navigate(['/check-in']);
    }
  }

  skip(): void {
    this.checkInState.setSeatSelections([]);
    this.router.navigate(['/check-in/bags']);
  }
}
