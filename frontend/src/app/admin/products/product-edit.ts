import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AdminProduct, AdminVariant, Category } from '../../core/models';
import { formatTl } from '../../core/tl.pipe';
import { AdminApi, apiError } from '../admin-api.service';

const SIZE_ORDER = ['XS', 'S', 'M', 'L', 'XL', 'XXL', '34', '36', '38', '40', '42', '44', '46', '48'];

function emptyProduct(): AdminProduct {
  return {
    id: 0, name: '', slug: '', code: '', description: '', fabricAndCare: null, categoryId: 0,
    price: 0, salePrice: null, cost: 0, vatRate: 0.1,
    isPublished: false, isNew: true, isFeatured: false, criticalThreshold: 1, lowStockDisplay: 3,
    imageUrls: [], variants: []
  };
}

@Component({
  selector: 'app-admin-product-edit',
  imports: [FormsModule, RouterLink],
  templateUrl: './product-edit.html',
  styleUrl: './product-edit.scss'
})
export class AdminProductEdit {
  private readonly api = inject(AdminApi);
  private readonly router = inject(Router);

  /** Rota parametresi; yeni üründe yok */
  readonly id = input<string>();

  protected readonly model = signal<AdminProduct | null>(null);
  protected readonly categories = signal<Category[]>([]);
  protected readonly message = signal<{ text: string; ok: boolean } | null>(null);
  protected readonly saving = signal(false);

  protected newImage = '';
  protected newColor = { name: '', hex: '#7e8780' };
  protected newSize = '';

  constructor() {
    this.api.categories().subscribe((c) => this.categories.set(c));
    effect(() => {
      const id = Number(this.id());
      untracked(() => {
        this.message.set(null);
        if (id) this.api.product(id).subscribe((p) => this.model.set(p));
        else this.model.set(emptyProduct());
      });
    });
  }

  // ---------- Fiyat hesapları ----------

  protected shownPrice(m: AdminProduct): number {
    return m.salePrice && m.salePrice > 0 && m.salePrice < m.price ? m.salePrice : m.price;
  }

  protected discountText(m: AdminProduct): string {
    return m.salePrice && m.salePrice > 0 && m.salePrice < m.price ? `%${Math.round((1 - m.salePrice / m.price) * 100)}` : 'İndirim yok';
  }

  protected profit(m: AdminProduct): number {
    return this.shownPrice(m) / (1 + (m.vatRate || 0)) - (m.cost || 0);
  }

  protected marginPct(m: AdminProduct): number {
    const net = this.shownPrice(m) / (1 + (m.vatRate || 0));
    return net > 0 ? Math.round((this.profit(m) / net) * 100) : 0;
  }

  protected tl(v: number): string {
    return formatTl(Math.round(v * 100) / 100);
  }

  // ---------- Varyant tablosu (renk × beden) ----------

  protected colors(m: AdminProduct): { name: string; hex: string }[] {
    const seen = new Map<string, string>();
    m.variants.forEach((v) => { if (!seen.has(v.colorName)) seen.set(v.colorName, v.colorHex); });
    return [...seen].map(([name, hex]) => ({ name, hex }));
  }

  protected sizes(m: AdminProduct): string[] {
    const set = [...new Set(m.variants.map((v) => v.size))];
    const rank = (s: string) => { const i = SIZE_ORDER.indexOf(s); return i < 0 ? 999 : i; };
    return set.sort((a, b) => rank(a) - rank(b) || a.localeCompare(b));
  }

  protected cell(m: AdminProduct, color: string, size: string): AdminVariant | undefined {
    return m.variants.find((v) => v.colorName === color && v.size === size);
  }

  protected colorTotal(m: AdminProduct, color: string): number {
    return m.variants.filter((v) => v.colorName === color).reduce((s, v) => s + (Number(v.stock) || 0), 0);
  }

  protected addColor(m: AdminProduct): void {
    const name = this.newColor.name.trim();
    if (!name || this.colors(m).some((c) => c.name.toLocaleLowerCase('tr') === name.toLocaleLowerCase('tr'))) return;
    const sizes = this.sizes(m);
    (sizes.length ? sizes : ['36', '38', '40']).forEach((size) =>
      m.variants.push({ id: null, colorName: name, colorHex: this.newColor.hex, size, sku: '', stock: 0 }));
    this.newColor = { name: '', hex: '#7e8780' };
  }

  protected addSize(m: AdminProduct): void {
    const size = this.newSize.trim().toUpperCase();
    if (!size || this.sizes(m).includes(size)) return;
    const colors = this.colors(m);
    if (!colors.length) {
      this.message.set({ text: 'Önce bir renk ekleyin.', ok: false });
      return;
    }
    colors.forEach((c) => m.variants.push({ id: null, colorName: c.name, colorHex: c.hex, size, sku: '', stock: 0 }));
    this.newSize = '';
  }

  protected removeColor(m: AdminProduct, color: string): void {
    m.variants = m.variants.filter((v) => v.colorName !== color);
  }

  protected removeSize(m: AdminProduct, size: string): void {
    m.variants = m.variants.filter((v) => v.size !== size);
  }

  protected stockClass(m: AdminProduct, v: AdminVariant): string {
    const n = Number(v.stock) || 0;
    return n === 0 ? 'out' : n <= m.criticalThreshold ? 'crit' : '';
  }

  // ---------- Görseller ----------

  protected addImage(m: AdminProduct): void {
    const url = this.newImage.trim();
    if (url) m.imageUrls = [...m.imageUrls, url];
    this.newImage = '';
  }

  protected removeImage(m: AdminProduct, i: number): void {
    m.imageUrls = m.imageUrls.filter((_, idx) => idx !== i);
  }

  protected makeCover(m: AdminProduct, i: number): void {
    const list = [...m.imageUrls];
    const [img] = list.splice(i, 1);
    m.imageUrls = [img, ...list];
  }

  // ---------- Kaydet / sil ----------

  protected save(m: AdminProduct): void {
    this.saving.set(true);
    this.message.set(null);
    const body: AdminProduct = {
      ...m,
      price: Number(m.price),
      salePrice: m.salePrice ? Number(m.salePrice) : null,
      cost: Number(m.cost) || 0,
      variants: m.variants.map((v) => ({ ...v, stock: Number(v.stock) || 0 }))
    };
    const req = m.id ? this.api.updateProduct(body) : this.api.createProduct(body);
    req.subscribe({
      next: (saved) => {
        this.saving.set(false);
        this.message.set({ text: 'Ürün kaydedildi.', ok: true });
        if (!m.id) this.router.navigate(['/admin/urunler', saved.id]);
        else this.model.set(saved);
      },
      error: (e) => {
        this.saving.set(false);
        this.message.set({ text: apiError(e), ok: false });
      }
    });
  }

  protected remove(m: AdminProduct): void {
    if (!m.id || !confirm(`"${m.name}" silinecek. Emin misiniz?`)) return;
    this.api.deleteProduct(m.id).subscribe({
      next: () => this.router.navigate(['/admin/urunler']),
      error: (e) => this.message.set({ text: apiError(e), ok: false })
    });
  }
}
