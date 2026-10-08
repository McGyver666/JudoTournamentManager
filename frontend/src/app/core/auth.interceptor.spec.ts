import { HttpHandlerFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom, of } from 'rxjs';
import { AuthStateService } from './auth-state.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  it('adds bearer token for api/* requests', async () => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthStateService,
          useValue: {
            token: () => 'token-123',
          },
        },
      ],
    });

    const req = new HttpRequest('GET', 'api/tournaments');
    let captured = null as HttpRequest<unknown> | null;

    const next: HttpHandlerFn = (forwarded) => {
      captured = forwarded;
      return of(new HttpResponse({ status: 200 }));
    };

    await firstValueFrom(TestBed.runInInjectionContext(() => authInterceptor(req, next)));

    expect(captured).not.toBeNull();
    expect(captured!.headers.get('Authorization')).toBe('Bearer token-123');
    expect(captured!.withCredentials).toBe(true);
  });

  it('adds credentials and the CSRF header for API state changes', async () => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthStateService,
          useValue: {
            token: () => null,
          },
        },
      ],
    });

    const req = new HttpRequest('POST', 'api/tournaments', {});
    let captured = null as HttpRequest<unknown> | null;

    const next: HttpHandlerFn = (forwarded) => {
      captured = forwarded;
      return of(new HttpResponse({ status: 200 }));
    };

    await firstValueFrom(TestBed.runInInjectionContext(() => authInterceptor(req, next)));

    expect(captured).not.toBeNull();
    expect(captured!.headers.has('Authorization')).toBe(false);
    expect(captured!.headers.get('X-Requested-With')).toBe('ShiaiManager');
    expect(captured!.withCredentials).toBe(true);
  });

  it('does not add token for non-api requests', async () => {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: AuthStateService,
          useValue: {
            token: () => 'token-123',
          },
        },
      ],
    });

    const req = new HttpRequest('GET', '/assets/i18n/de.json');
    let captured = null as HttpRequest<unknown> | null;

    const next: HttpHandlerFn = (forwarded) => {
      captured = forwarded;
      return of(new HttpResponse({ status: 200 }));
    };

    await firstValueFrom(TestBed.runInInjectionContext(() => authInterceptor(req, next)));

    expect(captured).not.toBeNull();
    expect(captured!.headers.has('Authorization')).toBe(false);
    expect(captured!.withCredentials).toBe(false);
  });
});
