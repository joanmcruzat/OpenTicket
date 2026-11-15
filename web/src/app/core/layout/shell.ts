import { BreakpointObserver, Breakpoints } from '@angular/cdk/layout';
import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatMenuModule } from '@angular/material/menu';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatToolbarModule } from '@angular/material/toolbar';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { map } from 'rxjs';
import { UserRole } from '../api/models';
import { RoleLabelPipe } from '@app/shared/labels';
import { AuthStore } from '../auth/auth-store';
import { ChangePasswordDialog } from '../auth/change-password-dialog';
import { DemoBanner } from './demo-banner';

interface NavItem {
  path: string;
  label: string;
  icon: string;
  roles: readonly UserRole[];
}

const NAV_ITEMS: readonly NavItem[] = [
  { path: '/dashboard', label: 'Dashboard', icon: 'dashboard', roles: ['Admin', 'Agent', 'Customer'] },
  { path: '/board', label: 'Board', icon: 'view_kanban', roles: ['Admin', 'Agent', 'Customer'] },
  { path: '/backlog', label: 'Backlog', icon: 'list_alt', roles: ['Admin', 'Agent'] },
  { path: '/tickets', label: 'All tickets', icon: 'view_list', roles: ['Admin', 'Agent', 'Customer'] },
  { path: '/tickets/new', label: 'Create ticket', icon: 'add_circle', roles: ['Admin', 'Agent', 'Customer'] },
  { path: '/help', label: 'Guide', icon: 'menu_book', roles: ['Admin', 'Agent', 'Customer'] },
  { path: '/admin/users', label: 'Users', icon: 'group', roles: ['Admin'] },
  { path: '/admin/categories', label: 'Categories', icon: 'category', roles: ['Admin'] },
];

@Component({
  selector: 'app-shell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    MatSidenavModule,
    MatToolbarModule,
    MatListModule,
    MatIconModule,
    MatButtonModule,
    MatMenuModule,
    RoleLabelPipe,
    DemoBanner,
  ],
  template: `
    <mat-sidenav-container class="shell">
      <mat-sidenav
        #drawer
        [mode]="isHandset() ? 'over' : 'side'"
        [opened]="!isHandset()"
        class="shell-nav"
        aria-label="Main navigation"
      >
        <div class="brand"><span class="logo"><mat-icon>bolt</mat-icon></span> OpenTicket</div>
        <mat-nav-list>
          @for (item of navItems(); track item.path) {
            <a
              mat-list-item
              [routerLink]="item.path"
              routerLinkActive="active"
              [routerLinkActiveOptions]="{ exact: true }"
              (click)="isHandset() && drawer.close()"
            >
              <mat-icon matListItemIcon>{{ item.icon }}</mat-icon>
              <span matListItemTitle>{{ item.label }}</span>
            </a>
          }
        </mat-nav-list>
      </mat-sidenav>
      <mat-sidenav-content>
        <mat-toolbar class="shell-toolbar">
          @if (isHandset()) {
            <button mat-icon-button (click)="drawer.toggle()" aria-label="Open navigation">
              <mat-icon>menu</mat-icon>
            </button>
          }
          <span class="spacer"></span>
          @if (auth.user(); as user) {
            <button mat-button [matMenuTriggerFor]="userMenu" aria-label="Account menu">
              <mat-icon>account_circle</mat-icon>
              {{ user.displayName }} <span class="role">· {{ user.role | roleLabel }}</span>
            </button>
            <mat-menu #userMenu="matMenu">
              <button mat-menu-item (click)="changePassword()">
                <mat-icon>key</mat-icon><span>Change password</span>
              </button>
              <button mat-menu-item (click)="auth.logout()">
                <mat-icon>logout</mat-icon><span>Sign out</span>
              </button>
            </mat-menu>
          }
        </mat-toolbar>
        <app-demo-banner />
        <main class="content"><router-outlet /></main>
      </mat-sidenav-content>
    </mat-sidenav-container>
  `,
  styles: `
    .shell { height: 100%; }
    .shell-nav { width: 240px; }
    .brand { display: flex; gap: 10px; align-items: center; padding: 20px 16px; font: var(--mat-sys-title-large); font-weight: 600; }
    .logo { display: inline-flex; padding: 4px; border-radius: 6px; background: #0052cc; color: #fff; }
    .active { background: var(--mat-sys-secondary-container); }
    .shell-toolbar { position: sticky; top: 0; z-index: 2; background: var(--mat-sys-surface-container); }
    .spacer { flex: 1; }
    .role { opacity: 0.7; }
    .content { padding: 24px; max-width: 1440px; margin: 0 auto; }
    @media (max-width: 599px) { .content { padding: 12px; } .role { display: none; } }
  `,
})
export class Shell {
  protected readonly auth = inject(AuthStore);
  private readonly dialog = inject(MatDialog);

  protected readonly isHandset = toSignal(
    inject(BreakpointObserver)
      .observe(Breakpoints.Handset)
      .pipe(map((result) => result.matches)),
    { initialValue: false },
  );

  protected readonly navItems = computed(() => {
    const role = this.auth.role();
    return NAV_ITEMS.filter((item) => role !== null && item.roles.includes(role));
  });

  protected changePassword(): void {
    this.dialog.open(ChangePasswordDialog, { width: '420px' });
  }
}
