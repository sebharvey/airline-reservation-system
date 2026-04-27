import { Component, HostListener, Input, forwardRef } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';
import { Airport, AIRPORTS } from '../../data/airports';
import { LucideAngularModule } from 'lucide-angular';

@Component({
  selector: 'app-airport-combobox',
  standalone: true,
  imports: [FormsModule, CommonModule, LucideAngularModule],
  templateUrl: './airport-combobox.html',
  styleUrl: './airport-combobox.css',
  providers: [{
    provide: NG_VALUE_ACCESSOR,
    useExisting: forwardRef(() => AirportComboboxComponent),
    multi: true
  }]
})
export class AirportComboboxComponent implements ControlValueAccessor {
  /** The `id` forwarded to the inner `<input>` so a parent `<label for="...">` can target it. */
  @Input() inputId = 'airport';
  @Input() placeholder = 'City or code';

  readonly airports = AIRPORTS;
  query = '';
  isOpen = false;
  selectedCode = '';
  isDisabled = false;

  private onChange: (code: string) => void = () => {};
  private onTouched: () => void = () => {};

  get suggestions(): Airport[] {
    const q = this.query.trim().toLowerCase();
    if (!q) return this.airports;
    return this.airports.filter(a =>
      a.code.toLowerCase().includes(q) ||
      a.city.toLowerCase().includes(q) ||
      a.name.toLowerCase().includes(q)
    );
  }

  onQueryInput(): void {
    this.selectedCode = '';
    this.onChange('');
    this.isOpen = true;
  }

  select(airport: Airport): void {
    this.selectedCode = airport.code;
    this.query = airport.code;
    this.isOpen = false;
    this.onChange(airport.code);
    this.onTouched();
  }

  toggle(): void {
    this.isOpen = !this.isOpen;
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('app-airport-combobox')) {
      this.isOpen = false;
    }
  }

  // ── ControlValueAccessor ──────────────────────────────────────────────────

  writeValue(code: string): void {
    this.selectedCode = code ?? '';
    this.query = code ?? '';
  }

  registerOnChange(fn: (code: string) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(disabled: boolean): void {
    this.isDisabled = disabled;
  }
}
