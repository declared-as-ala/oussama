import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { LoadingService } from '../services/loading.service';

export const loadingInterceptor: HttpInterceptorFn = (req, next) => {
  const loadingService = inject(LoadingService);

  const hasSkipHeader = req.headers.has('X-Skip-Loading');
  const skipLoading = hasSkipHeader || shouldSkipGlobalLoading(req);
  const request = hasSkipHeader ? req.clone({ headers: req.headers.delete('X-Skip-Loading') }) : req;

  if (!skipLoading) {
    loadingService.show();
  }

  return next(request).pipe(
    finalize(() => {
      if (!skipLoading) {
        loadingService.hide();
      }
    })
  );
};

function shouldSkipGlobalLoading(req: HttpRequest<unknown>): boolean {
  const url = req.url.toLowerCase();

  if (req.method === 'GET') {
    return true;
  }

  return url.includes('/api/notifications') || url.includes('/hubs/notifications');
}
