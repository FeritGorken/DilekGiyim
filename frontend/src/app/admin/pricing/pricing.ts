import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { BulkPriceRow, Campaign, Category, Coupon, StoreSettings } from '../../core/models';
import { TlPipe } from '../../core/tl.pipe';
import { AdminApi, apiError } from '../admin-api.service';

type Msg = { text: string; ok: boolean } | null;

@Component({
  selector: 'app-admin-pricing',
  imports: [FormsModule, TlPipe, DatePipe],
  templateUrl: './pricing.html',
  styleUrl: './pricing.scss'
})
export class AdminPricing implements OnInit {
  private readonly api = inject(AdminApi);

  protected readonly categories = signal<Category[]>([]);

  // Toplu fiyat
  protected bulk = { categoryId: null as number | null, increase: true, percent: 10, roundTo90: true };
  protected readonly preview = signal<BulkPriceRow[]>([]);
  protected readonly bulkMsg = signal<Msg>(null);

  // Kampanyalar
  protected readonly campaigns = signal<Campaign[]>([]);
  protected newCampaign = { name: '', description: '', startsAt: '', endsAt: '' };
  protected readonly campaignMsg = signal<Msg>(null);

  // Kuponlar
  protected readonly coupons = signal<Coupon[]>([]);
  protected readonly editingCoupon = signal<Coupon | null>(null);
  protected readonly couponMsg = signal<Msg>(null);

  // Sepet ayarları
  protected readonly settings = signal<StoreSettings | null>(null);
  protected readonly settingsMsg = signal<Msg>(null);

  ngOnInit(): void {
    this.api.categories().subscribe((c) => this.categories.set(c));
    this.loadCampaigns();
    this.loadCoupons();
    this.api.settings().subscribe((s) => this.settings.set(s));
    this.runPreview();
  }

  // ---------- Toplu fiyat ----------

  protected runPreview(): void {
    this.bulkMsg.set(null);
    this.api.bulkPrice({ ...this.bulk, percent: Number(this.bulk.percent), apply: false }).subscribe({
      next: (rows) => this.preview.set(rows),
      error: (e) => { this.preview.set([]); this.bulkMsg.set({ text: apiError(e), ok: false }); }
    });
  }

  protected applyBulk(): void {
    const n = this.preview().length;
    if (!n || !confirm(`${n} ürünün fiyatı güncellenecek. Devam edilsin mi?`)) return;
    this.api.bulkPrice({ ...this.bulk, percent: Number(this.bulk.percent), apply: true }).subscribe({
      next: () => { this.bulkMsg.set({ text: `${n} ürünün fiyatı güncellendi.`, ok: true }); this.runPreview(); },
      error: (e) => this.bulkMsg.set({ text: apiError(e), ok: false })
    });
  }

  // ---------- Kampanyalar ----------

  private loadCampaigns(): void {
    this.api.campaigns().subscribe((c) => this.campaigns.set(c));
  }

  protected campaignStatus(c: Campaign): { label: string; cls: string } {
    const now = Date.now();
    if (!c.isActive) return { label: 'Pasif', cls: 'pill-muted' };
    if (new Date(c.endsAt).getTime() < now) return { label: 'Bitti', cls: 'pill-muted' };
    if (new Date(c.startsAt).getTime() > now) return { label: 'Planlandı', cls: 'pill-info' };
    return { label: 'Aktif', cls: 'pill-ok' };
  }

  protected addCampaign(): void {
    const c = this.newCampaign;
    if (!c.name || !c.startsAt || !c.endsAt) {
      this.campaignMsg.set({ text: 'Ad ve tarihleri doldurun.', ok: false });
      return;
    }
    this.api.createCampaign({
      name: c.name, description: c.description,
      startsAt: new Date(c.startsAt).toISOString(), endsAt: new Date(c.endsAt).toISOString(), isActive: true
    }).subscribe({
      next: () => {
        this.newCampaign = { name: '', description: '', startsAt: '', endsAt: '' };
        this.campaignMsg.set({ text: 'Kampanya eklendi.', ok: true });
        this.loadCampaigns();
      },
      error: (e) => this.campaignMsg.set({ text: apiError(e), ok: false })
    });
  }

  protected deleteCampaign(c: Campaign): void {
    if (!confirm(`"${c.name}" silinsin mi?`)) return;
    this.api.deleteCampaign(c.id).subscribe(() => this.loadCampaigns());
  }

  // ---------- Kuponlar ----------

  private loadCoupons(): void {
    this.api.coupons().subscribe((c) => this.coupons.set(c));
  }

  protected usagePct(c: Coupon): number {
    return c.usageLimit ? Math.min(100, Math.round((c.usedCount / c.usageLimit) * 100)) : 0;
  }

  protected newCoupon(): void {
    this.couponMsg.set(null);
    this.editingCoupon.set({
      id: 0, code: '', type: 'Percent', value: 10, minCartTotal: 0, usageLimit: null,
      usedCount: 0, startsAt: null, endsAt: null, isActive: true
    });
  }

  protected editCoupon(c: Coupon): void {
    this.couponMsg.set(null);
    this.editingCoupon.set({ ...c });
  }

  protected saveCoupon(c: Coupon): void {
    this.api.saveCoupon({ ...c, value: Number(c.value), minCartTotal: Number(c.minCartTotal) || 0, usageLimit: c.usageLimit ? Number(c.usageLimit) : null }).subscribe({
      next: () => {
        this.editingCoupon.set(null);
        this.couponMsg.set({ text: 'İndirim kodu kaydedildi.', ok: true });
        this.loadCoupons();
      },
      error: (e) => this.couponMsg.set({ text: apiError(e), ok: false })
    });
  }

  protected deleteCoupon(c: Coupon): void {
    if (!confirm(`${c.code} silinsin mi?`)) return;
    this.api.deleteCoupon(c.id).subscribe(() => this.loadCoupons());
  }

  // ---------- Sepet ayarları ----------

  protected saveSettings(s: StoreSettings): void {
    this.api.saveSettings({
      ...s,
      freeShippingThreshold: Number(s.freeShippingThreshold),
      shippingFee: Number(s.shippingFee),
      minOrderTotal: Number(s.minOrderTotal) || 0,
      maxQuantityPerItem: Number(s.maxQuantityPerItem)
    }).subscribe({
      next: () => this.settingsMsg.set({ text: 'Ayarlar kaydedildi.', ok: true }),
      error: (e) => this.settingsMsg.set({ text: apiError(e), ok: false })
    });
  }
}
