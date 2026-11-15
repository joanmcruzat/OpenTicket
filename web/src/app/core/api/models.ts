// DTOs mirroring the OpenTicket API (enums are serialized as strings).

export type UserRole = 'Admin' | 'Agent' | 'Customer';
export type TicketStatus = 'Backlog' | 'ToDo' | 'InProgress' | 'InReview' | 'Done' | 'WontDo';
export type TicketType = 'Bug' | 'Task' | 'Story' | 'Improvement';
export type TicketPriority = 'Low' | 'Medium' | 'High' | 'Urgent';
export type TicketChangeType =
  | 'Created'
  | 'StatusChanged'
  | 'Assigned'
  | 'Unassigned'
  | 'PriorityChanged'
  | 'CategoryChanged'
  | 'DetailsEdited'
  | 'TypeChanged'
  | 'SprintChanged';

export const TICKET_STATUSES: readonly TicketStatus[] = ['Backlog', 'ToDo', 'InProgress', 'InReview', 'Done', 'WontDo'];
export const BOARD_STATUSES: readonly TicketStatus[] = ['Backlog', 'ToDo', 'InProgress', 'InReview', 'Done'];
export const CLOSED_STATUSES: readonly TicketStatus[] = ['Done', 'WontDo'];
export const TICKET_TYPES: readonly TicketType[] = ['Bug', 'Task', 'Story', 'Improvement'];
export const TICKET_PRIORITIES: readonly TicketPriority[] = ['Low', 'Medium', 'High', 'Urgent'];
export const USER_ROLES: readonly UserRole[] = ['Admin', 'Agent', 'Customer'];

export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
}

export interface SessionResponse {
  accessToken: string;
  expiresAtUtc: string;
  user: UserProfile;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface TicketSummary {
  id: string;
  number: number;
  title: string;
  type: TicketType;
  status: TicketStatus;
  priority: TicketPriority;
  categoryId: string;
  categoryName: string;
  createdById: string;
  createdByName: string | null;
  assignedToId: string | null;
  assignedToName: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
  version: number;
  /** Statuses the current user may move this ticket to (computed by the API). */
  allowedStatuses: TicketStatus[];
  /** Null when the ticket is in the backlog (no sprint). */
  sprintId: string | null;
  sprintName: string | null;
}

export interface TicketDetail extends TicketSummary {
  description: string;
  closedAtUtc: string | null;
  /** The reason, when the status is WontDo. */
  closureNote: string | null;
  canEdit: boolean;
}

export type SprintState = 'Planned' | 'Active' | 'Completed';

export interface SprintSummary {
  id: string;
  name: string;
  goal: string | null;
  state: SprintState;
  /** ISO dates (yyyy-MM-dd); null until a planned sprint gets dates. */
  startDate: string | null;
  endDate: string | null;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  ticketCount: number;
  /** Tickets in Done or Won't Do. */
  doneCount: number;
}

export interface SprintPlan {
  sprints: { sprint: SprintSummary; tickets: TicketSummary[] }[];
  backlog: { total: number; tickets: TicketSummary[] };
}

/** The board: the active sprint's tickets, or every ticket when no sprint is active. */
export interface BoardView {
  sprint: SprintSummary | null;
  columns: BoardColumn[];
}

export interface BoardColumn {
  status: TicketStatus;
  total: number;
  items: TicketSummary[];
}

export interface TicketHistoryEntry {
  id: string;
  changeType: TicketChangeType;
  oldValue: string | null;
  newValue: string | null;
  changedById: string;
  changedByName: string | null;
  createdAtUtc: string;
}

export interface CommentView {
  id: string;
  ticketId: string;
  authorId: string;
  authorName: string | null;
  authorRole: UserRole | null;
  body: string;
  createdAtUtc: string;
}

export interface Category {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface DashboardSummary {
  total: number;
  byStatus: { status: TicketStatus; count: number }[];
  assignedToMe: number | null;
  unassigned: number | null;
}

export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  role: UserRole;
  isActive: boolean;
  createdAtUtc: string;
}

export interface AgentOption {
  id: string;
  displayName: string;
  openTicketCount: number;
}

export interface TicketListFilter {
  page: number;
  pageSize: number;
  status: TicketStatus[];
  type: TicketType[];
  priority: TicketPriority[];
  categoryId: string | null;
  unassigned: boolean;
  search: string;
  sortBy: 'createdAt' | 'updatedAt' | 'priority' | 'status' | 'number';
  sortDir: 'asc' | 'desc';
}

export interface BoardFilter {
  type: TicketType[];
  categoryId: string | null;
  assignedToId: string | null;
  unassigned: boolean;
  search: string;
}
