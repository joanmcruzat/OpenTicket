import { httpResource } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, signal, viewChild } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { AdminApi } from '@app/core/api/admin-api';
import { API_BASE_URL } from '@app/core/api/api-config';
import {
  AgentOption,
  Category,
  CommentView,
  PagedResult,
  SprintSummary,
  TICKET_PRIORITIES,
  TICKET_TYPES,
  TicketDetail,
  TicketHistoryEntry,
  TicketStatus,
} from '@app/core/api/models';
import { TicketsApi, UpdateTicketInput } from '@app/core/api/tickets-api';
import { AuthStore } from '@app/core/auth/auth-store';
import { ApiError } from '@app/core/http/api-error';
import { Notifier } from '@app/core/http/notifier';
import { Avatar, PriorityBadge, StatusBadge, TypeIcon, statusCategory } from '@app/shared/badges';
import { STATUS_LABELS, TYPE_LABELS, TicketNumberPipe } from '@app/shared/labels';
import { ticketActions } from './ticket-actions';
import { TicketConversation } from './ticket-conversation';
import { EditTicketDialog, WontDoDialog } from './ticket-dialogs';
import { TicketTimeline } from './ticket-timeline';

@Component({
  selector: 'app-ticket-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe, RouterLink, MatButtonModule, MatIconModule, MatMenuModule, MatProgressBarModule, MatSelectModule,
    MatFormFieldModule, MatTabsModule, MatTooltipModule, StatusBadge, PriorityBadge, TypeIcon, Avatar, TicketNumberPipe,
    TicketConversation, TicketTimeline,
  ],
  templateUrl: './ticket-detail.html',
  styleUrl: './ticket-detail.scss',
})
export class TicketDetailPage {
  private readonly api = inject(TicketsApi);
  private readonly adminApi = inject(AdminApi);
  protected readonly auth = inject(AuthStore);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);
  private readonly baseUrl = inject(API_BASE_URL);

  /** Bound from the :ticketId route parameter. */
  readonly ticketId = input.required<string>();

  private readonly conversation = viewChild(TicketConversation);

  protected readonly ticket = httpResource<TicketDetail>(() => this.api.ticketUrl(this.ticketId()));
  protected readonly comments = httpResource<PagedResult<CommentView>>(() => ({
    url: `${this.api.ticketUrl(this.ticketId())}/comments`,
    params: { pageSize: 100 },
  }));
  protected readonly history = httpResource<TicketHistoryEntry[]>(() => `${this.api.ticketUrl(this.ticketId())}/history`);
  protected readonly developers = httpResource<AgentOption[]>(() => (this.auth.isAdmin() ? this.adminApi.agentsUrl : undefined));
  private readonly categories = httpResource<Category[]>(() => `${this.baseUrl}/categories`);
  /** Staff plan tickets into sprints; reporters only see the sprint name. */
  private readonly sprints = httpResource<SprintSummary[]>(() =>
    this.auth.hasRole('Admin', 'Agent') ? `${this.baseUrl}/sprints` : undefined,
  );
  protected readonly openSprints = computed(() =>
    ((this.sprints.hasValue() ? this.sprints.value() : undefined) ?? []).filter((s) => s.state !== 'Completed'),
  );
  protected readonly canPlan = computed(() => {
    const ticket = this.ticket.hasValue() ? this.ticket.value() : undefined;
    return !!ticket && this.auth.hasRole('Admin', 'Agent') && ticket.status !== 'Done' && ticket.status !== 'WontDo';
  });

  protected readonly types = TICKET_TYPES;
  protected readonly typeLabels = TYPE_LABELS;
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly statusCategory = statusCategory;
  protected readonly busy = signal(false);

  protected readonly actions = computed(() => {
    const ticket = (this.ticket.hasValue() ? this.ticket.value() : undefined);
    const user = this.auth.user();
    return ticket && user ? ticketActions(ticket, user) : null;
  });
  protected readonly notFound = computed(() => (this.ticket.error() as ApiError | undefined)?.status === 404);

  /** Active categories, plus the ticket's current one even if it was deactivated. */
  protected readonly categoryOptions = computed(() => {
    const ticket = (this.ticket.hasValue() ? this.ticket.value() : undefined);
    const active = ((this.categories.hasValue() ? this.categories.value() : undefined) ?? []).filter((c) => c.isActive);
    if (ticket && !active.some((c) => c.id === ticket.categoryId)) {
      return [...active, { id: ticket.categoryId, name: ticket.categoryName } as Category];
    }
    return active;
  });

  protected moveTo(status: TicketStatus): void {
    if (status === 'WontDo') {
      this.dialog
        .open<WontDoDialog, undefined, string>(WontDoDialog, { width: '480px' })
        .afterClosed()
        .subscribe((reason) => reason && this.run((t) => this.api.changeStatus(t, status, reason), "Closed as Won't Do."));
      return;
    }
    this.run((t) => this.api.changeStatus(t, status), `Moved to ${STATUS_LABELS[status]}.`);
  }

  protected moveToSprint(sprintId: string | null): void {
    this.run((t) => this.api.moveToSprint(t, sprintId), sprintId ? 'Added to the sprint.' : 'Moved back to the backlog.', () =>
      this.sprints.reload(),
    );
  }

  protected assignTo(assigneeId: string): void {
    this.run((t) => this.api.assign(t, assigneeId), 'Assignee updated.', () => this.developers.reload());
  }

  protected assignToMe(): void {
    const me = this.auth.user();
    if (me) this.run((t) => this.api.assign(t, me.id), 'Assigned to you.');
  }

  protected unassign(): void {
    this.run((t) => this.api.unassign(t), 'Ticket unassigned.', () => this.developers.reload());
  }

  protected updateField(change: UpdateTicketInput): void {
    this.run((t) => this.api.update(t, change), 'Saved.');
  }

  protected openEdit(): void {
    const ticket = (this.ticket.hasValue() ? this.ticket.value() : undefined);
    if (!ticket) return;
    this.dialog
      .open<EditTicketDialog, TicketDetail, UpdateTicketInput>(EditTicketDialog, { width: '640px', data: ticket })
      .afterClosed()
      .subscribe((change) => change && Object.keys(change).length > 0 && this.updateField(change));
  }

  protected postComment(body: string): void {
    this.busy.set(true);
    this.api.addComment(this.ticketId(), body).subscribe({
      next: () => {
        this.busy.set(false);
        this.conversation()?.reset();
        this.comments.reload();
      },
      error: (error: ApiError) => this.handleError(error),
    });
  }

  /** Runs a ticket command with the current version, then refreshes what changed. */
  private run(command: (ticket: TicketDetail) => Observable<TicketDetail>, message: string, after?: () => void): void {
    const ticket = (this.ticket.hasValue() ? this.ticket.value() : undefined);
    if (!ticket || this.busy()) return;
    this.busy.set(true);
    command(ticket).subscribe({
      next: (updated) => {
        this.busy.set(false);
        this.ticket.set(updated);
        this.history.reload();
        after?.();
        this.notifier.success(message);
      },
      error: (error: ApiError) => this.handleError(error),
    });
  }

  private handleError(error: ApiError): void {
    this.busy.set(false);
    if (error.status === 409) {
      this.notifier.error('Someone else changed this ticket. It has been reloaded; please try again.');
      this.ticket.reload();
      this.history.reload();
    } else if (error.status > 0 && error.status < 500 && error.status !== 429) {
      this.notifier.error(error.title);
    }
  }
}
