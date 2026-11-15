import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';
import { TicketHistoryEntry, TicketStatus, TicketType } from '@app/core/api/models';
import { STATUS_LABELS, TYPE_LABELS } from '@app/shared/labels';

const WONT_DO_PREFIX = 'WontDo: ';

/** Status values are enum names; Won't Do entries carry the reason as "WontDo: reason". */
function statusText(value: string | null): string {
  if (value === null) return '';
  if (value.startsWith(WONT_DO_PREFIX)) return `${STATUS_LABELS.WontDo} (${value.slice(WONT_DO_PREFIX.length)})`;
  return value in STATUS_LABELS ? STATUS_LABELS[value as TicketStatus] : value;
}

function typeText(value: string | null): string {
  return value !== null && value in TYPE_LABELS ? TYPE_LABELS[value as TicketType] : (value ?? '');
}

/** One human sentence per history entry. Exported for tests. */
export function describeChange(entry: TicketHistoryEntry): string {
  switch (entry.changeType) {
    case 'Created':
      return 'created the ticket';
    case 'StatusChanged':
      return `moved it from ${statusText(entry.oldValue)} to ${statusText(entry.newValue)}`;
    case 'Assigned':
      return entry.oldValue ? `reassigned it from ${entry.oldValue} to ${entry.newValue}` : `assigned it to ${entry.newValue}`;
    case 'Unassigned':
      return `unassigned ${entry.oldValue}`;
    case 'PriorityChanged':
      return `changed the priority from ${entry.oldValue} to ${entry.newValue}`;
    case 'CategoryChanged':
      return `moved it from ${entry.oldValue} to ${entry.newValue}`;
    case 'TypeChanged':
      return `changed the type from ${typeText(entry.oldValue)} to ${typeText(entry.newValue)}`;
    case 'SprintChanged':
      if (!entry.newValue) return `moved it from ${entry.oldValue} back to the backlog`;
      return entry.oldValue ? `moved it from ${entry.oldValue} to ${entry.newValue}` : `added it to ${entry.newValue}`;
    case 'DetailsEdited':
      return entry.oldValue === 'description' ? 'edited the description' : `renamed it from “${entry.oldValue}” to “${entry.newValue}”`;
  }
}

@Component({
  selector: 'app-ticket-timeline',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, MatIconModule],
  template: `
    <ol class="timeline">
      @for (entry of entries(); track entry.id) {
        <li>
          <mat-icon class="dot" aria-hidden="true">history</mat-icon>
          <div>
            <strong>{{ entry.changedByName ?? 'Someone' }}</strong> {{ describe(entry) }}
            <div class="when">{{ entry.createdAtUtc | date: 'medium' }}</div>
          </div>
        </li>
      } @empty {
        <li class="muted">No history yet.</li>
      }
    </ol>
  `,
  styles: `
    .timeline { list-style: none; padding: 0; margin: 0; display: grid; gap: 12px; }
    li { display: flex; gap: 8px; }
    .dot { font-size: 18px; width: 18px; height: 18px; color: var(--mat-sys-outline); }
    .when, .muted { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-small); }
  `,
})
export class TicketTimeline {
  readonly entries = input.required<readonly TicketHistoryEntry[]>();
  protected readonly describe = describeChange;
}
