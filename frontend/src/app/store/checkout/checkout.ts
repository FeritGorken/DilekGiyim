import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { CartService } from '../../core/cart.service';
import { CheckoutResult } from '../../core/models';
import { TlPipe } from '../../core/tl.pipe';
import { CheckoutHeader } from '../checkout-header';

@Component({
  selector: 'app-checkout-page',
  imports: [FormsModule, RouterLink, TlPipe, CheckoutHeader],
  templateUrl: './checkout.html',
  styleUrl: './checkout.scss'
})
export class CheckoutPage implements OnInit {
  protected readonly cart = inject(CartService);

  protected form = {
    customerName: '',
    email: '',
    phone: '',
    city: '',
    address: '',
    note: '',
    paymentMethod: 'Havale / EFT',
    accept: false
  };

  protected readonly paymentOptions = [
    { value: 'Havale / EFT', label: 'Havale / EFT', help: 'IBAN bilgisi sipariş onayıyla e-postanıza gönderilir.', disabled: false },
    { value: 'Kapıda ödeme', label: 'Kapıda ödeme', help: 'Teslimatta nakit veya kartla ödeyin.', disabled: false },
    { value: 'Kredi kartı', label: 'Kredi kartı', help: 'Yakında: ödeme altyapısı bağlandığında açılacak.', disabled: true }
  ];

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly result = signal<CheckoutResult | null>(null);

  ngOnInit(): void {
    this.cart.refresh();
  }

  protected submit(f: NgForm): void {
    this.error.set(null);
    if (f.invalid || !this.form.accept) {
      f.form.markAllAsTouched();
      this.error.set('Lütfen zorunlu alanları doldurun ve sözleşmeyi onaylayın.');
      return;
    }
    this.submitting.set(true);
    const { accept, ...data } = this.form;
    this.cart.checkout({ ...data, installments: 1, note: data.note || null }).subscribe({
      next: (r) => {
        this.result.set(r);
        this.submitting.set(false);
      },
      error: (e: HttpErrorResponse) => {
        this.error.set(e.error?.message ?? 'Sipariş oluşturulamadı. Lütfen tekrar deneyin.');
        this.submitting.set(false);
        this.cart.refresh();
      }
    });
  }
}
