import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, inject, input, output, viewChild } from '@angular/core';
import { FormGroupDirective, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { CommentView } from '@app/core/api/models';
import { Avatar } from '@app/shared/badges';
import { RoleLabelPipe } from '@app/shared/labels';

/** Comment thread plus composer. Bodies are rendered as text (never HTML). */
@Component({
  selector: 'app-ticket-conversation',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatButtonModule, Avatar, RoleLabelPipe],
  template: `
    <ol class="comments">
      @for (comment of comments(); track comment.id) {
        <li>
          <app-avatar [name]="comment.authorName" [size]="32" />
          <div class="content">
            <div class="meta">
              <strong>{{ comment.authorName ?? 'Unknown user' }}</strong>
              @if (comment.authorRole) { <span class="role">{{ comment.authorRole | roleLabel }}</span> }
              <span class="when">{{ comment.createdAtUtc | date: 'medium' }}</span>
            </div>
            <p class="body">{{ comment.body }}</p>
          </div>
        </li>
      } @empty {
        <li class="muted">No comments yet.</li>
      }
    </ol>
    @if (canComment()) {
      <form [formGroup]="form" (ngSubmit)="submit()">
        <mat-form-field class="full">
          <mat-label>Add a comment</mat-label>
          <textarea matInput formControlName="body" rows="3" maxlength="4000"></textarea>
        </mat-form-field>
        <div class="actions"><button mat-flat-button type="submit" [disabled]="posting() || form.invalid">Post comment</button></div>
      </form>
    } @else {
      <p class="muted">This ticket is closed; the conversation is read-only.</p>
    }
  `,
  styles: `
    .comments { list-style: none; padding: 0; margin: 0 0 16px; display: grid; gap: 16px; }
    li { display: flex; gap: 12px; align-items: flex-start; }
    .content { flex: 1; min-width: 0; }
    .meta { display: flex; gap: 8px; align-items: baseline; flex-wrap: wrap; }
    .role { font: var(--mat-sys-label-small); padding: 2px 8px; border-radius: 8px; background: var(--mat-sys-surface-container-high); }
    .when, .muted { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .body { white-space: pre-wrap; overflow-wrap: anywhere; margin: 8px 0 0; }
    .full { width: 100%; }
    .actions { display: flex; justify-content: flex-end; }
  `,
})
export class TicketConversation {
  readonly comments = input.required<readonly CommentView[]>();
  readonly canComment = input.required<boolean>();
  readonly posting = input(false);
  readonly post = output<string>();

  private readonly formDirective = viewChild(FormGroupDirective);
  protected readonly form = inject(NonNullableFormBuilder).group({
    body: ['', [Validators.required, Validators.maxLength(4000), Validators.pattern(/\S/)]],
  });

  protected submit(): void {
    if (this.form.invalid || this.posting()) return;
    this.post.emit(this.form.getRawValue().body.trim());
  }

  /** Called by the parent after a successful post. Also clears the "submitted" state so no error shows. */
  reset(): void {
    const directive = this.formDirective();
    if (directive) {
      directive.resetForm();
    } else {
      this.form.reset();
    }
  }
}
