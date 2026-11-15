import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { SprintSummary } from './models';

export interface SprintInput {
  name: string;
  goal: string | null;
  startDate?: string | null;
  endDate?: string | null;
}

@Injectable({ providedIn: 'root' })
export class SprintsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  readonly sprintsUrl = `${this.baseUrl}/sprints`;
  readonly planUrl = `${this.baseUrl}/sprints/plan`;

  list(): Observable<SprintSummary[]> {
    return this.http.get<SprintSummary[]>(this.sprintsUrl);
  }

  create(input: SprintInput): Observable<SprintSummary> {
    return this.http.post<SprintSummary>(this.sprintsUrl, input);
  }

  update(id: string, input: SprintInput): Observable<SprintSummary> {
    return this.http.put<SprintSummary>(`${this.sprintsUrl}/${id}`, input);
  }

  start(id: string, startDate: string, endDate: string): Observable<SprintSummary> {
    return this.http.post<SprintSummary>(`${this.sprintsUrl}/${id}/start`, { startDate, endDate });
  }

  /** Unfinished tickets go to `moveOpenTicketsTo` (a planned sprint) or, when null, back to the backlog. */
  complete(id: string, moveOpenTicketsTo: string | null): Observable<SprintSummary> {
    return this.http.post<SprintSummary>(`${this.sprintsUrl}/${id}/complete`, { moveOpenTicketsTo });
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.sprintsUrl}/${id}`);
  }
}
