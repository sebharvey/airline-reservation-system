import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home';
import { CheckInComponent } from './pages/check-in/check-in';
import { ManageBookingComponent } from './pages/manage-booking/manage-booking';
import { FlightStatusComponent } from './pages/flight-status/flight-status';
import { LoyaltyLoginComponent } from './pages/loyalty-login/loyalty-login';
import { SearchResultsComponent } from './pages/search-results/search-results';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'search', component: SearchResultsComponent },
  { path: 'check-in', component: CheckInComponent },
  { path: 'manage-booking', component: ManageBookingComponent },
  { path: 'flight-status', component: FlightStatusComponent },
  { path: 'loyalty', component: LoyaltyLoginComponent },
  { path: '**', redirectTo: '' }
];
