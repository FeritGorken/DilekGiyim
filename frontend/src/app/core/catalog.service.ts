import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Category, ProductDetail, ProductListItem, PublicSettings } from './models';

@Injectable({ providedIn: 'root' })
export class CatalogService {
  private readonly http = inject(HttpClient);

  categories(): Observable<Category[]> {
    return this.http.get<Category[]>('/api/categories');
  }

  settings(): Observable<PublicSettings> {
    return this.http.get<PublicSettings>('/api/settings');
  }

  products(opts: { category?: string; featured?: boolean; tab?: 'best' | 'new' | 'sale'; q?: string } = {}): Observable<ProductListItem[]> {
    let params = new HttpParams();
    if (opts.category) params = params.set('category', opts.category);
    if (opts.featured) params = params.set('featured', 'true');
    if (opts.tab) params = params.set('tab', opts.tab);
    if (opts.q) params = params.set('q', opts.q);
    return this.http.get<ProductListItem[]>('/api/products', { params });
  }

  product(slug: string): Observable<ProductDetail> {
    return this.http.get<ProductDetail>(`/api/products/${encodeURIComponent(slug)}`);
  }
}
