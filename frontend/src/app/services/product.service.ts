import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { Product } from '../models/product';

@Injectable({ providedIn: 'root' })
export class ProductService {
  private readonly http = inject(HttpClient);

  // Geliştirmede /api istekleri proxy.conf.json ile backend'e (localhost:5080) yönlenir
  getAll(): Observable<Product[]> {
    return this.http.get<Product[]>('/api/products');
  }

  getById(id: number): Observable<Product> {
    return this.http.get<Product>(`/api/products/${id}`);
  }
}
