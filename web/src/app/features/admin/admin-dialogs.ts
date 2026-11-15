import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { Observable } from 'rxjs';
import { AdminApi } from '@app/core/api/admin-api';
import { Category, USER_ROLES, UserListItem, UserRole } from '@app/core/api/models';
import { RoleLabelPipe } from '@app/shared/labels';
import { ApiError } from '@app/core/http/api-error';
import { applyServerErrors } from '@app/core/http/form-errors';

@Component({
  selector: 'app-create-user-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule, RoleLabelPipe],
  template: `
    <h2 mat-dialog-title>Add user</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        @if (error(); as message) { <p class="error" role="alert">{{ message }}</p> }
        <mat-form-field class="full"><mat-label>Name</mat-label><input matInput formControlName="displayName" maxlength="100" /></mat-form-field>
        <mat-form-field class="full">
          <mat-label>Email</mat-label><input matInput type="email" formControlName="email" />
          @if (form.controls.email.hasError('email')) { <mat-error>Enter a valid email.</mat-error> }
          @if (form.controls.email.hasError('server')) { <mat-error>{{ form.controls.email.getError('server') }}</mat-error> }
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Role</mat-label>
          <mat-select formControlName="role">@for (r of roles; track r) { <mat-option [value]="r">{{ r | roleLabel }}</mat-option> }</mat-select>
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Temporary password</mat-label>
          <input matInput formControlName="temporaryPassword" autocomplete="new-password" />
          <mat-hint>At least 10 characters. Share it securely; the user can change it after signing in.</mat-hint>
          @if (form.controls.temporaryPassword.hasError('server')) { <mat-error>{{ form.controls.temporaryPassword.getError('server') }}</mat-error> }
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Add user</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `.full { width: 100%; }`,
})
export class CreateUserDialog {
  private readonly api = inject(AdminApi);
  private readonly dialogRef = inject(MatDialogRef<CreateUserDialog, UserListItem>);
  protected readonly roles = USER_ROLES;
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    displayName: ['', [Validators.required, Validators.maxLength(100)]],
    email: ['', [Validators.required, Validators.email]],
    role: ['Agent' as UserRole, Validators.required],
    temporaryPassword: ['', [Validators.required, Validators.minLength(10)]],
  });

  protected submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.api.createUser(this.form.getRawValue()).subscribe({
      next: (user) => this.dialogRef.close(user),
      error: (error: ApiError) => {
        this.saving.set(false);
        if (error.code === 'auth.email_taken') {
          this.form.controls.email.setErrors({ server: error.title });
        } else if (applyServerErrors(this.form, error).length > 0 || Object.keys(error.fieldErrors).length === 0) {
          this.error.set(error.title);
        }
      },
    });
  }
}

@Component({
  selector: 'app-reset-password-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Reset password for {{ user.displayName }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        <p>The user is signed out everywhere and any lockout is cleared.</p>
        <mat-form-field class="full">
          <mat-label>Temporary password</mat-label>
          <input matInput formControlName="temporaryPassword" autocomplete="new-password" cdkFocusInitial />
          <mat-hint>At least 10 characters.</mat-hint>
          @if (form.controls.temporaryPassword.hasError('server')) { <mat-error>{{ form.controls.temporaryPassword.getError('server') }}</mat-error> }
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Reset password</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `.full { width: 100%; }`,
})
export class ResetPasswordDialog {
  protected readonly user = inject<UserListItem>(MAT_DIALOG_DATA);
  private readonly api = inject(AdminApi);
  private readonly dialogRef = inject(MatDialogRef<ResetPasswordDialog, boolean>);
  protected readonly saving = signal(false);
  protected readonly form = inject(NonNullableFormBuilder).group({
    temporaryPassword: ['', [Validators.required, Validators.minLength(10)]],
  });

  protected submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    this.api.resetPassword(this.user.id, this.form.getRawValue().temporaryPassword).subscribe({
      next: () => this.dialogRef.close(true),
      error: (error: ApiError) => {
        this.saving.set(false);
        applyServerErrors(this.form, error);
      },
    });
  }
}

@Component({
  selector: 'app-category-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ category ? 'Edit category' : 'New category' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        <mat-form-field class="full">
          <mat-label>Name</mat-label><input matInput formControlName="name" maxlength="100" cdkFocusInitial />
          @if (form.controls.name.hasError('required') || form.controls.name.hasError('pattern')) { <mat-error>A name is required.</mat-error> }
          @if (form.controls.name.hasError('server')) { <mat-error>{{ form.controls.name.getError('server') }}</mat-error> }
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Description (optional)</mat-label>
          <textarea matInput formControlName="description" rows="3" maxlength="500"></textarea>
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit" [disabled]="saving()">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `.full { width: 100%; }`,
})
export class CategoryDialog {
  protected readonly category = inject<Category | null>(MAT_DIALOG_DATA);
  private readonly api = inject(AdminApi);
  private readonly dialogRef = inject(MatDialogRef<CategoryDialog, Category>);
  protected readonly saving = signal(false);
  protected readonly form = inject(NonNullableFormBuilder).group({
    name: [this.category?.name ?? '', [Validators.required, Validators.maxLength(100), Validators.pattern(/\S/)]],
    description: [this.category?.description ?? '', Validators.maxLength(500)],
  });

  protected submit(): void {
    if (this.form.invalid || this.saving()) {
      this.form.markAllAsTouched();
      return;
    }
    this.saving.set(true);
    const { name, description } = this.form.getRawValue();
    const request: Observable<Category> = this.category
      ? this.api.updateCategory(this.category.id, name, description || null)
      : this.api.createCategory(name, description || null);
    request.subscribe({
      next: (saved) => this.dialogRef.close(saved),
      error: (error: ApiError) => {
        this.saving.set(false);
        if (error.code === 'category.duplicate_name') {
          this.form.controls.name.setErrors({ server: error.title });
        } else {
          applyServerErrors(this.form, error);
        }
      },
    });
  }
}
