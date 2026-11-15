import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { TICKET_PRIORITIES, TICKET_TYPES, TicketStatus } from '@app/core/api/models';
import { AuthStore } from '@app/core/auth/auth-store';
import { PriorityBadge, StatusBadge, TypeIcon } from '@app/shared/badges';
import { ROLE_LABELS } from '@app/shared/labels';
import { TRY_THIS } from '@app/shared/try-this';

interface StatusHelp {
  status: TicketStatus;
  meaning: string;
}

const STATUS_HELP: readonly StatusHelp[] = [
  { status: 'Backlog', meaning: 'Reported, not planned yet. Every new ticket starts here.' },
  { status: 'ToDo', meaning: 'Planned and ready to be picked up.' },
  { status: 'InProgress', meaning: 'A developer is working on it. Needs an assignee.' },
  { status: 'InReview', meaning: 'Work is done and being checked. Needs an assignee.' },
  { status: 'Done', meaning: 'Finished. Can be reopened to To Do if it comes back.' },
  { status: 'WontDo', meaning: 'Closed without doing it, always with a reason (duplicate, not needed…).' },
];

const TYPE_HELP: Record<string, string> = {
  Bug: 'Something is broken. Include steps to reproduce.',
  Task: 'A piece of work that is not a feature, e.g. an upgrade.',
  Story: 'A feature described from the user’s point of view.',
  Improvement: 'Make something that works today better.',
};

const FAQ: readonly { q: string; a: string }[] = [
  {
    q: 'Why can’t I drag a card or see a status in the menu?',
    a: 'You only see moves you are allowed to make. Developers move tickets assigned to them, admins move any ticket, and reporters can only close their own ticket as Won’t Do before work starts, or reopen it. In Progress and In Review also need an assignee.',
  },
  {
    q: 'How do I find a ticket quickly?',
    a: 'Search by words in the summary or by key, e.g. OT-000042 (or just 42), on the All tickets page or the board.',
  },
  {
    q: 'Why is the comment box gone?',
    a: 'Done and Won’t Do tickets are read-only. Reopen the ticket to To Do to continue the conversation.',
  },
  {
    q: 'I forgot my password.',
    a: 'Use “Forgot password?” on the sign-in page if your server has email set up; otherwise ask an admin to reset it under Users.',
  },
];

