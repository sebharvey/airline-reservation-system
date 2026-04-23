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
  ALL_CHANNELS,
  ALL_CHANNELS_JSON,
  ChannelCode,
  ProductAvailabilityRule,
  ProductRuleCondition,
  RuleConditionField,
  RuleConditionOperator,
} from '../../services/product.service';
import { ProductGroupService, ProductGroup } from '../../services/product-group.service';
import { SsrCatalogueService, SsrCatalogueEntry } from '../../services/ssr-catalogue.service';

const ALL_CHANNELS_DEFAULT = ALL_CHANNELS_JSON;

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

  // Tab navigation within the form
  activeTab = signal<'details' | 'rules'>('details');

  // Price management
  showPriceForm = signal(false);
  editingPrice = signal<ProductPrice | null>(null);
  savingPrice = signal(false);
  deletingPrice = signal<string | null>(null);
  pendingPrices = signal<CreateProductPriceRequest[]>([]);

  // Channel availability
  readonly channelOptions: { code: ChannelCode; label: string }[] = [
    { code: 'WEB',     label: 'Web' },
    { code: 'APP',     label: 'App' },
    { code: 'NDC',     label: 'NDC' },
    { code: 'KIOSK',   label: 'Kiosk' },
    { code: 'CC',      label: 'Contact Centre' },
    { code: 'AIRPORT', label: 'Airport' },
  ];

  createChannels = signal<Set<ChannelCode>>(new Set(ALL_CHANNELS));
  updateChannels = signal<Set<ChannelCode>>(new Set(ALL_CHANNELS));

  // Availability rules
  createRules = signal<ProductAvailabilityRule[]>([]);
  updateRules = signal<ProductAvailabilityRule[]>([]);

  readonly conditionFieldOptions: { value: RuleConditionField; label: string; hint: string }[] = [
    { value: 'departureAirport', label: 'Departure airport', hint: 'IATA code, e.g. JFK' },
    { value: 'arrivalAirport',   label: 'Arrival airport',   hint: 'IATA code, e.g. LHR' },
    { value: 'cabinClass',       label: 'Cabin class',       hint: 'F, J, W or Y' },
    { value: 'passengerType',    label: 'Passenger type',    hint: 'ADT, CHD, INF or YTH' },
    { value: 'route',            label: 'Route',             hint: 'e.g. JFK-LHR' },
    { value: 'flightNumber',     label: 'Flight number',     hint: 'e.g. AX101' },
    { value: 'dayOfWeek',        label: 'Day of week',       hint: 'MON, TUE, WED, THU, FRI, SAT or SUN' },
  ];

  createForm = signal<CreateProductRequest>({
    productGroupId: '',
    name: '',
    description: '',
    isSegmentSpecific: false,
    ssrCode: null,
    imageBase64: null,
    availableChannels: ALL_CHANNELS_DEFAULT,
    availabilityRules: null,
  });

  updateForm = signal<UpdateProductRequest>({
    productGroupId: '',
    name: '',
    description: '',
    isSegmentSpecific: false,
    ssrCode: null,
    imageBase64: null,
    availableChannels: ALL_CHANNELS_DEFAULT,
    availabilityRules: null,
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
    this.activeTab.set('details');
    this.showPriceForm.set(false);
    this.pendingPrices.set([]);
    this.createChannels.set(new Set(ALL_CHANNELS));
    this.createRules.set([]);
    this.createForm.set({
      productGroupId: this.groups()[0]?.productGroupId ?? '',
      name: '',
      description: '',
      isSegmentSpecific: false,
      ssrCode: null,
      imageBase64: null,
      availableChannels: ALL_CHANNELS_DEFAULT,
      availabilityRules: null,
    });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  async openEditForm(product: Product): Promise<void> {
    this.error.set('');
    this.success.set('');
    this.loading.set(true);
    try {
      const fresh = await this.#service.getById(product.productId);
      this.editing.set(fresh);
      this.activeTab.set('details');
      this.showPriceForm.set(false);
      this.editingPrice.set(null);
      const channels = this.#parseChannels(fresh.availableChannels);
      this.updateChannels.set(channels);
      this.updateRules.set(this.#parseRules(fresh.availabilityRules));
      this.updateForm.set({
        productGroupId: fresh.productGroupId,
        name: fresh.name,
        description: fresh.description,
        isSegmentSpecific: fresh.isSegmentSpecific,
        ssrCode: fresh.ssrCode,
        imageBase64: fresh.imageBase64,
        availableChannels: this.#channelsToString(channels),
        availabilityRules: fresh.availabilityRules,
        isActive: fresh.isActive,
      });
      this.showForm.set(true);
    } catch {
      this.error.set('Failed to load product. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editing.set(null);
    this.activeTab.set('details');
    this.showPriceForm.set(false);
    this.editingPrice.set(null);
    this.pendingPrices.set([]);
  }

  updateCreateField(field: keyof CreateProductRequest, value: unknown): void {
    this.createForm.update(f => ({ ...f, [field]: value }));
  }

  updateUpdateField(field: keyof UpdateProductRequest, value: unknown): void {
    this.updateForm.update(f => ({ ...f, [field]: value }));
  }

  // ── Channel helpers ───────────────────────────────────────────────────────

  #parseChannels(json: string): Set<ChannelCode> {
    try {
      const codes = (JSON.parse(json) as string[]).map(c => c.toUpperCase()) as ChannelCode[];
      return new Set(codes.filter(c => (ALL_CHANNELS as readonly string[]).includes(c)));
    } catch {
      return new Set(ALL_CHANNELS);
    }
  }

  isChannelSelected(mode: 'create' | 'update', code: ChannelCode): boolean {
    return mode === 'create' ? this.createChannels().has(code) : this.updateChannels().has(code);
  }

  toggleChannel(mode: 'create' | 'update', code: ChannelCode): void {
    if (mode === 'create') {
      this.createChannels.update(s => {
        const next = new Set(s);
        next.has(code) ? next.delete(code) : next.add(code);
        return next;
      });
      this.createForm.update(f => ({ ...f, availableChannels: this.#channelsToString(this.createChannels()) }));
    } else {
      this.updateChannels.update(s => {
        const next = new Set(s);
        next.has(code) ? next.delete(code) : next.add(code);
        return next;
      });
      this.updateForm.update(f => ({ ...f, availableChannels: this.#channelsToString(this.updateChannels()) }));
    }
  }

  selectAllChannels(mode: 'create' | 'update'): void {
    if (mode === 'create') {
      this.createChannels.set(new Set(ALL_CHANNELS));
      this.createForm.update(f => ({ ...f, availableChannels: ALL_CHANNELS_DEFAULT }));
    } else {
      this.updateChannels.set(new Set(ALL_CHANNELS));
      this.updateForm.update(f => ({ ...f, availableChannels: ALL_CHANNELS_DEFAULT }));
    }
  }

  allChannelsSelected(mode: 'create' | 'update'): boolean {
    const s = mode === 'create' ? this.createChannels() : this.updateChannels();
    return s.size === ALL_CHANNELS.length;
  }

  #channelsToString(s: Set<ChannelCode>): string {
    return JSON.stringify(ALL_CHANNELS.filter(c => s.has(c)));
  }

  channelBadges(availableChannels: string): string[] {
    try {
      const parsed = JSON.parse(availableChannels);
      return Array.isArray(parsed) ? parsed : [];
    } catch {
      return [];
    }
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

  // ── Availability rules ────────────────────────────────────────────────────

  currentMode(): 'create' | 'update' {
    return this.editing() ? 'update' : 'create';
  }

  currentRules(): ProductAvailabilityRule[] {
    return this.editing() ? this.updateRules() : this.createRules();
  }

  #parseRules(json: string | null): ProductAvailabilityRule[] {
    if (!json) return [];
    try {
      return JSON.parse(json) as ProductAvailabilityRule[];
    } catch {
      return [];
    }
  }

  #rulesToJson(rules: ProductAvailabilityRule[]): string | null {
    return rules.length > 0 ? JSON.stringify(rules) : null;
  }

  addRule(mode: 'create' | 'update'): void {
    const rule: ProductAvailabilityRule = {
      id: crypto.randomUUID(),
      conditions: [{ field: 'departureAirport', operator: 'is', value: '' }],
    };
    if (mode === 'create') {
      this.createRules.update(r => [...r, rule]);
    } else {
      this.updateRules.update(r => [...r, rule]);
    }
  }

  removeRule(mode: 'create' | 'update', ruleId: string): void {
    const filter = (rules: ProductAvailabilityRule[]) => rules.filter(r => r.id !== ruleId);
    if (mode === 'create') {
      this.createRules.update(filter);
    } else {
      this.updateRules.update(filter);
    }
  }

  addCondition(mode: 'create' | 'update', ruleId: string): void {
    const newCond: ProductRuleCondition = { field: 'departureAirport', operator: 'is', value: '' };
    const upd = (rules: ProductAvailabilityRule[]) =>
      rules.map(r => r.id === ruleId ? { ...r, conditions: [...r.conditions, newCond] } : r);
    if (mode === 'create') {
      this.createRules.update(upd);
    } else {
      this.updateRules.update(upd);
    }
  }

  removeCondition(mode: 'create' | 'update', ruleId: string, condIdx: number): void {
    const upd = (rules: ProductAvailabilityRule[]) =>
      rules.map(r => r.id === ruleId
        ? { ...r, conditions: r.conditions.filter((_, i) => i !== condIdx) }
        : r);
    if (mode === 'create') {
      this.createRules.update(upd);
    } else {
      this.updateRules.update(upd);
    }
  }

  updateCondition(
    mode: 'create' | 'update',
    ruleId: string,
    condIdx: number,
    field: 'field' | 'operator' | 'value',
    value: string,
  ): void {
    const upd = (rules: ProductAvailabilityRule[]) =>
      rules.map(r => r.id === ruleId
        ? {
            ...r,
            conditions: r.conditions.map((c, i) =>
              i === condIdx ? { ...c, [field]: value as RuleConditionField & RuleConditionOperator } : c
            ),
          }
        : r);
    if (mode === 'create') {
      this.createRules.update(upd);
    } else {
      this.updateRules.update(upd);
    }
  }

  conditionFieldLabel(field: RuleConditionField): string {
    return this.conditionFieldOptions.find(o => o.value === field)?.label ?? field;
  }

  conditionHint(field: RuleConditionField): string {
    return this.conditionFieldOptions.find(o => o.value === field)?.hint ?? '';
  }

  ruleConditionSummary(rule: ProductAvailabilityRule): string {
    if (rule.conditions.length === 0) return 'No conditions';
    return rule.conditions
      .map(c => `${this.conditionFieldLabel(c.field)} ${c.operator === 'is' ? 'is' : 'is not'} ${c.value || '…'}`)
      .join(' AND ');
  }

  // ── Save ──────────────────────────────────────────────────────────────────

  async save(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    this.success.set('');
    try {
      const editingProduct = this.editing();
      if (editingProduct) {
        const request: UpdateProductRequest = {
          ...this.updateForm(),
          availabilityRules: this.#rulesToJson(this.updateRules()),
        };
        await this.#service.update(editingProduct.productId, request);
        this.success.set('Product updated successfully.');
      } else {
        const request: CreateProductRequest = {
          ...this.createForm(),
          availabilityRules: this.#rulesToJson(this.createRules()),
        };
        const created = await this.#service.create(request);
        for (const price of this.pendingPrices()) {
          await this.#service.createPrice(created.productId, price);
        }
        this.pendingPrices.set([]);
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

  removePendingPrice(index: number): void {
    this.pendingPrices.update(prices => prices.filter((_, i) => i !== index));
  }

  async savePrice(): Promise<void> {
    const product = this.editing();
    if (!product) {
      const form = this.priceCreateForm();
      if (this.pendingPrices().some(p => p.currencyCode === form.currencyCode)) {
        this.error.set('A price for this currency is already configured.');
        return;
      }
      this.pendingPrices.update(prices => [
        ...prices,
        { ...form, tax: +(form.price * 0.2).toFixed(2) },
      ]);
      this.showPriceForm.set(false);
      return;
    }

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
    return `${amount.toLocaleString('en-GB', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ${currency}`;
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
