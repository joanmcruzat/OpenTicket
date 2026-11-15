import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { UserRole } from '../api/models';
import { AuthStore } from './auth-store';

// Guards only shape navigation. The API enforces every permission.

export const authGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthStore);
  return auth.isAuthenticated() || inject(Router).createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
};

export const guestGuard: CanActivateFn = () =>
  !inject(AuthStore).isAuthenticated() || inject(Router).createUrlTree(['/dashboard']);

export const roleGuard =
  (...roles: UserRole[]): CanActivateFn =>
  () =>
    inject(AuthStore).hasRole(...roles) || inject(Router).createUrlTree(['/dashboard']);
