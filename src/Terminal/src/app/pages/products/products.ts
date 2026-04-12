import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SlicePipe } from '@angular/common';
import {
  ProductService,
  Product,
  ProductPrice,
  CreateProductRequest,
  UpdateProductRequest,
  CreateProductPriceRequest,
  UpdateProductPriceRequest,
} from '../../services/product.service';
import { ProductGroupService, ProductGroup } from '../../services/product-group.service';
import { SsrCatalogueService, SsrCatalogueEntry } from '../../services/ssr-catalogue.service';

@Component({
  selector: 'app-products',
  imports: [FormsModule, SlicePipe],
  templateUrl: './products.html',
  styleUrl: './products.css',
})
export class ProductsComponent implements OnInit {
  #service = inject(ProductService);
  #groupService = inject(ProductGroupService);
  #ssrService = inject(SsrCatalogueService);

  products = signal<Product[]>([]);
  groups = signal<ProductGroup[]>([]);
  ssrCodes = signal<SsrCatalogueEntry[]>([]);

  loading = signal(false);
  error = signal('');
  success = signal('');
  loaded = signal(false);

  showForm = signal(false);
  editing = signal<Product | null>(null);
  saving = signal(false);
  deleting = signal<string | null>(null);

  // Price management
  showPriceForm = signal(false);
  editingPrice = signal<ProductPrice | null>(null);
  savingPrice = signal(false);
  deletingPrice = signal<string | null>(null);

  createForm = signal<CreateProductRequest>({
    productGroupId: '',
    name: '',
    description: '',
    isSegmentSpecific: false,
    ssrCode: null,
    imageBase64: null,
  });

  updateForm = signal<UpdateProductRequest>({
    productGroupId: '',
    name: '',
    description: '',
    isSegmentSpecific: false,
    ssrCode: null,
    imageBase64: null,
    isActive: true,
  });

  priceCreateForm = signal<CreateProductPriceRequest>({ currencyCode: 'GBP', price: 0, tax: 0 });
  priceUpdateForm = signal<UpdateProductPriceRequest>({ price: 0, tax: 0, isActive: true });

  stats = computed(() => {
    const all = this.products();
    const active = all.filter(p => p.isActive).length;
    const segmentSpecific = all.filter(p => p.isSegmentSpecific).length;
    return { total: all.length, active, segmentSpecific };
  });

  ngOnInit(): void {
    this.loadAll();
  }

