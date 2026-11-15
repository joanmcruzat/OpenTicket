import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { toApiError } from './api-error';
import { Notifier } from './notifier';

/**
 * Normalizes every HTTP failure into an ApiError. Server and network failures get a toast here;
 * 4xx errors are left to the calling page (forms show field errors, pages show inline messages).
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notifier = inject(Notifier);
  return next(req).pipe(
    catchError((error: unknown) => {
      const apiError = toApiError(error);
      if (apiError.status === 0 || apiError.status >= 500) {
        notifier.error(apiError.title);
      } else if (apiError.status === 429) {
        notifier.error('Too many attempts. Please wait a minute and try again.');
      }
      return throwError(() => apiError);
    }),
  );
};
