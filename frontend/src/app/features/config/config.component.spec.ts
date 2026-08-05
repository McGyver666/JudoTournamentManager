import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { I18nService } from '../../core/i18n.service';
import { Club, Tournament } from '../../core/models';
import { TournamentContextService } from '../../core/tournament-context.service';
import { ConfigComponent } from './config.component';

class TournamentContextStub {
  readonly tournamentId = signal<string | null>(null);
  readonly tournament = signal<Tournament | null>(null);
}

class AuthStateStub {
  readonly canOperate = signal(true);
}

class I18nServiceStub {
  private readonly values: Record<string, string> = {
    'common.confirmDelete': 'delete?',
    'errors.delete': 'Löschen fehlgeschlagen.',
    'errors.clubHasAthletes':
      'Der Verein kann nicht gelöscht werden, solange ihm Athleten zugeordnet sind. Bitte Athleten zuerst entfernen oder einem anderen Verein zuordnen.',
  };

  translate(key: string): string {
    return this.values[key] ?? key;
  }
}

describe('ConfigComponent', () => {
  const club: Club = {
    id: 'club-1',
    tournamentId: 't-1',
    name: 'DJK Test',
    contactName: null,
    contactEmail: null,
    contactPhone: null,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z',
  };

  let apiSpy: jasmine.SpyObj<Pick<ApiService, 'deleteClub'>>;
  let context: TournamentContextStub;
  let component: ConfigComponent;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj<Pick<ApiService, 'deleteClub'>>('ApiService', ['deleteClub']);
    context = new TournamentContextStub();

    await TestBed.configureTestingModule({
      imports: [ConfigComponent],
      providers: [
        { provide: ApiService, useValue: apiSpy },
        { provide: TournamentContextService, useValue: context },
        { provide: AuthStateService, useClass: AuthStateStub },
        { provide: I18nService, useClass: I18nServiceStub },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(ConfigComponent);
    component = fixture.componentInstance;
  });

  it('shows a specific localized message when deleting a club fails with HTTP 409', () => {
    context.tournamentId.set('t-1');
    apiSpy.deleteClub.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: { title: 'Verein hat Athleten.' },
          }),
      ),
    );
    spyOn(window, 'confirm').and.returnValue(true);

    (component as any).deleteClub(club);

    expect(apiSpy.deleteClub).toHaveBeenCalledWith('t-1', 'club-1');
    expect((component as any).error()).toBe(
      'Der Verein kann nicht gelöscht werden, solange ihm Athleten zugeordnet sind. Bitte Athleten zuerst entfernen oder einem anderen Verein zuordnen.',
    );
  });

  it('keeps generic delete error handling for non-409 club delete failures', () => {
    context.tournamentId.set('t-1');
    apiSpy.deleteClub.and.returnValue(throwError(() => new Error('network')));
    spyOn(window, 'confirm').and.returnValue(true);

    (component as any).deleteClub(club);

    expect((component as any).error()).toBe('Löschen fehlgeschlagen.');
  });

  it('removes the club from local state after successful delete', () => {
    context.tournamentId.set('t-1');
    apiSpy.deleteClub.and.returnValue(of(void 0));
    spyOn(window, 'confirm').and.returnValue(true);
    (component as any).clubs.set([club]);

    (component as any).deleteClub(club);

    expect((component as any).clubs()).toEqual([]);
    expect((component as any).error()).toBeNull();
  });
});
