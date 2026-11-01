import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import {
  AdminProduct, AdminProductListItem, BulkPriceRow, Campaign, Category, Coupon, Dashboard,
  OrderDetail, OrderStatus, OrderSummary, PaymentState, StockList, StockMovement, StockMovementType, StockRow, StoreSettings
} from '../core/models';

/** Yönetim paneli API çağrıları. Anahtar adminKeyInterceptor ile eklenir. */
@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/admin';

  dashboard(days = 30): Observable<Dashboard> {
    return this.http.get<Dashboard>(`${this.base}/dashboard`, { params: { days } });
  }

  // Siparişler
  orders(status?: OrderStatus | null, q?: string): Observable<OrderSummary[]> {
    let params = new HttpParams();
    if (status) params = params.set('status', status);
    if (q) params = params.set('q', q);
    return this.http.get<OrderSummary[]>(`${this.base}/orders`, { params });
  }
  orderCounts(): Observable<{ status: OrderStatus; count: number }[]> {
    return this.http.get<{ status: OrderStatus; count: number }[]>(`${this.base}/orders/counts`);
  }
  order(id: number): Observable<OrderDetail> {
    return this.http.get<OrderDetail>(`${this.base}/orders/${id}`);
  }
  updateOrderStatus(id: number, status: OrderStatus, trackingNo: string | null, paymentState: PaymentState | null = null): Observable<OrderDetail> {
    return this.http.put<OrderDetail>(`${this.base}/orders/${id}/status`, { status, trackingNo, paymentState });
  }

  // Stok
  stock(filter: string, q?: string): Observable<StockList> {
    let params = new HttpParams().set('filter', filter);
    if (q) params = params.set('q', q);
    return this.http.get<StockList>(`${this.base}/stock`, { params });
  }
  adjustStock(variantId: number, delta: number, type: StockMovementType, note?: string): Observable<StockRow> {
    return this.http.post<StockRow>(`${this.base}/stock/${variantId}/adjust`, { delta, type, note: note ?? null });
  }
  movements(take = 20): Observable<StockMovement[]> {
    return this.http.get<StockMovement[]>(`${this.base}/stock/movements`, { params: { take } });
  }

  // Ürünler
  categories(): Observable<Category[]> {
    return this.http.get<Category[]>(`${this.base}/categories`);
  }
  products(): Observable<AdminProductListItem[]> {
    return this.http.get<AdminProductListItem[]>(`${this.base}/products`);
  }
  product(id: number): Observable<AdminProduct> {
    return this.http.get<AdminProduct>(`${this.base}/products/${id}`);
  }
  createProduct(p: AdminProduct): Observable<AdminProduct> {
    return this.http.post<AdminProduct>(`${this.base}/products`, p);
  }
  updateProduct(p: AdminProduct): Observable<AdminProduct> {
    return this.http.put<AdminProduct>(`${this.base}/products/${p.id}`, p);
  }
  deleteProduct(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/products/${id}`);
  }

  // Fiyat, kampanya, kupon, ayarlar
  bulkPrice(req: { categoryId: number | null; increase: boolean; percent: number; roundTo90: boolean; apply: boolean }): Observable<BulkPriceRow[]> {
    return this.http.post<BulkPriceRow[]>(`${this.base}/prices/bulk`, req);
  }
  coupons(): Observable<Coupon[]> {
    return this.http.get<Coupon[]>(`${this.base}/coupons`);
  }
  saveCoupon(c: Coupon): Observable<Coupon> {
    return c.id ? this.http.put<Coupon>(`${this.base}/coupons/${c.id}`, c) : this.http.post<Coupon>(`${this.base}/coupons`, c);
  }
  deleteCoupon(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/coupons/${id}`);
  }
  campaigns(): Observable<Campaign[]> {
    return this.http.get<Campaign[]>(`${this.base}/campaigns`);
  }
  createCampaign(c: Omit<Campaign, 'id'>): Observable<Campaign> {
    return this.http.post<Campaign>(`${this.base}/campaigns`, { id: 0, ...c });
  }
  deleteCampaign(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/campaigns/${id}`);
  }
  settings(): Observable<StoreSettings> {
    return this.http.get<StoreSettings>(`${this.base}/settings`);
  }
  saveSettings(s: StoreSettings): Observable<StoreSettings> {
    return this.http.put<StoreSettings>(`${this.base}/settings`, s);
  }
}

/** API hata gövdesinden okunabilir mesaj çıkarır. */
export function apiError(e: unknown, fallback = 'İşlem yapılamadı.'): string {
  const err = e as { error?: { message?: string } };
  return err?.error?.message ?? fallback;
}
