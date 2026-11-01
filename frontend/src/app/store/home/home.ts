import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';

import { CatalogService } from '../../core/catalog.service';
import { Category, ProductListItem } from '../../core/models';
import { formatTl } from '../../core/tl.pipe';
import { ProductCard } from '../product-card';

type Tab = 'best' | 'new' | 'sale';

@Component({
  selector: 'app-home',
  imports: [RouterLink, ProductCard],
  templateUrl: './home.html',
  styleUrl: './home.scss'
})
export class Home implements OnInit {
  private readonly catalog = inject(CatalogService);

  protected readonly tabs: { id: Tab; label: string }[] = [
    { id: 'best', label: 'Çok Satanlar' },
    { id: 'new', label: 'Yeni Gelenler' },
    { id: 'sale', label: 'İndirimdekiler' }
  ];
  protected readonly tab = signal<Tab>('best');
  protected readonly products = signal<ProductListItem[]>([]);
  protected readonly loading = signal(true);
  protected readonly categories = signal<Category[]>([]);
  protected readonly freeShipping = signal('');

  ngOnInit(): void {
    this.load();
    this.catalog.categories().subscribe((c) => this.categories.set(c.slice(0, 4)));
    this.catalog.settings().subscribe((s) => this.freeShipping.set(formatTl(s.freeShippingThreshold).replace(',00', '') + ' ve üzeri'));
  }

  protected selectTab(t: Tab): void {
    this.tab.set(t);
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    const t = this.tab();
    this.catalog.products({ featured: t !== 'sale', tab: t }).subscribe({
      next: (list) => {
        this.products.set(list.slice(0, 8));
        this.loading.set(false);
      },
      error: () => {
        this.products.set([]);
        this.loading.set(false);
      }
    });
  }
}
