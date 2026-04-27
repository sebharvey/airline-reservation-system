import { Component, signal, computed, input, output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { CardDetails } from '../../models/order.model';

@Component({
  selector: 'app-payment-form',
  standalone: true,
  imports: [FormsModule],
  templateUrl: './payment-form.html',
  styleUrl: './payment-form.css'
})
export class PaymentFormComponent {
  readonly paying = input.required<boolean>();
  readonly disabled = input(false);
  readonly payLabel = input.required<string>();
  readonly paymentError = input('');

  readonly pay = output<CardDetails>();
  readonly errorDismissed = output<void>();

  cardholderName = signal('');
  cardNumber = signal('');
  expiryMonth = signal('');
  expiryYear = signal('');
  cvv = signal('');
  submitted = signal(false);

  readonly cardDisplayNumber = computed(() => {
    const raw = this.cardNumber().replace(/\D/g, '').substring(0, 16);
    return raw.replace(/(.{4})/g, '$1 ').trim();
  });

  readonly expiryYears = computed(() => {
    const current = new Date().getFullYear();
    return Array.from({ length: 12 }, (_, i) => current + i);
  });

  readonly expiryMonths = [
    { value: '01', label: '01 - Jan' }, { value: '02', label: '02 - Feb' },
    { value: '03', label: '03 - Mar' }, { value: '04', label: '04 - Apr' },
    { value: '05', label: '05 - May' }, { value: '06', label: '06 - Jun' },
    { value: '07', label: '07 - Jul' }, { value: '08', label: '08 - Aug' },
    { value: '09', label: '09 - Sep' }, { value: '10', label: '10 - Oct' },
    { value: '11', label: '11 - Nov' }, { value: '12', label: '12 - Dec' }
  ];

  detectCardType(): string {
    const num = this.cardNumber().replace(/\D/g, '');
    if (num.startsWith('4')) return 'Visa';
    if (/^5[1-5]/.test(num) || /^2[2-7]/.test(num)) return 'Mastercard';
    if (/^3[47]/.test(num)) return 'Amex';
    return 'Card';
  }

  isFormValid(): boolean {
    const name = this.cardholderName().trim();
    const num = this.cardNumber().replace(/\D/g, '');
    const month = this.expiryMonth();
    const year = this.expiryYear();
    const cvv = this.cvv().trim();
    return !!(name && num.length === 16 && month && year && cvv.length >= 3);
  }

  onCardNumberInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const digits = input.value.replace(/\D/g, '').substring(0, 16);
    this.cardNumber.set(digits);
    input.value = digits.replace(/(.{4})/g, '$1 ').trim();
  }

  fillTestCard(): void {
    const nextYear = (new Date().getFullYear() + 3).toString();
    this.cardholderName.set('Test User');
    this.cardNumber.set('4111111111111111');
    this.expiryMonth.set('12');
    this.expiryYear.set(nextYear);
    this.cvv.set('123');
  }

  onPay(): void {
    this.submitted.set(true);
    if (!this.isFormValid()) return;

    const num = this.cardNumber().replace(/\D/g, '');
    this.pay.emit({
      cardholderName: this.cardholderName().trim(),
      cardNumber: num,
      expiryMonth: this.expiryMonth(),
      expiryYear: this.expiryYear(),
      cvv: this.cvv().trim(),
      expiryDate: `${this.expiryMonth()}/${this.expiryYear().slice(-2)}`,
      cardType: this.detectCardType(),
      cardLast4: num.slice(-4)
    });
  }

  dismissErrorModal(): void {
    this.errorDismissed.emit();
  }
}
