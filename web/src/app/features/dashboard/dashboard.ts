import { httpResource } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router, RouterLink } from '@angular/router';
import { API_BASE_URL } from '@app/core/api/api-config';
import { DashboardSummary, TicketStatus } from '@app/core/api/models';
import { AuthStore } from '@app/core/auth/auth-store';
import { StatusChart } from '@app/shared/status-chart';
import { TRY_THIS } from '@app/shared/try-this';

function readFlag(key: string): boolean {
  try {
    return localStorage.getItem(key) === '1';
  } catch {
    return false;
  }
}

interface StatCard {
  label: string;
  value: number;
  icon: string;
  link: string;
  query?: Record<string, string | boolean>;
}

const TIPS_DISMISSED_KEY = 'ot.tips.dismissed';

@Component({
  selector: 'app-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatCardModule, MatIconModule, MatButtonModule, MatProgressBarModule, StatusChart],
  template: `
    <header class="page-header">
      <h1>Hello, {{ auth.user()?.displayName }}</h1>
      <div class="header-actions">
        <a mat-stroked-button routerLink="/board"><mat-icon>view_kanban</mat-icon> Open board</a>
        <a mat-flat-button routerLink="/tickets/new"><mat-icon>add</mat-icon> Create</a>
      </div>
    </header>
    <p class="subtitle">{{ scopeText() }}</p>

    @if (showTips()) {
      <section class="tips" aria-labelledby="tips-heading">
        <div class="tips-head">
          <h2 id="tips-heading"><mat-icon>tips_and_updates</mat-icon> Try this</h2>
          <a mat-button routerLink="/help"><mat-icon>menu_book</mat-icon> Full guide</a>
          <button mat-icon-button (click)="dismissTips()" aria-label="Hide tips"><mat-icon>close</mat-icon></button>
        </div>
        <ol>
          @for (step of tips(); track step.text) {
            <li><a [routerLink]="step.link" [queryParams]="step.query ?? {}">{{ step.text }}</a></li>
          }
        </ol>
      </section>
    }

    @if (summary.isLoading()) {
      <mat-progress-bar mode="indeterminate" />
    } @else if (summary.error()) {
      <p class="error" role="alert">The dashboard could not be loaded. <button mat-button (click)="summary.reload()">Retry</button></p>
    }

    <section class="stats">
      @for (card of cards(); track card.label) {
        <a class="stat" [routerLink]="card.link" [queryParams]="card.query ?? {}">
          <mat-card appearance="outlined">
            <mat-card-content>
              <mat-icon class="stat-icon">{{ card.icon }}</mat-icon>
              <div class="stat-value">{{ card.value }}</div>
              <div class="stat-label">{{ card.label }}</div>
            </mat-card-content>
          </mat-card>
        </a>
      }
    </section>

    @if (byStatus(); as data) {
      <section class="panel" aria-labelledby="by-status-heading">
        <h2 id="by-status-heading">Tickets by status</h2>
        <app-status-chart [data]="data" (select)="showStatus($event)" />
        <p class="hint">Click a slice or a status to list those tickets.</p>
      </section>
    }
  `,
  styles: `
    .tips { border: 1px solid #b3d4ff; background: #f4f8ff; border-radius: 10px; padding: 8px 16px 12px; margin: 8px 0 16px; }
    .tips-head { display: flex; align-items: center; gap: 4px; }
    .tips-head h2 { flex: 1; display: flex; align-items: center; gap: 8px; font: var(--mat-sys-title-small); margin: 0; }
    .tips ol { margin: 4px 0 0; padding-left: 20px; display: grid; gap: 4px; }
    .header-actions { display: flex; gap: 8px; flex-wrap: wrap; }
    .panel { border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; padding: 16px 20px; margin-top: 16px; }
    .panel h2 { font: var(--mat-sys-title-medium); margin: 0 0 12px; }
    .hint { margin: 12px 0 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .stats { display: grid; grid-template-columns: repeat(auto-fill, minmax(170px, 1fr)); gap: 16px; margin-top: 16px; }
    .stats:empty { display: none; }
    .stat { text-decoration: none; color: inherit; }
    .stat mat-card { height: 100%; transition: box-shadow 120ms; }
    .stat:hover mat-card, .stat:focus-visible mat-card { box-shadow: var(--mat-sys-level2); }
    .stat-icon { color: var(--mat-sys-primary); }
    .stat-value { font: var(--mat-sys-display-small); margin-top: 8px; }
    .stat-label { color: var(--mat-sys-on-surface-variant); }
  `,
})
export class Dashboard {
  protected readonly auth = inject(AuthStore);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly router = inject(Router);
  protected readonly summary = httpResource<DashboardSummary>(() => `${this.baseUrl}/dashboard/summary`);

  /** Per-browser convenience only; storage may be unavailable (private mode), so failures just keep tips visible. */
  private readonly tipsDismissed = signal(readFlag(TIPS_DISMISSED_KEY));
  protected readonly tips = computed(() => {
    const role = this.auth.role();
    return role ? TRY_THIS[role] : [];
  });
  protected readonly showTips = computed(() => !this.tipsDismissed() && this.tips().length > 0);

  protected dismissTips(): void {
    this.tipsDismissed.set(true);
    try {
      localStorage.setItem(TIPS_DISMISSED_KEY, '1');
    } catch {
      // Storage blocked: the tips stay hidden for this visit only.
    }
  }

  protected readonly scopeText = computed(() => {
    switch (this.auth.role()) {
      case 'Admin':
        return 'All tickets in the system.';
      case 'Agent':
        return "All tickets. Pick up unassigned work from the board with \"Assign to me\".";
      default:
        return 'Tickets you reported.';
    }
  });

  protected readonly cards = computed<StatCard[]>(() => {
    const s = (this.summary.hasValue() ? this.summary.value() : undefined);
    if (!s) return [];
    const cards: StatCard[] = [];
    if (s.assignedToMe !== null) {
      cards.push({ label: 'Assigned to me', value: s.assignedToMe, icon: 'assignment_ind', link: '/board' });
    }
    if (s.unassigned !== null) {
      cards.push({ label: 'Unassigned', value: s.unassigned, icon: 'person_off', link: '/tickets', query: { unassigned: true } });
    }
    return cards;
  });

  /** Per-status counts in workflow order (the API already sends them that way, zeros included). */
  protected readonly byStatus = computed(() => (this.summary.hasValue() ? this.summary.value().byStatus : null));

  protected showStatus(status: TicketStatus): void {
    void this.router.navigate(['/tickets'], { queryParams: { status } });
  }
}
