import { describe, expect, it } from 'vitest';
import { TICKET_STATUSES } from '@app/core/api/models';
import { statusCategory } from './badges';
import { ROLE_LABELS, STATUS_LABELS, TicketNumberPipe } from './labels';

describe('statusCategory', () => {
  it('groups statuses the way dev trackers colour them', () => {
    expect(TICKET_STATUSES.map(statusCategory)).toEqual(['todo', 'todo', 'doing', 'doing', 'done', 'dropped']);
  });
});

describe('labels', () => {
  it('has a label for every status', () => {
    expect(TICKET_STATUSES.map((s) => STATUS_LABELS[s])).toEqual([
      'Backlog', 'To Do', 'In Progress', 'In Review', 'Done', "Won't Do",
    ]);
  });

  it('shows team-friendly role names', () => {
    expect(ROLE_LABELS.Agent).toBe('Developer');
    expect(ROLE_LABELS.Customer).toBe('Reporter');
  });

  it('formats keys as OT- plus six digits', () => {
    expect(new TicketNumberPipe().transform(42)).toBe('OT-000042');
    expect(new TicketNumberPipe().transform(1234567)).toBe('OT-1234567');
  });
});
