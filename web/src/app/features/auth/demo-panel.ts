import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { Router } from '@angular/router';
import { AuthOptions, DemoRole } from '@app/core/auth/auth-options';
import { AuthStore } from '@app/core/auth/auth-store';
import { DEMO_ROLES } from '@app/core/auth/demo-roles';
import { ApiError } from '@app/core/http/api-error';

/** "Explore the demo": one click signs in as a demo persona. Renders only on demo servers. */
@Component({
  selector: 'app-demo-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  template: `
    @if (options.demo(); as demo) {
      <section class="demo" aria-labelledby="demo-heading">
        <h2 id="demo-heading"><mat-icon>rocket_launch</mat-icon> Explore the live demo</h2>
        <p class="hint">No sign-up needed. Pick a role to jump in.
          @if (demo.resetIntervalHours > 0) { Demo data resets every {{ demo.resetIntervalHours }} hours. }
        </p>
        @if (error(); as message) { <p class="error" role="alert">{{ message }}</p> }
        <div class="roles">
          @for (role of roles; track role.key) {
            <button type="button" class="role" (click)="signIn(role.key)" [disabled]="pending() !== null">
              <mat-icon class="role-icon">{{ role.icon }}</mat-icon>
              <span class="role-text">
                <strong>{{ role.label }}</strong><span class="person"> · {{ role.person }}</span>
                <span class="summary">{{ role.summary }}</span>
              </span>
              @if (pending() === role.key) { <mat-spinner diameter="20" /> } @else { <mat-icon class="go">arrow_forward</mat-icon> }
            </button>
          }
        </div>
      </section>
      <div class="divider" role="separator"><span>or sign in with an account</span></div>
    }
  `,
  styles: `
    .demo { border: 1px solid #b3d4ff; background: #f4f8ff; border-radius: 10px; padding: 16px; margin-bottom: 8px; }
    h2 { display: flex; align-items: center; gap: 8px; font: 500 16px/24px Roboto, sans-serif; margin: 0 0 4px; color: #0747a6; }
    .hint { margin: 0 0 12px; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .error { color: var(--mat-sys-error); margin: 0 0 8px; }
    .roles { display: grid; gap: 8px; }
    .role {
      display: flex; align-items: center; gap: 12px; width: 100%; text-align: left; cursor: pointer;
      background: #fff; border: 1px solid var(--mat-sys-outline-variant); border-radius: 8px; padding: 10px 12px;
      font: inherit; color: inherit; transition: border-color 120ms, box-shadow 120ms;
    }
    .role:hover:not(:disabled), .role:focus-visible { border-color: #0052cc; box-shadow: 0 1px 4px rgba(9, 30, 66, 0.2); outline: none; }
    .role:disabled { cursor: progress; opacity: 0.7; }
    .role-icon { color: #0052cc; flex: none; }
    .role-text { flex: 1; min-width: 0; }
    .person { color: var(--mat-sys-on-surface-variant); }
    .summary { display: block; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .go { color: var(--mat-sys-on-surface-variant); flex: none; }
    .divider { display: flex; align-items: center; gap: 12px; margin: 20px 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
    .divider::before, .divider::after { content: ''; flex: 1; border-top: 1px solid var(--mat-sys-outline-variant); }
  `,
})
export class DemoPanel {
  private readonly auth = inject(AuthStore);
  private readonly router = inject(Router);
  protected readonly options = inject(AuthOptions);
  protected readonly roles = DEMO_ROLES;
  protected readonly pending = signal<DemoRole | null>(null);
  protected readonly error = signal<string | null>(null);

  protected signIn(role: DemoRole): void {
    this.pending.set(role);
    this.error.set(null);
    this.auth.demoSignIn(role).subscribe({
      next: () => void this.router.navigateByUrl('/dashboard'),
      error: (error: ApiError) => {
        this.pending.set(null);
        this.error.set(error.status === 429 ? 'Too many attempts. Wait a minute and try again.' : error.title);
      },
    });
  }
}
