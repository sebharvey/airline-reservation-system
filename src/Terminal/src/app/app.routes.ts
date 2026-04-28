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
        path: 'check-in',
        loadComponent: () => import('./pages/check-in/check-in').then(m => m.CheckInComponent),
      },
      {
        path: 'flight-management',
        loadComponent: () => import('./pages/flight-management/flight-management').then(m => m.FlightManagementComponent),
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./pages/flight-management/flight-management-list/flight-management-list').then(m => m.FlightManagementListComponent),
          },
          {
            path: ':inventoryId',
            loadComponent: () =>
              import('./pages/flight-management/flight-management-detail/flight-management-detail').then(m => m.FlightManagementDetailComponent),
          },
        ],
      },
      {
        path: 'watchlist',
        loadComponent: () => import('./pages/flight-management/watchlist/watchlist').then(m => m.WatchlistComponent),
      },
      {
        path: 'inventory',
        loadComponent: () => import('./pages/inventory/inventory').then(m => m.InventoryComponent),
      },
      {
        path: 'inventory/:inventoryId',
        loadComponent: () => import('./pages/inventory/inventory-detail/inventory-detail').then(m => m.InventoryDetailComponent),
      },
      {
        path: 'disruption/:flightNumber/:departureDate',
        loadComponent: () => import('./pages/disruption/disruption').then(m => m.DisruptionComponent),
      },
      {
        path: 'aircraft-swap/:flightNumber/:departureDate',
        loadComponent: () => import('./pages/aircraft-swap/aircraft-swap').then(m => m.AircraftSwapComponent),
      },
      {
        path: 'new-order',
        loadComponent: () => import('./pages/new-order/new-order').then(m => m.NewOrderComponent),
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
        path: 'fare-families',
        loadComponent: () => import('./pages/fare-families/fare-families').then(m => m.FareFamiliesComponent),
      },
      {
        path: 'fare-rules',
        loadComponent: () => import('./pages/fare-rules/fare-rules').then(m => m.FareRulesComponent),
      },
      {
        path: 'bag-policy',
        loadComponent: () => import('./pages/bag-policy/bag-policy').then(m => m.BagPolicyComponent),
      },
      {
        path: 'bag-pricing',
        loadComponent: () => import('./pages/bag-pricing/bag-pricing').then(m => m.BagPricingComponent),
      },
      {
        path: 'seating',
        loadComponent: () => import('./pages/seating/seating').then(m => m.SeatingComponent),
      },
      {
        path: 'product-groups',
        loadComponent: () => import('./pages/product-groups/product-groups').then(m => m.ProductGroupsComponent),
      },
      {
        path: 'products',
        loadComponent: () => import('./pages/products/products').then(m => m.ProductsComponent),
      },
      {
        path: 'payments',
        loadComponent: () => import('./pages/payments/payment-list').then(m => m.PaymentListComponent),
      },
      {
        path: 'order-accounting',
        loadComponent: () => import('./pages/order-accounting/order-accounting').then(m => m.OrderAccountingComponent),
      },
      {
        path: 'ssr',
        loadComponent: () => import('./pages/ssr/ssr').then(m => m.SsrComponent),
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
