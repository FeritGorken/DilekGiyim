import { Component, computed, effect, inject, input, signal, untracked } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Router, RouterLink } from '@angular/router';

import { CartService } from '../../core/cart.service';
import { CatalogService } from '../../core/catalog.service';
import { ProductDetail, Variant } from '../../core/models';
import { formatTl, TlPipe } from '../../core/tl.pipe';

@Component({
  selector: 'app-product-page',
  imports: [RouterLink, TlPipe],
  templateUrl: './product.html',
  styleUrl: './product.scss'
})
export class ProductPage {
  private readonly catalog = inject(CatalogService);
  private readonly cart = inject(CartService);
  private readonly router = inject(Router);
  private readonly title = inject(Title);

  readonly slug = input.required<string>();

  protected readonly product = signal<ProductDetail | null>(null);
  protected readonly notFound = signal(false);
  protected readonly imageIndex = signal(0);
  protected readonly color = signal<string | null>(null);
  protected readonly size = signal<string | null>(null);
  protected readonly qty = signal(1);
  protected readonly message = signal<string | null>(null);

  protected readonly price = computed(() => {
    const p = this.product();
    return p ? p.salePrice ?? p.price : 0;
  });

  protected readonly installment = computed(() => formatTl(Math.round((this.price() * 1.047) / 6 * 100) / 100));

  protected readonly selectedVariant = computed<Variant | null>(() => {
    const p = this.product();
    if (!p) return null;
    return p.variants.find((v) => v.colorName === this.color() && v.size === this.size()) ?? null;
  });

  protected readonly sizeOptions = computed(() => {
    const p = this.product();
    if (!p) return [];
    return p.sizes.map((s) => {
      const v = p.variants.find((x) => x.colorName === this.color() && x.size === s);
      return { size: s, available: v?.available ?? 0, exists: !!v };
    }).filter((o) => o.exists);
  });

  protected readonly stockText = computed(() => {
    const p = this.product();
    const v = this.selectedVariant();
    if (!p || !this.size()) return null;
    if (!v || v.available === 0) return { text: 'Bu beden tükendi', cls: 'text-danger' };
    if (v.available <= p.lowStockDisplay) return { text: `Son ${v.available} ürün!`, cls: 'text-warn' };
    return { text: 'Stokta', cls: 'text-ok' };
  });

  constructor() {
    effect(() => {
      const slug = this.slug();
      untracked(() => this.load(slug));
    });
  }

  private load(slug: string): void {
    this.product.set(null);
    this.notFound.set(false);
    this.catalog.product(slug).subscribe({
      next: (p) => {
        this.product.set(p);
        this.title.setTitle(`${p.name} · Dilek Giyim`);
        this.imageIndex.set(0);
        this.qty.set(1);
        this.message.set(null);
        this.color.set(p.colors[0]?.name ?? null);
        // İlk stokta olan bedeni seç
        const first = p.sizes.find((s) => p.variants.some((v) => v.colorName === p.colors[0]?.name && v.size === s && v.available > 0));
        this.size.set(first ?? null);
      },
      error: () => this.notFound.set(true)
    });
  }

  protected pickColor(name: string): void {
    this.color.set(name);
    const v = this.selectedVariant();
    if (!v || v.available === 0) {
      const p = this.product()!;
      const first = p.sizes.find((s) => p.variants.some((x) => x.colorName === name && x.size === s && x.available > 0));
      this.size.set(first ?? null);
    }
    this.qty.set(1);
  }

  protected pickSize(size: string): void {
    this.size.set(size);
    this.qty.set(1);
    this.message.set(null);
  }

  protected changeQty(delta: number): void {
    const max = this.selectedVariant()?.available ?? 1;
    this.qty.set(Math.min(Math.max(1, this.qty() + delta), Math.max(1, max)));
  }

  protected addToCart(goToCart = false): void {
    const v = this.selectedVariant();
    if (!this.size()) {
      this.message.set('Lütfen bir beden seçin.');
      return;
    }
    if (!v || v.available === 0) {
      this.message.set('Seçtiğiniz beden tükendi.');
      return;
    }
    this.cart.add(v.id, this.qty());
    if (goToCart) this.router.navigate(['/sepet']);
    else this.message.set('Ürün sepetinize eklendi.');
  }
}
