import { Routes } from '@angular/router';

export const ADMIN_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'users' },
  { path: 'users', loadComponent: () => import('./user-list').then((m) => m.UserList), title: 'Users · OpenTicket' },
  {
    path: 'categories',
    loadComponent: () => import('./category-list').then((m) => m.CategoryList),
    title: 'Categories · OpenTicket',
  },
];
