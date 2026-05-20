import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthService } from '../services/auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthService);
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
  if (token && !isPublicAuthEndpoint) {
    const authReq = req.clone({
      headers: req.headers.set('Authorization', `Bearer ${token}`)
    });
    return next(authReq);
  }

  return next(req);
};
