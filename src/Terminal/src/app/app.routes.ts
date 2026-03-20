import { Routes } from '@angular/router';
import { authGuard } from './guards/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./pages/login/login').then(m => m.LoginComponent),
  },
  {
    path: '',
    canActivate: [authGuard],
    loadComponent: () => import('./app-shell').then(m => m.AppShell),
    children: [
      { path: '', redirectTo: 'terminal', pathMatch: 'full' },
      {
        path: 'offer',
        loadComponent: () => import('./pages/offer/offer').then(m => m.OfferComponent),
      },
      {
        path: 'order',
        loadComponent: () => import('./pages/order/order').then(m => m.OrderComponent),
      },
      {
        path: 'customer',
        loadComponent: () => import('./pages/customer/customer').then(m => m.CustomerComponent),
      },
      {
        path: 'terminal',
        loadComponent: () => import('./pages/terminal/terminal').then(m => m.TerminalComponent),
      },
      {
        path: 'users',
        loadComponent: () => import('./pages/users/users').then(m => m.UsersComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
