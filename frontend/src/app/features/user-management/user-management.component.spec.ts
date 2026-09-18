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

  let apiSpy: jasmine.SpyObj<any>;
  let component: UserManagementComponent;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj('ApiService', ['getUsers', 'deleteUser']);

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
    apiSpy.getUsers.and.returnValue(of([user]));
    apiSpy.deleteUser.and.returnValue(of(void 0));
    spyOn(window, 'confirm').and.returnValue(true);

    component.ngOnInit();
    (component as any).deleteUser(user);

    expect(apiSpy.deleteUser).toHaveBeenCalledWith('operator-id');
    expect((component as any).users()).toEqual([]);
    expect((component as any).info()).toBe('Benutzer gelöscht.');
  });

  it('does not delete the signed-in user', () => {
    const signedInUser = { ...user, id: 'admin-id', userName: 'admin', role: 'Admin' as const };
    apiSpy.getUsers.and.returnValue(of([signedInUser]));
    spyOn(window, 'confirm');

    component.ngOnInit();
    (component as any).deleteUser(signedInUser);

    expect(apiSpy.deleteUser).not.toHaveBeenCalled();
    expect(window.confirm).not.toHaveBeenCalled();
  });

  it('shows the API error when deletion fails', () => {
    apiSpy.getUsers.and.returnValue(of([user]));
    apiSpy.deleteUser.and.returnValue(
      throwError(() => new HttpErrorResponse({ status: 409, error: { detail: 'last admin' } })),
    );
    spyOn(window, 'confirm').and.returnValue(true);

    component.ngOnInit();
    (component as any).deleteUser(user);

    expect((component as any).error()).toBe('last admin');
  });
});
