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
        const activeWithPrice = response.products.filter(p =>
          p.prices.some(pr => pr.currencyCode === currency)
        );

        if (activeWithPrice.length === 0) {
          this.productGroups.set([]);
          this.loading.set(false);
          return;
        }

        // Group by productGroupId — preserve natural order from the API
        const groupMap = new Map<string, ProductGroup>();
        for (const product of activeWithPrice) {
          const gid = product.productGroupId;
          if (!groupMap.has(gid)) {
            groupMap.set(gid, { productGroupId: gid, name: product.productGroupName, products: [] });
          }
          groupMap.get(gid)!.products.push(product);
        }

        this.productGroups.set(Array.from(groupMap.values()));
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
