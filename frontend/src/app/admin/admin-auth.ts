import { HttpClient, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { catchError, map, Observable, of, throwError } from 'rxjs';

const STORAGE_KEY = 'dg-admin-key';
const HEADER = 'X-Admin-Key';

/**
 * Basit admin girişi: anahtar sekme kapanana kadar sessionStorage'da tutulur.
 * Canlıya çıkmadan önce kullanıcı adı/şifreli gerçek kimlik doğrulamayla değiştirilmeli.
 */
@Injectable({ providedIn: 'root' })
export class AdminAuthService {
  private readonly http = inject(HttpClient);
  readonly key = signal<string | null>(this.read());

  login(key: string): Observable<boolean> {
    return this.http.get('/api/admin/me', { headers: { [HEADER]: key } }).pipe(
      map(() => {
        this.key.set(key);
        try { sessionStorage.setItem(STORAGE_KEY, key); } catch { /* yok say */ }
        return true;
      }),
      catchError(() => of(false))
    );
  }

  logout(): void {
    this.key.set(null);
    try { sessionStorage.removeItem(STORAGE_KEY); } catch { /* yok say */ }
  }

  private read(): string | null {
    try { return sessionStorage.getItem(STORAGE_KEY); } catch { return null; }
  }
}

export const adminGuard: CanActivateFn = () => {
  const auth = inject(AdminAuthService);
  return auth.key() ? true : inject(Router).createUrlTree(['/admin/giris']);
};

/** /api/admin isteklerine anahtarı ekler; 401 gelirse giriş sayfasına yönlendirir. */
export const adminKeyInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith('/api/admin') || req.headers.has(HEADER)) return next(req);
  const auth = inject(AdminAuthService);
  const router = inject(Router);
  const key = auth.key();
  const withKey = key ? req.clone({ setHeaders: { [HEADER]: key } }) : req;
  return next(withKey).pipe(
    catchError((err: HttpErrorResponse) => {
      if (err.status === 401) {
        auth.logout();
        router.navigate(['/admin/giris']);
      }
      return throwError(() => err);
    })
  );
};
