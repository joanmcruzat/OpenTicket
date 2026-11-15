import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, map, of, shareReplay, tap } from 'rxjs';
import { API_BASE_URL } from '../api/api-config';
import { SessionResponse, UserProfile, UserRole } from '../api/models';

/**
 * Session state. The access token lives only in memory; the refresh token is an HttpOnly cookie
 * the browser sends to /api/v1/auth automatically, so a page reload restores the session via refresh().
 */
@Injectable({ providedIn: 'root' })
export class AuthStore {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = inject(API_BASE_URL);

  private readonly session = signal<SessionResponse | null>(null);
  private refreshInFlight: Observable<string | null> | null = null;

  readonly user = computed<UserProfile | null>(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly role = computed<UserRole | null>(() => this.user()?.role ?? null);
  readonly isAdmin = computed(() => this.role() === 'Admin');

  accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  hasRole(...roles: UserRole[]): boolean {
    const role = this.role();
    return role !== null && roles.includes(role);
  }

  login(email: string, password: string): Observable<UserProfile> {
    return this.http
      .post<SessionResponse>(`${this.baseUrl}/auth/login`, { email, password })
      .pipe(
        tap((session) => this.session.set(session)),
        map((session) => session.user),
      );
  }

  /** Demo servers only: sign in as the demo account for a role, no password needed. */
  demoSignIn(role: 'admin' | 'developer' | 'reporter'): Observable<UserProfile> {
    return this.http.post<SessionResponse>(`${this.baseUrl}/auth/demo/${role}`, null).pipe(
      tap((session) => this.session.set(session)),
      map((session) => session.user),
    );
  }

  register(email: string, password: string, displayName: string): Observable<UserProfile> {
    return this.http.post<UserProfile>(`${this.baseUrl}/auth/register`, { email, password, displayName });
  }

  /** Emails a reset link if the account exists. The API answers the same either way. */
  forgotPassword(email: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/auth/forgot-password`, { email });
  }

  resetPassword(userId: string, token: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/auth/reset-password`, { userId, token, newPassword });
  }

  /** Full-page URL that starts "Sign in with GitHub/Google"; the API redirects back to /auth/callback. */
  externalLoginUrl(provider: string): string {
    return `${this.baseUrl}/auth/external/${provider.toLowerCase()}`;
  }

  /**
   * Rotates the refresh cookie and returns a new access token (or null when the session is gone).
   * Concurrent callers share one request: presenting a rotated token twice would revoke every session.
   */
  refresh(): Observable<string | null> {
    this.refreshInFlight ??= this.http.post<SessionResponse>(`${this.baseUrl}/auth/refresh`, null).pipe(
      tap((session) => this.session.set(session)),
      map((session) => session.accessToken),
      catchError(() => {
        this.session.set(null);
        return of(null);
      }),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  /** Runs at startup: silently restores the session from the refresh cookie, if any. */
  restoreSession(): Observable<unknown> {
    return this.refresh();
  }

  logout(): void {
    this.http
      .post(`${this.baseUrl}/auth/logout`, null)
      .pipe(catchError(() => of(null)))
      .subscribe(() => this.endSession());
  }

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http
      .post<void>(`${this.baseUrl}/auth/change-password`, { currentPassword, newPassword })
      .pipe(tap(() => this.endSession()));
  }

  /** Clears local state and returns to the login page (used on logout and when refresh fails). */
  endSession(): void {
    this.session.set(null);
    void this.router.navigate(['/login']);
  }
}
