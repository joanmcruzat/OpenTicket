import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { beforeEach, describe, expect, it } from 'vitest';
import { SessionResponse } from '../api/models';
import { authInterceptor } from './auth.interceptor';
import { AuthStore } from './auth-store';

const session = (token: string): SessionResponse => ({
  accessToken: token,
  expiresAtUtc: '2026-11-07T10:00:00Z',
  user: { id: 'u1', email: 'u@x.test', displayName: 'U', role: 'Customer' },
});

describe('AuthStore + authInterceptor', () => {
  let http: HttpClient;
  let backend: HttpTestingController;
  let auth: AuthStore;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideRouter([]),
        provideHttpClient(withInterceptors([authInterceptor])),
        provideHttpClientTesting(),
      ],
    });
    http = TestBed.inject(HttpClient);
    backend = TestBed.inject(HttpTestingController);
    auth = TestBed.inject(AuthStore);
  });

  it('stores the session in memory after login', () => {
    auth.login('u@x.test', 'pw').subscribe();
    const req = backend.expectOne('/api/v1/auth/login');
    expect(req.request.headers.has('Authorization')).toBe(false);
    req.flush(session('t1'));

    expect(auth.isAuthenticated()).toBe(true);
    expect(auth.role()).toBe('Customer');
    expect(auth.accessToken()).toBe('t1');
  });

  it('sends one refresh request for concurrent callers', () => {
    let results: (string | null)[] = [];
    auth.refresh().subscribe((t) => results.push(t));
    auth.refresh().subscribe((t) => results.push(t));

    backend.expectOne('/api/v1/auth/refresh').flush(session('t2'));

    expect(results).toEqual(['t2', 't2']);
    results = [];
  });

  it('clears the session when refresh fails', () => {
    auth.login('u@x.test', 'pw').subscribe();
    backend.expectOne('/api/v1/auth/login').flush(session('t1'));
    let token: string | null | undefined;

    auth.refresh().subscribe((t) => (token = t));
    backend.expectOne('/api/v1/auth/refresh').flush(null, { status: 401, statusText: 'Unauthorized' });

    expect(token).toBeNull();
    expect(auth.isAuthenticated()).toBe(false);
  });

  it('attaches the bearer token, and on 401 refreshes once and retries with the new token', () => {
    auth.login('u@x.test', 'pw').subscribe();
    backend.expectOne('/api/v1/auth/login').flush(session('old'));
    let body: unknown;

    http.get('/api/v1/tickets').subscribe((b) => (body = b));
    const first = backend.expectOne('/api/v1/tickets');
    expect(first.request.headers.get('Authorization')).toBe('Bearer old');
    first.flush(null, { status: 401, statusText: 'Unauthorized' });

    backend.expectOne('/api/v1/auth/refresh').flush(session('new'));
    const retry = backend.expectOne('/api/v1/tickets');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer new');
    retry.flush({ items: [] });

    expect(body).toEqual({ items: [] });
    backend.verify();
  });

  it('does not touch requests outside the API', () => {
    http.get('/assets/logo.svg').subscribe();
    expect(backend.expectOne('/assets/logo.svg').request.headers.has('Authorization')).toBe(false);
  });
});
