import { Routes } from '@angular/router';
import { AuthGuard } from './core/guards/auth.guard';
import { GuestGuard } from './core/guards/guest.guard';
import { RoleGuard } from './core/guards/role.guard';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./home/home.component').then(m => m.HomeComponent),
    data: { title: 'QualiFlow - Accueil' }
  },
  {
    path: 'home',
    redirectTo: '',
    pathMatch: 'full'
  },
  {
    path: 'login',
    canActivate: [GuestGuard],
    loadComponent: () => import('./features/auth/login/login.component').then(m => m.LoginComponent),
    data: { title: 'Login' }
  },
  {
    path: 'register',
    canActivate: [GuestGuard],
    loadComponent: () => import('./features/auth/register/register.component').then(m => m.RegisterComponent),
    data: { title: 'Inscription' }
  },
  {
    path: 'forgot-password',
    canActivate: [GuestGuard],
    loadComponent: () => import('./features/auth/forgot-password/forgot-password.component').then(m => m.ForgotPasswordComponent),
    data: { title: 'Mot de passe oublie' }
  },
  {
    path: 'reset-password',
    canActivate: [GuestGuard],
    loadComponent: () => import('./features/auth/reset-password/reset-password.component').then(m => m.ResetPasswordComponent),
    data: { title: 'Reinitialiser mot de passe' }
  },
  {
    path: 'verify-email',
    canActivate: [GuestGuard],
    loadComponent: () => import('./features/auth/verify-email/verify-email.component').then(m => m.VerifyEmailComponent),
    data: { title: 'Verification de l\'email' }
  },
  {
    path: '',
    canActivate: [AuthGuard],
    canActivateChild: [AuthGuard],
    loadComponent: () => import('./layout/main-layout/main-layout.component').then(m => m.MainLayoutComponent),
    children: [
      {
        path: '',
        redirectTo: 'dashboard',
        pathMatch: 'full'
      },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent),
        data: {
          title: 'Dashboard',
          roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
        }
      },
      {
        path: 'super-admin',
        canActivate: [RoleGuard],
        canActivateChild: [RoleGuard],
        data: { roles: ['SUPER_ADMIN'] },
        children: [
          {
            path: 'dashboard',
            loadComponent: () => import('./features/super-admin/super-admin-dashboard/super-admin-dashboard.component').then(m => m.SuperAdminDashboardComponent),
            data: { title: 'Dashboard Super Admin', roles: ['SUPER_ADMIN'] }
          },
          {
            path: 'requests',
            loadComponent: () => import('./features/super-admin/requests-list/requests-list.component').then(m => m.RequestsListComponent),
            data: { title: 'Demandes d\'organisation', roles: ['SUPER_ADMIN'] }
          },
          {
            path: 'organizations',
            loadComponent: () => import('./features/super-admin/organizations-list/organizations-list.component').then(m => m.OrganizationsListComponent),
            data: { title: 'Instituts / Organisations', roles: ['SUPER_ADMIN'] }
          },
          {
            path: 'organizations/create',
            loadComponent: () => import('./features/super-admin/organization-form/organization-form.component').then(m => m.OrganizationFormComponent),
            data: { title: 'Creer organisation', roles: ['SUPER_ADMIN'] }
          },
          {
            path: 'organizations/:id/edit',
            loadComponent: () => import('./features/super-admin/organization-form/organization-form.component').then(m => m.OrganizationFormComponent),
            data: { title: 'Editer organisation', roles: ['SUPER_ADMIN'] }
          },
          {
            path: 'organizations/:id',
            loadComponent: () => import('./features/super-admin/organization-details/organization-details.component').then(m => m.OrganizationDetailsComponent),
            data: { title: 'Detail organisation', roles: ['SUPER_ADMIN'] }
          }
        ]
      },
      {
        path: 'processes',
        children: [
          {
            path: '',
            loadComponent: () => import('./features/processes/process-list/process-list.component').then(m => m.ProcessListComponent),
            data: {
              title: 'Processus',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          },
          {
            path: 'map',
            loadComponent: () => import('./features/processes/process-map/process-map.component').then(m => m.ProcessMapComponent),
            data: {
              title: 'Cartographie des processus',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          },
          {
            path: 'new',
            loadComponent: () => import('./features/processes/process-form/process-form.component').then(m => m.ProcessFormComponent),
            data: {
              title: 'Nouveau processus',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE']
            }
          },
          {
            path: ':id/edit',
            loadComponent: () => import('./features/processes/process-form/process-form.component').then(m => m.ProcessFormComponent),
            data: {
              title: 'Modifier processus',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE']
            }
          },
          {
            path: ':id',
            loadComponent: () => import('./features/processes/process-details/process-details.component').then(m => m.ProcessDetailsComponent),
            data: {
              title: 'Detail processus',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          }
        ]
      },
      {
        path: 'procedures',
        children: [
          {
            path: '',
            loadComponent: () => import('./features/procedures/procedure-list/procedure-list.component').then(m => m.ProcedureListComponent),
            data: {
              title: 'Procedures',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          },
          {
            path: 'new',
            loadComponent: () => import('./features/procedures/procedure-form/procedure-form.component').then(m => m.ProcedureFormComponent),
            data: {
              title: 'Nouvelle procedure',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE']
            }
          },
          {
            path: ':id/edit',
            loadComponent: () => import('./features/procedures/procedure-form/procedure-form.component').then(m => m.ProcedureFormComponent),
            data: {
              title: 'Modifier procedure',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE']
            }
          },
          {
            path: ':id',
            loadComponent: () => import('./features/procedures/procedure-details/procedure-details.component').then(m => m.ProcedureDetailsComponent),
            data: {
              title: 'Detail procedure',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          }
        ]
      },
      {
        path: 'non-conformities',
        children: [
          {
            path: '',
            loadComponent: () => import('./features/non-conformities/nonconformity-list/nonconformity-list.component').then(m => m.NonconformityListComponent),
            data: {
              title: 'Non-conformites',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          },
          {
            path: 'new',
            loadComponent: () => import('./features/non-conformities/nonconformity-form/nonconformity-form.component').then(m => m.NonconformityFormComponent),
            data: {
              title: 'Nouvelle non-conformite',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'UTILISATEUR']
            }
          },
          {
            path: ':id/edit',
            loadComponent: () => import('./features/non-conformities/nonconformity-form/nonconformity-form.component').then(m => m.NonconformityFormComponent),
            data: {
              title: 'Modifier non-conformite',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE']
            }
          },
          {
            path: ':id',
            loadComponent: () => import('./features/non-conformities/nonconformity-details/nonconformity-details.component').then(m => m.NonconformityDetailsComponent),
            data: {
              title: 'Detail non-conformite',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          }
        ]
      },
      {
        path: 'corrective-actions',
        canActivate: [RoleGuard],
        canActivateChild: [RoleGuard],
        data: {
          roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
        },
        children: [
          {
            path: '',
            loadComponent: () => import('./features/corrective-actions/corrective-action-list/corrective-action-list.component').then(m => m.CorrectiveActionListComponent),
            data: {
              title: 'Actions correctives',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          },
          {
            path: 'new',
            loadComponent: () => import('./features/corrective-actions/corrective-action-form/corrective-action-form.component').then(m => m.CorrectiveActionFormComponent),
            data: {
              title: 'Nouvelle action corrective',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE']
            }
          },
          {
            path: ':id/edit',
            loadComponent: () => import('./features/corrective-actions/corrective-action-form/corrective-action-form.component').then(m => m.CorrectiveActionFormComponent),
            data: {
              title: 'Modifier action corrective',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE']
            }
          },
          {
            path: ':id',
            loadComponent: () => import('./features/corrective-actions/corrective-action-details/corrective-action-details.component').then(m => m.CorrectiveActionDetailsComponent),
            data: {
              title: 'Detail action corrective',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          }
        ]
      },
      {
        path: 'indicators',
        canActivate: [RoleGuard],
        canActivateChild: [RoleGuard],
        data: {
          roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
        },
        children: [
          {
            path: '',
            loadComponent: () => import('./features/indicators/indicator-list/indicator-list.component').then(m => m.IndicatorListComponent),
            data: {
              title: 'Indicateurs KPI',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          },
          {
            path: 'dashboard',
            loadComponent: () => import('./features/indicators/indicator-dashboard/indicator-dashboard.component').then(m => m.IndicatorDashboardComponent),
            data: {
              title: 'Dashboard KPI',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          },
          {
            path: 'new',
            loadComponent: () => import('./features/indicators/indicator-form/indicator-form.component').then(m => m.IndicatorFormComponent),
            data: {
              title: 'Nouvel indicateur',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE']
            }
          },
          {
            path: ':id/edit',
            loadComponent: () => import('./features/indicators/indicator-form/indicator-form.component').then(m => m.IndicatorFormComponent),
            data: {
              title: 'Modifier indicateur',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE']
            }
          },
          {
            path: ':id',
            loadComponent: () => import('./features/indicators/indicator-details/indicator-details.component').then(m => m.IndicatorDetailsComponent),
            data: {
              title: 'Detail indicateur',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          }
        ]
      },
      {
        path: 'processus',
        redirectTo: 'processes',
        pathMatch: 'full'
      },
      {
        path: 'documents',
        children: [
          {
            path: '',
            loadComponent: () => import('./features/documents/documents-list/documents-list.component').then(m => m.DocumentsListComponent),
            data: {
              title: 'Documents',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          },
          {
            path: 'new',
            loadComponent: () => import('./features/documents/document-form/document-form.component').then(m => m.DocumentFormComponent),
            data: {
              title: 'Nouveau document',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          },
          {
            path: ':id/edit',
            loadComponent: () => import('./features/documents/document-form/document-form.component').then(m => m.DocumentFormComponent),
            data: {
              title: 'Modifier document',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE']
            }
          },
          {
            path: ':id/versions',
            loadComponent: () => import('./features/documents/document-versions/document-versions.component').then(m => m.DocumentVersionsComponent),
            data: {
              title: 'Versions document',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          },
          {
            path: ':id',
            loadComponent: () => import('./features/documents/document-details/document-details.component').then(m => m.DocumentDetailsComponent),
            data: {
              title: 'Detail document',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          }
        ]
      },
      {
        path: 'departments',
        children: [
          {
            path: '',
            loadComponent: () => import('./features/departments/department-list/department-list.component').then(m => m.DepartmentListComponent),
            data: {
              title: 'Départements',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          },
          {
            path: 'new',
            loadComponent: () => import('./features/departments/department-form/department-form.component').then(m => m.DepartmentFormComponent),
            data: {
              title: 'Nouveau département',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE']
            }
          },
          {
            path: ':id/edit',
            loadComponent: () => import('./features/departments/department-form/department-form.component').then(m => m.DepartmentFormComponent),
            data: {
              title: 'Modifier département',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE']
            }
          },
          {
            path: ':id',
            loadComponent: () => import('./features/departments/department-details/department-details.component').then(m => m.DepartmentDetailsComponent),
            data: {
              title: 'Détail département',
              roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR']
            }
          }
        ]
      },
      {
        path: 'notifications',
        loadComponent: () => import('./features/notifications/notification-center/notification-center.component').then(m => m.NotificationCenterComponent),
        data: {
          title: 'Notifications',
          roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR', 'SUPER_ADMIN']
        }
      },
      {
        path: 'chatbot',
        loadComponent: () => import('./features/chatbot/chatbot-page/chatbot-page.component').then(m => m.ChatbotPageComponent),
        data: {
          title: 'Assistant GED',
          roles: ['ADMIN_ORG', 'RESPONSABLE_QUALITE', 'CHEF_SERVICE', 'UTILISATEUR', 'SUPER_ADMIN']
        }
      },
      {
        path: 'users',
        children: [
          {
            path: '',
            loadComponent: () => import('./features/users/users-list/users-list.component').then(m => m.UsersListComponent),
            data: {
              title: 'Users Management',
              requiredRoles: ['ADMIN_ORG']
            }
          },
          {
            path: 'new',
            loadComponent: () => import('./features/users/users-form/users-form.component').then(m => m.UsersFormComponent),
            data: {
              title: 'Create User',
              requiredRoles: ['ADMIN_ORG']
            }
          },
          {
            path: 'edit/:id',
            loadComponent: () => import('./features/users/users-form/users-form.component').then(m => m.UsersFormComponent),
            data: {
              title: 'Edit User',
              requiredRoles: ['ADMIN_ORG']
            }
          },
          {
            path: ':id',
            loadComponent: () => import('./features/users/user-detail/user-detail.component').then(m => m.UserDetailComponent),
            data: {
              title: 'User Details',
              requiredRoles: ['ADMIN_ORG']
            }
          }
        ]
      },
      {
        path: 'organization/audit-log',
        loadComponent: () => import('./features/organizations/organization-audit-log/organization-audit-log.component').then(m => m.OrganizationAuditLogComponent),
        data: {
          title: 'Journal d\'actions',
          requiredRoles: ['ADMIN_ORG']
        }
      },
      {
        path: 'organizations',
        redirectTo: 'super-admin/organizations',
        pathMatch: 'full'
      },
      {
        path: 'organizations/new',
        redirectTo: 'super-admin/organizations/create',
        pathMatch: 'full'
      },
      {
        path: 'organizations/edit/:id',
        redirectTo: 'super-admin/organizations/:id/edit',
        pathMatch: 'full'
      },
      {
        path: 'organizations/:id',
        redirectTo: 'super-admin/organizations/:id',
        pathMatch: 'full'
      },
      {
        path: 'profile',
        loadComponent: () => import('./features/profile/profile.component').then(m => m.ProfileComponent),
        data: { title: 'My Profile' }
      },
      {
        path: 'settings',
        loadComponent: () => import('./features/settings/settings.component').then(m => m.SettingsComponent),
        data: { title: 'Settings' }
      }
    ]
  },
  {
    path: 'forbidden',
    loadComponent: () => import('./shared/forbidden/forbidden.component').then(m => m.ForbiddenComponent),
    data: { title: 'Access Forbidden' }
  },
  {
    path: '404',
    loadComponent: () => import('./shared/not-found/not-found.component').then(m => m.NotFoundComponent),
    data: { title: 'Page Not Found' }
  },
  {
    path: '**',
    redirectTo: '/404'
  }
];