/** The in-app user guide. The "start here" section adapts to the signed-in user's role. */
@Component({
  selector: 'app-guide',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, MatButtonModule, MatIconModule, StatusBadge, TypeIcon, PriorityBadge],
  template: `
    <header class="page-header">
      <div>
        <h1>Guide</h1>
        <p class="subtitle">Everything you need to work with OpenTicket, in five minutes.</p>
      </div>
    </header>

    <nav class="toc" aria-label="On this page">
      <a href="#start" (click)="jump($event, 'start')">Start here</a>
      <a href="#roles" (click)="jump($event, 'roles')">Roles</a>
      <a href="#workflow" (click)="jump($event, 'workflow')">Workflow</a>
      <a href="#types" (click)="jump($event, 'types')">Types &amp; priorities</a>
      <a href="#sprints" (click)="jump($event, 'sprints')">Sprints</a>
      <a href="#board" (click)="jump($event, 'board')">The board</a>
      <a href="#ticket" (click)="jump($event, 'ticket')">Working on a ticket</a>
      <a href="#faq" (click)="jump($event, 'faq')">FAQ</a>
    </nav>

    <section id="start" class="card highlight">
      <h2><mat-icon>flag</mat-icon> Start here: you are a {{ roleLabel() }}</h2>
      <ol class="steps">
        @for (step of steps(); track step.text) {
          <li><a [routerLink]="step.link" [queryParams]="step.query ?? {}">{{ step.text }}</a></li>
        }
      </ol>
    </section>

    <section id="roles" class="card">
      <h2><mat-icon>groups</mat-icon> Roles</h2>
      <div class="grid three">
        <div class="tile" [class.mine]="auth.role() === 'Customer'">
          <h3><mat-icon>campaign</mat-icon> Reporter</h3>
          <ul>
            <li>Creates bugs, tasks, stories and improvements</li>
            <li>Sees and comments on their own tickets</li>
            <li>Edits their ticket while it is in Backlog or To Do</li>
            <li>Can close their ticket as Won’t Do before work starts, or reopen it</li>
          </ul>
        </div>
        <div class="tile" [class.mine]="auth.role() === 'Agent'">
          <h3><mat-icon>code</mat-icon> Developer</h3>
          <ul>
            <li>Sees every ticket and the whole board</li>
            <li>Picks up unassigned work with “Assign to me”</li>
            <li>Moves and edits tickets assigned to them</li>
            <li>Comments on any open ticket</li>
          </ul>
        </div>
        <div class="tile" [class.mine]="auth.role() === 'Admin'">
          <h3><mat-icon>admin_panel_settings</mat-icon> Admin</h3>
          <ul>
            <li>Moves and edits any ticket</li>
            <li>Assigns and reassigns developers</li>
            <li>Manages users, roles and categories</li>
          </ul>
        </div>
      </div>
    </section>

    <section id="workflow" class="card">
      <h2><mat-icon>alt_route</mat-icon> Workflow</h2>
      <p>Open tickets move freely between Backlog, To Do, In Progress and In Review, and can be closed as Done or Won’t Do at any point.</p>
      <div class="flow" aria-hidden="true">
        @for (s of flow; track s; let last = $last) {
          <app-status-badge [status]="s" />
          @if (!last) { <mat-icon class="arrow">arrow_forward</mat-icon> }
        }
      </div>
      <dl class="defs">
        @for (item of statusHelp; track item.status) {
          <dt><app-status-badge [status]="item.status" /></dt><dd>{{ item.meaning }}</dd>
        }
      </dl>
    </section>

    <section id="types" class="card">
      <h2><mat-icon>category</mat-icon> Types &amp; priorities</h2>
      <div class="grid two">
        <dl class="defs">
          @for (type of types; track type) {
            <dt><app-type-icon [type]="type" [showLabel]="true" /></dt><dd>{{ typeHelp[type] }}</dd>
          }
        </dl>
        <dl class="defs">
          @for (p of priorities; track p) {
            <dt><app-priority-badge [priority]="p" /></dt><dd>{{ priorityHelp[p] }}</dd>
          }
        </dl>
      </div>
    </section>

    <section id="sprints" class="card">
      <h2><mat-icon>event_repeat</mat-icon> Sprints</h2>
      <ul>
        <li>A <strong>sprint</strong> is a time box (usually two weeks) with a goal. Only one sprint is active at a time.</li>
        <li>Plan on the <strong>Backlog</strong> page (admins and developers): drag tickets from the backlog into a sprint, or use the move button on a row. Joining a sprint doesn't change a ticket's status.</li>
        <li><strong>Start sprint</strong> puts it on the board with its dates; the board header shows days left and progress.</li>
        <li><strong>Complete sprint</strong> asks where unfinished tickets go: the next planned sprint or the backlog. Done tickets stay with the sprint.</li>
        <li>With no active sprint, the board simply shows every ticket.</li>
      </ul>
      @if (!auth.hasRole('Customer')) { <a mat-stroked-button routerLink="/backlog"><mat-icon>list_alt</mat-icon> Open the backlog</a> }
    </section>

    <section id="board" class="card">
      <h2><mat-icon>view_kanban</mat-icon> The board</h2>
      <ul>
        <li>During a sprint the board shows <strong>only the active sprint</strong>; otherwise it shows every ticket.</li>
        <li><strong>Drag a card</strong> to another column to change its status. Columns you can’t drop into are dimmed while you drag.</li>
        <li>On a phone, <strong>press and hold</strong> a card to pick it up; a normal swipe scrolls the board.</li>
        <li>Use <strong>Only mine</strong>, <strong>Unassigned</strong>, type chips and the category filter to focus.</li>
        <li>Each card shows the type, key, priority and assignee. <strong>Click a card</strong> to open it.</li>
      </ul>
      <a mat-stroked-button routerLink="/board"><mat-icon>view_kanban</mat-icon> Open the board</a>
    </section>

    <section id="ticket" class="card">
      <h2><mat-icon>description</mat-icon> Working on a ticket</h2>
      <ul>
        <li>The coloured <strong>status button</strong> at the top right lists the moves you can make. Won’t Do asks for a reason.</li>
        <li>Change <strong>type, priority and category</strong> right in the details panel; the pencil edits summary and description.</li>
        <li>Developers see <strong>Assign to me</strong> on unassigned tickets; admins pick any developer.</li>
        <li><strong>Comments</strong> are for the conversation; <strong>History</strong> records every change and who made it.</li>
      </ul>
    </section>

    <section id="faq" class="card">
      <h2><mat-icon>help_outline</mat-icon> FAQ</h2>
      @for (item of faq; track item.q) {
        <details>
          <summary>{{ item.q }}</summary>
          <p>{{ item.a }}</p>
        </details>
      }
    </section>
  `,
  styles: `
    .toc { display: flex; flex-wrap: wrap; gap: 6px 16px; margin: 0 0 16px; }
    .toc a { text-decoration: none; font-weight: 500; }
    .card { border: 1px solid var(--mat-sys-outline-variant); border-radius: 10px; padding: 16px 20px; margin-bottom: 16px; scroll-margin-top: 80px; }
    .highlight { border-color: #b3d4ff; background: #f4f8ff; }
    h2 { display: flex; align-items: center; gap: 8px; font: var(--mat-sys-title-medium); margin: 0 0 12px; }
    h3 { display: flex; align-items: center; gap: 6px; font: var(--mat-sys-title-small); margin: 0 0 8px; }
    .steps { margin: 0; padding-left: 20px; display: grid; gap: 6px; }
    ul { margin: 0 0 12px; padding-left: 20px; display: grid; gap: 4px; }
    .grid { display: grid; gap: 12px; }
    .three { grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); }
    .two { grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); }
    .tile { border: 1px solid var(--mat-sys-outline-variant); border-radius: 8px; padding: 12px; }
    .tile.mine { border-color: #0052cc; box-shadow: inset 0 0 0 1px #0052cc; }
    .flow { display: flex; flex-wrap: wrap; align-items: center; gap: 6px; margin: 8px 0 16px; }
    .arrow { font-size: 18px; width: 18px; height: 18px; color: var(--mat-sys-outline); }
    .defs { display: grid; grid-template-columns: max-content minmax(0, 1fr); gap: 10px 16px; align-items: center; margin: 0; }
    .defs dd { margin: 0; }
    details { border-top: 1px solid var(--mat-sys-outline-variant); padding: 10px 0; }
    details:first-of-type { border-top: 0; }
    summary { cursor: pointer; font-weight: 500; }
    details p { margin: 8px 0 0; color: var(--mat-sys-on-surface-variant); }
  `,
})
export class Guide {
  protected readonly auth = inject(AuthStore);
  protected readonly flow: readonly TicketStatus[] = ['Backlog', 'ToDo', 'InProgress', 'InReview', 'Done'];
  protected readonly statusHelp = STATUS_HELP;
  protected readonly types = TICKET_TYPES;
  protected readonly typeHelp = TYPE_HELP;
  protected readonly priorities = [...TICKET_PRIORITIES].reverse();
  protected readonly priorityHelp: Record<string, string> = {
    Urgent: 'Drop everything: production is broken or data is at risk.',
    High: 'Important; pick it up next.',
    Medium: 'The normal priority.',
    Low: 'Nice to have; do it when there is time.',
  };
  protected readonly faq = FAQ;

  protected readonly roleLabel = computed(() => {
    const role = this.auth.role();
    return role ? ROLE_LABELS[role] : '';
  });
  protected readonly steps = computed(() => {
    const role = this.auth.role();
    return role ? TRY_THIS[role] : [];
  });

  /** In-page links without changing the route (the SPA router would otherwise treat #anchors as navigation). */
  protected jump(event: Event, id: string): void {
    event.preventDefault();
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
