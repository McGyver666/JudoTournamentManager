import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ApiService } from './api.service';
import { AuthStateService } from './auth-state.service';

describe('AuthStateService', () => {
  const tokenKey = 'judo.auth.token';
  const expiresKey = 'judo.auth.expires';

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

  it('restores valid token from localStorage', () => {
    localStorage.setItem(tokenKey, 'stored-token');
    localStorage.setItem(expiresKey, new Date(Date.now() + 60_000).toISOString());

    const service = createService();

    expect(service.token()).toBe('stored-token');
  });

  it('clears expired token from localStorage', () => {
    localStorage.setItem(tokenKey, 'expired-token');
    localStorage.setItem(expiresKey, new Date(Date.now() - 60_000).toISOString());

    const service = createService();

    expect(service.token()).toBeNull();
    expect(localStorage.getItem(tokenKey)).toBeNull();
    expect(localStorage.getItem(expiresKey)).toBeNull();
  });

  it('init() clears session when me() fails for stored token', async () => {
    localStorage.setItem(tokenKey, 'stored-token');
    localStorage.setItem(expiresKey, new Date(Date.now() + 60_000).toISOString());
    apiSpy.me.and.returnValue(throwError(() => new Error('unauthorized')));

    const service = createService();
    await service.init();

    expect(service.token()).toBeNull();
    expect(service.user()).toBeNull();
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
    expect(service.token()).toBe('login-token');
    expect(service.isAuthenticated()).toBeTrue();
    expect(localStorage.getItem(tokenKey)).toBe('login-token');
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
