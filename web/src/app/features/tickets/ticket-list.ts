import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSortModule, Sort } from '@angular/material/sort';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { debounceTime, distinctUntilChanged } from 'rxjs';
import { TicketsApi } from '@app/core/api/tickets-api';
import { API_BASE_URL } from '@app/core/api/api-config';
import {
  Category,
  PagedResult,
  TICKET_PRIORITIES,
  TICKET_STATUSES,
  TICKET_TYPES,
  TicketListFilter,
  TicketStatus,
  TicketSummary,
} from '@app/core/api/models';
import { AuthStore } from '@app/core/auth/auth-store';
import { Avatar, PriorityBadge, StatusBadge, TypeIcon } from '@app/shared/badges';
import { STATUS_LABELS, TicketNumberPipe } from '@app/shared/labels';

const DEFAULT_FILTER: TicketListFilter = {
  page: 1,
  pageSize: 20,
  status: [],
  type: [],
  priority: [],
  categoryId: null,
  unassigned: false,
  search: '',
  sortBy: 'createdAt',
  sortDir: 'desc',
};

@Component({
  selector: 'app-ticket-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    DatePipe,
    MatTableModule,
    MatSortModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
    StatusBadge,
    PriorityBadge,
    TypeIcon,
    Avatar,
    TicketNumberPipe,
  ],
  template: `
    <header class="page-header">
      <h1>All tickets</h1>
      <div class="header-actions">
        <a mat-stroked-button routerLink="/board"><mat-icon>view_kanban</mat-icon> Board</a>
        <a mat-flat-button routerLink="/tickets/new"><mat-icon>add</mat-icon> Create</a>
      </div>
    </header>

    <section class="filters" aria-label="Filters">
      <mat-form-field class="search" subscriptSizing="dynamic">
        <mat-label>Search summary or key (OT-000042)</mat-label>
        <mat-icon matPrefix>search</mat-icon>
        <input matInput [value]="searchText()" (input)="onSearch($any($event.target).value)" maxlength="100" />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Status</mat-label>
        <mat-select multiple [value]="filter().status" (valueChange)="setFilter({ status: $event })">
          @for (s of statuses; track s) { <mat-option [value]="s">{{ statusLabels[s] }}</mat-option> }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Type</mat-label>
        <mat-select multiple [value]="filter().type" (valueChange)="setFilter({ type: $event })">
          <mat-select-trigger>{{ filter().type.join(', ') }}</mat-select-trigger>
          @for (t of types; track t) { <mat-option [value]="t"><app-type-icon [type]="t" [showLabel]="true" /></mat-option> }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Priority</mat-label>
        <mat-select multiple [value]="filter().priority" (valueChange)="setFilter({ priority: $event })">
          <mat-select-trigger>{{ filter().priority.join(', ') }}</mat-select-trigger>
          @for (p of priorities; track p) { <mat-option [value]="p"><app-priority-badge [priority]="p" /></mat-option> }
        </mat-select>
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Category</mat-label>
        <mat-select [value]="filter().categoryId" (valueChange)="setFilter({ categoryId: $event })">
          <mat-option [value]="null">All categories</mat-option>
          @for (c of (categories.hasValue() ? categories.value() : undefined) ?? []; track c.id) { <mat-option [value]="c.id">{{ c.name }}</mat-option> }
        </mat-select>
      </mat-form-field>
      @if (!auth.hasRole('Customer')) {
        <mat-slide-toggle [checked]="filter().unassigned" (change)="setFilter({ unassigned: $event.checked })">
          Unassigned only
        </mat-slide-toggle>
      }
      @if (hasFilters()) {
        <button mat-button (click)="clearFilters()"><mat-icon>filter_alt_off</mat-icon> Clear</button>
      }
    </section>

    @if (tickets.isLoading()) { <mat-progress-bar mode="indeterminate" /> }
    @if (tickets.error()) {
      <p class="error" role="alert">Tickets could not be loaded. <button mat-button (click)="tickets.reload()">Retry</button></p>
    }

    <div class="table-wrap">
      <table mat-table [dataSource]="rows()" matSort [matSortActive]="filter().sortBy" [matSortDirection]="filter().sortDir"
             matSortDisableClear (matSortChange)="onSort($event)">
        <ng-container matColumnDef="type">
          <th mat-header-cell *matHeaderCellDef class="narrow">Type</th>
          <td mat-cell *matCellDef="let t" class="narrow"><app-type-icon [type]="t.type" /></td>
        </ng-container>
        <ng-container matColumnDef="number">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Key</th>
          <td mat-cell *matCellDef="let t" class="nowrap"><a [routerLink]="['/tickets', t.id]">{{ t.number | ticketNumber }}</a></td>
        </ng-container>
        <ng-container matColumnDef="title">
          <th mat-header-cell *matHeaderCellDef>Summary</th>
          <td mat-cell *matCellDef="let t"><a class="title-link" [routerLink]="['/tickets', t.id]">{{ t.title }}</a></td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Status</th>
          <td mat-cell *matCellDef="let t"><app-status-badge [status]="t.status" /></td>
        </ng-container>
        <ng-container matColumnDef="priority">
          <th mat-header-cell *matHeaderCellDef mat-sort-header>Priority</th>
          <td mat-cell *matCellDef="let t"><app-priority-badge [priority]="t.priority" /></td>
        </ng-container>
        <ng-container matColumnDef="assignee">
          <th mat-header-cell *matHeaderCellDef class="wide-only">Assignee</th>
          <td mat-cell *matCellDef="let t" class="wide-only">
            <span class="person"><app-avatar [name]="t.assignedToName" /> {{ t.assignedToName ?? 'Unassigned' }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="reporter">
          <th mat-header-cell *matHeaderCellDef class="wide-only">Reporter</th>
          <td mat-cell *matCellDef="let t" class="wide-only">
            <span class="person"><app-avatar [name]="t.createdByName" /> {{ t.createdByName }}</span>
          </td>
        </ng-container>
        <ng-container matColumnDef="category">
          <th mat-header-cell *matHeaderCellDef class="wide-only">Category</th>
          <td mat-cell *matCellDef="let t" class="wide-only">{{ t.categoryName }}</td>
        </ng-container>
        <ng-container matColumnDef="updatedAt">
          <th mat-header-cell *matHeaderCellDef mat-sort-header class="wide-only">Updated</th>
          <td mat-cell *matCellDef="let t" class="wide-only nowrap">{{ t.updatedAtUtc | date: 'MMM d, y' }}</td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
        <tr class="mat-row" *matNoDataRow>
          <td class="mat-cell empty" [attr.colspan]="columns.length">
            {{ tickets.isLoading() ? 'Loading…' : 'No tickets match these filters.' }}
          </td>
        </tr>
      </table>
    </div>
    <mat-paginator [length]="(tickets.hasValue() ? tickets.value() : undefined)?.totalCount ?? 0" [pageIndex]="filter().page - 1" [pageSize]="filter().pageSize"
                   [pageSizeOptions]="[10, 20, 50, 100]" (page)="onPage($event)" aria-label="Select page" />
  `,
  styles: `
    .filters { display: flex; flex-wrap: wrap; gap: 12px; align-items: center; margin-bottom: 16px; }
    .filters mat-form-field { width: 180px; }
    .filters .search { flex: 1 1 260px; }
    .table-wrap { overflow-x: auto; }
    table { width: 100%; }
    .nowrap { white-space: nowrap; }
    .narrow { width: 40px; padding-right: 0; }
    .person { display: inline-flex; align-items: center; gap: 8px; white-space: nowrap; }
    .header-actions { display: flex; gap: 8px; }
    tr.mat-mdc-row:hover { background: var(--mat-sys-surface-container-low); }
    .title-link { color: inherit; text-decoration: none; font-weight: 500; }
    .title-link:hover { text-decoration: underline; }
    .empty { padding: 24px; text-align: center; color: var(--mat-sys-on-surface-variant); }
    @media (max-width: 799px) { .wide-only { display: none; } .filters mat-form-field { width: 100%; } }
  `,
})
export class TicketList {
  protected readonly auth = inject(AuthStore);
  private readonly api = inject(TicketsApi);

