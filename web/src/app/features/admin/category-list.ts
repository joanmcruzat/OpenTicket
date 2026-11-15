import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { AdminApi } from '@app/core/api/admin-api';
import { Category } from '@app/core/api/models';
import { ApiError } from '@app/core/http/api-error';
import { Notifier } from '@app/core/http/notifier';
import { CategoryDialog } from './admin-dialogs';

@Component({
  selector: 'app-category-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatTableModule, MatButtonModule, MatIconModule, MatSlideToggleModule, MatProgressBarModule],
  template: `
    <header class="page-header">
      <h1>Categories</h1>
      <button mat-flat-button (click)="open(null)"><mat-icon>add</mat-icon> New category</button>
    </header>
    <p class="subtitle">Inactive categories can't be chosen for new tickets but stay on existing ones.</p>
    @if (categories.isLoading()) { <mat-progress-bar mode="indeterminate" /> }
    <div class="table-wrap">
      <table mat-table [dataSource]="(categories.hasValue() ? categories.value() : undefined) ?? []">
        <ng-container matColumnDef="name">
          <th mat-header-cell *matHeaderCellDef>Name</th>
          <td mat-cell *matCellDef="let c">{{ c.name }}<div class="sub">{{ c.description }}</div></td>
        </ng-container>
        <ng-container matColumnDef="active">
          <th mat-header-cell *matHeaderCellDef>Active</th>
          <td mat-cell *matCellDef="let c">
            <mat-slide-toggle [checked]="c.isActive" (change)="toggle(c, $event.checked)" [attr.aria-label]="'Active: ' + c.name" />
          </td>
        </ng-container>
        <ng-container matColumnDef="actions">
          <th mat-header-cell *matHeaderCellDef><span class="cdk-visually-hidden">Actions</span></th>
          <td mat-cell *matCellDef="let c">
            <button mat-icon-button (click)="open(c)" [attr.aria-label]="'Edit ' + c.name"><mat-icon>edit</mat-icon></button>
          </td>
        </ng-container>
        <tr mat-header-row *matHeaderRowDef="columns"></tr>
        <tr mat-row *matRowDef="let row; columns: columns"></tr>
        <tr class="mat-row" *matNoDataRow>
          <td class="mat-cell empty" [attr.colspan]="columns.length">No categories yet. Create one (e.g. Frontend, Backend, Infrastructure) so people can create tickets.</td>
        </tr>
      </table>
    </div>
  `,
  styles: `
    .table-wrap { overflow-x: auto; }
    table { width: 100%; }
    .sub { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .empty { padding: 24px; text-align: center; }
  `,
})
export class CategoryList {
  private readonly api = inject(AdminApi);
  private readonly dialog = inject(MatDialog);
  private readonly notifier = inject(Notifier);

  protected readonly columns = ['name', 'active', 'actions'];
  protected readonly categories = httpResource<Category[]>(() => ({
    url: this.api.categoriesUrl,
    params: { includeInactive: true },
  }));

  protected open(category: Category | null): void {
    this.dialog
      .open(CategoryDialog, { width: '480px', data: category })
      .afterClosed()
      .subscribe((saved?: Category) => {
        if (saved) {
          this.notifier.success(`Category “${saved.name}” saved.`);
          this.categories.reload();
        }
      });
  }

  protected toggle(category: Category, isActive: boolean): void {
    this.api.setCategoryActive(category.id, isActive).subscribe({
      next: () => this.categories.reload(),
      error: (error: ApiError) => {
        this.categories.reload();
        if (error.status < 500) this.notifier.error(error.title);
      },
    });
  }
}
