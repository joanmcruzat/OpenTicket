import { Routes } from '@angular/router';

export const TICKET_ROUTES: Routes = [
  { path: '', loadComponent: () => import('./ticket-list').then((m) => m.TicketList), title: 'Tickets · OpenTicket' },
  {
    path: 'new',
    loadComponent: () => import('./ticket-create').then((m) => m.TicketCreate),
    title: 'Create ticket · OpenTicket',
  },
  { path: ':ticketId', loadComponent: () => import('./ticket-detail').then((m) => m.TicketDetailPage), title: 'Ticket · OpenTicket' },
];
