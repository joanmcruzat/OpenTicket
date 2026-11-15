import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';
import { SprintPlan, SprintSummary, TicketSummary } from '@app/core/api/models';
import { SprintInput, SprintsApi } from '@app/core/api/sprints-api';
import { TicketsApi } from '@app/core/api/tickets-api';
import { ApiError } from '@app/core/http/api-error';
import { Notifier } from '@app/core/http/notifier';
import { Avatar, PriorityBadge, StatusBadge, TypeIcon } from '@app/shared/badges';
import { TicketNumberPipe, formatTicketNumber } from '@app/shared/labels';
import { daysLeftLabel, sprintRange } from '@app/shared/sprint-dates';
import { confirmAction } from '@app/shared/confirm-dialog';
import { CompleteSprintData, CompleteSprintDialog, SprintFormDialog, StartSprintDialog } from '@app/shared/sprint-dialogs';

interface Section {
  /** Drop-list id: the sprint id, or 'backlog'. */
  key: string;
  sprint: SprintSummary | null;
  tickets: TicketSummary[];
  total: number;
}

/** Sprint planning: the active sprint, planned sprints and the unplanned backlog. Drag tickets between them. */
@Component({
  selector: 'app-backlog-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CdkDropListGroup, CdkDropList, CdkDrag, RouterLink, MatButtonModule, MatIconModule, MatMenuModule, MatProgressBarModule,
    MatTooltipModule, StatusBadge, PriorityBadge, TypeIcon, Avatar, TicketNumberPipe,
  ],
  template: `
    <header class="page-header">
      <div>
        <h1>Backlog</h1>
        <p class="subtitle">Plan sprints: drag tickets between the backlog and a sprint, then start the sprint to put it on the board.</p>
      </div>
      <button mat-flat-button (click)="createSprint()" [disabled]="busy()"><mat-icon>add</mat-icon> Create sprint</button>
    </header>

    @if (plan.isLoading() || busy()) { <mat-progress-bar mode="indeterminate" /> }
    @if (plan.error()) {
      <p class="error" role="alert">The backlog could not be loaded. <button mat-button (click)="plan.reload()">Retry</button></p>
    }

    <div cdkDropListGroup class="sections">
      @for (section of sections(); track section.key) {
        <section class="section" [class.active]="section.sprint?.state === 'Active'" [attr.aria-label]="sectionTitle(section)">
          <header class="section-head">
            <div class="title">
              <h2>{{ sectionTitle(section) }}</h2>
              @if (section.sprint; as sprint) {
                <span class="state" [class.state-active]="sprint.state === 'Active'">{{ sprint.state }}</span>
                @if (range(sprint); as r) { <span class="meta">{{ r }}</span> }
                @if (sprint.state === 'Active' && remaining(sprint); as left) { <span class="meta strong">{{ left }}</span> }
              }
              <span class="meta">{{ section.total }} {{ section.total === 1 ? 'ticket' : 'tickets' }}
                @if (section.sprint) { · {{ section.sprint.doneCount }} done }</span>
            </div>
            @if (section.sprint; as sprint) {
              <div class="actions">
                @if (sprint.state === 'Planned') {
                  <button mat-stroked-button (click)="startSprint(sprint)" [disabled]="busy() || hasActive()"
                          [matTooltip]="hasActive() ? 'Complete the active sprint first' : ''">Start sprint</button>
                } @else {
                  <button mat-flat-button (click)="completeSprint(sprint)" [disabled]="busy()">Complete sprint</button>
                }
                <button mat-icon-button [matMenuTriggerFor]="sprintMenu" [attr.aria-label]="'More actions for ' + sprint.name">
                  <mat-icon>more_horiz</mat-icon>
                </button>
                <mat-menu #sprintMenu="matMenu">
                  <button mat-menu-item (click)="editSprint(sprint)"><mat-icon>edit</mat-icon><span>Edit sprint</span></button>
                  @if (sprint.state === 'Planned') {
                    <button mat-menu-item (click)="deleteSprint(sprint)"><mat-icon>delete</mat-icon><span>Delete sprint</span></button>
                  }
                </mat-menu>
              </div>
            }
          </header>
          @if (section.sprint?.goal; as goal) { <p class="goal"><mat-icon>flag</mat-icon> {{ goal }}</p> }

          <ul class="rows" cdkDropList [id]="section.key" [cdkDropListData]="section.key"
              (cdkDropListDropped)="onDrop($event)">
            @for (ticket of section.tickets; track ticket.id) {
              <li class="row" cdkDrag [cdkDragData]="ticket" [cdkDragDisabled]="busy()" [cdkDragStartDelay]="{ touch: 250, mouse: 0 }">
                <mat-icon class="handle" aria-hidden="true">drag_indicator</mat-icon>
                <app-type-icon [type]="ticket.type" />
                <a class="key" [routerLink]="['/tickets', ticket.id]">{{ ticket.number | ticketNumber }}</a>
                <a class="summary" [routerLink]="['/tickets', ticket.id]">{{ ticket.title }}</a>
                <app-status-badge [status]="ticket.status" />
                <app-priority-badge [priority]="ticket.priority" [showLabel]="false" />
                <app-avatar [name]="ticket.assignedToName" />
                <button mat-icon-button class="move" [matMenuTriggerFor]="moveMenu" [disabled]="busy()"
                        [attr.aria-label]="'Move ' + (ticket.number | ticketNumber) + ' to another sprint'">
                  <mat-icon>low_priority</mat-icon>
                </button>
                <mat-menu #moveMenu="matMenu">
                  @for (target of targets(); track target.key) {
                    @if (target.key !== section.key) {
                      <button mat-menu-item (click)="move(ticket, target.key)">Move to {{ target.label }}</button>
                    }
                  }
                </mat-menu>
              </li>
            } @empty {
              <li class="empty">{{ section.sprint ? 'Drag tickets here to plan them into this sprint.' : 'The backlog is empty.' }}</li>
            }
          </ul>
          @if (section.total > section.tickets.length) {
            <p class="more">Showing {{ section.tickets.length }} of {{ section.total }}.</p>
          }
        </section>
      }
    </div>
  `,
  styles: `
    .sections { display: grid; gap: 16px; }
    .section { border: 1px solid var(--mat-sys-outline-variant); border-radius: 10px; padding: 12px 16px; }
    .section.active { border-color: #b3d4ff; background: #f4f8ff; }
    .section-head { display: flex; align-items: center; gap: 12px; flex-wrap: wrap; }
    .title { flex: 1; display: flex; align-items: center; gap: 4px 12px; flex-wrap: wrap; min-width: 0; }
    h2 { font: var(--mat-sys-title-medium); margin: 0; }
    .state { font: 700 11px/18px Roboto, sans-serif; text-transform: uppercase; padding: 0 6px; border-radius: 3px; background: #dfe1e6; color: #42526e; }
    .state-active { background: #deebff; color: #0747a6; }
    .meta { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .meta.strong { color: var(--mat-sys-on-surface); font-weight: 500; }
    .actions { display: flex; align-items: center; gap: 4px; }
    .goal { display: flex; align-items: center; gap: 6px; margin: 8px 0 0; color: var(--mat-sys-on-surface-variant); }
    .goal mat-icon { font-size: 18px; width: 18px; height: 18px; }
    .rows { list-style: none; margin: 10px 0 0; padding: 0; display: grid; gap: 4px; min-height: 44px; }
    .row {
      display: flex; align-items: center; gap: 10px; padding: 4px 4px 4px 6px; border-radius: 6px;
      background: var(--mat-sys-surface); border: 1px solid var(--mat-sys-outline-variant); cursor: grab;
    }
    .row:hover { box-shadow: 0 1px 4px rgba(9, 30, 66, 0.15); }
    .handle { color: var(--mat-sys-outline); font-size: 18px; width: 18px; height: 18px; flex: none; }
    .key { font-weight: 500; white-space: nowrap; text-decoration: none; }
    .summary { flex: 1; min-width: 0; color: inherit; text-decoration: none; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    .summary:hover, .key:hover { text-decoration: underline; }
    .empty { padding: 12px; text-align: center; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small);
      border: 1px dashed var(--mat-sys-outline-variant); border-radius: 6px; }
    .more { margin: 8px 0 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .cdk-drop-list-dragging .row:not(.cdk-drag-placeholder) { transition: transform 200ms ease; }
    .cdk-drag-placeholder { opacity: 0.3; }
    .cdk-drag-preview { box-shadow: 0 8px 20px rgba(9, 30, 66, 0.3); }
    @media (max-width: 599px) { app-status-badge, app-priority-badge { display: none; } }
  `,
})
export class BacklogPage {
  private readonly sprintsApi = inject(SprintsApi);
  private readonly ticketsApi = inject(TicketsApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);

