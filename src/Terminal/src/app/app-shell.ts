import { Component, inject, signal } from '@angular/core';
import { Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { ThemeService } from './services/theme.service';
import { AuthService } from './services/auth.service';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  description: string;
}

interface NavGroup {
  label: string;
  items: NavItem[];
  bottom?: boolean;
}

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.css',
})
export class AppShell {
  theme = inject(ThemeService);
  auth = inject(AuthService);
  #router = inject(Router);

  sidebarOpen = signal(false);

  navGroups: NavGroup[] = [
    {
      label: 'Operations',
      items: [
        { path: '/inventory', label: 'Inventory', icon: '✈',  description: 'Flight inventory & seat availability' },
        { path: '/new-order', label: 'New Order', icon: '➕', description: 'Create a new booking' },
        { path: '/order',     label: 'Order',     icon: '📋', description: 'Manage orders & payments' },
        { path: '/order-accounting', label: 'Order Accounting', icon: '🧾', description: 'Order accounting & financial records' },
        { path: '/customer',  label: 'Customer',  icon: '👤', description: 'Customer profiles & history' },
        { path: '/terminal',  label: 'Terminal',  icon: '⌨',  description: 'Cryptic command terminal' },
      ],
    },
    {
      label: 'Schedule & Fares',
      items: [
        { path: '/schedules',  label: 'Schedules',  icon: '🗓', description: 'Flight schedule management' },
        { path: '/fare-rules', label: 'Fare Rules', icon: '💰', description: 'Fare pricing rules' },
      ],
    },
    {
      label: 'Ancillaries',
      items: [
        { path: '/bag-policy',     label: 'Bag Policy',     icon: '🧳', description: 'Free bag allowances by cabin' },
        { path: '/bag-pricing',    label: 'Bag Pricing',    icon: '💼', description: 'Additional bag prices' },
        { path: '/seating',        label: 'Seating',        icon: '💺', description: 'Seat pricing rules' },
        { path: '/product-groups', label: 'Product Groups', icon: '📦', description: 'Ancillary product categories' },
        { path: '/products',       label: 'Products',       icon: '🛍', description: 'Duty free, meals and ancillary products' },
        { path: '/ssr',            label: 'SSR Catalogue',  icon: '♿', description: 'Special Service Request catalogue' },
      ],
    },
    {
      label: 'Administration',
      bottom: true,
      items: [
        { path: '/users', label: 'Users', icon: '👥', description: 'User & agent management' },
      ],
    },
  ];

  toggleSidebar(): void {
    this.sidebarOpen.update(v => !v);
  }

  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }

  logout(): void {
    this.auth.logout();
    this.#router.navigate(['/login']);
  }
}
