import { Component, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { CheckInStateService } from '../../../services/check-in-state.service';

@Component({
  selector: 'app-check-in-bags',
  standalone: true,
  imports: [],
  templateUrl: './check-in-bags.html',
  styleUrl: './check-in-bags.css'
})
export class CheckInBagsComponent implements OnInit {
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
    this.checkInState.setBagSelections([]);
    this.router.navigate(['/check-in/hazmat']);
  }
}
