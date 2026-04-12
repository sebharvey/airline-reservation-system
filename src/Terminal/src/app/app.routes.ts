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
      { path: '', redirectTo: 'inventory', pathMatch: 'full' },
      {
        path: 'inventory',
        loadComponent: () => import('./pages/inventory/inventory').then(m => m.InventoryComponent),
      },
      {
        path: 'order',
        loadComponent: () => import('./pages/order/order').then(m => m.OrderComponent),
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./pages/order/order-list/order-list').then(m => m.OrderListComponent),
          },
          {
            path: ':bookingRef',
            loadComponent: () =>
              import('./pages/order/order-detail/order-detail').then(m => m.OrderDetailComponent),
          },
        ],
      },
      {
        path: 'customer',
        loadComponent: () => import('./pages/customer/customer').then(m => m.CustomerComponent),
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./pages/customer/customer-list/customer-list').then(m => m.CustomerListComponent),
          },
          {
            path: ':loyaltyNumber',
            loadComponent: () =>
              import('./pages/customer/customer-detail/customer-detail').then(m => m.CustomerDetailComponent),
          },
        ],
      },
      {
        path: 'schedules',
        loadComponent: () => import('./pages/schedules/schedules').then(m => m.SchedulesComponent),
      },
      {
        path: 'fare-rules',
        loadComponent: () => import('./pages/fare-rules/fare-rules').then(m => m.FareRulesComponent),
      },
      {
        path: 'seating',
        loadComponent: () => import('./pages/seating/seating').then(m => m.SeatingComponent),
      },
      {
        path: 'terminal',
        loadComponent: () => import('./pages/terminal/terminal').then(m => m.TerminalComponent),
      },
      {
        path: 'users',
        loadComponent: () => import('./pages/users/user').then(m => m.UserComponent),
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./pages/users/user-list/user-list').then(m => m.UserListComponent),
          },
          {
            path: ':userId',
            loadComponent: () =>
              import('./pages/users/user-detail/user-detail').then(m => m.UserDetailComponent),
          },
        ],
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
