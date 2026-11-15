import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { rxResource } from '@angular/core/rxjs-interop';
import { retry } from 'rxjs';
import { API_BASE_URL } from '@app/core/api/api-config';

export type ExternalProvider = 'GitHub' | 'Google';

export type DemoRole = 'admin' | 'developer' | 'reporter';

interface AuthOptionsResponse {
  externalProviders: ExternalProvider[];
  passwordReset: boolean;
  /** Present only when the server runs as a public demo. */
  demo: { resetIntervalHours: number } | null;
}

/** What this server offers on the sign-in pages (and whether it is a demo). Loaded once and shared app-wide. */
@Injectable({ providedIn: 'root' })
export class AuthOptions {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(API_BASE_URL)}/auth/options`;

  // Retried: the first page load can race a server that is still starting (e.g. a sleeping free-tier instance).
  private readonly options = rxResource({
    stream: () => this.http.get<AuthOptionsResponse>(this.url).pipe(retry({ count: 5, delay: 2000 })),
  });

  readonly providers = computed(() => (this.options.hasValue() ? this.options.value() : undefined)?.externalProviders ?? []);
  readonly demo = computed(() => (this.options.hasValue() ? this.options.value() : undefined)?.demo ?? null);
  readonly passwordReset = computed(() => (this.options.hasValue() ? this.options.value() : undefined)?.passwordReset ?? false);
}

const EXTERNAL_ERRORS: Record<string, string> = {
  external_failed: 'Sign-in with the provider was cancelled or failed. Please try again.',
  'auth.external_email_unverified':
    'Your provider account has no verified email address. Verify it there, or sign up with email instead.',
  'auth.account_disabled': 'This account is disabled. Contact an admin.',
};

/** Message for the ?error= code the API puts on /login after a failed external sign-in. */
export function externalLoginError(code: string | undefined): string | null {
  if (!code) return null;
  return EXTERNAL_ERRORS[code] ?? 'Sign-in failed. Please try again.';
}
