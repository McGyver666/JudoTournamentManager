import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthStateService } from './auth-state.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStateService);
  const token = auth.token();

  if (!req.url.startsWith('api/')) {
    return next(req);
  }

  const setHeaders: Record<string, string> = {};
  if (token) {
    setHeaders['Authorization'] = `Bearer ${token}`;
  }

  if (!['GET', 'HEAD', 'OPTIONS'].includes(req.method)) {
    setHeaders['X-Requested-With'] = 'ShiaiManager';
  }

  const cloned = req.clone({
    withCredentials: true,
    setHeaders,
  });

  return next(cloned);
};
