import { Component } from '@angular/core';

@Component({
  selector: 'app-order',
  template: `
    <div class="placeholder-page fade-in">
      <span class="placeholder-icon">📋</span>
      <h2>Order Management</h2>
      <span class="badge">Coming Soon</span>
      <p>
        View, modify and manage customer orders. Handle payments, refunds,
        exchanges and ancillary add-ons across all booking channels.
      </p>
      <ul class="feature-list">
        <li>📄 PNR retrieval & display</li>
        <li>🔄 Flight changes & exchanges</li>
        <li>💳 Payment processing</li>
        <li>↩️ Refunds & cancellations</li>
        <li>🧳 Ancillary add-ons</li>
        <li>📧 Customer communications</li>
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
export class OrderComponent {}
