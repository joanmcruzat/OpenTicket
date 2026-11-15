import { describe, expect, it } from 'vitest';
import { TicketHistoryEntry } from '@app/core/api/models';
import { describeChange } from './ticket-timeline';

const entry = (changeType: TicketHistoryEntry['changeType'], oldValue: string | null, newValue: string | null): TicketHistoryEntry => ({
  id: '1', changeType, oldValue, newValue, changedById: 'u', changedByName: 'Ana', createdAtUtc: '',
});

describe('describeChange', () => {
  it('uses friendly status labels', () => {
    expect(describeChange(entry('StatusChanged', 'ToDo', 'InProgress'))).toBe('moved it from To Do to In Progress');
  });

  it("shows the Won't Do reason", () => {
    expect(describeChange(entry('StatusChanged', 'Backlog', 'WontDo: Duplicate of OT-3'))).toBe(
      "moved it from Backlog to Won't Do (Duplicate of OT-3)",
    );
  });

  it('describes sprint planning', () => {
    expect(describeChange(entry('SprintChanged', null, 'Sprint 4'))).toBe('added it to Sprint 4');
    expect(describeChange(entry('SprintChanged', 'Sprint 4', 'Sprint 5'))).toBe('moved it from Sprint 4 to Sprint 5');
    expect(describeChange(entry('SprintChanged', 'Sprint 4', null))).toBe('moved it from Sprint 4 back to the backlog');
  });

  it('describes type changes', () => {
    expect(describeChange(entry('TypeChanged', 'Bug', 'Improvement'))).toBe('changed the type from Bug to Improvement');
  });

  it('distinguishes first assignment from reassignment', () => {
    expect(describeChange(entry('Assigned', null, 'Dana'))).toBe('assigned it to Dana');
    expect(describeChange(entry('Assigned', 'Dana', 'Lee'))).toBe('reassigned it from Dana to Lee');
  });

  it('describes description edits without dumping the text', () => {
    expect(describeChange(entry('DetailsEdited', 'description', 'description'))).toBe('edited the description');
  });
});
