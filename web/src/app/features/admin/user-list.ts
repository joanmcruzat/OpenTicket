import { httpResource } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { debounceTime, distinctUntilChanged, Observable } from 'rxjs';
import { AdminApi } from '@app/core/api/admin-api';
import { PagedResult, USER_ROLES, UserListItem, UserRole } from '@app/core/api/models';
import { ROLE_LABELS, RoleLabelPipe } from '@app/shared/labels';
import { AuthStore } from '@app/core/auth/auth-store';
import { ApiError } from '@app/core/http/api-error';
import { Notifier } from '@app/core/http/notifier';
import { confirmAction } from '@app/shared/confirm-dialog';
import { CreateUserDialog, ResetPasswordDialog } from './admin-dialogs';

@Component({
  selector: 'app-user-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RoleLabelPipe,
    DatePipe, MatTableModule, MatPaginatorModule, MatFormFieldModule, MatInputModule, MatSelectModule,
    MatButtonModule, MatIconModule, MatMenuModule, MatProgressBarModule,
  ],
  template: `
    <header class="page-header">
      <h1>Users</h1>
      <button mat-flat-button (click)="openCreate()"><mat-icon>person_add</mat-icon> Add user</button>
    </header>
    <section class="filters">
      <mat-form-field subscriptSizing="dynamic" class="search">
        <mat-label>Search name or email</mat-label>
        <mat-icon matPrefix>search</mat-icon>
        <input matInput [value]="search()" (input)="search.set($any($event.target).value); page.set(1)" maxlength="100" />
      </mat-form-field>
      <mat-form-field subscriptSizing="dynamic">
        <mat-label>Role</mat-label>
        <mat-select [value]="role()" (valueChange)="role.set($event); page.set(1)">
          <mat-option [value]="null">All roles</mat-option>
          @for (r of roles; track r) { <mat-option [value]="r">{{ r | roleLabel }}</mat-option> }
        </mat-select>
      </mat-form-field>
    </section>
    @if (users.isLoading()) { <mat-progress-bar mode="indeterminate" /> }
    <div class="table-wrap">
      <table mat-table [dataSource]="(users.hasValue() ? users.value() : undefined)?.items ?? []">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Name</th>
          <td mat-cell *matCellDef="let u">{{ u.displayName }}<div class="sub">{{ u.email }}</div></td>
        </ng-container>
        <ng-container matColumnDef="role">
          <th mat-header-cell *matHeaderCellDef>Role</th>
          <td mat-cell *matCellDef="let u">{{ u.role | roleLabel }}</td>
        </ng-container>
        <ng-container matColumnDef="status">
          <th mat-header-cell *matHeaderCellDef>Status</th>
          <td mat-cell *matCellDef="let u"><span class="badge" [class.inactive]="!u.isActive">{{ u.isActive ? 'Active' : 'Inactive' }}</span></td>
        </ng-container>
        <ng-container matColumnDef="created">
          <th mat-header-cell *matHeaderCellDef class="wide-only">Created</th>
          <td mat-cell *matCellDef="let u" class="wide-only">{{ u.createdAtUtc | date: 'mediumDate' }}</td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef><span class="cdk-visually-hidden">Actions</span></th>
          <td mat-cell *matCellDef="let u">
            @if (u.id !== auth.user()?.id) {
              <button mat-icon-button [matMenuTriggerFor]="menu" [attr.aria-label]="'Actions for ' + u.displayName"><mat-icon>more_vert</mat-icon></button>
              <mat-menu #menu="matMenu">
                @for (r of roles; track r) {
                  @if (r !== u.role) { <button mat-menu-item (click)="changeRole(u, r)">Make {{ r | roleLabel }}</button> }
                }
                <button mat-menu-item (click)="toggleActive(u)">{{ u.isActive ? 'Deactivate' : 'Activate' }}</button>
                <button mat-menu-item (click)="resetPassword(u)">Reset password</button>
              </mat-menu>
            } @else {
              <span class="sub">You</span>
            }
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
        <tr class="mat-row" *matNoDataRow><td class="mat-cell empty" [attr.colspan]="columns.length">No users found.</td></tr>
      </table>
    </div>
    <mat-paginator [length]="(users.hasValue() ? users.value() : undefined)?.totalCount ?? 0" [pageIndex]="page() - 1" [pageSize]="pageSize()"
                   [pageSizeOptions]="[10, 20, 50]" (page)="onPage($event)" aria-label="Select page" />
  `,
  styles: `
    .filters { display: flex; gap: 12px; flex-wrap: wrap; margin-bottom: 16px; }
    .search { flex: 1 1 260px; }
    .table-wrap { overflow-x: auto; }
    table { width: 100%; }
    .sub { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .inactive { background: var(--mat-sys-surface-container-highest); color: var(--mat-sys-on-surface-variant); }
    .empty { padding: 24px; text-align: center; }
    @media (max-width: 699px) { .wide-only { display: none; } }
  `,
})
export class UserList {
  protected readonly auth = inject(AuthStore);
  private readonly api = inject(AdminApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);

  protected readonly roles = USER_ROLES;
  protected readonly columns = ['name', 'role', 'status', 'created', 'actions'];
  protected readonly search = signal('');
  protected readonly role = signal<UserRole | null>(null);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  private readonly debouncedSearch = toSignal(toObservable(this.search).pipe(debounceTime(300), distinctUntilChanged()), {
    initialValue: '',
  });

  private readonly params = computed(() => {
    const params: Record<string, string | number> = { page: this.page(), pageSize: this.pageSize() };
    const role = this.role();
    if (role) params['role'] = role;
    if (this.debouncedSearch().trim()) params['search'] = this.debouncedSearch().trim();
    return params;
  });
  protected readonly users = httpResource<PagedResult<UserListItem>>(() => ({ url: this.api.usersUrl, params: this.params() }));

  protected onPage(event: PageEvent): void {
    this.page.set(event.pageIndex + 1);
    this.pageSize.set(event.pageSize);
  }

  protected openCreate(): void {
    this.dialog
      .open(CreateUserDialog, { width: '480px' })
      .afterClosed()
      .subscribe((user?: UserListItem) => {
        if (user) {
          this.notifier.success(`${user.displayName} added as ${ROLE_LABELS[user.role]}.`);
          this.users.reload();
        }
      });
  }

  protected changeRole(user: UserListItem, role: UserRole): void {
    this.mutate(this.api.changeRole(user.id, role), `${user.displayName} is now ${ROLE_LABELS[role]}.`);
  }

  protected toggleActive(user: UserListItem): void {
    if (!user.isActive) {
      this.mutate(this.api.setUserActive(user.id, true), `${user.displayName} activated.`);
      return;
    }
    confirmAction(this.dialog, {
      title: `Deactivate ${user.displayName}?`,
      message: 'They are signed out everywhere at once and can no longer sign in. You can activate them again later.',
      confirmLabel: 'Deactivate',
    }).subscribe((confirmed) => {
      if (confirmed) this.mutate(this.api.setUserActive(user.id, false), `${user.displayName} deactivated and signed out.`);
    });
  }

  protected resetPassword(user: UserListItem): void {
    this.dialog
      .open(ResetPasswordDialog, { width: '480px', data: user })
      .afterClosed()
      .subscribe((done?: boolean) => done && this.notifier.success(`Password reset for ${user.displayName}.`));
  }

  private mutate(request: Observable<unknown>, message: string): void {
    request.subscribe({
      next: () => {
        this.notifier.success(message);
        this.users.reload();
      },
      error: (error: ApiError) => error.status < 500 && this.notifier.error(error.title),
    });
  }
}
