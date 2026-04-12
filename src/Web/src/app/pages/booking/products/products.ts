import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { BookingStateService } from '../../../services/booking-state.service';
import { RetailApiService } from '../../../services/retail-api.service';
import { Product, ProductGroup } from '../../../models/flight.model';

@Component({
  selector: 'app-products',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './products.html',
  styleUrl: './products.css'
})
export class ProductsComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly bookingState = inject(BookingStateService);
  private readonly retailApi = inject(RetailApiService);

  readonly basket = this.bookingState.basket;
  readonly currency = computed(() => this.basket()?.currency ?? 'GBP');

  loading = signal(true);
  error = signal('');
  productGroups = signal<ProductGroup[]>([]);

  ngOnInit(): void {
    if (!this.basket()) {
      this.router.navigate(['/']);
      return;
    }
    this.loadProducts();
  }

  private loadProducts(): void {
    this.retailApi.getProducts().subscribe({
      next: (response) => {
        const currency = this.currency();

        // Keep only groups that have at least one product priced in the basket currency
        const groups = response.productGroups
          .map(g => ({
            ...g,
            products: g.products.filter(p => p.prices.some(pr => pr.currencyCode === currency))
          }))
          .filter(g => g.products.length > 0);

        this.productGroups.set(groups);
        this.loading.set(false);
      },
      error: () => {
        this.error.set('Unable to load products. Please try again.');
        this.loading.set(false);
      }
    });
  }

  getPriceForCurrency(product: Product): number | null {
    const match = product.prices.find(p => p.currencyCode === this.currency());
    return match ? match.price : null;
  }

  /**
   * Build a data URI src for an imageBase64 value.
   * Handles strings that already carry a data: prefix, and detects PNG / WebP /
   * GIF from their well-known base64 header bytes — everything else is treated
   * as JPEG.
   */
  getImageSrc(imageBase64: string): string {
    if (imageBase64.startsWith('data:')) {
      return imageBase64;
    }
    if (imageBase64.startsWith('iVBORw0KGgo')) {
      return `data:image/png;base64,${imageBase64}`;
    }
    if (imageBase64.startsWith('UklGR')) {
      return `data:image/webp;base64,${imageBase64}`;
    }
    if (imageBase64.startsWith('R0lGOD')) {
      return `data:image/gif;base64,${imageBase64}`;
    }
    return `data:image/jpeg;base64,${imageBase64}`;
  }

  onAdd(_product: Product): void {
    // Wired up in a future iteration
  }

  onContinue(): void {
    this.router.navigate(['/booking/payment']);
  }
}
