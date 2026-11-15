import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

/** Split-screen frame for the sign-in pages: brand panel on the left (hidden on phones), form on the right. */
@Component({
  selector: 'app-auth-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule],
  template: `
    <aside class="brand-panel" aria-hidden="true">
      <div class="brand"><span class="logo"><mat-icon>bolt</mat-icon></span> OpenTicket</div>
      <div class="pitch">
        <h2>Plan, track and ship work with your team.</h2>
        <p>A free, open-source issue tracker with a Kanban board, a clear workflow and no per-seat fees.</p>
      </div>
      <div class="mini-board">
        @for (column of columns; track column.title) {
          <div class="mini-column">
            <span class="mini-title">{{ column.title }}</span>
            @for (card of column.cards; track $index) {
              <span class="mini-card"><span class="mini-type" [style.background]="card"></span><span class="mini-line"></span></span>
            }
          </div>
        }
      </div>
    </aside>
    <main class="form-panel">
      <div class="form-box">
        <div class="mobile-brand"><span class="logo"><mat-icon>bolt</mat-icon></span> OpenTicket</div>
        <h1>{{ heading() }}</h1>
        @if (subtitle()) { <p class="subtitle">{{ subtitle() }}</p> }
        <ng-content />
      </div>
    </main>
  `,
  styles: `
    :host { display: grid; grid-template-columns: minmax(0, 5fr) minmax(0, 6fr); min-height: 100vh; }
    .brand-panel {
      background: linear-gradient(160deg, #0747a6 0%, #0052cc 55%, #2684ff 100%); color: #fff;
      padding: 40px 48px; display: flex; flex-direction: column; gap: 40px; overflow: hidden;
    }
    .brand, .mobile-brand { display: flex; align-items: center; gap: 10px; font: 600 22px/28px Roboto, sans-serif; }
    .logo { display: inline-flex; padding: 4px; border-radius: 6px; background: #fff; color: #0052cc; }
    .mobile-brand { display: none; margin-bottom: 24px; color: var(--mat-sys-on-surface); }
    .mobile-brand .logo { background: #0052cc; color: #fff; }
    .pitch h2 { font: 500 32px/40px Roboto, sans-serif; margin: 0 0 12px; max-width: 420px; }
    .pitch p { font: 16px/24px Roboto, sans-serif; opacity: 0.85; margin: 0; max-width: 420px; }
    .mini-board { display: grid; grid-template-columns: repeat(3, 1fr); gap: 10px; max-width: 460px; margin-top: auto; }
    .mini-column { background: rgba(255, 255, 255, 0.12); border-radius: 8px; padding: 8px; display: grid; gap: 6px; align-content: start; }
    .mini-title { font: 600 10px/14px Roboto, sans-serif; letter-spacing: 0.06em; opacity: 0.8; }
    .mini-card { display: flex; align-items: center; gap: 6px; background: #fff; border-radius: 4px; padding: 8px; }
    .mini-type { width: 10px; height: 10px; border-radius: 2px; flex: none; }
    .mini-line { height: 6px; border-radius: 3px; background: #dfe1e6; flex: 1; }
    .form-panel { display: flex; align-items: center; justify-content: center; padding: 32px 16px; }
    .form-box { width: 100%; max-width: 400px; }
    h1 { font: 500 28px/36px Roboto, sans-serif; margin: 0 0 4px; }
    .subtitle { color: var(--mat-sys-on-surface-variant); margin: 0 0 24px; }
    @media (max-width: 899px) {
      :host { grid-template-columns: 1fr; }
      .brand-panel { display: none; }
      .mobile-brand { display: flex; }
      .form-panel { align-items: flex-start; padding-top: 48px; }
    }
  `,
})
export class AuthLayout {
  readonly heading = input.required<string>();
  readonly subtitle = input<string>();

  protected readonly columns = [
    { title: 'TO DO', cards: ['#e5493a', '#4bade8', '#63ba3c'] },
    { title: 'IN PROGRESS', cards: ['#904ee2', '#e5493a'] },
    { title: 'DONE', cards: ['#4bade8', '#63ba3c', '#4bade8'] },
  ];
}
