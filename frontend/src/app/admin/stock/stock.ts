import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';

import { MOVEMENT_LABELS, StockList, StockMovement, StockMovementType, StockRow } from '../../core/models';
import { AdminApi, apiError } from '../admin-api.service';

type Filter = 'all' | 'critical' | 'out' | 'ok';

@Component({
  selector: 'app-admin-stock',
  imports: [FormsModule, DatePipe],
  templateUrl: './stock.html',
  styles: `
    .head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 16px; }
    .summary { margin-top: 20px; display: grid; grid-template-columns: repeat(auto-fit, minmax(180px, 1fr)); gap: 16px; }
    .summary .label { font-size: 13px; color: var(--muted); }
    .summary .value { margin-top: 6px; font-size: 22px; font-weight: 600; }
    .layout { margin-top: 20px; display: flex; flex-wrap: wrap; gap: 16px; align-items: flex-start; }
    .list { flex: 999 1 560px; padding: 0; overflow: hidden; }
    .toolbar { padding: 14px 16px; display: flex; flex-wrap: wrap; gap: 10px; justify-content: space-between; align-items: center; border-bottom: 1px solid #f0ebe3; }
    .toolbar .input { width: 220px; max-width: 100%; height: 40px; }
    .side { flex: 1 1 300px; min-width: 0; display: flex; flex-direction: column; gap: 16px; }
    .stepper { display: inline-flex; align-items: center; border: 1px solid var(--line-2); border-radius: 6px; }
    .stepper button { width: 36px; height: 36px; border: 0; background: #fff; font-size: 16px; cursor: pointer; border-radius: 6px; }
    .stepper button:hover { background: var(--cream); }
    .stepper span { min-width: 36px; text-align: center; font-weight: 600; }
    .mv { margin: 12px 0 0; padding: 0; list-style: none; }
    .mv li { display: flex; justify-content: space-between; gap: 12px; padding: 10px 0; border-bottom: 1px solid #f0ebe3; font-size: 13px; }
    .mv small { display: block; color: var(--muted); }
    .plus { color: var(--ok); font-weight: 600; white-space: nowrap; }
    .minus { color: var(--danger); font-weight: 600; white-space: nowrap; }
    .entry { margin-top: 12px; display: flex; flex-direction: column; gap: 10px; }
    .sku { font-size: 13px; color: var(--text-2); }
  `
})
export class AdminStock implements OnInit {
  private readonly api = inject(AdminApi);

  protected readonly filters: { id: Filter; label: string }[] = [
    { id: 'all', label: 'Tümü' },
    { id: 'critical', label: 'Kritik' },
    { id: 'out', label: 'Tükendi' },
    { id: 'ok', label: 'Stokta' }
  ];
  protected readonly filter = signal<Filter>('all');
  protected readonly data = signal<StockList | null>(null);
  protected readonly movements = signal<StockMovement[]>([]);
  protected readonly message = signal<{ text: string; ok: boolean } | null>(null);
  protected readonly movementLabels = MOVEMENT_LABELS;
  protected q = '';

  /** Toplu stok girişi formu */
  protected entry = { variantId: 0, quantity: 1, note: '' };
  protected readonly allRows = signal<StockRow[]>([]);

  ngOnInit(): void {
    this.load();
    this.loadMovements();
    this.api.stock('all').subscribe((s) => this.allRows.set(s.rows));
  }

  protected count(f: Filter): number {
    const s = this.data()?.summary;
    if (!s) return 0;
    if (f === 'all') return s.variantCount;
    if (f === 'critical') return s.critical;
    if (f === 'out') return s.out;
    return s.variantCount - s.critical - s.out;
  }

  protected setFilter(f: Filter): void {
    this.filter.set(f);
    this.load();
  }

  protected load(): void {
    this.api.stock(this.filter(), this.q.trim()).subscribe((d) => this.data.set(d));
  }

  private loadMovements(): void {
    this.api.movements(12).subscribe((m) => this.movements.set(m));
  }

  /** Satırdaki +/- düğmeleri: sayım düzeltmesi olarak kaydedilir */
  protected step(row: StockRow, delta: number): void {
    if (row.stock + delta < 0) return;
    this.adjust(row.variantId, delta, 'Adjustment', 'Panelden hızlı düzeltme');
  }

  protected addEntry(): void {
    if (!this.entry.variantId || this.entry.quantity < 1) {
      this.message.set({ text: 'Varyant ve adet seçin.', ok: false });
      return;
    }
    this.adjust(this.entry.variantId, this.entry.quantity, 'Restock', this.entry.note || 'Stok girişi');
    this.entry = { variantId: 0, quantity: 1, note: '' };
  }

  private adjust(variantId: number, delta: number, type: StockMovementType, note: string): void {
    this.api.adjustStock(variantId, delta, type, note).subscribe({
      next: () => {
        this.message.set({ text: 'Stok güncellendi.', ok: true });
        this.load();
        this.loadMovements();
      },
      error: (e) => this.message.set({ text: apiError(e), ok: false })
    });
  }
}
