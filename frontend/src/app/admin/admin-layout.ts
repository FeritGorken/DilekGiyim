import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { AdminApi } from './admin-api.service';
import { AdminAuthService } from './admin-auth';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="shell">
      <aside class="side">
        <a routerLink="/" class="logo" aria-label="Siteye git"><img src="/images/dilek-logo.png" alt="Dilek 1968" width="56" height="56"></a>
        <div class="sub">YÖNETİM PANELİ</div>
        <nav aria-label="Yönetim menüsü">
          <a routerLink="/admin" routerLinkActive="on" [routerLinkActiveOptions]="{ exact: true }" ariaCurrentWhenActive="page">Genel Bakış</a>
          <a routerLink="/admin/siparisler" routerLinkActive="on" ariaCurrentWhenActive="page">
            Siparişler @if (pending() > 0) { <span class="badge">{{ pending() }}</span> }
          </a>
          <a routerLink="/admin/urunler" routerLinkActive="on" ariaCurrentWhenActive="page">Ürünler</a>
          <a routerLink="/admin/stok" routerLinkActive="on" ariaCurrentWhenActive="page">
            Stok Takibi @if (critical() > 0) { <span class="badge warn">{{ critical() }}</span> }
          </a>
          <a routerLink="/admin/fiyat" routerLinkActive="on" ariaCurrentWhenActive="page">Fiyat, Kampanya ve Sepet</a>
        </nav>
        <div class="user">
          @if (auth.me(); as me) { <a routerLink="/admin/hesap" class="who">{{ me.displayName }}<small>Şifre değiştir</small></a> }
          <button type="button" class="logout" (click)="logout()">Çıkış yap</button>
        </div>
      </aside>
      <main class="content">
        <router-outlet />
      </main>
    </div>
  `,
  styles: `
    .shell { min-height: 100vh; display: flex; flex-wrap: wrap; background: var(--admin-bg); }
    .side { flex: 1 0 240px; max-width: 100%; padding: 24px 16px; background: var(--ink); color: #cfc5b8; display: flex; flex-direction: column; }
    .logo { display: flex; padding: 0 12px 10px; }
    .logo img { width: 56px; height: 56px; }
    .sub { padding: 0 12px; font-size: 11px; letter-spacing: .28em; color: #a99e91; }
    nav { margin-top: 28px; display: flex; flex-direction: column; gap: 2px; }
    nav a { display: flex; justify-content: space-between; align-items: center; min-height: 44px; padding: 0 12px; border-radius: 6px; font-size: 15px; color: #cfc5b8; }
    nav a:hover { background: #2e2722; color: #fff; }
    nav a.on { background: var(--cream); color: var(--ink); font-weight: 500; }
    .badge { min-width: 22px; height: 22px; padding: 0 6px; border-radius: 11px; font-size: 12px; font-weight: 600; display: inline-flex; align-items: center; justify-content: center; background: var(--accent-light); color: var(--ink); }
    .badge.warn { background: #e8b04a; }
    .user { margin-top: auto; padding-top: 24px; display: flex; flex-direction: column; gap: 8px; }
    .who { display: flex; flex-direction: column; padding: 8px 12px; color: var(--cream); font-size: 14px; }
    .who small { font-size: 12px; color: #a99e91; }
    .logout { min-height: 44px; padding: 0 12px; border: 1px solid #3a322c; border-radius: 6px; background: transparent; color: #cfc5b8; text-align: left; cursor: pointer; }
    .content { flex: 999 1 560px; min-width: 0; padding: 28px 32px 56px; }
    @media (max-width: 640px) { .content { padding: 20px 16px 40px; } }
  `
})
export class AdminLayout implements OnInit {
  private readonly api = inject(AdminApi);
  protected readonly auth = inject(AdminAuthService);
  private readonly router = inject(Router);

  protected readonly pending = signal(0);
  protected readonly critical = signal(0);

  ngOnInit(): void {
    this.api.orderCounts().subscribe((list) =>
      this.pending.set(list.filter((x) => x.status === 'New').reduce((s, x) => s + x.count, 0)));
    this.api.stock('all').subscribe((s) => this.critical.set(s.summary.critical + s.summary.out));
  }

  protected logout(): void {
    this.auth.logout().subscribe(() => this.router.navigate(['/admin/giris']));
  }
}
