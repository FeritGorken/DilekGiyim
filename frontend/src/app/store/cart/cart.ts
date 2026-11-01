import { Component, computed, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { CartService } from '../../core/cart.service';
import { formatTl, TlPipe } from '../../core/tl.pipe';
import { CheckoutHeader } from '../checkout-header';

@Component({
  selector: 'app-cart-page',
  imports: [RouterLink, FormsModule, TlPipe, CheckoutHeader],
  templateUrl: './cart.html',
  styleUrl: './cart.scss'
})
export class CartPage implements OnInit {
  protected readonly cart = inject(CartService);
  protected couponInput = '';

  protected readonly shipMessage = computed(() => {
    const q = this.cart.quote();
    if (!q) return '';
    return q.freeShippingRemaining <= 0
      ? 'Tebrikler! Kargonuz ücretsiz.'
      : `Ücretsiz kargo için ${formatTl(q.freeShippingRemaining)} daha ekleyin.`;
  });

  protected readonly shipPct = computed(() => {
    const q = this.cart.quote();
    if (!q || q.freeShippingThreshold <= 0) return 100;
    return Math.min(100, Math.round(((q.subtotal - q.discount) / q.freeShippingThreshold) * 100));
  });

  ngOnInit(): void {
    this.couponInput = this.cart.coupon() ?? '';
    this.cart.refresh();
  }

  protected applyCoupon(): void {
    this.cart.applyCoupon(this.couponInput);
  }

  protected removeCoupon(): void {
    this.couponInput = '';
    this.cart.applyCoupon(null);
  }
}
