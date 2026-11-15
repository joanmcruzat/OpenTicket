import { SprintSummary } from '@app/core/api/models';

/** Today as an ISO date (yyyy-MM-dd) in the user's time zone. */
export function isoToday(): string {
  return toIsoDate(new Date());
}

export function toIsoDate(date: Date): string {
  const pad = (n: number) => n.toString().padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}`;
}

export function addDays(iso: string, days: number): string {
  const [y, m, d] = iso.split('-').map(Number);
  return toIsoDate(new Date(y!, m! - 1, d! + days));
}

/** "Nov 3 – Nov 17", or null without dates. */
export function sprintRange(sprint: SprintSummary): string | null {
  if (!sprint.startDate || !sprint.endDate) return null;
  const fmt = (iso: string) => {
    const [y, m, d] = iso.split('-').map(Number);
    return new Date(y!, m! - 1, d!).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });
  };
  return `${fmt(sprint.startDate)} – ${fmt(sprint.endDate)}`;
}

/** Whole days from `today` until the end date: negative once overdue, null without an end date. */
export function daysLeft(sprint: SprintSummary, today = isoToday()): number | null {
  if (!sprint.endDate) return null;
  const [ey, em, ed] = sprint.endDate.split('-').map(Number);
  const [ty, tm, td] = today.split('-').map(Number);
  return Math.round((Date.UTC(ey!, em! - 1, ed!) - Date.UTC(ty!, tm! - 1, td!)) / 86_400_000);
}

export function daysLeftLabel(sprint: SprintSummary, today = isoToday()): string | null {
  const days = daysLeft(sprint, today);
  if (days === null) return null;
  if (days < 0) return `${-days} ${days === -1 ? 'day' : 'days'} overdue`;
  if (days === 0) return 'Ends today';
  return `${days} ${days === 1 ? 'day' : 'days'} left`;
}
