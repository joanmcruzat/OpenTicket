import { describe, expect, it } from 'vitest';
import { SprintSummary } from '@app/core/api/models';
import { addDays, daysLeft, daysLeftLabel } from './sprint-dates';

const sprint = (endDate: string | null): SprintSummary => ({
  id: 's', name: 'Sprint', goal: null, state: 'Active', startDate: '2026-11-03', endDate,
  startedAtUtc: null, completedAtUtc: null, ticketCount: 0, doneCount: 0,
});

describe('sprint dates', () => {
  it('counts days left, today, and overdue', () => {
    expect(daysLeft(sprint('2026-11-17'), '2026-11-09')).toBe(8);
    expect(daysLeftLabel(sprint('2026-11-10'), '2026-11-09')).toBe('1 day left');
    expect(daysLeftLabel(sprint('2026-11-09'), '2026-11-09')).toBe('Ends today');
    expect(daysLeftLabel(sprint('2026-11-07'), '2026-11-09')).toBe('2 days overdue');
    expect(daysLeftLabel(sprint(null), '2026-11-09')).toBeNull();
  });

  it('adds days across month ends', () => {
    expect(addDays('2026-11-25', 14)).toBe('2026-12-09');
  });
});
