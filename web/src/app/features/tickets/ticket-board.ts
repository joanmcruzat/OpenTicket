import { CdkDrag, CdkDragDrop, CdkDragStart, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, linkedSignal, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { Router, RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { API_BASE_URL } from '@app/core/api/api-config';
import { BoardColumn, BoardFilter, Category, SprintSummary, TICKET_TYPES, BoardView, TicketStatus, TicketSummary } from '@app/core/api/models';
import { SprintsApi } from '@app/core/api/sprints-api';
import { MatDialog } from '@angular/material/dialog';
import { CompleteSprintData, CompleteSprintDialog } from '@app/shared/sprint-dialogs';
import { daysLeftLabel, sprintRange } from '@app/shared/sprint-dates';
import { TicketsApi } from '@app/core/api/tickets-api';
import { AuthStore } from '@app/core/auth/auth-store';
import { ApiError } from '@app/core/http/api-error';
import { Notifier } from '@app/core/http/notifier';
import { Avatar, PriorityBadge, TypeIcon } from '@app/shared/badges';
import { STATUS_LABELS, TicketNumberPipe, formatTicketNumber } from '@app/shared/labels';

type Scope = 'all' | 'mine' | 'unassigned';

@Component({
  selector: 'app-ticket-board',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    CdkDropListGroup, CdkDropList, CdkDrag, RouterLink, MatButtonModule, MatChipsModule, MatFormFieldModule, MatIconModule,
    MatInputModule, MatProgressBarModule, MatSelectModule, Avatar, PriorityBadge, TypeIcon, TicketNumberPipe,
  ],
  template: `
    <header class="page-header">
      <div>
        <h1>{{ sprint()?.name ?? 'Board' }}</h1>
        <p class="subtitle">Drag a card to another column to change its status.</p>
      </div>
      <a mat-flat-button routerLink="/tickets/new"><mat-icon>add</mat-icon> Create</a>
    </header>

    <section class="toolbar" aria-label="Board filters">
      <mat-form-field class="search" subscriptSizing="dynamic">
        <mat-icon matPrefix>search</mat-icon>
        <mat-label>Search board</mat-label>
        <input matInput [value]="searchText()" (input)="searchText.set($any($event.target).value)" maxlength="100" />
      </mat-form-field>
      @if (!auth.hasRole('Customer')) {
        <mat-chip-listbox [value]="scope()" (change)="scope.set($event.value ?? 'all')" aria-label="Whose tickets">
          <mat-chip-option value="all">Everyone</mat-chip-option>
          @if (auth.hasRole('Agent')) { <mat-chip-option value="mine">Only mine</mat-chip-option> }
          <mat-chip-option value="unassigned">Unassigned</mat-chip-option>
        </mat-chip-listbox>
      }
      <mat-chip-listbox multiple [value]="types()" (change)="types.set($event.value)" aria-label="Issue types">
        @for (type of allTypes; track type) {
          <mat-chip-option [value]="type"><app-type-icon [type]="type" [showLabel]="true" /></mat-chip-option>
        }
      </mat-chip-listbox>
      <mat-form-field subscriptSizing="dynamic" class="category">
        <mat-label>Category</mat-label>
        <mat-select [value]="categoryId()" (valueChange)="categoryId.set($event)">
          <mat-option [value]="null">All categories</mat-option>
          @for (c of (categories.hasValue() ? categories.value() : undefined) ?? []; track c.id) { <mat-option [value]="c.id">{{ c.name }}</mat-option> }
        </mat-select>
      </mat-form-field>
    </section>

    @if (sprint(); as s) {
      <section class="sprint-bar" aria-label="Active sprint">
        <div class="sprint-info">
          @if (range(s); as r) { <span><mat-icon>date_range</mat-icon> {{ r }}</span> }
          @if (remaining(s); as left) { <span class="strong">{{ left }}</span> }
          <span>{{ s.doneCount }} of {{ s.ticketCount }} done</span>
          @if (s.goal) { <span class="goal"><mat-icon>flag</mat-icon> {{ s.goal }}</span> }
        </div>
        <div class="sprint-progress" role="progressbar" [attr.aria-valuenow]="progress()" aria-valuemin="0" aria-valuemax="100"
             [attr.aria-label]="'Sprint progress ' + progress() + '%'">
          <span [style.width.%]="progress()"></span>
        </div>
        @if (!auth.hasRole('Customer')) {
          <div class="sprint-actions">
            <a mat-button routerLink="/backlog"><mat-icon>list_alt</mat-icon> Backlog</a>
            <button mat-stroked-button (click)="completeSprint(s)" [disabled]="saving()">Complete sprint</button>
          </div>
        }
      </section>
    } @else if (board.hasValue()) {
      <p class="no-sprint" role="status">
        <mat-icon>info</mat-icon>
        <span>No sprint is active, so the board shows every ticket.
          @if (!auth.hasRole('Customer')) { <a routerLink="/backlog">Plan and start a sprint</a> to focus the board. }</span>
      </p>
    }

    @if (board.isLoading()) { <mat-progress-bar mode="indeterminate" /> }
    @if (board.error()) {
      <p class="error" role="alert">The board could not be loaded. <button mat-button (click)="board.reload()">Retry</button></p>
    }

    <div class="board" cdkDropListGroup>
      @for (column of visibleColumns(); track column.status) {
        <section class="column" [class.drop-allowed]="canDrop(column.status)" [class.drop-blocked]="dragging() && !canDrop(column.status)">
          <h2 class="column-title">
            {{ statusLabels[column.status] }} <span class="count">{{ column.total }}</span>
          </h2>
          <div class="cards" cdkDropList [cdkDropListData]="column.status" [cdkDropListEnterPredicate]="enterPredicate"
               (cdkDropListDropped)="onDrop($event)" [attr.aria-label]="statusLabels[column.status] + ' column'">
            @for (card of column.items; track card.id) {
              <article class="card" cdkDrag [cdkDragData]="card" [cdkDragDisabled]="card.allowedStatuses.length === 0 || saving()"
                       [cdkDragStartDelay]="{ touch: 250, mouse: 0 }"
                       (cdkDragStarted)="onDragStart($event)" (cdkDragEnded)="dragging.set(null)"
                       (click)="open(card)" (keydown.enter)="open(card)" tabindex="0"
                       [attr.aria-label]="(card.number | ticketNumber) + ' ' + card.title">
                <p class="card-title">{{ card.title }}</p>
                <div class="card-meta">
                  <app-type-icon [type]="card.type" />
                  <span class="issue-key" [class.done]="column.status === 'Done'">{{ card.number | ticketNumber }}</span>
                  <span class="spacer"></span>
                  <app-priority-badge [priority]="card.priority" [showLabel]="false" />
                  <app-avatar [name]="card.assignedToName" />
                </div>
              </article>
            } @empty {
              <p class="empty">{{ dragging() ? 'Drop here' : 'No tickets' }}</p>
            }
          </div>
          @if (column.total > column.items.length) {
            <a class="more" routerLink="/tickets" [queryParams]="{ status: column.status }">
              Showing {{ column.items.length }} of {{ column.total }} · see all
            </a>
          }
        </section>
      }
    </div>
  `,
  styles: `
    .sprint-bar {
      display: flex; flex-wrap: wrap; align-items: center; gap: 8px 16px; margin: 0 0 16px; padding: 10px 14px;
      border: 1px solid #b3d4ff; background: #f4f8ff; border-radius: 8px;
    }
    .sprint-info { flex: 1 1 320px; display: flex; flex-wrap: wrap; gap: 4px 16px; align-items: center; color: var(--mat-sys-on-surface-variant); }
    .sprint-info span { display: inline-flex; align-items: center; gap: 4px; }
    .sprint-info mat-icon { font-size: 18px; width: 18px; height: 18px; }
    .sprint-info .strong { color: var(--mat-sys-on-surface); font-weight: 500; }
    .sprint-progress { flex: 0 1 160px; height: 8px; border-radius: 4px; background: #dfe1e6; overflow: hidden; }
    .sprint-progress span { display: block; height: 100%; background: #0052cc; border-radius: 4px 0 0 4px; }
    .sprint-actions { display: flex; gap: 4px; align-items: center; }
    .no-sprint { display: flex; gap: 8px; align-items: center; margin: 0 0 16px; color: var(--mat-sys-on-surface-variant); }
    .toolbar { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; margin-bottom: 16px; }
    .search { flex: 0 1 260px; }
    .category { width: 180px; }
    .board {
      display: grid; grid-auto-flow: column; grid-auto-columns: minmax(220px, 1fr); gap: 12px;
      overflow-x: auto; padding-bottom: 8px; align-items: start;
    }
    .column {
      background: var(--mat-sys-surface-container-low); border-radius: 8px; padding: 8px;
      display: flex; flex-direction: column; min-height: 240px; transition: background 120ms, outline-color 120ms;
      outline: 2px dashed transparent; outline-offset: -2px;
    }
    .column.drop-allowed { outline-color: var(--mat-sys-primary); background: var(--mat-sys-primary-container); }
    .column.drop-blocked { opacity: 0.55; }
    .column-title {
      font: 600 12px/16px Roboto, sans-serif; text-transform: uppercase; letter-spacing: 0.04em;
      color: var(--mat-sys-on-surface-variant); margin: 4px 4px 8px;
    }
    .count { margin-left: 4px; font-weight: 400; }
    .cards { display: flex; flex-direction: column; gap: 6px; flex: 1; min-height: 60px; }
    .card {
      background: var(--mat-sys-surface); border-radius: 4px; padding: 10px 10px 8px; cursor: pointer;
      box-shadow: 0 1px 1px rgba(9, 30, 66, 0.25), 0 0 1px rgba(9, 30, 66, 0.31);
    }
    .card:hover { background: var(--mat-sys-surface-container-lowest); box-shadow: 0 2px 6px rgba(9, 30, 66, 0.25); }
    .card:focus-visible { outline: 2px solid var(--mat-sys-primary); }
    .card-title {
      margin: 0 0 10px; overflow-wrap: anywhere; display: -webkit-box; -webkit-line-clamp: 3; -webkit-box-orient: vertical; overflow: hidden;
    }
    .card-meta { display: flex; align-items: center; gap: 6px; }
    .spacer { flex: 1; }
    .done { text-decoration: line-through; }
    .empty { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); text-align: center; margin: 16px 0; }
    .more { font: var(--mat-sys-body-small); margin: 8px 4px 0; }
    .cdk-drag-preview { box-shadow: 0 8px 20px rgba(9, 30, 66, 0.3); transform: rotate(2deg); }
    .cdk-drag-placeholder { opacity: 0.3; }
    .cdk-drag-animating, .cards.cdk-drop-list-dragging .card:not(.cdk-drag-placeholder) { transition: transform 200ms ease; }
    @media (max-width: 599px) { .search, .category { flex: 1 1 100%; width: 100%; } .board { grid-auto-columns: 82vw; } }
  `,
})
export class TicketBoard {
  protected readonly auth = inject(AuthStore);
  private readonly api = inject(TicketsApi);
  private readonly router = inject(Router);
  private readonly notifier = inject(Notifier);
  private readonly baseUrl = inject(API_BASE_URL);

  protected readonly allTypes = TICKET_TYPES;
  protected readonly statusLabels = STATUS_LABELS;

  protected readonly searchText = signal('');
  protected readonly scope = signal<Scope>('all');
  protected readonly types = signal<BoardFilter['type']>([]);
  protected readonly categoryId = signal<string | null>(null);
  private readonly search = toSignal(toObservable(this.searchText).pipe(debounceTime(300), distinctUntilChanged()), {
    initialValue: '',
  });

  private readonly filter = computed<BoardFilter>(() => ({
    type: this.types(),
    categoryId: this.categoryId(),
    assignedToId: this.scope() === 'mine' ? (this.auth.user()?.id ?? null) : null,
    unassigned: this.scope() === 'unassigned',
    search: this.search(),
  }));

  protected readonly board = httpResource<BoardView>(() => ({
    url: this.api.boardUrl,
    params: this.api.boardParams(this.filter()),
  }));
  protected readonly categories = httpResource<Category[]>(() => `${this.baseUrl}/categories`);

  /** Local copy so a dropped card moves immediately; replaced by the server's view on every reload. */
  protected readonly columns = linkedSignal<BoardColumn[]>(() => (this.board.hasValue() ? this.board.value().columns : []));
  protected readonly sprint = computed<SprintSummary | null>(() => (this.board.hasValue() ? this.board.value().sprint : null));

  /** In a sprint, an empty Backlog column is just noise; it reappears when a sprint ticket is in Backlog. */
  protected readonly visibleColumns = computed(() =>
    this.columns().filter((c) => !(this.sprint() && c.status === 'Backlog' && c.total === 0)),
  );
  protected readonly progress = computed(() => {
    const s = this.sprint();
    return s && s.ticketCount > 0 ? Math.round((s.doneCount / s.ticketCount) * 100) : 0;
  });
  protected readonly range = sprintRange;
  protected readonly remaining = daysLeftLabel;

  private readonly sprintsApi = inject(SprintsApi);
  private readonly dialog = inject(MatDialog);

  protected completeSprint(sprint: SprintSummary): void {
    this.sprintsApi.list().subscribe((all) => {
      const planned = all.filter((s) => s.state === 'Planned');
      this.dialog
        .open<CompleteSprintDialog, CompleteSprintData, { target: string | null }>(CompleteSprintDialog, {
          width: '440px',
          data: { sprint, planned },
        })
        .afterClosed()
        .subscribe((choice) => {
          if (!choice) return;
          this.saving.set(true);
          this.sprintsApi.complete(sprint.id, choice.target).subscribe({
            next: () => {
              this.saving.set(false);
              this.notifier.success(`${sprint.name} completed.`);
              this.board.reload();
            },
            error: (error: ApiError) => {
              this.saving.set(false);
              this.notifier.error(error.title);
              this.board.reload();
            },
          });
        });
    });
  }
  protected readonly dragging = signal<TicketSummary | null>(null);
  protected readonly saving = signal(false);

  protected readonly enterPredicate = (drag: CdkDrag<TicketSummary>, drop: CdkDropList<TicketStatus>): boolean =>
    drag.data.status === drop.data || drag.data.allowedStatuses.includes(drop.data);

  protected canDrop(status: TicketStatus): boolean {
    const ticket = this.dragging();
    return ticket !== null && ticket.status !== status && ticket.allowedStatuses.includes(status);
  }

  protected onDragStart(event: CdkDragStart<TicketSummary>): void {
    this.dragging.set(event.source.data);
  }

  protected onDrop(event: CdkDragDrop<TicketStatus, TicketStatus, TicketSummary>): void {
    const ticket = event.item.data;
    const target = event.container.data;
    if (event.previousContainer === event.container || !ticket.allowedStatuses.includes(target)) return;

    this.moveCardLocally(ticket, target, event.currentIndex);
    this.saving.set(true);
    this.api.changeStatus(ticket, target).subscribe({
      next: () => {
        this.saving.set(false);
        this.notifier.success(`${formatTicketNumber(ticket.number)} moved to ${STATUS_LABELS[target]}.`);
        this.board.reload();
      },
      error: (error: ApiError) => {
        this.saving.set(false);
        this.notifier.error(error.status === 409 ? 'Someone else changed this ticket. The board has been refreshed.' : error.title);
        this.board.reload();
      },
    });
  }

  protected open(ticket: TicketSummary): void {
    if (this.dragging() === null) void this.router.navigate(['/tickets', ticket.id]);
  }

  private moveCardLocally(ticket: TicketSummary, target: TicketStatus, index: number): void {
    this.columns.update((columns) =>
      columns.map((column) => {
        if (column.status === ticket.status) {
          return { ...column, total: column.total - 1, items: column.items.filter((t) => t.id !== ticket.id) };
        }
        if (column.status === target) {
          const items = [...column.items];
          items.splice(index, 0, { ...ticket, status: target, allowedStatuses: [] });
          return { ...column, total: column.total + 1, items };
        }
        return column;
      }),
    );
  }
}
