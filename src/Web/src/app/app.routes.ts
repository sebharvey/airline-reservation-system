import { Routes } from '@angular/router';
import { HomeComponent } from './pages/home/home';
import { SearchResultsComponent } from './pages/search-results/search-results';
import { CheckInComponent } from './pages/check-in/check-in';
import { ManageBookingComponent } from './pages/manage-booking/manage-booking';
import { FlightStatusComponent } from './pages/flight-status/flight-status';
import { LoyaltyLoginComponent } from './pages/loyalty/loyalty-login';
import { NdcComponent } from './pages/ndc/ndc';

export const routes: Routes = [
  { path: '', component: HomeComponent },
  { path: 'search', component: SearchResultsComponent },

  // Booking flow
  {
    path: 'booking',
    children: [
      { path: 'reward-login', loadComponent: () => import('./pages/booking/reward-login/reward-login').then(m => m.RewardLoginComponent) },
      { path: 'flight-summary', loadComponent: () => import('./pages/booking/flight-summary/flight-summary').then(m => m.FlightSummaryComponent) },
      { path: 'passengers', loadComponent: () => import('./pages/booking/passengers/passengers').then(m => m.PassengersComponent) },
      { path: 'seats', loadComponent: () => import('./pages/booking/seats/seats').then(m => m.SeatsComponent) },
      { path: 'bags', loadComponent: () => import('./pages/booking/bags/bags').then(m => m.BagsComponent) },
      { path: 'products', loadComponent: () => import('./pages/booking/products/products').then(m => m.ProductsComponent) },
      { path: 'payment', loadComponent: () => import('./pages/booking/payment/payment').then(m => m.PaymentComponent) },
      { path: 'confirmation', loadComponent: () => import('./pages/booking/confirmation/confirmation').then(m => m.ConfirmationComponent) },
    ]
  },

  // Manage booking flow
  {
    path: 'manage-booking',
    children: [
      { path: '', component: ManageBookingComponent },
      { path: 'detail', loadComponent: () => import('./pages/manage-booking/detail/detail').then(m => m.ManageBookingDetailComponent) },
      { path: 'seat', loadComponent: () => import('./pages/manage-booking/seat/manage-seat').then(m => m.ManageSeatComponent) },
      { path: 'bags', loadComponent: () => import('./pages/manage-booking/bags/manage-bags').then(m => m.ManageBagsComponent) },
      { path: 'bags-payment', loadComponent: () => import('./pages/manage-booking/bags-payment/manage-bags-payment').then(m => m.ManageBagsPaymentComponent) },
      { path: 'bags-confirmation', loadComponent: () => import('./pages/manage-booking/bags-confirmation/manage-bags-confirmation').then(m => m.ManageBagsConfirmationComponent) },
      { path: 'change-flight', loadComponent: () => import('./pages/manage-booking/change-flight/change-flight').then(m => m.ChangeFlightComponent) },
      { path: 'cancel', loadComponent: () => import('./pages/manage-booking/cancel/cancel').then(m => m.CancelBookingComponent) },
    ]
  },

  // Check-in flow
  {
    path: 'check-in',
    children: [
      { path: '', component: CheckInComponent },
      { path: 'details', loadComponent: () => import('./pages/check-in/details/check-in-details').then(m => m.CheckInDetailsComponent) },
      { path: 'bags', loadComponent: () => import('./pages/check-in/bags/check-in-bags').then(m => m.CheckInBagsComponent) },
      { path: 'seats', loadComponent: () => import('./pages/check-in/seats/check-in-seats').then(m => m.CheckInSeatsComponent) },
      { path: 'payment', loadComponent: () => import('./pages/check-in/payment/check-in-payment').then(m => m.CheckInPaymentComponent) },
      { path: 'hazmat', loadComponent: () => import('./pages/check-in/hazmat/check-in-hazmat').then(m => m.CheckInHazmatComponent) },
      { path: 'failed', loadComponent: () => import('./pages/check-in/failed/check-in-failed').then(m => m.CheckInFailedComponent) },
      { path: 'boarding-pass', loadComponent: () => import('./pages/check-in/boarding-pass/boarding-pass').then(m => m.BoardingPassComponent) },
    ]
  },

  // Loyalty programme flow
  {
    path: 'loyalty',
    children: [
      { path: '', component: LoyaltyLoginComponent },
      { path: 'register', loadComponent: () => import('./pages/loyalty/register/register').then(m => m.LoyaltyRegisterComponent) },
      { path: 'account', loadComponent: () => import('./pages/loyalty/account/account').then(m => m.LoyaltyAccountComponent) },
      { path: 'password-reset', loadComponent: () => import('./pages/loyalty/password-reset/password-reset').then(m => m.PasswordResetComponent) },
      { path: 'confirm-email', loadComponent: () => import('./pages/loyalty/confirm-email/confirm-email').then(m => m.ConfirmEmailComponent) },
    ]
  },

  { path: 'flight-status', component: FlightStatusComponent },
  { path: 'ndc', component: NdcComponent },
  { path: '**', redirectTo: '' }
];
