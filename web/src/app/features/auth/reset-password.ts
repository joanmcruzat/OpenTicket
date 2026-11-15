import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { AuthStore } from '@app/core/auth/auth-store';
import { ApiError } from '@app/core/http/api-error';
import { applyServerErrors } from '@app/core/http/form-errors';
import { AuthLayout } from './auth-layout';

function passwordsMatch(group: AbstractControl): ValidationErrors | null {
  const value = group.value as { newPassword: string; confirm: string };
  return value.confirm && value.newPassword !== value.confirm ? { mismatch: true } : null;
}

@Component({
  selector: 'app-reset-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, AuthLayout],
  styleUrl: './auth-page.scss',
  template: `
    <app-auth-layout heading="Choose a new password" subtitle="You'll be signed out everywhere else.">
      @if (!linkLooksValid()) {
        <p class="form-error" role="alert"><mat-icon>link_off</mat-icon> This reset link is incomplete. Open it again from the email.</p>
        <a mat-flat-button class="submit" routerLink="/forgot-password">Request a new link</a>
      } @else {
        @if (error(); as message) {
          <p class="form-error" role="alert"><mat-icon>error_outline</mat-icon>
            <span>{{ message }} @if (linkExpired()) { <a routerLink="/forgot-password">Request a new link</a> }</span>
          </p>
        }
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <mat-form-field class="full">
            <mat-label>New password</mat-label>
            <input matInput [type]="showPassword() ? 'text' : 'password'" formControlName="newPassword" autocomplete="new-password" />
            <button mat-icon-button matSuffix type="button" (click)="showPassword.set(!showPassword())"
                    [attr.aria-label]="showPassword() ? 'Hide password' : 'Show password'">
              <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
            </button>
            <mat-hint>At least 10 characters.</mat-hint>
            @if (form.controls.newPassword.hasError('minlength') || form.controls.newPassword.hasError('required')) {
              <mat-error>Use at least 10 characters.</mat-error>
            }
            @if (form.controls.newPassword.hasError('server')) { <mat-error>{{ form.controls.newPassword.getError('server') }}</mat-error> }
          </mat-form-field>
          <mat-form-field class="full">
            <mat-label>Confirm new password</mat-label>
            <input matInput [type]="showPassword() ? 'text' : 'password'" formControlName="confirm" autocomplete="new-password" />
          </mat-form-field>
          @if (form.hasError('mismatch') && form.controls.confirm.touched) {
            <p class="form-error" role="alert"><mat-icon>error_outline</mat-icon> The passwords don't match.</p>
          }
          <button mat-flat-button class="submit" type="submit" [disabled]="submitting()">
            {{ submitting() ? 'Saving…' : 'Set new password' }}
          </button>
        </form>
      }
      <p class="alt"><a routerLink="/login">Back to sign in</a></p>
    </app-auth-layout>
  `,
})
export class ResetPassword {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);

  /** Bound from the emailed link: /reset-password?uid=…&token=… */
  readonly uid = input<string>();
  readonly token = input<string>();

  protected readonly submitting = signal(false);
  protected readonly showPassword = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly linkExpired = signal(false);
  protected readonly linkLooksValid = computed(() => !!this.uid() && !!this.token());
  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      newPassword: ['', [Validators.required, Validators.minLength(10)]],
      confirm: ['', Validators.required],
    },
    { validators: passwordsMatch },
  );

  protected submit(): void {
    const uid = this.uid();
    const token = this.token();
    if (this.form.invalid || this.submitting() || !uid || !token) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    this.auth.resetPassword(uid, token, this.form.getRawValue().newPassword).subscribe({
      next: () => void this.router.navigate(['/login'], { queryParams: { reset: 'done' } }),
      error: (error: ApiError) => {
        this.submitting.set(false);
        this.linkExpired.set(error.code === 'auth.invalid_reset_link');
        if (applyServerErrors(this.form, error).length > 0 || Object.keys(error.fieldErrors).length === 0) {
          this.error.set(error.title);
        }
      },
    });
  }
}
