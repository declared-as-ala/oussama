import { HttpInterceptorFn, HttpRequest, HttpHandlerFn, HttpEvent, HttpErrorResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';
import { NotificationService } from '../services/notification.service';
import { BehaviorSubject, Observable, throwError } from 'rxjs';
import { catchError, filter, switchMap, take } from 'rxjs/operators';

let isRefreshing = false;
const refreshTokenSubject: BehaviorSubject<string | null> = new BehaviorSubject<string | null>(null);

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
  const notificationService = inject(NotificationService);
  const token = authService.getAccessToken();
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
  const isPublicAuthEndpoint = publicAuthEndpoints.some(endpoint => req.url.includes(endpoint));

  // Ajouter le token d'authentification si disponible
  let authReq = req;
  if (token && !isPublicAuthEndpoint) {
    authReq = req.clone({
      headers: req.headers.set('Authorization', `Bearer ${token}`)
    });
  }

  return next(authReq).pipe(
    catchError((error) => {
      // Si on reçoit une erreur 401 et que ce n'est pas un endpoint public, tenter de rafraîchir le token
      if (error instanceof HttpErrorResponse && error.status === 401 && !isPublicAuthEndpoint) {
        const sentToken = authReq.headers.get('Authorization')?.replace('Bearer ', '');
        const currentToken = authService.getAccessToken();

        // Si le token envoyé est le même que le token actuel, il faut rafraîchir
        if (!sentToken || sentToken === currentToken) {
          return handle401Error(authReq, next, authService, notificationService);
        } else if (currentToken) {
          // Le token a déjà été rafraîchi par une autre requête, on réessaie avec le nouveau token
          return next(req.clone({
            headers: req.headers.set('Authorization', `Bearer ${currentToken}`)
          }));
        }
      }
      return throwError(() => error);
    })
  );
};

function handle401Error(
  request: HttpRequest<any>,
  next: HttpHandlerFn,
  authService: AuthService,
  notificationService: NotificationService
): Observable<HttpEvent<any>> {
  if (!isRefreshing) {
    isRefreshing = true;
    refreshTokenSubject.next(null);

    return authService.refreshToken().pipe(
      switchMap((response) => {
        isRefreshing = false;
        const newToken = response.accessToken;
        refreshTokenSubject.next(newToken);

        // Réessayer la requête initiale avec le nouveau token
        return next(request.clone({
          headers: request.headers.set('Authorization', `Bearer ${newToken}`)
        }));
      }),
      catchError((err) => {
        isRefreshing = false;
        refreshTokenSubject.next(null);

        // Déconnexion forcée et affichage du toast d'erreur
        authService.forceLogout();
        notificationService.showError('Votre session a expiré. Veuillez vous reconnecter.');

        return throwError(() => err);
      })
    );
  } else {
    // Si un rafraîchissement est déjà en cours, on attend le nouveau token
    return refreshTokenSubject.pipe(
      filter(token => token !== null),
      take(1),
      switchMap((newToken) => {
        return next(request.clone({
          headers: request.headers.set('Authorization', `Bearer ${newToken!}`)
        }));
      }),
      catchError((err) => {
        return throwError(() => err);
      })
    );
  }
}