  async loadAll(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const [products, groups, ssrCodes] = await Promise.all([
        this.#service.getAll(),
        this.#groupService.getAll(),
        this.#ssrService.getAll().catch(() => [] as SsrCatalogueEntry[]),
      ]);
      this.products.set(products);
      this.groups.set(groups);
      this.ssrCodes.set(ssrCodes);
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load products. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  groupName(groupId: string): string {
    return this.groups().find(g => g.productGroupId === groupId)?.name ?? groupId;
  }

  openCreateForm(): void {
    this.editing.set(null);
    this.showPriceForm.set(false);
    this.createForm.set({
      productGroupId: this.groups()[0]?.productGroupId ?? '',
      name: '',
      description: '',
      isSegmentSpecific: false,
      ssrCode: null,
      imageBase64: null,
    });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  openEditForm(product: Product): void {
    this.editing.set(product);
    this.showPriceForm.set(false);
    this.editingPrice.set(null);
    this.updateForm.set({
      productGroupId: product.productGroupId,
      name: product.name,
      description: product.description,
      isSegmentSpecific: product.isSegmentSpecific,
      ssrCode: product.ssrCode,
      imageBase64: product.imageBase64,
      isActive: product.isActive,
    });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editing.set(null);
    this.showPriceForm.set(false);
    this.editingPrice.set(null);
  }

  updateCreateField(field: keyof CreateProductRequest, value: unknown): void {
    this.createForm.update(f => ({ ...f, [field]: value }));
  }

  updateUpdateField(field: keyof UpdateProductRequest, value: unknown): void {
    this.updateForm.update(f => ({ ...f, [field]: value }));
  }

  onImageChange(event: Event, mode: 'create' | 'update'): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = () => {
      const base64 = reader.result as string;
      if (mode === 'create') {
        this.createForm.update(f => ({ ...f, imageBase64: base64 }));
      } else {
        this.updateForm.update(f => ({ ...f, imageBase64: base64 }));
      }
    };
    reader.readAsDataURL(file);
  }

  clearImage(mode: 'create' | 'update'): void {
    if (mode === 'create') {
      this.createForm.update(f => ({ ...f, imageBase64: null }));
    } else {
      this.updateForm.update(f => ({ ...f, imageBase64: null }));
    }
  }

  async save(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    try {
      const editingProduct = this.editing();
      if (editingProduct) {
        await this.#service.update(editingProduct.productId, this.updateForm());
        this.success.set('Product updated successfully.');
      } else {
        await this.#service.create(this.createForm());
        this.success.set('Product created successfully.');
      }
      this.showForm.set(false);
      this.editing.set(null);
      this.showPriceForm.set(false);
      await this.loadAll();
    } catch {
      this.error.set('Failed to save product. Check the data and try again.');
    } finally {
      this.saving.set(false);
    }
  }

  async deleteProduct(productId: string): Promise<void> {
    this.deleting.set(productId);
    this.error.set('');
    this.success.set('');
    try {
      await this.#service.delete(productId);
      this.success.set('Product deleted successfully.');
      await this.loadAll();
    } catch {
      this.error.set('Failed to delete product. Please try again.');
    } finally {
      this.deleting.set(null);
    }
  }

  // ── Price management ──────────────────────────────────────────────────────

  openAddPriceForm(): void {
    this.editingPrice.set(null);
    this.priceCreateForm.set({ currencyCode: 'GBP', price: 0, tax: 0 });
    this.showPriceForm.set(true);
  }

  openEditPriceForm(price: ProductPrice): void {
    this.editingPrice.set(price);
    this.priceUpdateForm.set({ price: price.price, tax: price.tax, isActive: price.isActive });
    this.showPriceForm.set(true);
  }

  cancelPriceForm(): void {
    this.showPriceForm.set(false);
    this.editingPrice.set(null);
  }

  updatePriceCreateField(field: keyof CreateProductPriceRequest, value: unknown): void {
    this.priceCreateForm.update(f => ({ ...f, [field]: value }));
  }

  updatePriceUpdateField(field: keyof UpdateProductPriceRequest, value: unknown): void {
    this.priceUpdateForm.update(f => ({ ...f, [field]: value }));
  }

  async savePrice(): Promise<void> {
    const product = this.editing();
    if (!product) return;

    this.savingPrice.set(true);
    this.error.set('');
    this.success.set('');
    try {
      const ep = this.editingPrice();
      if (ep) {
        await this.#service.updatePrice(product.productId, ep.priceId, this.priceUpdateForm());
        this.success.set('Price updated successfully.');
      } else {
        await this.#service.createPrice(product.productId, this.priceCreateForm());
        this.success.set('Price added successfully.');
      }
      this.showPriceForm.set(false);
      this.editingPrice.set(null);
      // Reload to get latest prices on the editing product
      const updated = await this.#service.getById(product.productId);
      this.editing.set(updated);
      this.products.update(list => list.map(p => p.productId === updated.productId ? updated : p));
    } catch {
      this.error.set('Failed to save price. The currency may already have a price configured.');
    } finally {
      this.savingPrice.set(false);
    }
  }

  async deletePrice(productId: string, priceId: string): Promise<void> {
    this.deletingPrice.set(priceId);
    this.error.set('');
    this.success.set('');
    try {
      await this.#service.deletePrice(productId, priceId);
      this.success.set('Price removed successfully.');
      const updated = await this.#service.getById(productId);
      this.editing.set(updated);
      this.products.update(list => list.map(p => p.productId === updated.productId ? updated : p));
    } catch {
      this.error.set('Failed to remove price. Please try again.');
    } finally {
      this.deletingPrice.set(null);
    }
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  formatAmount(amount: number, currency: string): string {
    return `${currency} ${amount.toFixed(2)}`;
  }

  formatDate(iso: string): string {
    return new Date(iso).toLocaleDateString('en-GB', {
      day: '2-digit', month: 'short', year: 'numeric',
    });
  }

  currentPrices(): ProductPrice[] {
    return this.editing()?.prices ?? [];
  }
}
