import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

import { CartService } from '../core/cart.service';
import { CatalogService } from '../core/catalog.service';
import { Category } from '../core/models';
import { formatTl } from '../core/tl.pipe';

@Component({
  selector: 'app-store-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule],
  templateUrl: './store-layout.html',
  styleUrl: './store-layout.scss'
})
export class StoreLayout implements OnInit {
  private readonly catalog = inject(CatalogService);
  private readonly router = inject(Router);
  protected readonly cart = inject(CartService);

  protected readonly categories = signal<Category[]>([]);
  protected readonly topMessage = signal('14 gün içinde kolay iade');
  protected readonly year = new Date().getFullYear();
  protected search = '';

  ngOnInit(): void {
    this.catalog.categories().subscribe((c) => this.categories.set(c));
    this.catalog.settings().subscribe((s) =>
      this.topMessage.set(`${formatTl(s.freeShippingThreshold).replace(',00', '')} üzeri ücretsiz kargo · 14 gün içinde kolay iade`)
    );
  }

  protected onSearch(): void {
    const q = this.search.trim();
    this.router.navigate(['/urunler'], { queryParams: q ? { q } : {} });
  }
}