  protected readonly plan = httpResource<SprintPlan>(() => this.sprintsApi.planUrl);
  protected readonly busy = signal(false);

  protected readonly sections = computed<Section[]>(() => {
    const plan = this.plan.hasValue() ? this.plan.value() : undefined;
    if (!plan) return [];
    return [
      ...plan.sprints.map((s) => ({ key: s.sprint.id, sprint: s.sprint, tickets: s.tickets, total: s.sprint.ticketCount })),
      { key: 'backlog', sprint: null, tickets: plan.backlog.tickets, total: plan.backlog.total },
    ];
  });
  protected readonly hasActive = computed(() => this.sections().some((s) => s.sprint?.state === 'Active'));
  protected readonly targets = computed(() =>
    this.sections().map((s) => ({ key: s.key, label: s.sprint ? s.sprint.name : 'the backlog' })),
  );

  protected readonly range = sprintRange;
  protected readonly remaining = daysLeftLabel;

  protected sectionTitle(section: Section): string {
    return section.sprint ? section.sprint.name : 'Backlog';
  }

  protected onDrop(event: CdkDragDrop<string, string, TicketSummary>): void {
    if (event.previousContainer !== event.container) this.move(event.item.data, event.container.data);
  }

  protected move(ticket: TicketSummary, targetKey: string): void {
    const target = targetKey === 'backlog' ? null : targetKey;
    const label = this.targets().find((t) => t.key === targetKey)?.label ?? 'the backlog';
    this.run(this.ticketsApi.moveToSprint(ticket, target), `${formatTicketNumber(ticket.number)} moved to ${label}.`);
  }

