import { Component, computed, inject, input } from '@angular/core';
import { Router, RouterLink } from '@angular/router';

import { AccountService } from '../core/account.service';
import { ProductListItem } from '../core/models';
import { TlPipe } from '../core/tl.pipe';

@Component({
  selector: 'app-product-card',
  imports: [RouterLink, TlPipe],
  template: `
    @let p = product();
    <article class="card">
      <div class="media-wrap">
      <a [routerLink]="['/urun', p.slug]" class="media">
        @if (p.imageUrl) {
          <img [src]="p.imageUrl" [alt]="p.name" loading="lazy">
        } @else {
          <span class="placeholder">
            <svg width="44" height="44" viewBox="0 0 48 48" fill="none" stroke="currentColor" stroke-width="1.4" aria-hidden="true"><path d="M24 12a4 4 0 1 1 4 4c-2 0-4 1.5-4 4v1"></path><path d="M24 21L6 34h36L24 21z"></path></svg>
            Ürün fotoğrafı
          </span>
        }
        @if (discountPct(); as d) {
          <span class="tag accent">%{{ d }} indirim</span>
        } @else if (p.isNew) {
          <span class="tag">Yeni</span>
        }
      </a>
        <button type="button" class="fav" [class.on]="isFav()" [attr.aria-pressed]="isFav()"
          [attr.aria-label]="isFav() ? 'Favorilerden çıkar' : 'Favorilere ekle'" (click)="toggleFav($event)">
          <svg width="18" height="18" viewBox="0 0 24 24" [attr.fill]="isFav() ? 'currentColor' : 'none'" stroke="currentColor" stroke-width="1.6" aria-hidden="true"><path d="M12 20s-7-4.4-7-10a4 4 0 0 1 7-2.6A4 4 0 0 1 19 10c0 5.6-7 10-7 10z"></path></svg>
        </button>
      </div>
      <div class="meta">{{ p.categoryName.toLocaleUpperCase('tr-TR') }}</div>
      <h3><a [routerLink]="['/urun', p.slug]">{{ p.name }}</a></h3>
      @if (p.colors.length) {
        <div class="swatches">
          @for (c of p.colors; track c.name) {
            <span class="sw" [style.background]="c.hex" [title]="c.name"></span>
          }
        </div>
      }
      <div class="price">
        <span>{{ (p.salePrice ?? p.price) | tl }}</span>
        @if (p.salePrice) {
          <s>{{ p.price | tl }}</s>
        }
      </div>
      @switch (p.stockStatus) {
        @case ('out') { <div class="stock text-danger">Tükendi</div> }
        @case ('low') { <div class="stock text-warn">Son {{ p.lowStockCount }} ürün</div> }
        @default { <div class="stock text-ok">Stokta</div> }
      }
    </article>
  `,
  styles: `
    .card { display: flex; flex-direction: column; }
    .media { position: relative; display: block; aspect-ratio: 3 / 4; border-radius: 4px; overflow: hidden; background: var(--cream-2); border: 1px solid #efe9e0; }
    .media img { width: 100%; height: 100%; object-fit: cover; object-position: center top; transition: transform .4s; }
    .media:hover img { transform: scale(1.03); }
    .placeholder { width: 100%; height: 100%; display: flex; flex-direction: column; align-items: center; justify-content: center; gap: 10px; color: var(--muted); font-size: 13px; }
    .tag { position: absolute; left: 12px; top: 12px; padding: 5px 10px; border-radius: 4px; font-size: 12px; font-weight: 500; color: #fff; background: var(--ink); }
    .tag.accent { background: var(--accent); }
    .media-wrap { position: relative; }
    .fav { position: absolute; right: 10px; top: 10px; z-index: 1; width: 40px; height: 40px; border-radius: 20px; border: 0; background: #fff; color: var(--ink); cursor: pointer; display: flex; align-items: center; justify-content: center; }
    .fav.on { color: var(--accent); }
    .meta { margin-top: 14px; font-size: 12px; letter-spacing: .14em; color: var(--muted); }
    h3 { margin: 6px 0 0; font-family: var(--serif); font-size: 23px; font-weight: 500; line-height: 1.2; }
    .swatches { margin-top: 8px; display: flex; gap: 6px; }
    .sw { width: 16px; height: 16px; border-radius: 8px; border: 1px solid var(--line-3); }
    .price { margin-top: 10px; display: flex; align-items: baseline; gap: 10px; }
    .price span { font-size: 18px; font-weight: 600; color: var(--accent); }
    .price s { font-size: 14px; color: var(--muted); }
    .stock { margin-top: 6px; font-size: 13px; font-weight: 500; }
  `
})
export class ProductCard {
  private readonly account = inject(AccountService);
  private readonly router = inject(Router);
  readonly product = input.required<ProductListItem>();

  protected readonly isFav = computed(() => this.account.favoriteIds().has(this.product().id));

  protected toggleFav(e: Event): void {
    e.preventDefault();
    e.stopPropagation();
    if (!this.account.toggleFavorite(this.product().id))
      this.router.navigate(['/hesap/giris'], { queryParams: { returnUrl: this.router.url } });
  }

  protected readonly discountPct = computed(() => {
    const p = this.product();
    return p.salePrice ? Math.round((1 - p.salePrice / p.price) * 100) : 0;
  });
}
