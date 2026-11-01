import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, OnInit, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { ORDER_STATUS_LABELS, OrderDetail, OrderStatus, OrderSummary } from '../../core/models';
import { TlPipe } from '../../core/tl.pipe';
import { AdminApi, apiError } from '../admin-api.service';

@Component({
  selector: 'app-admin-orders',
  imports: [FormsModule, DatePipe, TlPipe],
  templateUrl: './orders.html',
  styles: `
    .head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 16px; }
    .head form { display: flex; gap: 8px; }
    .head .input { width: 280px; max-width: 100%; height: 44px; }
    .chips { margin-top: 20px; }
    .layout { margin-top: 16px; display: flex; flex-wrap: wrap; gap: 16px; align-items: flex-start; }
    .list { flex: 999 1 520px; padding: 0; overflow: hidden; }
    .detail { flex: 1 1 320px; }
    .linkbtn { padding: 0; min-height: 32px; border: 0; background: transparent; font-weight: 600; text-decoration: underline; cursor: pointer; }
    .lines { margin: 16px 0 0; padding: 0; list-style: none; border-top: 1px solid #f0ebe3; }
    .lines li { display: flex; justify-content: space-between; gap: 12px; padding: 12px 0; border-bottom: 1px solid #f0ebe3; font-size: 14px; }
    .lines small { display: block; font-size: 12px; color: var(--muted); }
    .totals { margin: 12px 0 0; display: grid; grid-template-columns: 1fr auto; row-gap: 6px; font-size: 14px; }
    .totals dd { margin: 0; text-align: right; }
    .totals .t { font-weight: 600; font-size: 15px; }
    .addr { margin-top: 12px; padding: 14px; border-radius: 6px; background: var(--admin-bg); font-size: 13px; line-height: 1.7; }
    .form { margin-top: 16px; display: flex; flex-direction: column; gap: 10px; }
    .note { margin: 12px 0 0; font-size: 12px; line-height: 1.6; color: var(--muted); }
    .detail-head { display: flex; justify-content: space-between; align-items: center; gap: 8px; }
  `
})
export class AdminOrders implements OnInit {
  private readonly api = inject(AdminApi);

  /** ?id=... ile doğrudan bir sipariş açılabilir */
  readonly id = input<string>();

  protected readonly labels = ORDER_STATUS_LABELS;
  protected readonly statuses: (OrderStatus | null)[] = [null, 'New', 'Preparing', 'Shipped', 'Delivered', 'Returned', 'Cancelled'];
  protected readonly filter = signal<OrderStatus | null>(null);
  protected readonly counts = signal<Partial<Record<OrderStatus, number>>>({});
  protected readonly total = signal(0);
  protected readonly orders = signal<OrderSummary[]>([]);
  protected readonly selected = signal<OrderDetail | null>(null);
  protected readonly message = signal<{ text: string; ok: boolean } | null>(null);
  protected readonly saving = signal(false);
  protected q = '';
  protected edit = { status: 'New' as OrderStatus, trackingNo: '' };

  constructor() {
    effect(() => {
      const id = Number(this.id());
      if (id) untracked(() => this.open(id));
    });
  }

  ngOnInit(): void {
    this.load();
    this.loadCounts();
  }

  protected setFilter(s: OrderStatus | null): void {
    this.filter.set(s);
    this.load();
  }

  protected load(): void {
    this.api.orders(this.filter(), this.q.trim()).subscribe((list) => {
      this.orders.set(list);
      if (!this.selected() && list.length) this.open(list[0].id);
    });
  }

  private loadCounts(): void {
    this.api.orderCounts().subscribe((list) => {
      const map: Partial<Record<OrderStatus, number>> = {};
      list.forEach((x) => (map[x.status] = x.count));
      this.counts.set(map);
      this.total.set(list.reduce((s, x) => s + x.count, 0));
    });
  }

  protected open(id: number): void {
    this.message.set(null);
    this.api.order(id).subscribe((o) => {
      this.selected.set(o);
      this.edit = { status: o.status, trackingNo: o.trackingNo ?? '' };
    });
  }

  protected save(): void {
    const o = this.selected();
    if (!o) return;
    if ((this.edit.status === 'Cancelled' || this.edit.status === 'Returned') && o.status !== this.edit.status &&
        !confirm('Sipariş iptal/iade olarak işaretlenecek ve ürünler stoğa geri eklenecek. Devam edilsin mi?')) return;
    this.saving.set(true);
    this.api.updateOrderStatus(o.id, this.edit.status, this.edit.trackingNo).subscribe({
      next: (d) => {
        this.selected.set(d);
        this.saving.set(false);
        this.message.set({ text: 'Sipariş güncellendi.', ok: true });
        this.load();
        this.loadCounts();
      },
      error: (e) => {
        this.saving.set(false);
        this.message.set({ text: apiError(e), ok: false });
      }
    });
  }
}
