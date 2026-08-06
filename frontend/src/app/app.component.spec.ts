import { WritableSignal, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { AppComponent } from './app.component';
import { ApiService } from './core/api.service';
import { AuthStateService } from './core/auth-state.service';
import { I18nService } from './core/i18n.service';
import { Tatami } from './core/models';
import { ThemeService } from './core/theme.service';
import { TournamentContextService } from './core/tournament-context.service';

/**
 * Shell navigation tests (Category=UnitTest): role-gated entries render only
 * for the permitted role, and the per-Tatami Match/Display sections render
 * dynamically from the loaded tatami signals.
 */
describe('AppComponent shell navigation', () => {
  let auth: {
    isAuthenticated: WritableSignal<boolean>;
    isAdmin: WritableSignal<boolean>;
    canOperate: WritableSignal<boolean>;
    user: WritableSignal<{ userId: string; userName: string; role: string } | null>;
  };

  function createTatami(overrides: Partial<Tatami> = {}): Tatami {
    return {
      id: 'tatami-1',
      tournamentId: 'tournament-1',
      name: 'Matte 1',
      displayOrder: 1,
      isActive: true,
      createdAtUtc: new Date().toISOString(),
      updatedAtUtc: new Date().toISOString(),
      ...overrides,
    };
  }

  function configure(tatamis: Tatami[] = []): void {
    auth = {
      isAuthenticated: signal(true),
      isAdmin: signal(false),
      canOperate: signal(false),
      user: signal({ userId: 'u1', userName: 'M. Kaminski', role: 'Operator' }),
    };

    TestBed.configureTestingModule({
      imports: [AppComponent],
      providers: [
        provideRouter([]),
        { provide: AuthStateService, useValue: auth },
        { provide: I18nService, useValue: { translate: (key: string) => key, language: signal('de'), use: () => undefined } },
        { provide: ThemeService, useValue: { theme: signal('light'), toggle: () => undefined } },
        { provide: TournamentContextService, useValue: { tournamentId: signal('tournament-1'), tournament: signal({ name: 'Testturnier' }) } },
        { provide: ApiService, useValue: { getTatamis: () => of(tatamis) } },
      ],
    });
  }

  function render() {
    const fixture = TestBed.createComponent(AppComponent);
    fixture.detectChanges();
    return fixture;
  }

  afterEach(() => TestBed.resetTestingModule());

  it('hides operator- and admin-only entries for a display-only user', () => {
    configure();
    auth.canOperate.set(false);
    auth.isAdmin.set(false);
    const el = render().nativeElement as HTMLElement;

    expect(el.querySelector('a[href="/tournaments"]')).not.toBeNull();
    expect(el.querySelector('a[href="/results"]')).not.toBeNull();
    expect(el.querySelector('a[href="/config"]')).toBeNull();
    expect(el.querySelector('a[href="/registrations"]')).toBeNull();
    expect(el.querySelector('a[href="/users"]')).toBeNull();
    expect(el.querySelector('button[title="nav.match"]')).toBeNull();
    // Display section is not operator-gated and stays available.
    expect(el.querySelector('button[title="nav.display"]')).not.toBeNull();
  });

  it('shows operator entries but not admin entries for an operator', () => {
    configure();
    auth.canOperate.set(true);
    auth.isAdmin.set(false);
    const el = render().nativeElement as HTMLElement;

    expect(el.querySelector('a[href="/config"]')).not.toBeNull();
    expect(el.querySelector('a[href="/registrations"]')).not.toBeNull();
    expect(el.querySelector('a[href="/draw"]')).not.toBeNull();
    expect(el.querySelector('button[title="nav.match"]')).not.toBeNull();
    expect(el.querySelector('a[href="/users"]')).toBeNull();
  });

  it('shows the admin user-management entry for an admin', () => {
    configure();
    auth.canOperate.set(true);
    auth.isAdmin.set(true);
    const el = render().nativeElement as HTMLElement;

    expect(el.querySelector('a[href="/users"]')).not.toBeNull();
  });

  it('renders the login entry when unauthenticated', () => {
    configure();
    auth.isAuthenticated.set(false);
    const el = render().nativeElement as HTMLElement;

    expect(el.querySelector('a[href="/login"]')).not.toBeNull();
    expect(el.querySelector('a[href="/tournaments"]')).toBeNull();
  });

  it('renders one Match sub-entry per active tatami from the signal', () => {
    configure([
      createTatami({ id: 't1', name: 'Matte 1', isActive: true, displayOrder: 1 }),
      createTatami({ id: 't2', name: 'Matte 2', isActive: false, displayOrder: 2 }),
      createTatami({ id: 't3', name: 'Matte 3', isActive: true, displayOrder: 3 }),
    ]);
    auth.canOperate.set(true);
    const fixture = render();
    (fixture.componentInstance as unknown as { matchMenuOpen: WritableSignal<boolean> }).matchMenuOpen.set(true);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const matchLinks = Array.from(el.querySelectorAll('a[href^="/match"]'));
    expect(matchLinks.length).toBe(2);
    const names = matchLinks.map((a) => a.textContent?.trim());
    expect(names).toContain('Matte 1');
    expect(names).toContain('Matte 3');
    expect(names).not.toContain('Matte 2');
  });

  it('renders overview, match-lists and one display sub-entry per tatami', () => {
    configure([
      createTatami({ id: 't1', name: 'Matte 1', isActive: true, displayOrder: 1 }),
      createTatami({ id: 't2', name: 'Matte 2', isActive: false, displayOrder: 2 }),
    ]);
    const fixture = render();
    (fixture.componentInstance as unknown as { displayMenuOpen: WritableSignal<boolean> }).displayMenuOpen.set(true);
    fixture.detectChanges();

    const el = fixture.nativeElement as HTMLElement;
    const subitems = Array.from(el.querySelectorAll('.nav-subitem'));
    // overview + match-lists + 2 per-tatami display links
    expect(subitems.length).toBe(4);
    expect(el.querySelector('a[href^="/display?"]')).not.toBeNull();
    expect(el.querySelector('a[href^="/display/match-lists"]')).not.toBeNull();
    expect(el.querySelectorAll('a[href^="/display/tatami/"]').length).toBe(2);
  });
});
