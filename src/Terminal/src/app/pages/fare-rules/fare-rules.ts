import { Component, signal, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { FareRulesService, FareRule, CreateFareRuleRequest, RuleType, TaxLine } from '../../services/fare-rules.service';

@Component({
  selector: 'app-fare-rules',
  imports: [FormsModule],
  templateUrl: './fare-rules.html',
  styleUrl: './fare-rules.css',
})
export class FareRulesComponent implements OnInit {
  #fareRulesService = inject(FareRulesService);

  rules = signal<FareRule[]>([]);
  filter = signal('');
  loading = signal(false);
  error = signal('');
  success = signal('');
  loaded = signal(false);

  // Form state
  showForm = signal(false);
  editing = signal<FareRule | null>(null);
  saving = signal(false);
  deleting = signal<string | null>(null);

  // Form fields
  form = signal<CreateFareRuleRequest>({
    ruleType: 'Money',
    flightNumber: null,
    fareBasisCode: '',
    fareFamily: null,
    cabinCode: 'Y',
    bookingClass: 'Y',
    currencyCode: 'GBP',
    minAmount: 0,
    maxAmount: 0,
    taxAmount: 0,
    minPoints: null,
    maxPoints: null,
    pointsTaxes: null,
    taxLines: [],
    isRefundable: false,
    isChangeable: false,
    changeFeeAmount: 0,
    cancellationFeeAmount: 0,
    validFrom: '',
    validTo: '',
  });

  filtered = computed(() => {
    const q = this.filter().toLowerCase().trim();
    const all = this.rules();
    if (!q) return all;
    return all.filter(
      r =>
        r.fareBasisCode.toLowerCase().includes(q) ||
        (r.fareFamily ?? '').toLowerCase().includes(q) ||
        (r.flightNumber ?? '').toLowerCase().includes(q) ||
        r.cabinCode.toLowerCase().includes(q) ||
        r.ruleType.toLowerCase().includes(q) ||
        (r.currencyCode ?? '').toLowerCase().includes(q)
    );
  });

  stats = computed(() => {
    const all = this.rules();
    const cabins = new Set(all.map(r => r.cabinCode));
    const moneyRules = all.filter(r => r.ruleType === 'Money').length;
    const pointsRules = all.filter(r => r.ruleType === 'Points').length;
    return {
      total: all.length,
      cabins: cabins.size,
      moneyRules,
      pointsRules,
    };
  });

  ngOnInit(): void {
    this.loadRules();
  }

  async loadRules(): Promise<void> {
    this.loading.set(true);
    this.error.set('');
    try {
      const result = await this.#fareRulesService.searchFareRules();
      this.rules.set(result);
      this.loaded.set(true);
    } catch {
      this.error.set('Failed to load fare rules. Please try again.');
    } finally {
      this.loading.set(false);
    }
  }

  setFilter(val: string): void {
    this.filter.set(val);
  }

  openCreateForm(): void {
    this.editing.set(null);
    this.form.set({
      ruleType: 'Money',
      flightNumber: null,
      fareBasisCode: '',
      fareFamily: null,
      cabinCode: 'Y',
      bookingClass: 'Y',
      currencyCode: 'GBP',
      minAmount: 0,
      maxAmount: 0,
      taxAmount: 0,
      minPoints: null,
      maxPoints: null,
      pointsTaxes: null,
      taxLines: [],
      isRefundable: false,
      isChangeable: false,
      changeFeeAmount: 0,
      cancellationFeeAmount: 0,
      validFrom: '',
      validTo: '',
    });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  openEditForm(rule: FareRule): void {
    this.editing.set(rule);
    this.form.set({
      ruleType: rule.ruleType,
      flightNumber: rule.flightNumber,
      fareBasisCode: rule.fareBasisCode,
      fareFamily: rule.fareFamily,
      cabinCode: rule.cabinCode,
      bookingClass: rule.bookingClass,
      currencyCode: rule.currencyCode,
      minAmount: rule.minAmount,
      maxAmount: rule.maxAmount,
      taxAmount: rule.taxAmount,
      minPoints: rule.minPoints,
      maxPoints: rule.maxPoints,
      pointsTaxes: rule.pointsTaxes,
      taxLines: rule.taxLines ? [...rule.taxLines] : [],
      isRefundable: rule.isRefundable,
      isChangeable: rule.isChangeable,
      changeFeeAmount: rule.changeFeeAmount,
      cancellationFeeAmount: rule.cancellationFeeAmount,
      validFrom: rule.validFrom ? rule.validFrom.substring(0, 10) : '',
      validTo: rule.validTo ? rule.validTo.substring(0, 10) : '',
    });
    this.showForm.set(true);
    this.error.set('');
    this.success.set('');
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editing.set(null);
  }

  updateField(field: string, value: unknown): void {
    this.form.update(f => ({ ...f, [field]: value }));
  }

  onRuleTypeChange(ruleType: RuleType): void {
    this.form.update(f => ({
      ...f,
      ruleType,
      currencyCode: ruleType === 'Money' ? (f.currencyCode || 'GBP') : null,
      minAmount: ruleType === 'Money' ? (f.minAmount ?? 0) : null,
      maxAmount: ruleType === 'Money' ? (f.maxAmount ?? 0) : null,
      taxAmount: ruleType === 'Money' ? (f.taxAmount ?? 0) : null,
      minPoints: ruleType === 'Points' ? (f.minPoints ?? 0) : null,
      maxPoints: ruleType === 'Points' ? (f.maxPoints ?? 0) : null,
      pointsTaxes: ruleType === 'Points' ? (f.pointsTaxes ?? 0) : null,
    }));
  }

  addTaxLine(): void {
    this.form.update(f => ({
      ...f,
      taxLines: [...(f.taxLines ?? []), { code: '', amount: 0 }],
    }));
  }

  removeTaxLine(index: number): void {
    this.form.update(f => ({
      ...f,
      taxLines: (f.taxLines ?? []).filter((_, i) => i !== index),
    }));
  }

  updateTaxLineCode(index: number, code: string): void {
    this.form.update(f => {
      const lines = [...(f.taxLines ?? [])];
      lines[index] = { ...lines[index], code: code.toUpperCase() };
      return { ...f, taxLines: lines };
    });
  }

  updateTaxLineAmount(index: number, amount: number): void {
    this.form.update(f => {
      const lines = [...(f.taxLines ?? [])];
      lines[index] = { ...lines[index], amount };
      return { ...f, taxLines: lines };
    });
  }

  async saveRule(): Promise<void> {
    this.saving.set(true);
    this.error.set('');
    this.success.set('');

    const data = this.form();
    const cleanedTaxLines = (data.taxLines ?? []).filter(t => t.code.trim().length > 0);
    const request: CreateFareRuleRequest = {
      ...data,
      flightNumber: data.flightNumber || null,
      fareFamily: data.fareFamily || null,
      currencyCode: data.ruleType === 'Money' ? (data.currencyCode || 'GBP') : null,
      minAmount: data.ruleType === 'Money' ? data.minAmount : null,
      maxAmount: data.ruleType === 'Money' ? data.maxAmount : null,
      taxAmount: data.ruleType === 'Money' ? data.taxAmount : null,
      minPoints: data.ruleType === 'Points' ? data.minPoints : null,
      maxPoints: data.ruleType === 'Points' ? data.maxPoints : null,
      pointsTaxes: data.ruleType === 'Points' ? data.pointsTaxes : null,
      taxLines: cleanedTaxLines.length > 0 ? cleanedTaxLines : null,
      validFrom: data.validFrom ? `${data.validFrom}T00:00:00Z` : null,
      validTo: data.validTo ? `${data.validTo}T23:59:59Z` : null,
    };

    try {
      const editingRule = this.editing();
      if (editingRule) {
        await this.#fareRulesService.updateFareRule(editingRule.fareRuleId, request);
        this.success.set('Fare rule updated successfully.');
      } else {
        await this.#fareRulesService.createFareRule(request);
        this.success.set('Fare rule created successfully.');
      }
      this.showForm.set(false);
      this.editing.set(null);
      await this.loadRules();
    } catch {
      this.error.set('Failed to save fare rule. Please check the data and try again.');
    } finally {
      this.saving.set(false);
    }
  }

  async deleteRule(fareRuleId: string): Promise<void> {
    this.deleting.set(fareRuleId);
    this.error.set('');
    this.success.set('');
    try {
      await this.#fareRulesService.deleteFareRule(fareRuleId);
      this.success.set('Fare rule deleted successfully.');
      await this.loadRules();
    } catch {
      this.error.set('Failed to delete fare rule. Please try again.');
    } finally {
      this.deleting.set(null);
    }
  }

  generateFareBasisCode(): void {
    const f = this.form();
    const bookingClass = (f.bookingClass || 'Y').toUpperCase();
    let code = bookingClass;

    // Fare family indicator
    if (f.fareFamily === 'Flex') {
      code += 'FLEX';
    } else if (f.fareFamily === 'Non-Flex') {
      code += 'NRF';
    }

    // Advance purchase / restriction indicators
    if (!f.isRefundable && !f.isChangeable) {
      code += 'X';
    } else if (f.isRefundable) {
      code += 'R';
    }

    // Cabin and changeable indicators for additional context
    const cabin = (f.cabinCode || 'Y').toUpperCase();
    if (cabin !== bookingClass) {
      code += cabin;
    }
    if (f.isChangeable) {
      code += 'C';
    }

    // Pad with 'O' (open) suffix to ensure minimum 6 characters
    while (code.length < 6) {
      code += 'O';
    }

    // Trim to max 8 characters (IATA fare basis code limit)
    code = code.substring(0, 8).toUpperCase();
    this.updateField('fareBasisCode', code);
  }

  formatDate(iso: string | null): string {
    if (!iso) return '';
    return new Date(iso).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' });
  }

  formatAmount(amount: number | null, currency: string | null): string {
    if (amount == null) return '-';
    return `${currency ?? ''} ${amount.toFixed(2)}`.trim();
  }

  formatPoints(points: number | null): string {
    if (points == null) return '-';
    return points.toLocaleString();
  }

  cabinLabel(code: string): string {
    switch (code) {
      case 'F': return 'First';
      case 'J': return 'Business';
      case 'W': return 'Premium Economy';
      case 'Y': return 'Economy';
      default: return code;
    }
  }
}
