import { ApplicationConfig, provideZoneChangeDetection, provideAppInitializer, inject } from '@angular/core';
import { provideRouter } from '@angular/router';
import { provideHttpClient, withInterceptors } from '@angular/common/http';

import { routes } from './app.routes';
import { I18nService } from './core/i18n.service';
import { authInterceptor } from './core/auth.interceptor';
import { AuthStateService } from './core/auth-state.service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideAppInitializer(() => inject(I18nService).init()),
    provideAppInitializer(() => inject(AuthStateService).init()),
  ],
};
