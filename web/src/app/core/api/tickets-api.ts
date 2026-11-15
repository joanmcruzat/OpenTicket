import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import {
  BoardFilter,
  CommentView,
  TicketDetail,
  TicketListFilter,
  TicketPriority,
  TicketStatus,
  TicketSummary,
  TicketType,
} from './models';

export interface CreateTicketInput {
  title: string;
  description: string;
  type: TicketType;
  categoryId: string;
  priority: TicketPriority;
}

export interface UpdateTicketInput {
  title?: string;
  description?: string;
  type?: TicketType;
  categoryId?: string;
  priority?: TicketPriority;
}

@Injectable({ providedIn: 'root' })
export class TicketsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  readonly ticketsUrl = `${this.baseUrl}/tickets`;
  readonly boardUrl = `${this.baseUrl}/tickets/board`;

  listParams(filter: TicketListFilter): HttpParams {
    let params = new HttpParams()
      .set('page', filter.page)
      .set('pageSize', filter.pageSize)
      .set('sortBy', filter.sortBy)
      .set('sortDir', filter.sortDir);
    filter.status.forEach((s) => (params = params.append('status', s)));
    filter.type.forEach((t) => (params = params.append('type', t)));
    filter.priority.forEach((p) => (params = params.append('priority', p)));
    if (filter.categoryId) params = params.set('categoryId', filter.categoryId);
    if (filter.unassigned) params = params.set('unassigned', true);
    if (filter.search.trim()) params = params.set('search', filter.search.trim());
    return params;
  }

  boardParams(filter: BoardFilter): HttpParams {
    let params = new HttpParams();
    filter.type.forEach((t) => (params = params.append('type', t)));
    if (filter.categoryId) params = params.set('categoryId', filter.categoryId);
    if (filter.assignedToId) params = params.set('assignedToId', filter.assignedToId);
    if (filter.unassigned) params = params.set('unassigned', true);
    if (filter.search.trim()) params = params.set('search', filter.search.trim());
    return params;
  }

  ticketUrl(id: string): string {
    return `${this.ticketsUrl}/${id}`;
  }

  create(input: CreateTicketInput): Observable<TicketDetail> {
    return this.http.post<TicketDetail>(this.ticketsUrl, input);
  }

  update(ticket: TicketDetail, input: UpdateTicketInput): Observable<TicketDetail> {
    return this.http.patch<TicketDetail>(this.ticketUrl(ticket.id), { ...input, version: ticket.version });
  }

  /** Works from a summary too (the board), since it needs only the id and version. */
  changeStatus(ticket: TicketSummary, status: TicketStatus, reason?: string): Observable<TicketDetail> {
    return this.http.post<TicketDetail>(`${this.ticketUrl(ticket.id)}/status`, {
      status,
      reason: reason ?? null,
      version: ticket.version,
    });
  }

  /** Plans the ticket into a sprint, or back to the backlog with null. */
  moveToSprint(ticket: TicketSummary, sprintId: string | null): Observable<TicketDetail> {
    return this.http.put<TicketDetail>(`${this.ticketUrl(ticket.id)}/sprint`, { sprintId, version: ticket.version });
  }

  assign(ticket: TicketSummary, assigneeId: string): Observable<TicketDetail> {
    return this.http.put<TicketDetail>(`${this.ticketUrl(ticket.id)}/assignee`, { assigneeId, version: ticket.version });
  }

  unassign(ticket: TicketSummary): Observable<TicketDetail> {
    return this.http.delete<TicketDetail>(`${this.ticketUrl(ticket.id)}/assignee`, {
      params: { version: ticket.version },
    });
  }

  addComment(ticketId: string, body: string): Observable<CommentView> {
    return this.http.post<CommentView>(`${this.ticketUrl(ticketId)}/comments`, { body });
  }
}
