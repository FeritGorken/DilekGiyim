import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, input, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AccountService } from '../../core/account.service';
import { Address, CustomerOrder, ORDER_STATUS_LABELS, OrderStatus, ProductListItem } from '../../core/models';
import { TlPipe } from '../../core/tl.pipe';
import { ProductCard } from '../product-card';

type Tab = 'siparisler' | 'favoriler' | 'adresler' | 'bilgiler';
type Msg = { text: string; ok: boolean } | null;

const STEP_ORDER: OrderStatus[] = ['New', 'Preparing', 'Shipped', 'Delivered'];

@Component({
  selector: 'app-account-page',
  imports: [FormsModule, RouterLink, DatePipe, TlPipe, ProductCard],
  templateUrl: './account.html',
  styleUrl: './account.scss'
})
export class AccountPage {
  protected readonly account = inject(AccountService);
  private readonly router = inject(Router);

  /** ?tab=favoriler gibi */
  readonly tab = input<string>();

  protected readonly tabs: { id: Tab; label: string }[] = [
    { id: 'siparisler', label: 'Siparişlerim' },
    { id: 'favoriler', label: 'Favorilerim' },
    { id: 'adresler', label: 'Adreslerim' },
    { id: 'bilgiler', label: 'Hesap bilgileri' }
  ];
  protected readonly active = signal<Tab>('siparisler');
  protected readonly labels = ORDER_STATUS_LABELS;

  protected readonly orders = signal<CustomerOrder[] | null>(null);
  protected readonly openOrder = signal<number | null>(null);
  protected readonly returnFor = signal<number | null>(null);
  protected returnReason = '';
  protected readonly orderMsg = signal<Msg>(null);

  protected readonly favorites = signal<ProductListItem[] | null>(null);

  protected readonly addresses = signal<Address[] | null>(null);
  protected readonly editing = signal<Address | null>(null);
  protected readonly addrMsg = signal<Msg>(null);

  protected profile = { firstName: '', lastName: '', email: '', phone: '', marketingConsent: false };
  protected readonly profileMsg = signal<Msg>(null);
  protected pw = { current: '', next: '', next2: '' };
  protected readonly pwMsg = signal<Msg>(null);

  constructor() {
    effect(() => {
      const t = this.tab();
      untracked(() => this.select(this.isTab(t) ? t : 'siparisler', false));
    });
  }

  private isTab(t: string | undefined): t is Tab {
    return !!t && this.tabs.some((x) => x.id === t);
  }

  protected select(t: Tab, updateUrl = true): void {
    this.active.set(t);
    if (updateUrl) this.router.navigate([], { queryParams: { tab: t }, replaceUrl: true });
    if (t === 'siparisler' && !this.orders()) this.loadOrders();
    if (t === 'favoriler') this.account.favorites().subscribe((f) => this.favorites.set(f));
    if (t === 'adresler' && !this.addresses()) this.loadAddresses();
    if (t === 'bilgiler') {
      const me = this.account.me();
      if (me) this.profile = { firstName: me.firstName, lastName: me.lastName, email: me.email, phone: me.phone, marketingConsent: me.marketingConsent };
    }
  }

  // ---------- Siparişler ----------

  private loadOrders(): void {
    this.account.orders().subscribe((o) => {
      this.orders.set(o);
      if (o.length && this.openOrder() === null) this.openOrder.set(o[0].id);
    });
  }

  protected toggle(id: number): void {
    this.openOrder.set(this.openOrder() === id ? null : id);
    this.returnFor.set(null);
  }

  protected steps(o: CustomerOrder): { label: string; done: boolean }[] {
    const idx = STEP_ORDER.indexOf(o.status);
    return STEP_ORDER.map((s, i) => ({ label: this.labels[s], done: idx >= 0 && i <= idx }));
  }

  protected hasTimeline(o: CustomerOrder): boolean {
    return STEP_ORDER.includes(o.status);
  }

