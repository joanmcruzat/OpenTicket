import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TicketPriority, TicketStatus, TicketType } from '@app/core/api/models';
import { STATUS_LABELS, TYPE_LABELS } from './labels';

/** Status category drives the colour, as on most dev trackers: grey = not started, blue = in flight, green = done. */
export function statusCategory(status: TicketStatus): 'todo' | 'doing' | 'done' | 'dropped' {
  switch (status) {
    case 'InProgress':
    case 'InReview':
      return 'doing';
    case 'Done':
      return 'done';
    case 'WontDo':
      return 'dropped';
    default:
      return 'todo';
  }
}

@Component({
  selector: 'app-status-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<span class="lozenge" [class]="'lozenge lozenge-' + category()">{{ label() }}</span>`,
})
export class StatusBadge {
  readonly status = input.required<TicketStatus>();
  protected readonly label = computed(() => STATUS_LABELS[this.status()]);
  protected readonly category = computed(() => statusCategory(this.status()));
}

const TYPE_ICONS: Record<TicketType, string> = {
  Bug: 'bug_report',
  Task: 'check_box',
  Story: 'bookmark',
  Improvement: 'trending_up',
};

/** A small coloured square with the issue type's icon. */
@Component({
  selector: 'app-type-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatTooltipModule],
  template: `
    <span class="type-icon" [class]="'type-icon type-' + type().toLowerCase()" [matTooltip]="label()" role="img" [attr.aria-label]="label()">
      <mat-icon>{{ icon() }}</mat-icon>
    </span>
    @if (showLabel()) { <span class="type-label">{{ label() }}</span> }
  `,
  styles: `:host { display: inline-flex; align-items: center; gap: 6px; } .type-label { white-space: nowrap; }`,
})
export class TypeIcon {
  readonly type = input.required<TicketType>();
  readonly showLabel = input(false);
  protected readonly icon = computed(() => TYPE_ICONS[this.type()]);
  protected readonly label = computed(() => TYPE_LABELS[this.type()]);
}

const PRIORITY_ICONS: Record<TicketPriority, string> = {
  Urgent: 'keyboard_double_arrow_up',
  High: 'keyboard_arrow_up',
  Medium: 'drag_handle',
  Low: 'keyboard_arrow_down',
};

/** Arrow icon in the priority's colour, optionally followed by its name. */
@Component({
  selector: 'app-priority-badge',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatTooltipModule],
  template: `
    <mat-icon class="priority-icon" [class]="'priority-icon priority-' + priority().toLowerCase()"
              [matTooltip]="priority() + ' priority'" role="img" [attr.aria-label]="priority() + ' priority'">{{ icon() }}</mat-icon>
    @if (showLabel()) { <span>{{ priority() }}</span> }
  `,
  styles: `:host { display: inline-flex; align-items: center; gap: 2px; white-space: nowrap; }`,
})
export class PriorityBadge {
  readonly priority = input.required<TicketPriority>();
  readonly showLabel = input(true);
  protected readonly icon = computed(() => PRIORITY_ICONS[this.priority()]);
}

const AVATAR_COLORS = ['#0052cc', '#00875a', '#6554c0', '#ff5630', '#00a3bf', '#ff8b00', '#5243aa', '#36b37e'];

/** Initials in a coloured circle; the colour is stable per name. Shows a dashed outline when nobody is assigned. */
@Component({
  selector: 'app-avatar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatIconModule, MatTooltipModule],
  template: `
    @if (name(); as n) {
      <span class="avatar" [style.background]="color()" [style.width.px]="size()" [style.height.px]="size()"
            [style.font-size.px]="size() * 0.42" [matTooltip]="n" role="img" [attr.aria-label]="n">{{ initials() }}</span>
    } @else {
      <span class="avatar avatar-empty" [style.width.px]="size()" [style.height.px]="size()" matTooltip="Unassigned"
            role="img" aria-label="Unassigned"><mat-icon [style.font-size.px]="size() * 0.7">person</mat-icon></span>
    }
  `,
})
export class Avatar {
  readonly name = input<string | null>(null);
  readonly size = input(24);

  protected readonly initials = computed(() =>
    (this.name() ?? '')
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase() ?? '')
      .join(''),
  );

  protected readonly color = computed(() => {
    let hash = 0;
    for (const ch of this.name() ?? '') hash = (hash * 31 + ch.charCodeAt(0)) | 0;
    return AVATAR_COLORS[Math.abs(hash) % AVATAR_COLORS.length];
  });
}
