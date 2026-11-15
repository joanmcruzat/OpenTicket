import { UserRole } from '@app/core/api/models';

export interface TryThisStep {
  text: string;
  link: string;
  query?: Record<string, string>;
}

/** Suggested first steps per role, used by the dashboard and the guide. */
export const TRY_THIS: Record<UserRole, readonly TryThisStep[]> = {
  Agent: [
    { text: 'Open the board and drag one of your In Progress tickets to In Review.', link: '/board' },
    { text: 'Find an unassigned ticket and click "Assign to me".', link: '/tickets', query: { unassigned: 'true' } },
    { text: 'Open the Backlog and drag a ticket into the next sprint.', link: '/backlog' },
  ],
  Customer: [
    { text: 'Create a ticket: pick "Bug" and follow the hints in the form.', link: '/tickets/new' },
    { text: 'Open the board to see where your tickets are in the workflow.', link: '/board' },
    { text: 'Add a comment to one of your tickets to give the team more detail.', link: '/tickets' },
  ],
  Admin: [
    { text: 'Assign an unassigned ticket to a developer from its details panel.', link: '/tickets', query: { unassigned: 'true' } },
    { text: 'Drag any ticket on the board: admins can move every ticket.', link: '/board' },
    { text: 'Plan the next sprint on the Backlog page, or complete the active one.', link: '/backlog' },
  ],
};
