import { Component, computed, inject, signal } from '@angular/core';
import { Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { LucideAngularModule } from 'lucide-angular';
import { ThemeService } from './services/theme.service';
import { AuthService } from './services/auth.service';
import { HttpDebugService, HttpLogEntry } from './services/http-debug.service';

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
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LucideAngularModule],
  templateUrl: './app-shell.html',
  styleUrl: './app-shell.css',
})
export class AppShell {
  theme = inject(ThemeService);
  auth = inject(AuthService);
  #router = inject(Router);

  sidebarOpen = signal(false);
  navCollapsed = signal(false);

  readonly httpDebug = inject(HttpDebugService);
  readonly httpDebugOpen = signal(false);
  readonly httpDebugSelectedId = signal<number | null>(null);
  readonly httpDebugCopied = signal(false);
  readonly httpDebugTab = signal<'request' | 'response'>('request');
  readonly httpDebugSelected = computed<HttpLogEntry | null>(() => {
    const id = this.httpDebugSelectedId();
    if (id === null) return null;
    return this.httpDebug.entries().find(e => e.id === id) ?? null;
  });

  navGroups: NavGroup[] = [
    {
      label: 'Operations',
      items: [
        { path: '/inventory', label: 'Stock Keeper', icon: 'plane',          description: 'Flight stock keeper & seat availability' },
        { path: '/new-order', label: 'New Order',    icon: 'plus',           description: 'Create a new booking' },
        { path: '/order',     label: 'Order',        icon: 'clipboard-list', description: 'Manage orders & payments' },
        { path: '/payments',  label: 'Payments',     icon: 'credit-card',    description: 'Daily payment transactions' },
        { path: '/order-accounting', label: 'Order Accounting', icon: 'receipt', description: 'Order accounting & financial records' },
        { path: '/customer',  label: 'Customer',     icon: 'user',           description: 'Customer profiles & history' },
      ],
    },
    {
      label: 'Departure Control',
      items: [
        { path: '/check-in',        label: 'Check In',        icon: 'plane-takeoff', description: 'Agent desk check-in for passengers' },
        { path: '/flight-management', label: 'Flight Management', icon: 'plane',       description: 'Despatch flights and manage departure paperwork' },
        { path: '/watchlist',        label: 'Watchlist',        icon: 'shield-alert', description: 'Departure screening watchlist' },
      ],
    },
    {
      label: 'Schedule & Fares',
      items: [
        { path: '/schedules',     label: 'Schedules',     icon: 'calendar',           description: 'Flight schedule management' },
        { path: '/fare-families', label: 'Fare Families', icon: 'tag',                description: 'Fare family names catalogue' },
        { path: '/fare-rules',    label: 'Fare Rules',    icon: 'circle-dollar-sign', description: 'Fare pricing rules' },
      ],
    },
    {
      label: 'Ancillaries',
      items: [
        { path: '/bag-policy',     label: 'Bag Policy',      icon: 'luggage',       description: 'Free bag allowances by cabin' },
        { path: '/bag-pricing',    label: 'Bag Pricing',     icon: 'briefcase',     description: 'Additional bag prices' },
        { path: '/seating',        label: 'Seating',         icon: 'armchair',      description: 'Seat pricing rules' },
        { path: '/product-groups', label: 'Product Groups',  icon: 'package',       description: 'Ancillary product categories' },
        { path: '/products',       label: 'Products',        icon: 'shopping-bag',  description: 'Duty free, meals and ancillary products' },
        { path: '/ssr',            label: 'Service catalogue', icon: 'accessibility', description: 'Special Service Request catalogue' },
      ],
    },
    {
      label: 'Administration',
      bottom: true,
      items: [
        { path: '/users', label: 'Users', icon: 'users', description: 'User & agent management' },
      ],
    },
  ];

  toggleSidebar(): void {
    if (window.innerWidth < 901) {
      this.sidebarOpen.update(v => !v);
    } else {
      this.navCollapsed.update(v => !v);
    }
  }

  closeSidebar(): void {
    this.sidebarOpen.set(false);
  }

  openHttpDebug(): void {
    const entries = this.httpDebug.entries();
    if (entries.length > 0 && this.httpDebugSelectedId() === null) {
      this.httpDebugSelectedId.set(entries[0].id);
    }
    this.httpDebugOpen.set(true);
  }

  closeHttpDebug(): void {
    this.httpDebugOpen.set(false);
  }

  selectHttpEntry(id: number): void {
    this.httpDebugSelectedId.set(id);
    this.httpDebugTab.set('request');
    this.httpDebugCopied.set(false);
  }

  clearHttpLog(): void {
    this.httpDebug.clear();
    this.httpDebugSelectedId.set(null);
    this.httpDebugCopied.set(false);
  }

  copyHttpEntry(): void {
    const entry = this.httpDebugSelected();
    if (!entry) return;
    const payload = {
      timestamp: entry.timestamp,
      method: entry.method,
      url: entry.url,
      request: {
        headers: entry.requestHeaders,
        body: entry.requestBody
      },
      response: {
        status: entry.responseStatus,
        headers: entry.responseHeaders,
        body: entry.responseBody
      },
      durationMs: entry.durationMs
    };
    navigator.clipboard.writeText(JSON.stringify(payload, null, 2)).then(() => {
      this.httpDebugCopied.set(true);
      setTimeout(() => this.httpDebugCopied.set(false), 2000);
    });
  }

  formatJson(value: unknown): string {
    if (value === null || value === undefined) return '(none)';
    if (typeof value === 'string') {
      try {
        return JSON.stringify(JSON.parse(value), null, 2);
      } catch {
        return value;
      }
    }
    return JSON.stringify(value, null, 2);
  }

  logout(): void {
    this.auth.logout();
    this.#router.navigate(['/login']);
  }
}
