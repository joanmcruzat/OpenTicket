import { describe, expect, it } from 'vitest';
import { TicketStatus, TicketSummary, UserProfile } from '@app/core/api/models';
import { ticketActions } from './ticket-actions';

const reporter: UserProfile = { id: 'c1', email: 'c@x.test', displayName: 'Rep', role: 'Customer' };
const developer: UserProfile = { id: 'a1', email: 'a@x.test', displayName: 'Dev', role: 'Agent' };
const otherDeveloper: UserProfile = { ...developer, id: 'a2' };
const admin: UserProfile = { id: 'ad', email: 'ad@x.test', displayName: 'Adm', role: 'Admin' };

function ticket(status: TicketStatus, assignedToId: string | null = 'a1'): TicketSummary {
  return {
    id: 't1', number: 1, title: 'T', type: 'Bug', status, priority: 'Medium',
    categoryId: 'cat', categoryName: 'Cat', createdById: 'c1', createdByName: 'Rep',
    assignedToId, assignedToName: assignedToId ? 'Dev' : null,
    createdAtUtc: '', updatedAtUtc: '', version: 1, allowedStatuses: [], sprintId: null, sprintName: null,
  };
}

describe('ticketActions', () => {
  it('lets a developer pick up an unassigned ticket but not one that is taken', () => {
    expect(ticketActions(ticket('Backlog', null), developer).canAssignToMe).toBe(true);
    expect(ticketActions(ticket('Backlog'), otherDeveloper).canAssignToMe).toBe(false);
    expect(ticketActions(ticket('Backlog', null), reporter).canAssignToMe).toBe(false);
  });

  it('lets only admins assign anyone, and never on closed tickets', () => {
    expect(ticketActions(ticket('ToDo'), admin).canAssignAnyone).toBe(true);
    expect(ticketActions(ticket('ToDo'), developer).canAssignAnyone).toBe(false);
    expect(ticketActions(ticket('Done'), admin).canAssignAnyone).toBe(false);
  });

  it('allows unassigning by admin or the assignee only before work is in flight', () => {
    expect(ticketActions(ticket('ToDo'), developer).canUnassign).toBe(true);
    expect(ticketActions(ticket('ToDo'), admin).canUnassign).toBe(true);
    expect(ticketActions(ticket('ToDo'), otherDeveloper).canUnassign).toBe(false);
    expect(ticketActions(ticket('InProgress'), admin).canUnassign).toBe(false);
    expect(ticketActions(ticket('InReview'), developer).canUnassign).toBe(false);
  });

  it('closes the conversation once the ticket is Done or Won’t Do', () => {
    expect(ticketActions(ticket('InReview'), reporter).canComment).toBe(true);
    expect(ticketActions(ticket('Done'), reporter).canComment).toBe(false);
    expect(ticketActions(ticket('WontDo'), admin).canComment).toBe(false);
  });
});
