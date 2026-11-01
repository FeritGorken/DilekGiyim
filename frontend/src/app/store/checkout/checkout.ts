import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule, NgForm } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AccountService } from '../../core/account.service';
import { CartService } from '../../core/cart.service';
import { CheckoutResult, PAYMENT_METHODS } from '../../core/models';
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
  protected readonly account = inject(AccountService);

  protected form = {
    customerName: '',
    email: '',
    phone: '',
    city: '',
    address: '',
    note: '',
    identityNumber: '',
    paymentMethod: PAYMENT_METHODS.card as string,
    accept: false
  };

  protected readonly cardMethod = PAYMENT_METHODS.card;

  protected readonly paymentOptions = [
    { value: PAYMENT_METHODS.card, label: 'Kredi / banka kartı', help: '3D Secure ile güvenli ödeme sayfasına yönlendirilirsiniz. Taksit seçenekleri orada gösterilir.' },
    { value: PAYMENT_METHODS.transfer, label: 'Havale / EFT', help: 'IBAN bilgisi sipariş onayıyla e-postanıza gönderilir.' },
    { value: PAYMENT_METHODS.cod, label: 'Kapıda ödeme', help: 'Teslimatta nakit veya kartla ödeyin.' }
  ];

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly result = signal<CheckoutResult | null>(null);
  protected readonly redirecting = signal(false);
  protected readonly prefilled = signal(false);

  ngOnInit(): void {
    this.cart.refresh();
    // Üye ise bilgileri ve varsayılan adresi doldur
    const fill = () => {
      const me = this.account.me();
      if (!me) return;
      this.form.customerName ||= `${me.firstName} ${me.lastName}`;
      this.form.email ||= me.email;
      this.form.phone ||= me.phone;
      this.account.addresses().subscribe((list) => {
        const a = list.find((x) => x.isDefault) ?? list[0];
        if (!a) return;
        this.form.city ||= a.city;
        this.form.address ||= `${a.line}, ${a.district}`;
        this.prefilled.set(true);
      });
    };
    if (this.account.me() === undefined) this.account.check().subscribe(fill);
    else fill();
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
    const isCard = data.paymentMethod === PAYMENT_METHODS.card;
    this.cart.checkout({
      ...data,
      installments: 1,
      note: data.note || null,
      identityNumber: isCard && data.identityNumber ? data.identityNumber : null
    }).subscribe({
      next: (r) => {
        if (r.paymentPageUrl) {
          // Ödeme kuruluşunun güvenli sayfasına geç; dönüşte /odeme/sonuc açılır
          this.redirecting.set(true);
          window.location.href = r.paymentPageUrl;
          return;
        }
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
