import { Pipe, PipeTransform } from '@angular/core';
import { TicketStatus, TicketType, UserRole } from '@app/core/api/models';

export const STATUS_LABELS: Record<TicketStatus, string> = {
  Backlog: 'Backlog',
  ToDo: 'To Do',
  InProgress: 'In Progress',
  InReview: 'In Review',
  Done: 'Done',
  WontDo: "Won't Do",
};

export const TYPE_LABELS: Record<TicketType, string> = {
  Bug: 'Bug',
  Task: 'Task',
  Story: 'Story',
  Improvement: 'Improvement',
};

/** The API keeps its role names; the UI speaks the language of a development team. */
export const ROLE_LABELS: Record<UserRole, string> = {
  Admin: 'Admin',
  Agent: 'Developer',
  Customer: 'Reporter',
};

/** The project-wide ticket key format (CLAUDE.md): OT- plus the number padded to six digits, e.g. OT-000042. */
export function formatTicketNumber(value: number): string {
  return `OT-${value.toString().padStart(6, '0')}`;
}

@Pipe({ name: 'ticketNumber' })
export class TicketNumberPipe implements PipeTransform {
  transform(value: number): string {
    return formatTicketNumber(value);
  }
}

@Pipe({ name: 'roleLabel' })
export class RoleLabelPipe implements PipeTransform {
  transform(value: UserRole | null | undefined): string {
    return value ? ROLE_LABELS[value] : '';
  }
}
