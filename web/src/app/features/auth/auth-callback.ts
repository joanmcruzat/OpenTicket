import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { AuthStore } from '@app/core/auth/auth-store';

/**
 * Landing page after "Sign in with GitHub/Google". The API has already set the refresh cookie; exchanging it for an
 * access token finishes the sign-in. No token ever travels in the URL.
 */
@Component({
  selector: 'app-auth-callback',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatProgressSpinnerModule],
  template: `<div class="center" role="status"><mat-spinner diameter="40" /><p>Signing you in…</p></div>`,
  styles: `.center { min-height: 100vh; display: grid; place-content: center; justify-items: center; gap: 16px; }`,
})
export class AuthCallback implements OnInit {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);

  ngOnInit(): void {
    if (this.auth.isAuthenticated()) {
      void this.router.navigateByUrl('/dashboard');
      return;
    }
    this.auth.refresh().subscribe((token) =>
      token
        ? void this.router.navigateByUrl('/dashboard')
        : void this.router.navigate(['/login'], { queryParams: { error: 'external_failed' } }),
    );
  }
}
