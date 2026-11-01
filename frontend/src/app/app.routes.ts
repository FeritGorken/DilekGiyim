import { Routes } from '@angular/router';

import { adminGuard } from './admin/admin-auth';
import { customerGuard } from './core/account.service';

export const routes: Routes = [
  // ---------- Mağaza ----------
  {
    path: '',
    loadComponent: () => import('./store/store-layout').then((m) => m.StoreLayout),
    children: [
      { path: '', title: 'Dilek Giyim', loadComponent: () => import('./store/home/home').then((m) => m.Home) },
      { path: 'kategori/:category', loadComponent: () => import('./store/listing/listing').then((m) => m.Listing) },
      { path: 'urunler', title: 'Tüm Ürünler · Dilek Giyim', loadComponent: () => import('./store/listing/listing').then((m) => m.Listing) },
      { path: 'urun/:slug', loadComponent: () => import('./store/product/product').then((m) => m.ProductPage) },
      { path: 'hesap/giris', title: 'Giriş Yap / Üye Ol · Dilek Giyim', loadComponent: () => import('./store/account/login').then((m) => m.AccountLogin) },
      { path: 'hesap/sifre-yenile', title: 'Yeni Şifre · Dilek Giyim', loadComponent: () => import('./store/account/reset-and-track').then((m) => m.AccountReset) },
      { path: 'hesap', title: 'Hesabım · Dilek Giyim', canActivate: [customerGuard], loadComponent: () => import('./store/account/account').then((m) => m.AccountPage) },
      { path: 'siparis-takip', title: 'Sipariş Takibi · Dilek Giyim', loadComponent: () => import('./store/account/reset-and-track').then((m) => m.OrderTrack) },
    ],
  },
  {
    path: 'sepet',
    title: 'Sepetim · Dilek Giyim',
    loadComponent: () => import('./store/cart/cart').then((m) => m.CartPage),
  },
  {
    path: 'odeme',
    title: 'Teslimat ve Ödeme · Dilek Giyim',
    loadComponent: () => import('./store/checkout/checkout').then((m) => m.CheckoutPage),
  },
  {
    path: 'odeme/sonuc',
    title: 'Ödeme Sonucu · Dilek Giyim',
    loadComponent: () => import('./store/checkout/payment-result').then((m) => m.PaymentResult),
  },
  {
    // Yalnızca geliştirmede (backend Mock ödeme sağlayıcısı) kullanılır
    path: 'odeme/test',
    title: 'Test Ödeme',
    loadComponent: () => import('./store/checkout/test-payment').then((m) => m.TestPayment),
  },

  // ---------- Yönetim paneli ----------
  {
    path: 'admin/giris',
    title: 'Yönetim Girişi',
    loadComponent: () => import('./admin/login/login').then((m) => m.AdminLogin),
  },
  {
    path: 'admin/sifre-yenile',
    title: 'Yeni Şifre · Yönetim',
    loadComponent: () => import('./admin/admin-password').then((m) => m.AdminReset),
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./admin/admin-layout').then((m) => m.AdminLayout),
    children: [
      { path: '', title: 'Genel Bakış · Admin', loadComponent: () => import('./admin/dashboard/dashboard').then((m) => m.AdminDashboard) },
      { path: 'siparisler', title: 'Siparişler · Admin', loadComponent: () => import('./admin/orders/orders').then((m) => m.AdminOrders) },
      { path: 'stok', title: 'Stok Takibi · Admin', loadComponent: () => import('./admin/stock/stock').then((m) => m.AdminStock) },
      { path: 'urunler', title: 'Ürünler · Admin', loadComponent: () => import('./admin/products/product-list').then((m) => m.AdminProductList) },
      { path: 'urunler/yeni', title: 'Yeni Ürün · Admin', loadComponent: () => import('./admin/products/product-edit').then((m) => m.AdminProductEdit) },
      { path: 'urunler/:id', title: 'Ürün Düzenle · Admin', loadComponent: () => import('./admin/products/product-edit').then((m) => m.AdminProductEdit) },
      { path: 'fiyat', title: 'Fiyat, Kampanya ve Sepet · Admin', loadComponent: () => import('./admin/pricing/pricing').then((m) => m.AdminPricing) },
      { path: 'hesap', title: 'Hesabım · Admin', loadComponent: () => import('./admin/admin-password').then((m) => m.AdminAccount) },
    ],
  },

  { path: '**', redirectTo: '' },
];
