import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { CartItem, CartQuote, CheckoutRequest, CheckoutResult } from './models';

const STORAGE_KEY = 'dg-cart';

interface StoredCart { items: CartItem[]; coupon: string | null; }

/**
 * Sepet tarayıcıda tutulur (yalnızca varyant ve adet).
 * Fiyat, indirim ve kargo her zaman sunucudan (/api/cart/quote) alınır.
 */
@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly http = inject(HttpClient);

  readonly items = signal<CartItem[]>([]);
  readonly coupon = signal<string | null>(null);
  readonly quote = signal<CartQuote | null>(null);
  readonly loading = signal(false);
  /** Stok değiştiğinde gösterilecek bilgi mesajı */
  readonly notice = signal<string | null>(null);

  readonly count = computed(() => this.items().reduce((s, i) => s + i.quantity, 0));

  constructor() {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);
      if (raw) {
        const saved = JSON.parse(raw) as StoredCart;
        this.items.set(Array.isArray(saved.items) ? saved.items : []);
        this.coupon.set(saved.coupon ?? null);
      }
    } catch {
      // bozuk kayıt: boş sepetle devam
    }
  }

  add(variantId: number, quantity = 1): void {
    const items = this.items();
    const exists = items.some((i) => i.variantId === variantId);
    this.items.set(
      exists
        ? items.map((i) => (i.variantId === variantId ? { ...i, quantity: i.quantity + quantity } : i))
        : [...items, { variantId, quantity }]
    );
    this.persist();
    this.refresh();
  }

  setQuantity(variantId: number, quantity: number): void {
    if (quantity <= 0) return this.remove(variantId);
    this.items.set(this.items().map((i) => (i.variantId === variantId ? { ...i, quantity } : i)));
    this.persist();
    this.refresh();
  }

  remove(variantId: number): void {
    this.items.set(this.items().filter((i) => i.variantId !== variantId));
    this.persist();
    this.refresh();
  }

  applyCoupon(code: string | null): void {
    this.coupon.set(code && code.trim() ? code.trim().toUpperCase() : null);
    this.persist();
    this.refresh();
  }

  clear(): void {
    this.items.set([]);
    this.coupon.set(null);
    this.quote.set(null);
    this.persist();
  }

  /** Sunucudan güncel tutarı al; stok yetmiyorsa adetleri düzelt ve kullanıcıya bildir. */
  refresh(): void {
    if (this.items().length === 0) {
      this.quote.set(null);
      return;
    }
    this.loading.set(true);
    this.http.post<CartQuote>('/api/cart/quote', { items: this.items(), couponCode: this.coupon() }).subscribe({
      next: (q) => {
        this.quote.set(q);
        this.loading.set(false);
        const warnings = q.lines.filter((l) => l.warning).map((l) => `${l.productName} (${l.colorName}, ${l.size}): ${l.warning}`);
        const known = new Set(q.lines.map((l) => l.variantId));
        const missing = this.items().some((i) => !known.has(i.variantId));
        if (warnings.length || missing) {
          this.notice.set([...warnings, ...(missing ? ['Satıştan kalkan ürünler sepetten çıkarıldı.'] : [])].join(' '));
          this.items.set(q.lines.filter((l) => l.quantity > 0).map((l) => ({ variantId: l.variantId, quantity: l.quantity })));
          this.persist();
        }
      },
      error: () => this.loading.set(false)
    });
  }

  checkout(req: Omit<CheckoutRequest, 'items' | 'couponCode'>): Observable<CheckoutResult> {
    return this.http
      .post<CheckoutResult>('/api/orders', { ...req, items: this.items(), couponCode: this.coupon() })
      // Kartla ödemede sepet, ödeme onaylanınca (sonuç sayfasında) boşaltılır
      .pipe(tap((r) => { if (!r.paymentPageUrl) this.clear(); }));
  }

  private persist(): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify({ items: this.items(), coupon: this.coupon() } satisfies StoredCart));
    } catch {
      // gizli sekme vb.
    }
  }
}
