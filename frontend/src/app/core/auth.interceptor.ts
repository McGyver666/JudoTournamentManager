import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthStateService } from './auth-state.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const auth = inject(AuthStateService);
  const token = auth.token();

  if (!token || !req.url.startsWith('api/')) {
    return next(req);
  }

  const cloned = req.clone({
    setHeaders: {
      Authorization: `Bearer ${token}`,
    },
  });

  return next(cloned);
};
