import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { API_BASE_URL } from '@app/core/api/api-config';
import { Category, TICKET_PRIORITIES, TICKET_TYPES, TicketPriority, TicketType } from '@app/core/api/models';
import { TicketsApi } from '@app/core/api/tickets-api';
import { ApiError } from '@app/core/http/api-error';
import { applyServerErrors } from '@app/core/http/form-errors';
import { Notifier } from '@app/core/http/notifier';
import { PriorityBadge, TypeIcon } from '@app/shared/badges';
import { TicketNumberPipe } from '@app/shared/labels';

const SUMMARY_HINTS: Record<TicketType, string> = {
  Bug: 'e.g. Login button does nothing on Safari',
  Task: 'e.g. Upgrade Npgsql to the latest patch',
  Story: 'e.g. As a reporter I can attach screenshots',
  Improvement: 'e.g. Make the ticket list load faster',
};

const DESCRIPTION_HINTS: Record<TicketType, string> = {
  Bug: 'Steps to reproduce, expected result, actual result, environment (browser, OS, version)',
  Task: 'What needs to be done and how we know it is finished',
  Story: 'Who wants it, what they want, and why. Add acceptance criteria.',
  Improvement: 'What works today, what could be better, and why it matters',
};

@Component({
  selector: 'app-ticket-create',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule, RouterLink, MatCardModule, MatFormFieldModule, MatInputModule, MatSelectModule, MatButtonModule,
    MatButtonToggleModule, TypeIcon, PriorityBadge,
  ],
  providers: [TicketNumberPipe],
  template: `
    <header class="page-header"><h1>Create ticket</h1></header>
    <mat-card appearance="outlined" class="form-card">
      <mat-card-content>
        <form [formGroup]="form" (ngSubmit)="submit()" novalidate>
          @if (error(); as message) { <p class="error" role="alert">{{ message }}</p> }
          <div class="type-picker">
            <span class="label" id="type-label">Issue type</span>
            <mat-button-toggle-group formControlName="type" aria-labelledby="type-label">
              @for (t of types; track t) {
                <mat-button-toggle [value]="t"><app-type-icon [type]="t" [showLabel]="true" /></mat-button-toggle>
              }
            </mat-button-toggle-group>
          </div>
          <mat-form-field class="full">
            <mat-label>Summary</mat-label>
            <input matInput formControlName="title" maxlength="200" [placeholder]="summaryHint()" />
            <mat-hint align="end">{{ form.controls.title.value.length }}/200</mat-hint>
            @if (form.controls.title.hasError('required')) { <mat-error>A summary is required.</mat-error> }
          </mat-form-field>
          <mat-form-field class="full">
            <mat-label>Description</mat-label>
            <textarea matInput formControlName="description" rows="8" maxlength="8000"
                      [placeholder]="descriptionHint()"></textarea>
            @if (form.controls.description.hasError('required')) { <mat-error>A description is required.</mat-error> }
          </mat-form-field>
          <div class="row">
            <mat-form-field>
              <mat-label>Category</mat-label>
              <mat-select formControlName="categoryId">
                @for (c of (categories.hasValue() ? categories.value() : undefined) ?? []; track c.id) { <mat-option [value]="c.id">{{ c.name }}</mat-option> }
              </mat-select>
              @if (form.controls.categoryId.hasError('required')) { <mat-error>Choose a category.</mat-error> }
              @if (form.controls.categoryId.hasError('server')) { <mat-error>{{ form.controls.categoryId.getError('server') }}</mat-error> }
              @if (categories.hasValue() && categories.value().length === 0) {
                <mat-hint>No categories yet. An admin must create one first.</mat-hint>
              }
            </mat-form-field>
            <mat-form-field>
              <mat-label>Priority</mat-label>
              <mat-select formControlName="priority">
                <mat-select-trigger><app-priority-badge [priority]="form.controls.priority.value" /></mat-select-trigger>
                @for (p of priorities; track p) { <mat-option [value]="p"><app-priority-badge [priority]="p" /></mat-option> }
              </mat-select>
            </mat-form-field>
          </div>
          <div class="actions">
            <a mat-button routerLink="/tickets">Cancel</a>
            <button mat-flat-button type="submit" [disabled]="submitting()">Create</button>
          </div>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .form-card { max-width: 760px; }
    .type-picker { display: flex; flex-direction: column; gap: 6px; margin-bottom: 20px; }
    .type-picker .label { font: var(--mat-sys-label-large); color: var(--mat-sys-on-surface-variant); }
    mat-button-toggle-group { flex-wrap: wrap; }
    .full { width: 100%; }
    .row { display: flex; gap: 16px; flex-wrap: wrap; }
    .row mat-form-field { flex: 1 1 220px; }
    .actions { display: flex; justify-content: flex-end; gap: 8px; }
  `,
})
export class TicketCreate {
  private readonly api = inject(TicketsApi);
  private readonly router = inject(Router);
  private readonly notifier = inject(Notifier);
  private readonly ticketNumber = inject(TicketNumberPipe);
  private readonly baseUrl = inject(API_BASE_URL);

  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly types = TICKET_TYPES;
  protected readonly categories = httpResource<Category[]>(() => `${this.baseUrl}/categories`);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly form = inject(NonNullableFormBuilder).group({
    type: ['Bug' as TicketType, Validators.required],
    title: ['', [Validators.required, Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.maxLength(8000)]],
    categoryId: ['', Validators.required],
    priority: ['Medium' as TicketPriority, Validators.required],
  });

  private readonly selectedType = toSignal(this.form.controls.type.valueChanges, { initialValue: this.form.controls.type.value });
  protected readonly summaryHint = computed(() => SUMMARY_HINTS[this.selectedType()]);
  protected readonly descriptionHint = computed(() => DESCRIPTION_HINTS[this.selectedType()]);

  protected submit(): void {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    this.api.create(this.form.getRawValue()).subscribe({
      next: (ticket) => {
        this.notifier.success(`Ticket ${this.ticketNumber.transform(ticket.number)} created.`);
        void this.router.navigate(['/tickets', ticket.id]);
      },
      error: (error: ApiError) => {
        this.submitting.set(false);
        if (applyServerErrors(this.form, error).length > 0 || Object.keys(error.fieldErrors).length === 0) {
          this.error.set(error.title);
        }
      },
    });
  }
}
