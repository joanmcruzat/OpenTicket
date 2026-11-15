import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '@app/core/auth/auth-store';
import { ApiError } from '@app/core/http/api-error';
import { AuthLayout } from './auth-layout';
import { AuthOptions, externalLoginError } from '@app/core/auth/auth-options';
import { DemoPanel } from './demo-panel';
import { ProviderButtons } from './provider-buttons';

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, AuthLayout, ProviderButtons, DemoPanel,
  ],
  styleUrl: './auth-page.scss',
  template: `
    <app-auth-layout heading="Welcome back" subtitle="Sign in to continue to your board.">
      @if (resetDone()) {
        <p class="form-success" role="status"><mat-icon>check_circle</mat-icon> Your password was changed. Sign in with the new one.</p>
      }
      @if (message(); as text) {
        <p class="form-error" role="alert"><mat-icon>error_outline</mat-icon> {{ text }}</p>
      }
      <app-demo-panel />
      <app-provider-buttons />
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <mat-form-field class="full">
          <mat-label>Email</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="username" />
          @if (form.controls.email.hasError('required')) { <mat-error>Email is required.</mat-error> }
          @if (form.controls.email.hasError('email')) { <mat-error>Enter a valid email.</mat-error> }
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Password</mat-label>
          <input matInput [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="current-password" />
          <button mat-icon-button matSuffix type="button" (click)="showPassword.set(!showPassword())"
                  [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'">
            <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
          </button>
          @if (form.controls.password.hasError('required')) { <mat-error>Password is required.</mat-error> }
        </mat-form-field>
        @if (options.passwordReset()) {
          <div class="label-row"><a routerLink="/forgot-password">Forgot password?</a></div>
        }
        <button mat-flat-button class="submit" type="submit" [disabled]="submitting()">
          {{ submitting() ? 'Signing in…' : 'Sign in' }}
        </button>
      </form>
      @if (!options.demo()) {
        <p class="alt">New to OpenTicket? <a routerLink="/register">Create an account</a></p>
      }
    </app-auth-layout>
  `,
})
export class Login {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  protected readonly options = inject(AuthOptions);

  /** Bound from the query string by the router. Only in-app paths are honoured for returnUrl. */
  readonly returnUrl = input<string>();
  readonly error = input<string>();
  readonly reset = input<string>();

  protected readonly submitting = signal(false);
  protected readonly showPassword = signal(false);
  private readonly submitError = signal<string | null>(null);
  protected readonly message = computed(() => this.submitError() ?? externalLoginError(this.error()));
  protected readonly resetDone = computed(() => this.reset() === 'done' && this.submitError() === null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.submitError.set(null);
    const { email, password } = this.form.getRawValue();
    this.auth.login(email, password).subscribe({
      next: () => {
        const target = this.returnUrl();
        void this.router.navigateByUrl(target?.startsWith('/') && !target.startsWith('//') ? target : '/dashboard');
      },
      error: (error: ApiError) => {
        this.submitting.set(false);
        this.submitError.set(error.status === 401 ? 'Incorrect email or password.' : error.title);
      },
    });
  }
}
