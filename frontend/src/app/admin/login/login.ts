import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import { AdminAuthService } from '../admin-auth';

type View = 'login' | 'forgot';

@Component({
  selector: 'app-admin-login',
  imports: [FormsModule],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class AdminLogin {
  private readonly auth = inject(AdminAuthService);
  private readonly router = inject(Router);

  /** Oturum gerektiren bir sayfadan gelindiyse geri dönülecek adres */
  readonly returnUrl = input<string>();

  protected readonly view = signal<View>('login');
  protected readonly busy = signal(false);
  protected readonly error = signal<{ text: string; locked: boolean } | null>(null);
  protected readonly info = signal<string | null>(null);
  protected readonly showPassword = signal(false);

  protected form = { userName: '', password: '', remember: false };
  protected forgotEmail = '';

  protected submit(): void {
    if (!this.form.userName || !this.form.password) {
      this.error.set({ text: 'Kullanıcı adı ve şifre gerekli.', locked: false });
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    this.auth.login(this.form.userName.trim(), this.form.password, this.form.remember).subscribe({
      next: () => {
        this.busy.set(false);
        const back = this.returnUrl();
        this.router.navigateByUrl(back && back.startsWith('/admin') ? back : '/admin');
      },
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.form.password = '';
        this.error.set({
          text: e.error?.message ?? (e.status === 0 ? 'Sunucuya ulaşılamadı.' : 'Giriş yapılamadı.'),
          locked: e.status === 423 || e.status === 429
        });
      }
    });
  }

  protected sendReset(): void {
    if (!this.forgotEmail) return;
    this.busy.set(true);
    this.auth.forgot(this.forgotEmail.trim()).subscribe({
      next: (r) => { this.busy.set(false); this.info.set(r.message); },
      error: (e: HttpErrorResponse) => { this.busy.set(false); this.info.set(e.error?.message ?? 'İstek gönderilemedi.'); }
    });
  }

  protected setView(v: View): void {
    this.view.set(v);
    this.error.set(null);
    this.info.set(null);
  }
}
