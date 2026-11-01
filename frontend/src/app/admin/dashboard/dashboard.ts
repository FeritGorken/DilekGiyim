import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

import { Dashboard, ORDER_STATUS_LABELS } from '../../core/models';
import { formatTl, TlPipe } from '../../core/tl.pipe';
import { AdminApi } from '../admin-api.service';

@Component({
  selector: 'app-admin-dashboard',
  imports: [RouterLink, TlPipe, DatePipe, FormsModule],
  templateUrl: './dashboard.html',
  styles: `
    .head { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 16px; }
    .head .tools { display: flex; flex-wrap: wrap; gap: 10px; }
    .head .select { width: auto; height: 44px; }
    .kpis { margin-top: 24px; display: grid; grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); gap: 16px; }
    .kpi .label { font-size: 13px; color: var(--muted); }
    .kpi .value { margin-top: 8px; font-size: 28px; font-weight: 600; }
    .kpi .delta { margin-top: 6px; font-size: 13px; font-weight: 500; }
    .row { margin-top: 16px; display: grid; grid-template-columns: repeat(auto-fit, minmax(320px, 1fr)); gap: 16px; }
    .wide { grid-column: span 2; }
    @media (max-width: 760px) { .wide { grid-column: auto; } }
    .card-head { display: flex; justify-content: space-between; align-items: baseline; gap: 12px; }
    .card-head a, .card-head span { font-size: 13px; }
    .card-head a { color: var(--accent); }
    .chart { margin-top: 20px; height: 200px; display: flex; align-items: flex-end; gap: 8px; border-bottom: 1px solid var(--admin-line); }
    .chart div { flex: 1; display: flex; align-items: flex-end; justify-content: center; height: 100%; }
    .chart span { width: 100%; max-width: 32px; border-radius: 4px 4px 0 0; background: #d9cbb8; min-height: 2px; }
    .chart div:last-child span { background: var(--accent); }
    .days { margin-top: 8px; display: flex; gap: 8px; font-size: 11px; color: var(--muted); }
    .days span { flex: 1; text-align: center; }
    ul { margin: 12px 0 0; padding: 0; list-style: none; }
    li { display: flex; justify-content: space-between; align-items: center; gap: 12px; padding: 10px 0; border-bottom: 1px solid #f0ebe3; font-size: 14px; }
    li small { display: block; font-size: 12px; color: var(--muted); }
    .side { display: flex; flex-direction: column; gap: 16px; min-width: 0; }
    .table-wrap { margin: 12px -20px -20px; }
  `
})
export class AdminDashboard implements OnInit {
  private readonly api = inject(AdminApi);

  protected readonly data = signal<Dashboard | null>(null);
  protected readonly error = signal<string | null>(null);
  protected days = 30;
  protected readonly labels = ORDER_STATUS_LABELS;
  protected readonly today = new Date();

  protected readonly bars = computed(() => {
    const d = this.data();
    if (!d) return [];
    const max = Math.max(1, ...d.daily.map((x) => x.total));
    return d.daily.map((x) => ({
      day: x.date.slice(8, 10),
      height: Math.round((x.total / max) * 100),
      title: `${x.date}: ${formatTl(x.total)}`
    }));
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    this.api.dashboard(this.days).subscribe({
      next: (d) => this.data.set(d),
      error: () => this.error.set('Veriler yüklenemedi. Backend çalışıyor mu?')
    });
  }
}
