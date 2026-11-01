import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Sepet ve ödeme sayfalarındaki sade üst bilgi (adım göstergesiyle). */
@Component({
  selector: 'app-checkout-header',
  imports: [RouterLink],
  template: `
    <header>
      <div class="container inner">
        <a routerLink="/" class="logo" aria-label="Dilek Giyim ana sayfa">dilek<span>giyim</span></a>
        <ol aria-label="Sipariş adımları">
          <li [class.on]="step() === 1">1. SEPET</li>
          <li [class.on]="step() === 2">2. TESLİMAT VE ÖDEME</li>
          <li [class.on]="step() === 3">3. ONAY</li>
        </ol>
        <span class="secure">
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><rect x="5" y="10" width="14" height="10" rx="2"></rect><path d="M8 10V7a4 4 0 0 1 8 0v3"></path></svg>
          Güvenli alışveriş
        </span>
      </div>
    </header>
  `,
  styles: `
    header { border-bottom: 1px solid var(--line); }
    .inner { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 16px; padding-top: 20px; padding-bottom: 20px; }
    .logo { font-family: var(--serif); font-size: 38px; font-weight: 600; line-height: 1; }
    .logo span { color: var(--accent); font-style: italic; font-weight: 500; }
    ol { margin: 0; padding: 0; list-style: none; display: flex; flex-wrap: wrap; gap: 8px 28px; font-size: 14px; letter-spacing: .08em; color: var(--muted); }
    li.on { color: var(--ink); font-weight: 600; }
    .secure { display: flex; align-items: center; gap: 8px; font-size: 13px; color: var(--text-2); }
  `
})
export class CheckoutHeader {
  readonly step = input(1);
}
