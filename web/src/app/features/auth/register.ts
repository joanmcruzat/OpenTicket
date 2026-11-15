import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { switchMap } from 'rxjs';
import { AuthStore } from '@app/core/auth/auth-store';
import { ApiError } from '@app/core/http/api-error';
import { applyServerErrors } from '@app/core/http/form-errors';
import { AuthLayout } from './auth-layout';
import { AuthOptions } from '@app/core/auth/auth-options';
import { ProviderButtons } from './provider-buttons';
import { passwordStrength } from './password-strength';

@Component({
  selector: 'app-register',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, AuthLayout, ProviderButtons,
  ],
  styleUrl: './auth-page.scss',
  styles: `
    .meter { display: grid; grid-template-columns: repeat(4, 1fr); gap: 4px; margin: -8px 0 4px; }
    .meter span { height: 4px; border-radius: 2px; background: var(--mat-sys-outline-variant); }
    .meter[data-score='1'] span:nth-child(-n + 1) { background: #de350b; }
    .meter[data-score='2'] span:nth-child(-n + 2) { background: #ff991f; }
    .meter[data-score='3'] span:nth-child(-n + 3) { background: #36b37e; }
    .meter[data-score='4'] span { background: #00875a; }
    .meter-label { font: var(--mat-sys-body-small); color: var(--mat-sys-on-surface-variant); margin: 0 0 12px; }
    .terms { font: var(--mat-sys-body-small); color: var(--mat-sys-on-surface-variant); margin: 12px 0 0; }
  `,
  template: `
    <app-auth-layout heading="Create your account" subtitle="Report bugs, request features and follow their progress.">
      @if (options.demo()) {
        <p class="form-error" role="status"><mat-icon>info</mat-icon>
          <span>Sign-up is turned off on this demo. <a routerLink="/login">Try a demo role</a> instead.</span>
        </p>
      }
      @if (error(); as message) {
        <p class="form-error" role="alert"><mat-icon>error_outline</mat-icon> {{ message }}</p>
      }
      <app-provider-buttons verb="Sign up" />
      <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
        <mat-form-field class="full">
          <mat-label>Full name</mat-label>
          <input matInput formControlName="displayName" autocomplete="name" maxlength="100" />
          @if (form.controls.displayName.hasError('required')) { <mat-error>Name is required.</mat-error> }
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Work email</mat-label>
          <input matInput type="email" formControlName="email" autocomplete="email" />
          @if (form.controls.email.hasError('required')) { <mat-error>Email is required.</mat-error> }
          @if (form.controls.email.hasError('email')) { <mat-error>Enter a valid email.</mat-error> }
          @if (form.controls.email.hasError('server')) { <mat-error>{{ form.controls.email.getError('server') }}</mat-error> }
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Password</mat-label>
          <input matInput [type]="showPassword() ? 'text' : 'password'" formControlName="password" autocomplete="new-password" />
          <button mat-icon-button matSuffix type="button" (click)="showPassword.set(!showPassword())"
                  [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'">
            <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
          </button>
          @if (form.controls.password.hasError('required')) { <mat-error>Password is required.</mat-error> }
          @if (form.controls.password.hasError('minlength')) { <mat-error>Use at least 10 characters.</mat-error> }
          @if (form.controls.password.hasError('server')) { <mat-error>{{ form.controls.password.getError('server') }}</mat-error> }
        </mat-form-field>
        <div class="meter" [attr.data-score]="strength().score" aria-hidden="true"><span></span><span></span><span></span><span></span></div>
        <p class="meter-label" aria-live="polite">{{ strength().label }}</p>
        <button mat-flat-button class="submit" type="submit" [disabled]="submitting()">
          {{ submitting() ? 'Creating account…' : 'Create account' }}
        </button>
        <p class="terms">New accounts start as Reporters. An admin can make you a Developer.</p>
      </form>
      <p class="alt">Already have an account? <a routerLink="/login">Sign in</a></p>
    </app-auth-layout>
  `,
})
export class Register {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  protected readonly options = inject(AuthOptions);

  protected readonly submitting = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    displayName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, Validators.minLength(10)]],
  });
  private readonly password = toSignal(this.form.controls.password.valueChanges, { initialValue: '' });
  protected readonly strength = computed(() => passwordStrength(this.password()));

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    const { email, password, displayName } = this.form.getRawValue();
    this.auth
      .register(email, password, displayName)
      .pipe(switchMap(() => this.auth.login(email, password)))
      .subscribe({
        next: () => void this.router.navigateByUrl('/dashboard'),
        error: (error: ApiError) => {
          this.submitting.set(false);
          if (error.code === 'auth.email_taken') {
            this.form.controls.email.setErrors({ server: 'An account with this email already exists. Try signing in.' });
          } else if (applyServerErrors(this.form, error).length > 0 || Object.keys(error.fieldErrors).length === 0) {
            this.error.set(error.title);
          }
        },
      });
  }
}
