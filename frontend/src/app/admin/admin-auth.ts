import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, Observable, of, tap, throwError } from 'rxjs';

export interface AdminMe { id: number; userName: string; displayName: string; email: string; }

/**
 * Yönetim paneli oturumu. Oturum sunucuda, HttpOnly çerezde tutulur;
 * tarayıcı tarafında hiçbir anahtar veya şifre saklanmaz.
 */
@Injectable({ providedIn: 'root' })
export class AdminAuthService {
  private readonly http = inject(HttpClient);
  private readonly base = '/api/admin/auth';

  /** undefined: henüz kontrol edilmedi, null: giriş yok */
  readonly me = signal<AdminMe | null | undefined>(undefined);

  check(): Observable<boolean> {
    return this.http.get<AdminMe>(`${this.base}/me`).pipe(
      tap((m) => this.me.set(m)),
      map(() => true),
      catchError(() => {
        this.me.set(null);
        return of(false);
      })
    );
  }

  login(userName: string, password: string, remember: boolean): Observable<AdminMe> {
    return this.http.post<AdminMe>(`${this.base}/login`, { userName, password, remember }).pipe(tap((m) => this.me.set(m)));
  }

  logout(): Observable<unknown> {
    return this.http.post(`${this.base}/logout`, {}).pipe(
      catchError(() => of(null)),
      tap(() => this.me.set(null))
    );
  }

  forgot(email: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/forgot`, { email });
  }

  reset(token: string, newPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/reset`, { token, newPassword });
  }

  changePassword(currentPassword: string, newPassword: string): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${this.base}/change-password`, { currentPassword, newPassword });
  }
}

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AdminAuthService);
  const router = inject(Router);
  if (auth.me()) return true;
  return auth.check().pipe(map((ok) => (ok ? true : router.createUrlTree(['/admin/giris']))));
};

/** Admin isteklerinde oturum düşerse (401) giriş sayfasına yönlendirir. */
export const adminKeyInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api/admin') || req.url.startsWith('/api/admin/auth/')) return next(req);
  const auth = inject(AdminAuthService);
  const router = inject(Router);
  return next(req).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401) {
        auth.me.set(null);
        router.navigate(['/admin/giris']);
      }
      return throwError(() => err);
    })
  );
};
