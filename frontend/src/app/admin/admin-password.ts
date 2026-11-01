import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { AdminAuthService } from './admin-auth';

/** /admin/sifre-yenile?token=… : e-postadaki bağlantıyla yeni şifre belirleme */
@Component({
  selector: 'app-admin-reset',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="wrap">
      <form class="box" (ngSubmit)="submit()">
        <img src="/images/dilek-logo.png" alt="Dilek 1968" width="64" height="64">
        <h1>Yeni şifre belirle</h1>
        @if (done()) {
          <div class="alert success" role="status">{{ done() }}</div>
          <a routerLink="/admin/giris" class="adm-btn primary">Girişe git</a>
        } @else {
          <label class="field">Yeni şifre
            <input class="input" type="password" name="pw" autocomplete="new-password" [(ngModel)]="password">
            <small class="text-muted">En az 8 karakter; bir harf ve bir rakam.</small>
          </label>
          <label class="field">Yeni şifre (tekrar)
            <input class="input" type="password" name="pw2" autocomplete="new-password" [(ngModel)]="password2">
          </label>
          @if (error()) { <div class="alert" role="alert">{{ error() }}</div> }
          <button type="submit" class="adm-btn primary" [disabled]="busy()">Şifreyi kaydet</button>
        }
      </form>
    </main>
  `,
  styles: `
    .wrap { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; background: var(--admin-bg); }
    .box { width: 100%; max-width: 400px; padding: 32px; border-radius: 10px; background: #fff; border: 1px solid var(--admin-line); display: flex; flex-direction: column; gap: 16px; }
    h1 { margin: 0; font-family: var(--serif); font-size: 32px; font-weight: 600; }
    small { font-weight: 400; font-size: 12px; }
  `
})
export class AdminReset {
  private readonly auth = inject(AdminAuthService);
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
    this.auth.reset(this.token() ?? '', this.password).subscribe({
      next: (r) => { this.busy.set(false); this.done.set(r.message); },
      error: (e: HttpErrorResponse) => { this.busy.set(false); this.error.set(e.error?.message ?? 'Şifre kaydedilemedi.'); }
    });
  }
}

/** /admin/hesap : oturum açmış yöneticinin şifre değiştirmesi */
@Component({
  selector: 'app-admin-account',
  imports: [FormsModule],
  template: `
    <h1 class="adm-title">Hesabım</h1>
    @if (auth.me(); as me) {
      <p class="text-muted">{{ me.displayName }} · {{ me.email }}</p>
    }
    <form class="adm-card form" (ngSubmit)="submit()">
      <h2>Şifre değiştir</h2>
      <label class="field">Mevcut şifre<input class="input" type="password" name="cur" autocomplete="current-password" [(ngModel)]="current"></label>
      <label class="field">Yeni şifre<input class="input" type="password" name="pw" autocomplete="new-password" [(ngModel)]="password"></label>
      <label class="field">Yeni şifre (tekrar)<input class="input" type="password" name="pw2" autocomplete="new-password" [(ngModel)]="password2"></label>
      <p class="text-muted hint">En az 8 karakter; bir harf ve bir rakam. Şifre değişince diğer cihazlardaki oturumlar kapanır.</p>
      @if (message(); as m) { <div class="alert" [class.success]="m.ok" role="status">{{ m.text }}</div> }
      <button type="submit" class="adm-btn primary" [disabled]="busy()">Şifreyi güncelle</button>
    </form>
  `,
  styles: `
    .form { margin-top: 20px; max-width: 460px; display: flex; flex-direction: column; gap: 14px; }
    .hint { margin: 0; font-size: 13px; }
  `
})
export class AdminAccount {
  protected readonly auth = inject(AdminAuthService);
  protected current = '';
  protected password = '';
  protected password2 = '';
  protected readonly busy = signal(false);
  protected readonly message = signal<{ text: string; ok: boolean } | null>(null);

  protected submit(): void {
    if (this.password !== this.password2) {
      this.message.set({ text: 'Yeni şifreler aynı değil.', ok: false });
      return;
    }
    this.busy.set(true);
    this.auth.changePassword(this.current, this.password).subscribe({
      next: (r) => {
        this.busy.set(false);
        this.message.set({ text: r.message, ok: true });
        this.current = this.password = this.password2 = '';
      },
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.message.set({ text: e.error?.message ?? 'Şifre güncellenemedi.', ok: false });
      }
    });
  }
}
