import type { MockedObject } from 'vitest';
import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { I18nService } from '../../core/i18n.service';
import { AuthenticatedUser, LocalUserAccount } from '../../core/models';
import { UserManagementComponent } from './user-management.component';

class AuthStateServiceStub {
  readonly user = signal<AuthenticatedUser | null>({
    userId: 'admin-id',
    userName: 'admin',
    role: 'Admin',
  });
}

class I18nServiceStub {
  private readonly values: Record<string, string> = {
    'common.confirmDelete': 'delete?',
    'errors.delete': 'Löschen fehlgeschlagen.',
    'users.deleted': 'Benutzer gelöscht.',
  };

  translate(key: string): string {
    return this.values[key] ?? key;
  }
}

describe('UserManagementComponent', () => {
  const user: LocalUserAccount = {
    id: 'operator-id',
    userName: 'operator1',
    role: 'Operator',
    isActive: true,
    createdUtc: '2026-01-01T00:00:00Z',
    updatedUtc: '2026-01-01T00:00:00Z',
  };

  let apiSpy: MockedObject<any>;
  let component: UserManagementComponent;

  beforeEach(async () => {
    apiSpy = {
      getUsers: vi.fn().mockName('ApiService.getUsers'),
      deleteUser: vi.fn().mockName('ApiService.deleteUser')
    };

    await TestBed.configureTestingModule({
      imports: [UserManagementComponent],
      providers: [
        { provide: ApiService, useValue: apiSpy },
        { provide: AuthStateService, useClass: AuthStateServiceStub },
        { provide: I18nService, useClass: I18nServiceStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(UserManagementComponent);
    component = fixture.componentInstance;
  });

  it('removes a confirmed user deletion from local state', () => {
    apiSpy.getUsers.mockReturnValue(of([user]));
    apiSpy.deleteUser.mockReturnValue(of(void 0));
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    component.ngOnInit();
    (component as any).deleteUser(user);

    expect(apiSpy.deleteUser).toHaveBeenCalledWith('operator-id');
    expect((component as any).users()).toEqual([]);
    expect((component as any).info()).toBe('Benutzer gelöscht.');
  });

  it('does not delete the signed-in user', () => {
    const signedInUser = { ...user, id: 'admin-id', userName: 'admin', role: 'Admin' as const };
    apiSpy.getUsers.mockReturnValue(of([signedInUser]));
    vi.spyOn(window, 'confirm').mockReturnValue(false);

    component.ngOnInit();
    (component as any).deleteUser(signedInUser);

    expect(apiSpy.deleteUser).not.toHaveBeenCalled();
    expect(window.confirm).not.toHaveBeenCalled();
  });

  it('shows the API error when deletion fails', () => {
    apiSpy.getUsers.mockReturnValue(of([user]));
    apiSpy.deleteUser.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 409, error: { detail: 'last admin' } })));
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    component.ngOnInit();
    (component as any).deleteUser(user);

    expect((component as any).error()).toBe('last admin');
  });
});
