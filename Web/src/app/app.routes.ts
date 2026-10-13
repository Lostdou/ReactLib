import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () => import('./features/grid/grid-page').then((m) => m.GridPageComponent),
  },
  {
    path: 'v/:seq',
    loadComponent: () => import('./features/detail/detail-page').then((m) => m.DetailPageComponent),
  },
  {
    path: 'sumar',
    loadComponent: () => import('./features/submit/submit-page').then((m) => m.SubmitPageComponent),
  },
  { path: '**', redirectTo: '' },
];
