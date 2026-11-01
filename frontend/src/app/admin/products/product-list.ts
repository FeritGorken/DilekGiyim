import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { AdminProductListItem } from '../../core/models';
import { TlPipe } from '../../core/tl.pipe';
import { AdminApi } from '../admin-api.service';

@Component({
  selector: 'app-admin-product-list',
  imports: [RouterLink, TlPipe],
  template: `
    <div class="head">
      <h1 class="adm-title">Ürünler</h1>
      <a routerLink="/admin/urunler/yeni" class="adm-btn primary">+ Yeni ürün</a>
    </div>
    <section class="adm-card list">
      <div class="table-wrap">
        <table class="adm-table" style="min-width: 680px">
          <thead><tr><th></th><th>Ürün</th><th>Kategori</th><th>Fiyat</th><th>Stok</th><th>Durum</th></tr></thead>
          <tbody>
            @for (p of products(); track p.id) {
              <tr>
                <td style="width: 64px">
                  <span class="thumb">@if (p.imageUrl) { <img [src]="p.imageUrl" alt=""> }</span>
                </td>
                <td><a [routerLink]="['/admin/urunler', p.id]"><strong>{{ p.name }}</strong></a><div class="text-muted small">{{ p.code }}</div></td>
                <td>{{ p.categoryName }}</td>
                <td>
                  <strong>{{ (p.salePrice ?? p.price) | tl }}</strong>
                  @if (p.salePrice) { <div class="text-muted small"><s>{{ p.price | tl }}</s></div> }
                </td>
                <td>{{ p.totalStock }} adet <span class="text-muted small">· {{ p.variantCount }} varyant</span></td>
                <td><span class="pill" [class]="p.isPublished ? 'pill-ok' : 'pill-muted'">{{ p.isPublished ? 'Yayında' : 'Taslak' }}</span></td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="text-muted">Henüz ürün yok.</td></tr>
            }
          </tbody>
        </table>
      </div>
    </section>
  `,
  styles: `
    .head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 16px; }
    .list { margin-top: 20px; padding: 0; overflow: hidden; }
    .thumb { display: block; width: 44px; height: 58px; border-radius: 4px; overflow: hidden; background: var(--cream-2); }
    .thumb img { width: 100%; height: 100%; object-fit: cover; }
    .small { font-size: 12px; }
  `
})
export class AdminProductList implements OnInit {
  private readonly api = inject(AdminApi);
  protected readonly products = signal<AdminProductListItem[]>([]);

  ngOnInit(): void {
    this.api.products().subscribe((p) => this.products.set(p));
  }
}
