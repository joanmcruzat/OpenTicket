import { CLOSED_STATUSES, TicketSummary, UserProfile } from '@app/core/api/models';

/**
 * Which assignment and comment controls to show. Status moves and edit rights come from the API
 * (allowedStatuses, canEdit); these mirror the remaining domain rules (docs/architecture.md §11). The API stays the authority.
 */
export interface TicketActions {
  canAssignAnyone: boolean;
  canAssignToMe: boolean;
  canUnassign: boolean;
  canComment: boolean;
}

export function ticketActions(ticket: TicketSummary, user: UserProfile): TicketActions {
  const closed = CLOSED_STATUSES.includes(ticket.status);
  const isAdmin = user.role === 'Admin';
  const isDeveloper = user.role === 'Agent';
  const isAssignee = isDeveloper && ticket.assignedToId === user.id;
  const workInFlight = ticket.status === 'InProgress' || ticket.status === 'InReview';

  return {
    canAssignAnyone: !closed && isAdmin,
    canAssignToMe: !closed && isDeveloper && ticket.assignedToId === null,
    canUnassign: !closed && !workInFlight && ticket.assignedToId !== null && (isAdmin || isAssignee),
    canComment: !closed,
  };
}
