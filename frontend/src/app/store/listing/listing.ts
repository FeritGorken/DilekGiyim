import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { Title } from '@angular/platform-browser';

import { CatalogService } from '../../core/catalog.service';
import { ProductListItem } from '../../core/models';
import { ProductCard } from '../product-card';

/** /urunler, /urunler?q=..., /urunler?tab=sale, /kategori/:category */
@Component({
  selector: 'app-listing',
  imports: [ProductCard],
  template: `
    <section class="container listing">
      <div class="eyebrow">{{ eyebrow() }}</div>
      <h1 class="section-title">{{ heading() }}</h1>
      @if (loading()) {
        <p class="text-muted">Yükleniyor…</p>
      } @else {
        <p class="text-muted count">{{ products().length }} ürün</p>
        <div class="grid">
          @for (p of products(); track p.id) {
            <app-product-card [product]="p" />
          } @empty {
            <p>Aradığınız kriterlere uygun ürün bulunamadı.</p>
          }
        </div>
      }
    </section>
  `,
  styles: `
    .listing { padding-top: 48px; }
    .count { margin: 12px 0 0; }
    .grid { margin-top: 28px; display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 28px 24px; }
  `
})
export class Listing {
  private readonly catalog = inject(CatalogService);
  private readonly title = inject(Title);

  // Rota parametresi ve sorgu dizesinden otomatik gelir (withComponentInputBinding)
  readonly category = input<string>();
  readonly q = input<string>();
  readonly tab = input<string>();

  protected readonly products = signal<ProductListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly heading = signal('Tüm ürünler');
  protected readonly eyebrow = signal('Yeni sezon');

  constructor() {
    effect(() => {
      const category = this.category();
      const q = this.q();
      const tab = this.tab();
      untracked(() => this.load(category, q, tab));
    });
  }

  private load(category?: string, q?: string, tab?: string): void {
    this.loading.set(true);
    const t = tab === 'sale' || tab === 'new' || tab === 'best' ? tab : undefined;
    this.catalog.products({ category, q, tab: t }).subscribe({
      next: (list) => {
        this.products.set(list);
        this.loading.set(false);
        let h = 'Tüm ürünler';
        let e = 'Yeni sezon';
        if (q) { h = `“${q}” için sonuçlar`; e = 'Arama'; }
        else if (t === 'sale') { h = 'İndirimdeki ürünler'; e = 'Kampanya'; }
        else if (category) { h = list[0]?.categoryName ?? 'Kategori'; e = 'Kategori'; }
        this.heading.set(h);
        this.eyebrow.set(e);
        this.title.setTitle(`${h} · Dilek Giyim`);
      },
      error: () => {
        this.products.set([]);
        this.loading.set(false);
      }
    });
  }
}
