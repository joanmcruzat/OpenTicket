import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { TicketDetail } from '@app/core/api/models';
import { UpdateTicketInput } from '@app/core/api/tickets-api';

/** Asks why a ticket won't be done. Closes with the trimmed reason, or undefined. */
@Component({
  selector: 'app-wont-do-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Won't do this ticket?</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        <p>The ticket will be closed. It can be reopened to To Do later.</p>
        <mat-form-field class="full">
          <mat-label>Reason</mat-label>
          <textarea matInput formControlName="reason" rows="4" maxlength="1000" cdkFocusInitial
                    placeholder="e.g. Duplicate of OT-000012, works as designed, no longer needed"></textarea>
          @if (form.controls.reason.hasError('required') || form.controls.reason.hasError('pattern')) {
            <mat-error>A reason is required.</mat-error>
          }
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Keep it open</button>
        <button mat-flat-button type="submit" class="danger">Close as Won't Do</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `.full { width: 100%; } .danger { --mat-button-filled-container-color: var(--mat-sys-error); }`,
})
export class WontDoDialog {
  private readonly dialogRef = inject(MatDialogRef<WontDoDialog, string>);
  protected readonly form = inject(NonNullableFormBuilder).group({
    reason: ['', [Validators.required, Validators.maxLength(1000), Validators.pattern(/\S/)]],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close(this.form.getRawValue().reason.trim());
  }
}

/** Edits the title and description. Closes with only the changed fields. */
@Component({
  selector: 'app-edit-ticket-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Edit ticket</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        <mat-form-field class="full">
          <mat-label>Summary</mat-label>
          <input matInput formControlName="title" maxlength="200" />
          @if (form.controls.title.invalid) { <mat-error>A summary is required.</mat-error> }
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Description</mat-label>
          <textarea matInput formControlName="description" rows="10" maxlength="8000"></textarea>
          @if (form.controls.description.invalid) { <mat-error>A description is required.</mat-error> }
        </mat-form-field>
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit">Save</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `.full { width: 100%; }`,
})
export class EditTicketDialog {
  private readonly ticket = inject<TicketDetail>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<EditTicketDialog, UpdateTicketInput>);
  protected readonly form = inject(NonNullableFormBuilder).group({
    title: [this.ticket.title, [Validators.required, Validators.pattern(/\S/)]],
    description: [this.ticket.description, [Validators.required, Validators.pattern(/\S/)]],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.getRawValue();
    const change: UpdateTicketInput = {};
    if (value.title.trim() !== this.ticket.title) change.title = value.title;
    if (value.description.trim() !== this.ticket.description) change.description = value.description;
    this.dialogRef.close(change);
  }
}
