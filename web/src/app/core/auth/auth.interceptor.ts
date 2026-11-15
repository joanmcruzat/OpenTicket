import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { API_BASE_URL } from '../api/api-config';
import { AuthStore } from './auth-store';

/** Endpoints authenticated by credentials or the refresh cookie, never by the bearer token. */
const PUBLIC_AUTH_PATHS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout'];

/** Adds the bearer token to API calls; on a 401, refreshes once and retries the request. */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStore);
  const baseUrl = inject(API_BASE_URL);

  if (!req.url.startsWith(baseUrl) || PUBLIC_AUTH_PATHS.some((path) => req.url.startsWith(baseUrl + path))) {
    return next(req);
  }

  const withToken = (request: HttpRequest<unknown>, token: string | null) =>
    token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;

  return next(withToken(req, auth.accessToken())).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }

      return auth.refresh().pipe(
        switchMap((token) => {
          if (!token) {
            auth.endSession();
            return throwError(() => error);
          }
          return next(withToken(req, token));
        }),
      );
    }),
  );
};