  /** Optional ?status= and ?unassigned=true from dashboard and board links. */
  readonly status = input<TicketStatus>();
  readonly unassigned = input<string>();

  protected readonly statuses = TICKET_STATUSES;
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly types = TICKET_TYPES;
  protected readonly statusLabels = STATUS_LABELS;
  protected readonly columns = ['type', 'number', 'title', 'status', 'priority', 'assignee', 'reporter', 'category', 'updatedAt'];

  protected readonly filter = linkedSignal<TicketListFilter>(() => {
    const status = this.status();
    return {
      ...DEFAULT_FILTER,
      status: status && TICKET_STATUSES.includes(status) ? [status] : [],
      unassigned: this.unassigned() === 'true',
    };
  });
  protected readonly searchText = signal('');
  private readonly debouncedSearch = toSignal(
    toObservable(this.searchText).pipe(debounceTime(300), distinctUntilChanged()),
    { initialValue: '' },
  );

  protected readonly tickets = httpResource<PagedResult<TicketSummary>>(() => ({
    url: this.api.ticketsUrl,
    params: this.api.listParams({ ...this.filter(), search: this.debouncedSearch() }),
  }));
  private readonly baseUrl = inject(API_BASE_URL);
  protected readonly categories = httpResource<Category[]>(() => `${this.baseUrl}/categories`);

  protected readonly rows = computed(() => (this.tickets.hasValue() ? this.tickets.value() : undefined)?.items ?? []);
  protected readonly hasFilters = computed(() => {
    const f = this.filter();
    const hasLists = f.status.length > 0 || f.type.length > 0 || f.priority.length > 0;
    return hasLists || f.categoryId !== null || f.unassigned || this.searchText() !== '';
  });

  protected setFilter(change: Partial<Pick<TicketListFilter, 'status' | 'type' | 'priority' | 'categoryId' | 'unassigned'>>): void {
    this.filter.update((f) => ({ ...f, ...change, page: 1 }));
  }

  protected onSearch(value: string): void {
    this.searchText.set(value);
    this.filter.update((f) => ({ ...f, page: 1 }));
  }

  protected onSort(sort: Sort): void {
    const sortBy = sort.active as TicketListFilter['sortBy'];
    this.filter.update((f) => ({ ...f, sortBy, sortDir: sort.direction === 'asc' ? 'asc' : 'desc', page: 1 }));
  }

  protected onPage(event: PageEvent): void {
    this.filter.update((f) => ({ ...f, page: event.pageIndex + 1, pageSize: event.pageSize }));
  }

  protected clearFilters(): void {
    this.searchText.set('');
    this.filter.set(DEFAULT_FILTER);
  }
}
