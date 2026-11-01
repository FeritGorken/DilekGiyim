import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, Observable, of, tap } from 'rxjs';

import { Address, CustomerMe, CustomerOrder, ProductListItem, TrackedOrder } from './models';

/**
 * Müşteri oturumu ve hesap işlemleri. Oturum HttpOnly çerezde; tarayıcıda şifre/anahtar tutulmaz.
 */
@Injectable({ providedIn: 'root' })
export class AccountService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/account';

  /** undefined: henüz kontrol edilmedi, null: misafir */
  readonly me = signal<CustomerMe | null | undefined>(undefined);
  /** Favori ürün kimlikleri (kalp ikonları için) */
  readonly favoriteIds = signal<Set<number>>(new Set());

  check(): Observable<boolean> {
    return this.http.get<CustomerMe>(`${this.base}/me`).pipe(
      tap((m) => { this.me.set(m); this.loadFavoriteIds(); }),
      map(() => true),
      catchError(() => { this.me.set(null); this.favoriteIds.set(new Set()); return of(false); })
    );
  }

  register(body: { firstName: string; lastName: string; email: string; phone: string; password: string; acceptTerms: boolean; marketingConsent: boolean }): Observable<CustomerMe> {
    return this.http.post<CustomerMe>(`${this.base}/register`, body).pipe(tap((m) => { this.me.set(m); this.loadFavoriteIds(); }));
  }

  login(email: string, password: string, remember: boolean): Observable<CustomerMe> {
    return this.http.post<CustomerMe>(`${this.base}/login`, { email, password, remember }).pipe(tap((m) => { this.me.set(m); this.loadFavoriteIds(); }));
  }

  logout(): Observable<unknown> {
    return this.http.post(`${this.base}/logout`, {}).pipe(
      catchError(() => of(null)),
      tap(() => { this.me.set(null); this.favoriteIds.set(new Set()); })
    );
  }

  forgot(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/forgot`, { email });
  }

  reset(token: string, newPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/reset`, { token, newPassword });
  }

  updateProfile(p: Omit<CustomerMe, 'id'>): Observable<CustomerMe> {
    return this.http.put<CustomerMe>(`${this.base}/me`, p).pipe(tap((m) => this.me.set(m)));
  }

  changePassword(currentPassword: string, newPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/change-password`, { currentPassword, newPassword });
  }

  // Adresler
  addresses(): Observable<Address[]> { return this.http.get<Address[]>(`${this.base}/addresses`); }
  saveAddress(a: Address): Observable<Address> {
    return a.id ? this.http.put<Address>(`${this.base}/addresses/${a.id}`, a) : this.http.post<Address>(`${this.base}/addresses`, a);
  }
  deleteAddress(id: number): Observable<void> { return this.http.delete<void>(`${this.base}/addresses/${id}`); }

  // Favoriler
  favorites(): Observable<ProductListItem[]> { return this.http.get<ProductListItem[]>(`${this.base}/favorites`); }

  isFavorite(productId: number): boolean { return this.favoriteIds().has(productId); }

  /** Giriş yapılmamışsa false döner (çağıran giriş sayfasına yönlendirir). */
  toggleFavorite(productId: number): boolean {
    if (!this.me()) return false;
    const ids = new Set(this.favoriteIds());
    const on = ids.has(productId);
    if (on) ids.delete(productId); else ids.add(productId);
    this.favoriteIds.set(ids); // anında göster, hata olursa geri al
    const req = on
      ? this.http.delete(`${this.base}/favorites/${productId}`)
      : this.http.post(`${this.base}/favorites/${productId}`, {});
    req.subscribe({ error: () => this.loadFavoriteIds() });
    return true;
  }

  private loadFavoriteIds(): void {
    this.http.get<number[]>(`${this.base}/favorites/ids`).subscribe({
      next: (ids) => this.favoriteIds.set(new Set(ids)),
      error: () => this.favoriteIds.set(new Set())
    });
  }

  // Siparişler
  orders(): Observable<CustomerOrder[]> { return this.http.get<CustomerOrder[]>(`${this.base}/orders`); }
  cancelOrder(id: number): Observable<CustomerOrder> { return this.http.post<CustomerOrder>(`${this.base}/orders/${id}/cancel`, {}); }
  requestReturn(id: number, reason: string): Observable<CustomerOrder> {
    return this.http.post<CustomerOrder>(`${this.base}/orders/${id}/return`, { reason });
  }

  /** Üye olmadan sipariş takibi */
  track(orderNo: string, email: string): Observable<TrackedOrder> {
    return this.http.get<TrackedOrder>('/api/orders/track', { params: { orderNo, email } });
  }
}

export const customerGuard: CanActivateFn = (_route, state) => {
  const account = inject(AccountService);
  const router = inject(Router);
  const toLogin = () => router.createUrlTree(['/hesap/giris'], { queryParams: { returnUrl: state.url } });
  if (account.me()) return true;
  if (account.me() === null) return toLogin();
  return account.check().pipe(map((ok) => (ok ? true : toLogin())));
};
