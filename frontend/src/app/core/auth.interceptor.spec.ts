import { HttpHandlerFn, HttpRequest, HttpResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { AuthStateService } from './auth-state.service';
import { authInterceptor } from './auth.interceptor';

describe('authInterceptor', () => {
  it('adds bearer token for api/* requests', (done) => {
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
    let captured: HttpRequest<unknown> | null = null;

    const next: HttpHandlerFn = (forwarded) => {
      captured = forwarded;
      return of(new HttpResponse({ status: 200 }));
    };

    TestBed.runInInjectionContext(() => {
      authInterceptor(req, next).subscribe({
        next: () => {
          expect(captured).not.toBeNull();
          expect(captured!.headers.get('Authorization')).toBe('Bearer token-123');
          expect(captured!.withCredentials).toBeTrue();
          done();
        },
        error: done.fail,
      });
    });
  });

  it('adds credentials and the CSRF header for API state changes', (done) => {
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
    let captured: HttpRequest<unknown> | null = null;

    const next: HttpHandlerFn = (forwarded) => {
      captured = forwarded;
      return of(new HttpResponse({ status: 200 }));
    };

    TestBed.runInInjectionContext(() => {
      authInterceptor(req, next).subscribe({
        next: () => {
          expect(captured).not.toBeNull();
          expect(captured!.headers.has('Authorization')).toBeFalse();
          expect(captured!.headers.get('X-Requested-With')).toBe('ShiaiManager');
          expect(captured!.withCredentials).toBeTrue();
          done();
        },
        error: done.fail,
      });
    });
  });

  it('does not add token for non-api requests', (done) => {
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
    let captured: HttpRequest<unknown> | null = null;

    const next: HttpHandlerFn = (forwarded) => {
      captured = forwarded;
      return of(new HttpResponse({ status: 200 }));
    };

    TestBed.runInInjectionContext(() => {
      authInterceptor(req, next).subscribe({
        next: () => {
          expect(captured).not.toBeNull();
          expect(captured!.headers.has('Authorization')).toBeFalse();
          expect(captured!.withCredentials).toBeFalse();
          done();
        },
        error: done.fail,
      });
    });
  });
});
