import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-search-results',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './search-results.html',
  styleUrl: './search-results.css'
})
export class SearchResultsComponent implements OnInit {
  origin = '';
  destination = '';
  departDate = '';
  returnDate = '';
  tripType = '';
  adults = 1;
  children = 0;

  constructor(private route: ActivatedRoute) {}

  ngOnInit(): void {
    const p = this.route.snapshot.queryParamMap;
    this.origin      = p.get('origin')      ?? '';
    this.destination = p.get('destination') ?? '';
    this.departDate  = p.get('departDate')  ?? '';
    this.returnDate  = p.get('returnDate')  ?? '';
    this.tripType    = p.get('tripType')    ?? 'one-way';
    this.adults      = Number(p.get('adults')   ?? 1);
    this.children    = Number(p.get('children') ?? 0);
  }

  get totalPassengers(): number {
    return this.adults + this.children;
  }
}
