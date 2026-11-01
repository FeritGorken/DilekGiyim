import { Component, computed, input } from '@angular/core';

import { formatTl } from '../../core/tl.pipe';

/**
 * YALNIZCA GELİŞTİRME: backend "Payment:Provider = Mock" iken ödeme kuruluşu sayfasının yerine açılır.
 * Formu gerçek bir ödeme kuruluşu gibi /api/payments/callback adresine POST eder.
 */
@Component({
  selector: 'app-test-payment',
  template: `
    <main class="wrap">
      <div class="box">
        <div class="badge">TEST ÖDEME SAYFASI</div>
        <h1>Sipariş #{{ order() }}</h1>
        <p class="amount">{{ amountText() }}</p>
        <p class="text-muted">Bu sayfa yalnızca geliştirme ortamında görünür. Gerçek ödeme kuruluşu (iyzico) bağlandığında müşteri onun güvenli sayfasına gider.</p>
        <form method="post" action="/api/payments/callback">
          <input type="hidden" name="token" [value]="token()">
          <div class="buttons">
            <button type="submit" name="mockResult" value="success" class="btn btn-dark">Ödeme başarılı</button>
            <button type="submit" name="mockResult" value="fail" class="btn btn-line">Kart reddedildi</button>
            <button type="submit" name="mockResult" value="review" class="btn btn-line">Güvenlik incelemesi</button>
          </div>
        </form>
      </div>
    </main>
  `,
  styles: `
    .wrap { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; background: var(--admin-bg); }
    .box { width: 100%; max-width: 520px; padding: 32px; border-radius: 10px; background: #fff; border: 1px solid var(--admin-line); }
    .badge { display: inline-block; padding: 4px 10px; border-radius: 4px; background: #fbebd3; color: #7a3e0b; font-size: 12px; font-weight: 600; letter-spacing: .1em; }
    h1 { margin: 16px 0 0; font-family: var(--serif); font-size: 34px; font-weight: 500; }
    .amount { margin: 8px 0 0; font-size: 28px; font-weight: 600; color: var(--accent); }
    p { line-height: 1.6; }
    .buttons { margin-top: 20px; display: flex; flex-direction: column; gap: 10px; }
  `
})
export class TestPayment {
  readonly token = input<string>('');
  readonly amount = input<string>('0');
  readonly order = input<string>('');

  protected readonly amountText = computed(() => formatTl(Number(this.amount())));
}