  protected pillClass(o: CustomerOrder): string {
    return 'pill pill-' + o.status;
  }

  protected cancel(o: CustomerOrder): void {
    if (!confirm(`#${o.orderNo} numaralı sipariş iptal edilsin mi?`)) return;
    this.account.cancelOrder(o.id).subscribe({
      next: (u) => {
        this.replaceOrder(u);
        this.orderMsg.set({ text: o.paymentState === 'Paid' ? 'Sipariş iptal edildi. Ödemeniz birkaç iş günü içinde kartınıza iade edilecek.' : 'Sipariş iptal edildi.', ok: true });
      },
      error: (e: HttpErrorResponse) => this.orderMsg.set({ text: e.error?.message ?? 'Sipariş iptal edilemedi.', ok: false })
    });
  }

  protected sendReturn(o: CustomerOrder): void {
    this.account.requestReturn(o.id, this.returnReason).subscribe({
      next: (u) => {
        this.replaceOrder(u);
        this.returnFor.set(null);
        this.returnReason = '';
        this.orderMsg.set({ text: 'İade talebiniz alındı. Kargo bilgisi e-postayla gönderilecek.', ok: true });
      },
      error: (e: HttpErrorResponse) => this.orderMsg.set({ text: e.error?.message ?? 'İade talebi gönderilemedi.', ok: false })
    });
  }

  private replaceOrder(u: CustomerOrder): void {
    this.orders.set((this.orders() ?? []).map((x) => (x.id === u.id ? u : x)));
  }

  // ---------- Adresler ----------

  private loadAddresses(): void {
    this.account.addresses().subscribe((a) => this.addresses.set(a));
  }

  protected newAddress(): void {
    const me = this.account.me();
    this.addrMsg.set(null);
    this.editing.set({
      id: 0, title: 'Ev', fullName: me ? `${me.firstName} ${me.lastName}` : '', phone: me?.phone ?? '',
      city: '', district: '', line: '', isDefault: !(this.addresses()?.length)
    });
  }

  protected editAddress(a: Address): void {
    this.addrMsg.set(null);
    this.editing.set({ ...a });
  }

  protected saveAddress(a: Address): void {
    this.account.saveAddress(a).subscribe({
      next: () => { this.editing.set(null); this.addrMsg.set({ text: 'Adres kaydedildi.', ok: true }); this.loadAddresses(); },
      error: (e: HttpErrorResponse) => this.addrMsg.set({ text: e.error?.message ?? 'Adres kaydedilemedi.', ok: false })
    });
  }

  protected makeDefault(a: Address): void {
    this.saveAddress({ ...a, isDefault: true });
  }

  protected removeAddress(a: Address): void {
    if (!confirm(`"${a.title}" adresi silinsin mi?`)) return;
    this.account.deleteAddress(a.id).subscribe(() => this.loadAddresses());
  }

  // ---------- Profil ----------

  protected saveProfile(): void {
    this.account.updateProfile(this.profile).subscribe({
      next: () => this.profileMsg.set({ text: 'Bilgileriniz kaydedildi.', ok: true }),
      error: (e: HttpErrorResponse) => this.profileMsg.set({ text: e.error?.message ?? 'Kaydedilemedi.', ok: false })
    });
  }

  protected changePassword(): void {
    if (this.pw.next !== this.pw.next2) {
      this.pwMsg.set({ text: 'Yeni şifreler aynı değil.', ok: false });
      return;
    }
    this.account.changePassword(this.pw.current, this.pw.next).subscribe({
      next: (r) => { this.pwMsg.set({ text: r.message, ok: true }); this.pw = { current: '', next: '', next2: '' }; },
      error: (e: HttpErrorResponse) => this.pwMsg.set({ text: e.error?.message ?? 'Şifre güncellenemedi.', ok: false })
    });
  }

  protected logout(): void {
    this.account.logout().subscribe(() => this.router.navigate(['/']));
  }
}
