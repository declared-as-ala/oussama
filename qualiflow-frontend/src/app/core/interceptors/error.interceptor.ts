import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../services/auth.service';
import { NotificationService } from '../services/notification.service';

export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const notificationService = inject(NotificationService);
  const authService = inject(AuthService);
  const publicAuthEndpoints = [
    '/api/auth/register',
    '/api/auth/login',
    '/api/auth/refresh-token',
    '/api/auth/forgot-password',
    '/api/auth/reset-password',
    '/api/auth/verify-email',
    '/api/auth/verify-email-code',
    '/api/auth/resend-verification-code'
  ];
  const isPublicAuthRequest = publicAuthEndpoints.some(endpoint => req.url.includes(endpoint));
  const isNotificationRequest = req.url.includes('/api/notifications') || req.url.includes('/hubs/notifications');

  return next(req).pipe(
    catchError((error) => {
      // Missing org logo is an expected state for many tenants.
      // Let callers handle fallback rendering without a global toast.
      if (
        error?.status === 404 &&
        req.method === 'GET' &&
        req.url.includes('/api/organizations/my/logo')
      ) {
        return throwError(() => error);
      }

      let errorMessage = 'Une erreur est survenue';
      const shouldSilenceNotificationError =
        isNotificationRequest &&
        (error?.status === 401 || error?.status === 403);

      if (shouldSilenceNotificationError) {
        return throwError(() => error);
      }

      if (error.error instanceof ErrorEvent) {
        errorMessage = `Erreur: ${error.error.message}`;
      } else {
        switch (error.status) {
          case 0:
            errorMessage = "Impossible de se connecter au serveur. Veuillez vérifier votre connexion internet.";
            if (authService.isAuthenticated() && !isPublicAuthRequest) {
              authService.forceLogout();
            }
            break;
          case 401:
            if (error?.error?.requiresEmailVerification) {
              // Email verification flow is handled by the login component without global toasts.
              return throwError(() => error);
            }

            if (isPublicAuthRequest) {
              // Public auth requests (login, register) handle their own error displays.
              return throwError(() => error);
            }

            if (authService.isAuthenticated()) {
              errorMessage = error.error?.message || 'Votre session a expiré. Veuillez vous reconnecter.';
              authService.forceLogout();
            } else {
              // Silent fallback for guest/unauthenticated calls.
              return throwError(() => error);
            }
            break;
          case 403:
            errorMessage = 'Acces interdit.';
            break;
          case 404:
            errorMessage = 'Ressource non trouvee.';
            break;
          case 500:
            errorMessage = error.error?.message || 'Erreur serveur interne.';
            break;
          default:
            errorMessage = error.error?.message || `Erreur ${error.status}: ${error.statusText}`;
            break;
        }
      }

      notificationService.showError(errorMessage);
      return throwError(() => error);
    })
  );
};
