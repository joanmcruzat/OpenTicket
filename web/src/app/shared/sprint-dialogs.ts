import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule, ValidationErrors, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';
import { SprintSummary } from '@app/core/api/models';
import { SprintInput } from '@app/core/api/sprints-api';
import { addDays, isoToday } from './sprint-dates';

function endNotBeforeStart(group: AbstractControl): ValidationErrors | null {
  const { startDate, endDate } = group.value as { startDate: string; endDate: string };
  return startDate && endDate && endDate < startDate ? { dateOrder: true } : null;
}

/** Create or edit a sprint. Closes with the values to save, or undefined. */
@Component({
  selector: 'app-sprint-form-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>{{ sprint ? 'Edit sprint' : 'Create sprint' }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        <mat-form-field class="full">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" maxlength="100" cdkFocusInitial placeholder="e.g. Sprint 6" />
          @if (form.controls.name.invalid) { <mat-error>A name is required.</mat-error> }
        </mat-form-field>
        <mat-form-field class="full">
          <mat-label>Sprint goal (optional)</mat-label>
          <textarea matInput formControlName="goal" rows="3" maxlength="500"
                    placeholder="What should be true when this sprint ends?"></textarea>
        </mat-form-field>
        <div class="dates">
          <mat-form-field>
            <mat-label>Start date</mat-label>
            <input matInput type="date" formControlName="startDate" />
          </mat-form-field>
          <mat-form-field>
            <mat-label>End date</mat-label>
            <input matInput type="date" formControlName="endDate" />
          </mat-form-field>
        </div>
        @if (form.hasError('dateOrder')) { <p class="error" role="alert">The end date must be on or after the start date.</p> }
        @if (sprint?.state === 'Active') { <p class="hint">An active sprint always needs both dates.</p> }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit">{{ sprint ? 'Save' : 'Create' }}</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .full { width: 100%; }
    .dates { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .error { color: var(--mat-sys-error); margin: 0; }
    .hint { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); margin: 0; }
  `,
})
export class SprintFormDialog {
  protected readonly sprint = inject<SprintSummary | null>(MAT_DIALOG_DATA, { optional: true });
  private readonly dialogRef = inject(MatDialogRef<SprintFormDialog, SprintInput>);
  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      name: [this.sprint?.name ?? '', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(100)]],
      goal: [this.sprint?.goal ?? '', Validators.maxLength(500)],
      startDate: [this.sprint?.startDate ?? ''],
      endDate: [this.sprint?.endDate ?? ''],
    },
    { validators: endNotBeforeStart },
  );

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      name: v.name.trim(),
      goal: v.goal.trim() || null,
      startDate: v.startDate || null,
      endDate: v.endDate || null,
    });
  }
}

/** Confirms the dates and starts a sprint. Closes with { startDate, endDate }, or undefined. */
@Component({
  selector: 'app-start-sprint-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Start {{ sprint.name }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        <p>{{ sprint.ticketCount }} {{ sprint.ticketCount === 1 ? 'ticket' : 'tickets' }} will appear on the board.</p>
        <div class="dates">
          <mat-form-field>
            <mat-label>Start date</mat-label>
            <input matInput type="date" formControlName="startDate" required />
          </mat-form-field>
          <mat-form-field>
            <mat-label>End date</mat-label>
            <input matInput type="date" formControlName="endDate" required />
          </mat-form-field>
        </div>
        @if (form.hasError('dateOrder')) { <p class="error" role="alert">The end date must be on or after the start date.</p> }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit">Start sprint</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `
    .dates { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .error { color: var(--mat-sys-error); margin: 0; }
  `,
})
export class StartSprintDialog {
  protected readonly sprint = inject<SprintSummary>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<StartSprintDialog, { startDate: string; endDate: string }>);
  private readonly start = this.sprint.startDate ?? isoToday();
  protected readonly form = inject(NonNullableFormBuilder).group(
    {
      startDate: [this.start, Validators.required],
      // Two weeks is the most common sprint length.
      endDate: [this.sprint.endDate ?? addDays(this.start, 13), Validators.required],
    },
    { validators: endNotBeforeStart },
  );

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close(this.form.getRawValue());
  }
}

export interface CompleteSprintData {
  sprint: SprintSummary;
  /** Planned sprints that can take the unfinished tickets. */
  planned: SprintSummary[];
}

/** Completes a sprint. Closes with { target: sprintId | null } (null = backlog), or undefined. */
@Component({
  selector: 'app-complete-sprint-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatRadioModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Complete {{ data.sprint.name }}</h2>
    <form [formGroup]="form" (ngSubmit)="submit()">
      <mat-dialog-content>
        <p>
          <strong>{{ data.sprint.doneCount }}</strong> done ·
          <strong>{{ unfinished() }}</strong> {{ unfinished() === 1 ? 'ticket is' : 'tickets are' }} not finished.
        </p>
        @if (unfinished() > 0) {
          <p id="move-label">Move the unfinished tickets to:</p>
          <mat-radio-group formControlName="target" aria-labelledby="move-label" class="options">
            @for (sprint of data.planned; track sprint.id) {
              <mat-radio-button [value]="sprint.id">{{ sprint.name }}</mat-radio-button>
            }
            <mat-radio-button value="backlog">The backlog</mat-radio-button>
          </mat-radio-group>
        }
      </mat-dialog-content>
      <mat-dialog-actions align="end">
        <button mat-button type="button" mat-dialog-close>Cancel</button>
        <button mat-flat-button type="submit">Complete sprint</button>
      </mat-dialog-actions>
    </form>
  `,
  styles: `.options { display: grid; gap: 4px; }`,
})
export class CompleteSprintDialog {
  protected readonly data = inject<CompleteSprintData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject(MatDialogRef<CompleteSprintDialog, { target: string | null }>);
  protected readonly unfinished = computed(() => this.data.sprint.ticketCount - this.data.sprint.doneCount);
  protected readonly form = inject(NonNullableFormBuilder).group({
    target: [this.data.planned[0]?.id ?? 'backlog'],
  });

  protected submit(): void {
    const target = this.form.getRawValue().target;
    this.dialogRef.close({ target: target === 'backlog' ? null : target });
  }
}
