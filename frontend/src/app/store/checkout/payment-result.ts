import { HttpClient } from '@angular/common/http';
import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CartService } from '../../core/cart.service';
import { OrderPaymentStatus } from '../../core/models';
import { TlPipe } from '../../core/tl.pipe';
import { CheckoutHeader } from '../checkout-header';

/** Ödeme kuruluşundan dönüş: /odeme/sonuc?order=10483&key=... */
@Component({
  selector: 'app-payment-result',
  imports: [RouterLink, TlPipe, CheckoutHeader],
  template: `
    <app-checkout-header [step]="3" />
    <main class="container page" aria-live="polite">
      @switch (view()) {
        @case ('loading') {
          <p class="text-muted">Ödeme sonucu kontrol ediliyor…</p>
        }
        @case ('paid') {
          <h1>Teşekkürler, ödemeniz alındı.</h1>
          <p>Sipariş numaranız <strong>#{{ data()!.orderNo }}</strong> · Toplam <strong>{{ data()!.total | tl }}</strong></p>
          <p class="text-muted">Sipariş özeti e-posta adresinize gönderilecek.</p>
          <a routerLink="/" class="btn btn-dark">Alışverişe devam et</a>
        }
        @case ('review') {
          <h1>Siparişiniz alındı.</h1>
          <p>Ödemeniz ödeme kuruluşu tarafından kısa bir güvenlik kontrolünden geçiyor. Onaylandığında siparişiniz hazırlanacak.</p>
          <p>Sipariş numaranız <strong>#{{ data()!.orderNo }}</strong></p>
          <a routerLink="/" class="btn btn-dark">Alışverişe devam et</a>
        }
        @case ('pending') {
          <h1>Ödemeniz henüz tamamlanmadı.</h1>
          <p>Sonuç birkaç dakika içinde netleşecek. Bu sayfayı yenileyebilirsiniz.</p>
          <button type="button" class="btn btn-line" (click)="load()">Tekrar kontrol et</button>
        }
        @default {
          <h1>Ödeme tamamlanamadı.</h1>
          <p>{{ data()?.error || 'İşlem sırasında bir sorun oluştu.' }} Kartınızdan para çekilmedi.</p>
          <p class="text-muted">Sepetiniz duruyor; tekrar deneyebilir veya başka bir ödeme yöntemi seçebilirsiniz.</p>
          <div class="actions">
            <a routerLink="/odeme" class="btn btn-dark">Tekrar dene</a>
            <a routerLink="/sepet" class="btn btn-line">Sepete dön</a>
          </div>
        }
      }
    </main>
  `,
  styles: `
    .page { padding-top: 56px; padding-bottom: 96px; display: flex; flex-direction: column; align-items: flex-start; gap: 14px; }
    h1 { margin: 0; font-family: var(--serif); font-weight: 500; font-size: 44px; }
    p { margin: 0; font-size: 16px; max-width: 620px; line-height: 1.6; }
    .btn { margin-top: 10px; }
    .actions { display: flex; flex-wrap: wrap; gap: 12px; }
  `
})
export class PaymentResult {
  private readonly http = inject(HttpClient);
  private readonly cart = inject(CartService);

  readonly order = input<string>();
  readonly key = input<string>();
  readonly error = input<string>();

  protected readonly data = signal<OrderPaymentStatus | null>(null);
  protected readonly view = signal<'loading' | 'paid' | 'review' | 'pending' | 'failed'>('loading');

  constructor() {
    effect(() => {
      this.order();
      this.key();
      untracked(() => this.load());
    });
  }

  protected load(): void {
    const order = this.order();
    const key = this.key();
    if (this.error() || !order || !key) {
      this.view.set('failed');
      return;
    }
    this.view.set('loading');
    this.http.get<OrderPaymentStatus>(`/api/orders/${encodeURIComponent(order)}/status`, { params: { key } }).subscribe({
      next: (s) => {
        this.data.set(s);
        if (s.paymentState === 'Paid') {
          this.view.set('paid');
          this.cart.clear();
        } else if (s.paymentState === 'UnderReview') {
          this.view.set('review');
          this.cart.clear();
        } else if (s.status === 'AwaitingPayment') {
          this.view.set('pending');
        } else {
          this.view.set('failed');
        }
      },
      error: () => this.view.set('failed')
    });
  }
}
