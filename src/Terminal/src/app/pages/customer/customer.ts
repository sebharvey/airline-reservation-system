import { Component } from '@angular/core';

@Component({
  selector: 'app-customer',
  template: `
    <div class="placeholder-page fade-in">
      <span class="placeholder-icon">👤</span>
      <h2>Customer Management</h2>
      <span class="badge">Coming Soon</span>
      <p>
        Access and manage customer profiles, booking history, loyalty accounts,
        and preferences. Provide personalised service with a full 360° customer view.
      </p>
      <ul class="feature-list">
        <li>🔍 Customer profile search</li>
        <li>📜 Booking history</li>
        <li>⭐ Loyalty account management</li>
        <li>🎯 Preferences & special requests</li>
        <li>📞 Contact history & notes</li>
        <li>🛡️ Data privacy & consent</li>
      </ul>
    </div>
  `,
  styles: [`
    .feature-list {
      list-style: none;
      display: flex;
      flex-direction: column;
      gap: 8px;
      text-align: left;
      background: var(--bg-light);
      border: 1px solid var(--border);
      border-radius: var(--radius-sm);
      padding: 16px 20px;
      width: 100%;
      max-width: 360px;
    }
    .feature-list li {
      font-size: 0.875rem;
      color: var(--text);
    }
  `],
})
export class CustomerComponent {}
