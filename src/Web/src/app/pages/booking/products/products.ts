import { Component, OnInit, signal, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CommonModule } from '@angular/common';
import { BookingStateService } from '../../../services/booking-state.service';
import { RetailApiService } from '../../../services/retail-api.service';
import { Product, ProductGroup } from '../../../models/flight.model';
import { BasketProductSelection } from '../../../models/order.model';

interface PaxSegmentEntry {
  basketItemId: string;
  label: string;
  selected: boolean;
}

interface PaxModalEntry {
  passengerId: string;
  name: string;
  /** Used for non-segment-specific products */
  selected: boolean;
  /** Used for segment-specific products */
  segments: PaxSegmentEntry[];
}

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

  // Modal state
  showModal = signal(false);
  modalProduct = signal<Product | null>(null);
  modalPaxEntries = signal<PaxModalEntry[]>([]);
  modalSaving = signal(false);
  modalSaveError = signal('');

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

  /** Returns the basket selections for a given product. Safe against a stale basket. */
  getProductSelections(product: Product): BasketProductSelection[] {
    return this.basket()?.productSelections?.filter(s => s.productId === product.productId) ?? [];
  }

  /** True when the product has at least one selection in the basket. */
  isProductAdded(product: Product): boolean {
    return this.getProductSelections(product).length > 0;
  }

  /** Total selection count label for an added product tile. */
  getAddedLabel(product: Product): string {
    const count = this.getProductSelections(product).length;
    return count === 1 ? '✓ Added (1)' : `✓ Added (${count})`;
  }

  /** Open the PAX / segment selection modal for the given product. */
  onAdd(product: Product): void {
    const basket = this.basket();
    if (!basket) return;

    const currency = this.currency();
    const priceEntry = product.prices.find(p => p.currencyCode === currency);
    if (!priceEntry) return;

    const existingSelections = this.getProductSelections(product);

    const paxRefs = basket.flightOffers[0]?.passengerRefs ?? [];
    const passengers = basket.passengers;

    const entries: PaxModalEntry[] = paxRefs.map(paxId => {
      const pax = passengers.find(p => p.passengerId === paxId);
      const name = pax
        ? `${pax.givenName} ${pax.surname}`.trim() || (pax.type === 'ADT' ? 'Adult' : 'Child')
        : paxId;

      if (product.isSegmentSpecific) {
        const segments: PaxSegmentEntry[] = basket.flightOffers.map(fo => {
          const isSelected = existingSelections.some(
            s => s.passengerId === paxId && s.segmentRef === fo.basketItemId
          );
          return {
            basketItemId: fo.basketItemId,
            label: `${fo.origin} → ${fo.destination} (${fo.flightNumber})`,
            selected: isSelected
          };
        });
        return { passengerId: paxId, name, selected: false, segments };
      } else {
        const isSelected = existingSelections.some(s => s.passengerId === paxId);
        return { passengerId: paxId, name, selected: isSelected, segments: [] };
      }
    });

    this.modalProduct.set(product);
    this.modalPaxEntries.set(entries);
    this.showModal.set(true);
  }

  /** Toggle the selected state for a passenger (non-segment-specific). */
  togglePax(passengerId: string): void {
    this.modalPaxEntries.update(entries =>
      entries.map(e =>
        e.passengerId === passengerId ? { ...e, selected: !e.selected } : e
      )
    );
  }

  /** Toggle the selected state for a specific (passenger, segment) pair. */
  toggleSegment(passengerId: string, basketItemId: string): void {
    this.modalPaxEntries.update(entries =>
      entries.map(e => {
        if (e.passengerId !== passengerId) return e;
        return {
          ...e,
          segments: e.segments.map(s =>
            s.basketItemId === basketItemId ? { ...s, selected: !s.selected } : s
          )
        };
      })
    );
  }

  /** Apply the modal selections to the basket and close the modal. */
  onConfirmModal(): void {
    const product = this.modalProduct();
    const basket = this.basket();
    if (!product || !basket) return;

    const currency = this.currency();
    const priceEntry = product.prices.find(p => p.currencyCode === currency);
    if (!priceEntry) return;

    // Keep selections for all OTHER products; replace this product's selections
    const otherSelections = (basket.productSelections ?? []).filter(
      s => s.productId !== product.productId
    );

    const newSelections: BasketProductSelection[] = [];

    for (const entry of this.modalPaxEntries()) {
      if (product.isSegmentSpecific) {
        for (const seg of entry.segments) {
          if (seg.selected) {
            newSelections.push({
              basketItemId: `PI-${Math.random().toString(36).slice(2, 8)}`,
              productId: product.productId,
              offerId: priceEntry.offerId,
              name: product.name,
              passengerId: entry.passengerId,
              segmentRef: seg.basketItemId,
              price: priceEntry.price,
              currency
            });
          }
        }
      } else {
        if (entry.selected) {
          newSelections.push({
            basketItemId: `PI-${Math.random().toString(36).slice(2, 8)}`,
            productId: product.productId,
            offerId: priceEntry.offerId,
            name: product.name,
            passengerId: entry.passengerId,
            price: priceEntry.price,
            currency
          });
        }
      }
    }

    const allSelections = [...otherSelections, ...newSelections];

    this.modalSaving.set(true);
    this.modalSaveError.set('');

    this.retailApi.updateBasketProducts(basket.basketId, allSelections).subscribe({
      next: () => {
        this.bookingState.setProductSelections(allSelections);
        this.modalSaving.set(false);
        this.closeModal();
      },
      error: () => {
        this.modalSaving.set(false);
        this.modalSaveError.set('Failed to update basket. Please try again.');
      }
    });
  }

  closeModal(): void {
    this.showModal.set(false);
    this.modalProduct.set(null);
    this.modalPaxEntries.set([]);
    this.modalSaving.set(false);
    this.modalSaveError.set('');
  }

  /** Whether the modal has any selections currently ticked. */
  get modalHasSelections(): boolean {
    const product = this.modalProduct();
    if (!product) return false;
    if (product.isSegmentSpecific) {
      return this.modalPaxEntries().some(e => e.segments.some(s => s.selected));
    }
    return this.modalPaxEntries().some(e => e.selected);
  }

  /** Human-readable summary of current basket product selections total. */
  readonly productTotal = computed(() => this.basket()?.totalProductAmount ?? 0);

  /** Navigate to payment. Product selections are already saved to basket when confirmed via modal. */
  onContinue(): void {
    this.router.navigate(['/booking/payment']);
  }

  /** Navigate to payment without any product selections. */
  onSkip(): void {
    this.router.navigate(['/booking/payment']);
  }
}
