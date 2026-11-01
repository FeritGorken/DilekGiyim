import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AccountService } from '../../core/account.service';
import { ORDER_STATUS_LABELS, TrackedOrder } from '../../core/models';
import { TlPipe } from '../../core/tl.pipe';

/** /hesap/sifre-yenile?token=… */
@Component({
  selector: 'app-account-reset',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="container narrow">
      <h1>Yeni şifre belirle</h1>
      @if (done()) {
        <div class="alert success" role="status">{{ done() }}</div>
        <a routerLink="/hesap/giris" class="btn btn-dark">Giriş yap</a>
      } @else {
        <form (ngSubmit)="submit()">
          <label class="field">Yeni şifre
            <input class="input" type="password" name="pw" autocomplete="new-password" [(ngModel)]="password">
            <small class="text-muted">En az 8 karakter; bir harf ve bir rakam.</small>
          </label>
          <label class="field">Yeni şifre (tekrar)
            <input class="input" type="password" name="pw2" autocomplete="new-password" [(ngModel)]="password2">
          </label>
          @if (error()) { <div class="alert" role="alert">{{ error() }}</div> }
          <button type="submit" class="btn btn-dark" [disabled]="busy()">Şifreyi kaydet</button>
        </form>
      }
    </main>
  `,
  styles: `
    .narrow { max-width: 480px; padding-top: 56px; display: flex; flex-direction: column; gap: 18px; }
    h1 { margin: 0; font-family: var(--serif); font-weight: 500; font-size: 40px; }
    form { display: flex; flex-direction: column; gap: 16px; }
    small { font-size: 12px; font-weight: 400; }
  `
})
export class AccountReset {
  private readonly account = inject(AccountService);
  readonly token = input<string>('');

  protected password = '';
  protected password2 = '';
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly done = signal<string | null>(null);

  protected submit(): void {
    if (this.password !== this.password2) {
      this.error.set('Şifreler aynı değil.');
      return;
    }
    this.busy.set(true);
    this.account.reset(this.token() ?? '', this.password).subscribe({
      next: (r) => { this.busy.set(false); this.done.set(r.message); },
      error: (e: HttpErrorResponse) => { this.busy.set(false); this.error.set(e.error?.message ?? 'Şifre kaydedilemedi.'); }
    });
  }
}

/** /siparis-takip : üye olmadan sipariş no + e-posta ile takip */
@Component({
  selector: 'app-order-track',
  imports: [FormsModule, RouterLink, DatePipe, TlPipe],
  template: `
    <main class="container narrow">
      <div class="eyebrow">Sipariş takibi</div>
      <h1>Siparişim nerede?</h1>
      <p class="text-muted">Sipariş onay e-postanızdaki sipariş numarasını ve siparişte kullandığınız e-postayı yazın.</p>
      <form (ngSubmit)="submit()">
        <label class="field">Sipariş no<input class="input" name="no" placeholder="Örn. 10483" [(ngModel)]="orderNo"></label>
        <label class="field">E-posta<input class="input" type="email" name="em" autocomplete="email" [(ngModel)]="email"></label>
        <button type="submit" class="btn btn-dark" [disabled]="busy() || !orderNo || !email">Sorgula</button>
      </form>
      @if (error()) { <div class="alert" role="alert">{{ error() }}</div> }
      @if (result(); as r) {
        <section class="result" aria-live="polite">
          <div class="row"><span>Sipariş</span><strong>#{{ r.orderNo }}</strong></div>
          <div class="row"><span>Tarih</span><span>{{ r.createdAt | date: 'd MMMM y' }}</span></div>
          <div class="row"><span>Durum</span><span class="pill" [class]="'pill-' + r.status">{{ labels[r.status] }}</span></div>
          @if (r.trackingNo) { <div class="row"><span>Kargo takip no</span><strong>{{ r.trackingNo }}</strong></div> }
          <div class="row"><span>Tutar</span><span>{{ r.total | tl }} · {{ r.itemCount }} ürün</span></div>
        </section>
        <p class="text-muted">Siparişlerinizi tek yerden görmek için <a routerLink="/hesap/giris">üye olun</a>.</p>
      }
    </main>
  `,
  styles: `
    .narrow { max-width: 520px; padding-top: 56px; display: flex; flex-direction: column; gap: 16px; }
    h1 { margin: 0; font-family: var(--serif); font-weight: 500; font-size: 42px; }
    p { margin: 0; }
    p a { text-decoration: underline; }
    form { display: flex; flex-direction: column; gap: 14px; }
    .result { padding: 20px; border: 1px solid var(--line); border-radius: 8px; display: flex; flex-direction: column; gap: 10px; font-size: 15px; }
    .row { display: flex; justify-content: space-between; align-items: center; gap: 12px; }
  `
})
export class OrderTrack {
  private readonly account = inject(AccountService);
  protected readonly labels = ORDER_STATUS_LABELS;
  protected orderNo = '';
  protected email = '';
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly result = signal<TrackedOrder | null>(null);

  protected submit(): void {
    this.busy.set(true);
    this.error.set(null);
    this.result.set(null);
    this.account.track(this.orderNo.trim(), this.email.trim()).subscribe({
      next: (r) => { this.busy.set(false); this.result.set(r); },
      error: (e: HttpErrorResponse) => { this.busy.set(false); this.error.set(e.error?.message ?? 'Sipariş bulunamadı.'); }
    });
  }
}
