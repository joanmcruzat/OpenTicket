import { ChangeDetectionStrategy, Component, computed, input, output, signal } from '@angular/core';
import { TicketStatus } from '@app/core/api/models';
import { STATUS_LABELS } from '@app/shared/labels';

export interface StatusCount {
  status: TicketStatus;
  count: number;
}

export interface Slice extends StatusCount {
  label: string;
  color: string;
  /** Whole percentage of the total. */
  share: number;
  /** SVG path for the ring segment; empty for a zero count. */
  path: string;
}

/**
 * Fixed hue per status (never by rank), in workflow order. Validated as a categorical palette on the light
 * surface, including the wrap-around pair (Won't Do beside Backlog). Some fall below 3:1 contrast, so the
 * legend always shows each name and count next to its swatch.
 */
export const STATUS_COLORS: Record<TicketStatus, string> = {
  Backlog: '#2a78d6',
  ToDo: '#eb6834',
  InProgress: '#1baf7a',
  InReview: '#eda100',
  Done: '#e87ba4',
  WontDo: '#008300',
};

const CENTER = 100;
const OUTER = 92;
const INNER = 58;

function point(radius: number, fraction: number): string {
  const angle = fraction * 2 * Math.PI - Math.PI / 2; // start at 12 o'clock, go clockwise
  return `${(CENTER + radius * Math.cos(angle)).toFixed(3)} ${(CENTER + radius * Math.sin(angle)).toFixed(3)}`;
}

/** A ring segment from `start` to `end` (fractions of a full turn). */
function ringPath(start: number, end: number): string {
  if (end - start >= 0.9999) {
    // A full ring can't be one arc: draw it as two halves.
    return `${ringPath(0, 0.5)} ${ringPath(0.5, 1)}`;
  }
  const large = end - start > 0.5 ? 1 : 0;
  return [
    `M ${point(OUTER, start)}`,
    `A ${OUTER} ${OUTER} 0 ${large} 1 ${point(OUTER, end)}`,
    `L ${point(INNER, end)}`,
    `A ${INNER} ${INNER} 0 ${large} 0 ${point(INNER, start)}`,
    'Z',
  ].join(' ');
}

/** Turns counts into donut slices in the given (workflow) order. */
export function toSlices(data: readonly StatusCount[]): Slice[] {
  const total = data.reduce((sum, d) => sum + d.count, 0);
  let start = 0;
  return data.map((d) => {
    const fraction = total === 0 ? 0 : d.count / total;
    const path = fraction === 0 ? '' : ringPath(start, start + fraction);
    start += fraction;
    return {
      ...d,
      label: STATUS_LABELS[d.status],
      color: STATUS_COLORS[d.status],
      share: Math.round(fraction * 100),
      path,
    };
  });
}

/**
 * Donut chart of ticket counts per status. The centre shows the total, or the hovered status. The legend lists
 * every status with its count and share and is the keyboard path; clicking a slice or legend row emits the status.
 */
@Component({
  selector: 'app-status-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="chart">
      <svg viewBox="0 0 200 200" class="donut" role="img" [attr.aria-label]="'Tickets per status, ' + total() + ' in total'">
        @if (total() === 0) {
          <circle cx="100" cy="100" r="75" class="empty-ring" />
        }
        @for (slice of slices(); track slice.status) {
          @if (slice.path) {
            <path [attr.d]="slice.path" [attr.fill]="slice.color" class="slice"
                  [class.dim]="hovered() !== null && hovered() !== slice.status"
                  (pointerenter)="hovered.set(slice.status)" (pointerleave)="hovered.set(null)"
                  (click)="select.emit(slice.status)">
              <title>{{ slice.label }}: {{ slice.count }} ({{ slice.share }}%)</title>
            </path>
          }
        }
        <text x="100" y="98" class="center-value">{{ focus()?.count ?? total() }}</text>
        <text x="100" y="120" class="center-label">{{ focus()?.label ?? 'tickets' }}</text>
        @if (focus(); as f) { <text x="100" y="137" class="center-share">{{ f.share }}%</text> }
      </svg>

      <ul class="legend" aria-label="Tickets per status">
        @for (slice of slices(); track slice.status) {
          <li>
            <button type="button" class="row" [class.active]="hovered() === slice.status"
                    (pointerenter)="hovered.set(slice.status)" (pointerleave)="hovered.set(null)"
                    (focus)="hovered.set(slice.status)" (blur)="hovered.set(null)"
                    (click)="select.emit(slice.status)"
                    [attr.aria-label]="slice.label + ': ' + slice.count + ' tickets, ' + slice.share + ' percent. Show them in the list.'">
              <span class="swatch" [style.background]="slice.color"></span>
              <span class="name">{{ slice.label }}</span>
              <span class="count">{{ slice.count }}</span>
              <span class="share">{{ slice.share }}%</span>
            </button>
          </li>
        }
      </ul>
    </div>
  `,
  styles: `
    :host { display: block; }
    .chart { display: flex; align-items: center; gap: 24px 40px; flex-wrap: wrap; }
    .donut { width: 200px; height: 200px; flex: none; overflow: visible; }
    /* A 2px surface-coloured stroke is the gap between neighbouring slices. */
    .slice { stroke: var(--mat-sys-surface); stroke-width: 2; cursor: pointer; transition: opacity 120ms; }
    .slice.dim { opacity: 0.35; }
    .empty-ring { fill: none; stroke: var(--mat-sys-outline-variant); stroke-width: 34; }
    .center-value { text-anchor: middle; font: 600 30px Roboto, sans-serif; fill: var(--mat-sys-on-surface); }
    .center-label { text-anchor: middle; font: 13px Roboto, sans-serif; fill: var(--mat-sys-on-surface-variant); }
    .center-share { text-anchor: middle; font: 12px Roboto, sans-serif; fill: var(--mat-sys-on-surface-variant); }
    .legend { list-style: none; margin: 0; padding: 0; display: grid; gap: 2px; flex: 1 1 240px; max-width: 360px; }
    .row {
      display: grid; grid-template-columns: 12px minmax(0, 1fr) auto 44px; align-items: center; gap: 10px;
      width: 100%; padding: 6px 8px; border: 0; border-radius: 6px; background: none; cursor: pointer;
      font: var(--mat-sys-body-medium); color: var(--mat-sys-on-surface); text-align: left;
    }
    .row:hover, .row.active, .row:focus-visible { background: var(--mat-sys-surface-container-low); outline: none; }
    .row:focus-visible { box-shadow: inset 0 0 0 2px var(--mat-sys-primary); }
    .swatch { width: 12px; height: 12px; border-radius: 3px; }
    .count { font-weight: 600; }
    .share { color: var(--mat-sys-on-surface-variant); text-align: right; }
    @media (max-width: 599px) { .chart { justify-content: center; } .legend { max-width: none; } }
  `,
})
export class StatusChart {
  readonly data = input.required<readonly StatusCount[]>();
  /** The user clicked a slice or legend row: show that status's tickets. */
  readonly select = output<TicketStatus>();

  protected readonly hovered = signal<TicketStatus | null>(null);
  protected readonly slices = computed(() => toSlices(this.data()));
  protected readonly total = computed(() => this.data().reduce((sum, d) => sum + d.count, 0));
  protected readonly focus = computed(() => this.slices().find((s) => s.status === this.hovered()) ?? null);
}
