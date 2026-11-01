import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';

import { AccountService } from '../../core/account.service';

/** /hesap/giris : giriş yap + üye ol (yan yana), şifremi unuttum */
@Component({
  selector: 'app-account-login',
  imports: [FormsModule, RouterLink],
  templateUrl: './login.html',
  styleUrl: './login.scss'
})
export class AccountLogin {
  private readonly account = inject(AccountService);
  private readonly router = inject(Router);

  readonly returnUrl = input<string>();

  protected readonly resetMode = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly busy = signal(false);
  protected readonly loginError = signal<string | null>(null);
  protected readonly registerError = signal<string | null>(null);
  protected readonly resetInfo = signal<string | null>(null);

  protected login = { email: '', password: '', remember: true };
  protected reg = { firstName: '', lastName: '', email: '', phone: '', password: '', acceptTerms: false, marketingConsent: false };
  protected resetEmail = '';

  protected submitLogin(): void {
    this.loginError.set(null);
    if (!this.login.email || !this.login.password) {
      this.loginError.set('E-posta ve şifre gerekli.');
      return;
    }
    this.busy.set(true);
    this.account.login(this.login.email.trim(), this.login.password, this.login.remember).subscribe({
      next: () => this.done(),
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.login.password = '';
        this.loginError.set(e.error?.message ?? 'Giriş yapılamadı.');
      }
    });
  }

  protected submitRegister(): void {
    this.registerError.set(null);
    if (!this.reg.acceptTerms) {
      this.registerError.set('Üyelik sözleşmesini ve KVKK aydınlatma metnini onaylamanız gerekiyor.');
      return;
    }
    this.busy.set(true);
    this.account.register({ ...this.reg, email: this.reg.email.trim() }).subscribe({
      next: () => this.done(),
      error: (e: HttpErrorResponse) => {
        this.busy.set(false);
        this.registerError.set(e.error?.message ?? 'Üyelik oluşturulamadı.');
      }
    });
  }

  protected submitReset(): void {
    if (!this.resetEmail) return;
    this.busy.set(true);
    this.account.forgot(this.resetEmail.trim()).subscribe({
      next: (r) => { this.busy.set(false); this.resetInfo.set(r.message); },
      error: (e: HttpErrorResponse) => { this.busy.set(false); this.resetInfo.set(e.error?.message ?? 'İstek gönderilemedi.'); }
    });
  }

  protected setReset(on: boolean): void {
    this.resetMode.set(on);
    this.resetInfo.set(null);
    this.loginError.set(null);
  }

  private done(): void {
    this.busy.set(false);
    const back = this.returnUrl();
    // Yalnızca site içi adreslere dön (açık yönlendirme olmasın)
    this.router.navigateByUrl(back && back.startsWith('/') && !back.startsWith('//') ? back : '/hesap');
  }
}
