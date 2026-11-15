import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { Router, RouterLink } from '@angular/router';
import { AuthOptions, DemoRole } from '../auth/auth-options';
import { AuthStore } from '../auth/auth-store';
import { DEMO_ROLES } from '../auth/demo-roles';
import { Notifier } from '../http/notifier';

/** Strip above every page on demo servers: who you are, when data resets, and a one-click role switch. */
@Component({
  selector: 'app-demo-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButtonModule, MatIconModule, MatMenuModule],
  template: `
    @if (options.demo(); as demo) {
      <div class="banner" role="note">
        <mat-icon>science</mat-icon>
        <span class="text">
          <strong>Live demo.</strong> You're {{ current()?.person ?? 'signed in' }} ({{ current()?.label ?? '' }}).
          @if (demo.resetIntervalHours > 0) { <span class="reset">Changes reset every {{ demo.resetIntervalHours }} h.</span> }
        </span>
        <button mat-button class="action" [matMenuTriggerFor]="roles" [disabled]="switching()">
          <mat-icon>swap_horiz</mat-icon> Switch role
        </button>
        <a mat-button class="action" routerLink="/help"><mat-icon>menu_book</mat-icon> Guide</a>
        <mat-menu #roles="matMenu">
          @for (role of others(); track role.key) {
            <button mat-menu-item (click)="switchTo(role.key)">
              <mat-icon>{{ role.icon }}</mat-icon><span>{{ role.label }} ({{ role.person }})</span>
            </button>
          }
        </mat-menu>
      </div>
    }
  `,
  styles: `
    .banner {
      display: flex; align-items: center; flex-wrap: wrap; gap: 4px 10px; padding: 6px 16px;
      background: #fffae6; color: #172b4d; border-bottom: 1px solid #ffe380; font: var(--mat-sys-body-medium);
    }
    .text { flex: 1 1 260px; }
    .reset { color: #5e6c84; }
    .action { --mat-button-text-label-text-color: #0747a6; }
  `,
})
export class DemoBanner {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly notifier = inject(Notifier);
  protected readonly options = inject(AuthOptions);
  protected readonly switching = signal(false);

  protected readonly current = computed(() => DEMO_ROLES.find((r) => r.role === this.auth.role()));
  protected readonly others = computed(() => DEMO_ROLES.filter((r) => r.role !== this.auth.role()));

  protected switchTo(role: DemoRole): void {
    this.switching.set(true);
    this.auth.demoSignIn(role).subscribe({
      next: (user) => {
        this.switching.set(false);
        this.notifier.success(`You're now ${user.displayName}.`);
        // Leave and re-enter the route so every page reloads its data for the new role.
        void this.router
          .navigateByUrl('/help', { skipLocationChange: true })
          .then(() => this.router.navigateByUrl('/dashboard'));
      },
      error: () => {
        this.switching.set(false);
        this.notifier.error('Could not switch role. Please try again.');
      },
    });
  }
}
