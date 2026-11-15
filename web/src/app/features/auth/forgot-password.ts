import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { AuthStore } from '@app/core/auth/auth-store';
import { ApiError } from '@app/core/http/api-error';
import { AuthLayout } from './auth-layout';

@Component({
  selector: 'app-forgot-password',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, RouterLink, MatFormFieldModule, MatInputModule, MatButtonModule, MatIconModule, AuthLayout],
  styleUrl: './auth-page.scss',
  template: `
    <app-auth-layout heading="Reset your password" subtitle="Enter your account email and we'll send you a reset link.">
      @if (sentTo(); as email) {
        <p class="form-success" role="status">
          <mat-icon>mark_email_read</mat-icon>
          <span>If an account exists for <strong>{{ email }}</strong>, a reset link is on its way. It works for one hour.
            Check your spam folder if it doesn't arrive.</span>
        </p>
        <button mat-stroked-button class="submit" type="button" (click)="sentTo.set(null)">Use a different email</button>
      } @else {
        @if (error(); as message) {
          <p class="form-error" role="alert"><mat-icon>error_outline</mat-icon> {{ message }}</p>
        }
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <mat-form-field class="full">
            <mat-label>Email</mat-label>
            <input matInput type="email" formControlName="email" autocomplete="email" />
            @if (form.controls.email.hasError('required')) { <mat-error>Email is required.</mat-error> }
            @if (form.controls.email.hasError('email')) { <mat-error>Enter a valid email.</mat-error> }
          </mat-form-field>
          <button mat-flat-button class="submit" type="submit" [disabled]="submitting()">
            {{ submitting() ? 'Sending…' : 'Send reset link' }}
          </button>
        </form>
      }
      <p class="alt"><a routerLink="/login">Back to sign in</a></p>
    </app-auth-layout>
  `,
})
export class ForgotPassword {
  private readonly auth = inject(AuthStore);

  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly sentTo = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, Validators.email]],
  });

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    const { email } = this.form.getRawValue();
    this.auth.forgotPassword(email).subscribe({
      next: () => {
        this.submitting.set(false);
        this.sentTo.set(email);
      },
      error: (error: ApiError) => {
        this.submitting.set(false);
        this.error.set(error.title);
      },
    });
  }
}
