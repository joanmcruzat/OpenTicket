import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { ApiError } from '../http/api-error';
import { applyServerErrors } from '../http/form-errors';
import { Notifier } from '../http/notifier';
import { AuthStore } from './auth-store';

@Component({
  selector: 'app-change-password-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Change password</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        <p>You will be signed out on every device and asked to sign in again.</p>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Current password</mat-label>
          <input matInput type="password" formControlName="currentPassword" autocomplete="current-password" />
          @if (form.controls.currentPassword.hasError('server')) {
            <mat-error>{{ form.controls.currentPassword.getError('server') }}</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>New password</mat-label>
          <input matInput type="password" formControlName="newPassword" autocomplete="new-password" />
          <mat-hint>At least 10 characters.</mat-hint>
          @if (form.controls.newPassword.hasError('minlength')) {
            <mat-error>At least 10 characters.</mat-error>
          }
          @if (form.controls.newPassword.hasError('server')) {
            <mat-error>{{ form.controls.newPassword.getError('server') }}</mat-error>
          }
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Change password</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `.full { width: 100%; }`,
})
export class ChangePasswordDialog {
  private readonly auth = inject(AuthStore);
  private readonly notifier = inject(Notifier);
  private readonly dialogRef = inject(MatDialogRef<ChangePasswordDialog>);

  protected readonly saving = signal(false);
  protected readonly form = inject(NonNullableFormBuilder).group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, Validators.minLength(10)]],
  });

  protected submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const { currentPassword, newPassword } = this.form.getRawValue();
    this.auth.changePassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.dialogRef.close();
        this.notifier.success('Password changed. Please sign in again.');
      },
      error: (error: ApiError) => {
        this.saving.set(false);
        applyServerErrors(this.form, error);
      },
    });
  }
}
