import { Component } from '@angular/core';

@Component({
  selector: 'app-offer',
  template: `
    <div class="placeholder-page fade-in">
      <span class="placeholder-icon">✈</span>
      <h2>Offer Management</h2>
      <span class="badge">Coming Soon</span>
      <p>
        Search and price flight itineraries, ancillary services, and fare quotes.
        Build and present offers to customers with real-time availability and pricing.
      </p>
      <ul class="feature-list">
        <li>🔍 Flight availability search</li>
        <li>💰 Fare quoting & pricing</li>
        <li>🧳 Ancillary offer management</li>
        <li>📊 Availability calendar</li>
        <li>🎫 Fare rules & restrictions</li>
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
export class OfferComponent {}