  protected createSprint(): void {
    this.dialog
      .open<SprintFormDialog, SprintSummary | null, SprintInput>(SprintFormDialog, { width: '520px', data: null })
      .afterClosed()
      .subscribe((input) => input && this.run(this.sprintsApi.create(input), `${input.name} created.`));
  }

  protected editSprint(sprint: SprintSummary): void {
    this.dialog
      .open<SprintFormDialog, SprintSummary, SprintInput>(SprintFormDialog, { width: '520px', data: sprint })
      .afterClosed()
      .subscribe((input) => input && this.run(this.sprintsApi.update(sprint.id, input), 'Sprint saved.'));
  }

  protected startSprint(sprint: SprintSummary): void {
    this.dialog
      .open<StartSprintDialog, SprintSummary, { startDate: string; endDate: string }>(StartSprintDialog, { width: '440px', data: sprint })
      .afterClosed()
      .subscribe((dates) =>
        dates && this.run(this.sprintsApi.start(sprint.id, dates.startDate, dates.endDate), `${sprint.name} started. It is on the board now.`),
      );
  }

  protected completeSprint(sprint: SprintSummary): void {
    const planned = this.sections().flatMap((s) => (s.sprint?.state === 'Planned' ? [s.sprint] : []));
    this.dialog
      .open<CompleteSprintDialog, CompleteSprintData, { target: string | null }>(CompleteSprintDialog, {
        width: '440px',
        data: { sprint, planned },
      })
      .afterClosed()
      .subscribe((choice) => choice && this.run(this.sprintsApi.complete(sprint.id, choice.target), `${sprint.name} completed.`));
  }

  protected deleteSprint(sprint: SprintSummary): void {
    confirmAction(this.dialog, {
      title: `Delete ${sprint.name}?`,
      message: `This can't be undone. Its ${sprint.ticketCount} ${sprint.ticketCount === 1 ? 'ticket goes' : 'tickets go'} back to the backlog.`,
      confirmLabel: 'Delete sprint',
    }).subscribe((confirmed) => confirmed && this.run(this.sprintsApi.delete(sprint.id), `${sprint.name} deleted.`));
  }

  private run(request: Observable<unknown>, message: string): void {
    this.busy.set(true);
    request.subscribe({
      next: () => {
        this.busy.set(false);
        this.notifier.success(message);
        this.plan.reload();
      },
      error: (error: ApiError) => {
        this.busy.set(false);
        this.notifier.error(error.status === 409 ? `${error.title} The backlog has been refreshed.` : error.title);
        this.plan.reload();
      },
    });
  }
}
