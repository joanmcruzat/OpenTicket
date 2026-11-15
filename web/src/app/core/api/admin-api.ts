import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-config';
import { Category, UserListItem, UserRole } from './models';

export interface CreateUserInput {
  email: string;
  displayName: string;
  role: UserRole;
  temporaryPassword: string;
}

@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  readonly usersUrl = `${this.baseUrl}/users`;
  readonly agentsUrl = `${this.baseUrl}/users/agents`;
  readonly categoriesUrl = `${this.baseUrl}/categories`;

  createUser(input: CreateUserInput): Observable<UserListItem> {
    return this.http.post<UserListItem>(this.usersUrl, input);
  }

  changeRole(userId: string, role: UserRole): Observable<UserListItem> {
    return this.http.put<UserListItem>(`${this.usersUrl}/${userId}/role`, { role });
  }

  setUserActive(userId: string, isActive: boolean): Observable<UserListItem> {
    return this.http.post<UserListItem>(`${this.usersUrl}/${userId}/${isActive ? 'activate' : 'deactivate'}`, null);
  }

  resetPassword(userId: string, temporaryPassword: string): Observable<void> {
    return this.http.post<void>(`${this.usersUrl}/${userId}/reset-password`, { temporaryPassword });
  }

  createCategory(name: string, description: string | null): Observable<Category> {
    return this.http.post<Category>(this.categoriesUrl, { name, description });
  }

  updateCategory(id: string, name: string, description: string | null): Observable<Category> {
    return this.http.put<Category>(`${this.categoriesUrl}/${id}`, { name, description });
  }

  setCategoryActive(id: string, isActive: boolean): Observable<Category> {
    return this.http.post<Category>(`${this.categoriesUrl}/${id}/${isActive ? 'activate' : 'deactivate'}`, null);
  }
}
