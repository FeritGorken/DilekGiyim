import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AdminAuthService } from '../admin-auth';

@Component({
  selector: 'app-admin-login',
  imports: [FormsModule, RouterLink],
  template: `
    <main class="wrap">
      <form class="box" (ngSubmit)="submit()">
        <a routerLink="/" class="logo">dilek<span>giyim</span></a>
        <div class="sub">YÖNETİM PANELİ</div>
        <label class="field">Yönetici anahtarı
          <input class="input" type="password" name="key" autocomplete="current-password" required [(ngModel)]="key">
        </label>
        @if (error()) { <div class="alert" role="alert">{{ error() }}</div> }
        <button type="submit" class="adm-btn primary" [disabled]="busy() || !key">{{ busy() ? 'Kontrol ediliyor…' : 'Giriş yap' }}</button>
      </form>
    </main>
  `,
  styles: `
    .wrap { min-height: 100vh; display: flex; align-items: center; justify-content: center; padding: 24px; background: var(--admin-bg); }
    .box { width: 100%; max-width: 380px; padding: 32px; border-radius: 10px; background: #fff; border: 1px solid var(--admin-line); display: flex; flex-direction: column; gap: 16px; }
    .logo { font-family: var(--serif); font-size: 38px; font-weight: 600; line-height: 1; }
    .logo span { color: var(--accent); font-weight: 500; }
    .sub { margin-top: -10px; font-size: 11px; letter-spacing: .28em; color: var(--muted); }
  `
})
export class AdminLogin {
  private readonly auth = inject(AdminAuthService);
  private readonly router = inject(Router);

  protected key = '';
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected submit(): void {
    if (!this.key) return;
    this.busy.set(true);
    this.error.set(null);
    this.auth.login(this.key).subscribe((ok) => {
      this.busy.set(false);
      if (ok) this.router.navigate(['/admin']);
      else this.error.set('Anahtar hatalı veya sunucuya ulaşılamadı.');
    });
  }
}
