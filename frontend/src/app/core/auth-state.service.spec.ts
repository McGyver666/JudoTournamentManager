import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ApiService } from './api.service';
import { AuthStateService } from './auth-state.service';

describe('AuthStateService', () => {
  let apiSpy: jasmine.SpyObj<Pick<ApiService, 'me' | 'login' | 'logout'>>;

  beforeEach(() => {
    localStorage.clear();
    apiSpy = jasmine.createSpyObj<Pick<ApiService, 'me' | 'login' | 'logout'>>('ApiService', ['me', 'login', 'logout']);
  });

  function createService(): AuthStateService {
    TestBed.configureTestingModule({
      providers: [{ provide: ApiService, useValue: apiSpy }],
    });

    return TestBed.inject(AuthStateService);
  }

  it('does not restore an auth token from localStorage', () => {
    localStorage.setItem('judo.auth.token', 'stored-token');
    localStorage.setItem('judo.auth.expires', new Date(Date.now() + 60_000).toISOString());

    const service = createService();

    expect(service.token()).toBeNull();
  });

  it('restores the authenticated user through me()', async () => {
    apiSpy.me.and.returnValue(
      of({
        userId: 'u1',
        userName: 'admin',
        role: 'Admin',
      }),
    );

    const service = createService();
    await service.init();

    expect(service.user()).toEqual({ userId: 'u1', userName: 'admin', role: 'Admin' });
    expect(service.isAuthenticated()).toBeTrue();
    expect(service.token()).toBeNull();
  });

  it('login() stores token and resolves true on success', async () => {
    apiSpy.login.and.returnValue(
      of({
        accessToken: 'login-token',
        expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
        userName: 'admin',
        role: 'Admin',
      }),
    );

    apiSpy.me.and.returnValue(
      of({
        userId: 'u1',
        userName: 'admin',
        role: 'Admin',
      }),
    );

    const service = createService();
    const ok = await service.login('admin', 'pw');

    expect(ok).toBeTrue();
    expect(service.token()).toBeNull();
    expect(service.isAuthenticated()).toBeTrue();
    expect(localStorage.getItem('judo.auth.token')).toBeNull();
    expect(localStorage.getItem('judo.auth.expires')).toBeNull();
  });

  it('logout() clears session even when API logout fails', async () => {
    apiSpy.login.and.returnValue(
      of({
        accessToken: 'login-token',
        expiresAtUtc: new Date(Date.now() + 60_000).toISOString(),
        userName: 'operator',
        role: 'Operator',
      }),
    );

    apiSpy.me.and.returnValue(
      of({
        userId: 'u2',
        userName: 'operator',
        role: 'Operator',
      }),
    );

    apiSpy.logout.and.returnValue(throwError(() => new Error('network')));

    const service = createService();
    await service.login('operator', 'pw');
    await service.logout();

    expect(service.token()).toBeNull();
    expect(service.user()).toBeNull();
  });
});
