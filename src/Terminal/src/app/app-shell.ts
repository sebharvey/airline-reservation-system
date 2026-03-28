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

  navItems: NavItem[] = [
    { path: '/offer',    label: 'Offer',    icon: '✈',  description: 'Flight & ancillary offers' },
    { path: '/order',    label: 'Order',    icon: '📋', description: 'Manage orders & payments' },
    { path: '/customer', label: 'Customer', icon: '👤', description: 'Customer profiles & history' },
    { path: '/schedules', label: 'Schedules', icon: '🗓', description: 'Flight schedule management' },
    { path: '/fare-rules', label: 'Fare Rules', icon: '💰', description: 'Fare pricing rules' },
    { path: '/terminal', label: 'Terminal', icon: '⌨',  description: 'Cryptic command terminal' },
  ];

  secondaryNavItems: NavItem[] = [
    { path: '/users', label: 'Users', icon: '👥', description: 'User & agent management' },
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
