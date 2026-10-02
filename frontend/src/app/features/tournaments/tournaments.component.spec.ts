import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { throwError } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { I18nService } from '../../core/i18n.service';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TournamentsComponent } from './tournaments.component';

class AuthStateServiceStub {
  readonly canOperate = signal(true);
  readonly isAdmin = signal(true);
}

class TournamentContextServiceStub {
  readonly tournamentId = signal<string | null>(null);
}

class I18nServiceStub {
  private readonly values: Record<string, string> = {
    'tournaments.restoreFailed': 'Backup konnte nicht wiederhergestellt werden.',
    'tournaments.restoreTooLarge': 'Die Backup-Datei ist zu groß (maximal {maxMb} MB).',
  };

  translate(key: string, params?: Record<string, string | number>): string {
    const template = this.values[key] ?? key;
    return template.replace(/\{(\w+)\}/g, (_m, token: string) => String(params?.[token] ?? `{${token}}`));
  }
}

describe('TournamentsComponent restore', () => {
  let apiSpy: jasmine.SpyObj<ApiService>;
  let component: TournamentsComponent;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj<ApiService>('ApiService', ['getTournaments', 'restoreTournamentBackup']);

    await TestBed.configureTestingModule({
      imports: [TournamentsComponent],
      providers: [
        { provide: ApiService, useValue: apiSpy },
        { provide: AuthStateService, useClass: AuthStateServiceStub },
        { provide: TournamentContextService, useClass: TournamentContextServiceStub },
        { provide: I18nService, useClass: I18nServiceStub },
      ],
    }).compileComponents();

    component = TestBed.createComponent(TournamentsComponent).componentInstance;
  });

  function fileChangeEvent(): Event {
    const input = document.createElement('input');
    input.type = 'file';
    Object.defineProperty(input, 'files', { value: [new File(['{}'], 'backup.json')] });
    return { target: input } as unknown as Event;
  }

  it('shows a German size hint when the proxy rejects the upload with 413', async () => {
    apiSpy.restoreTournamentBackup.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 413,
      statusText: 'Request Entity Too Large',
      error: '<html><body>413 Request Entity Too Large</body></html>',
    })));

    await (component as any).restoreFromFile(fileChangeEvent());

    expect((component as any).error()).toBe('Die Backup-Datei ist zu groß (maximal 50 MB).');
    expect((component as any).restoring()).toBeFalse();
  });

  it('keeps the generic message for other non-ProblemDetails failures', async () => {
    apiSpy.restoreTournamentBackup.and.returnValue(throwError(() => new HttpErrorResponse({
      status: 502,
      error: '<html>Bad Gateway</html>',
    })));

    await (component as any).restoreFromFile(fileChangeEvent());

    expect((component as any).error()).toBe('Backup konnte nicht wiederhergestellt werden.');
  });
});
