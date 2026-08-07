import { Component, OnDestroy, OnInit, computed, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { NavigationEnd, Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { filter, Subscription } from 'rxjs';
import { TranslatePipe } from './core/translate.pipe';
import { I18nService, AppLanguage } from './core/i18n.service';
import { ThemeService } from './core/theme.service';
import { TournamentContextService } from './core/tournament-context.service';
import { AuthStateService } from './core/auth-state.service';
import { ApiService } from './core/api.service';
import { APP_VERSION } from './core/app-info';
import { Tatami } from './core/models';

/**
 * Application shell: SHIAI left sidebar, slim top bar, active-tournament
 * indicator, theme toggle and the language switcher. All visible labels are
 * resolved through the translation pipe so the UI stays fully localizable.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe, DatePipe],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly i18n = inject(I18nService);
  private readonly themeService = inject(ThemeService);
  private readonly auth = inject(AuthStateService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  protected readonly context = inject(TournamentContextService);

  protected readonly language = this.i18n.language;
  protected readonly theme = this.themeService.theme;
  protected readonly isAuthenticated = this.auth.isAuthenticated;
  protected readonly isAdmin = this.auth.isAdmin;
  protected readonly canOperate = this.auth.canOperate;
  protected readonly currentUser = this.auth.user;
  protected readonly displayTatamis = signal<Tatami[]>([]);
  protected readonly activeTatamis = computed(() =>
    this.displayTatamis().filter((tatami) => tatami.isActive));
  /** Sidebar footer + nav-badge metadata (shell parity with the design mockup). */
  protected readonly appVersion = APP_VERSION;
  /** Live nav-item count badges; null hides the badge (also offline-safe on error). */
  protected readonly tournamentCount = signal<number | null>(null);
  protected readonly categoryCount = signal<number | null>(null);
  protected readonly tatamiCount = computed(() => this.displayTatamis().length);
  /** Expandable per-Tatami "Anzeigetafel" (display) section in the sidebar. */
  protected readonly displayMenuOpen = signal(false);
  /** Expandable per-Tatami "Mattenrichter" (match) section in the sidebar. */
  protected readonly matchMenuOpen = signal(false);
  /** Desktop rail: collapses the sidebar to a kanji-only glyph rail. */
  protected readonly sidebarCollapsed = signal(false);
  /** Narrow screens: off-canvas hamburger drawer with full labels. */
  protected readonly drawerOpen = signal(false);
  protected readonly showShell = signal(true);
  protected readonly routeTatamiId = signal<string | null>(null);

  private shellRouteSub?: Subscription;

  private readonly loadTatamisEffect = effect((onCleanup) => {
    const tournamentId = this.context.tournamentId();
    const authenticated = this.isAuthenticated();

    if (!authenticated || !tournamentId) {
      this.displayTatamis.set([]);
      return;
    }

    const sub = this.loadTatamis(tournamentId);

    onCleanup(() => sub.unsubscribe());
  });

  /** Loads the tournament-count badge whenever the user is authenticated. */
  private readonly loadTournamentCountEffect = effect((onCleanup) => {
    if (!this.isAuthenticated()) {
      this.tournamentCount.set(null);
      return;
    }

    const sub = this.api.getTournaments().subscribe({
      next: (tournaments) => this.tournamentCount.set(tournaments.length),
      error: () => this.tournamentCount.set(null),
    });

    onCleanup(() => sub.unsubscribe());
  });

  /** Loads the category-count badge for the active tournament. */
  private readonly loadCategoryCountEffect = effect((onCleanup) => {
    const tournamentId = this.context.tournamentId();
    const authenticated = this.isAuthenticated();

    if (!authenticated || !tournamentId) {
      this.categoryCount.set(null);
      return;
    }

    const sub = this.api.getCategories(tournamentId).subscribe({
      next: (categories) => this.categoryCount.set(categories.length),
      error: () => this.categoryCount.set(null),
    });

    onCleanup(() => sub.unsubscribe());
  });

  ngOnInit(): void {
    this.updateShellVisibility(this.router.url);
    this.shellRouteSub = this.router.events
      .pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd))
      .subscribe((event) => this.updateShellVisibility(event.urlAfterRedirects));
  }

  ngOnDestroy(): void {
    this.shellRouteSub?.unsubscribe();
  }

  protected switchLanguage(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as AppLanguage;
    this.i18n.use(value);
  }

  protected toggleTheme(): void {
    this.themeService.toggle();
  }

  protected toggleSidebar(): void {
    this.sidebarCollapsed.update((collapsed) => !collapsed);
  }

  protected toggleDrawer(): void {
    this.drawerOpen.update((open) => !open);
  }

  protected closeDrawer(): void {
    this.drawerOpen.set(false);
  }

  protected toggleDisplayMenu(): void {
    if (this.sidebarCollapsed()) {
      this.sidebarCollapsed.set(false);
    }
    const willOpen = !this.displayMenuOpen();
    this.displayMenuOpen.update((open) => !open);
    if (willOpen) {
      this.refreshTatamis();
    }
  }

  protected toggleMatchMenu(): void {
    if (this.sidebarCollapsed()) {
      this.sidebarCollapsed.set(false);
    }
    const willOpen = !this.matchMenuOpen();
    this.matchMenuOpen.update((open) => !open);
    if (willOpen) {
      this.refreshTatamis();
    }
  }

  protected displayOverviewUrl(tournamentId: string): string {
    return `/display?tournamentId=${encodeURIComponent(tournamentId)}`;
  }

  protected matchListsUrl(tournamentId: string): string {
    return `/display/match-lists?tournamentId=${encodeURIComponent(tournamentId)}`;
  }

  protected tatamiDisplayUrl(tournamentId: string, tatamiId: string): string {
    return `/display/tatami/${encodeURIComponent(tatamiId)}?tournamentId=${encodeURIComponent(tournamentId)}`;
  }

  protected matchTatamiQueryParams(tournamentId: string, tatamiId: string): { tournamentId: string; tatamiId: string } {
    return { tournamentId, tatamiId };
  }

  private refreshTatamis(): void {
    const tournamentId = this.context.tournamentId();
    if (tournamentId) {
      this.loadTatamis(tournamentId);
    }
  }

  private loadTatamis(tournamentId: string): Subscription {
    return this.api.getTatamis(tournamentId).subscribe({
      next: (tatamis) => {
        this.displayTatamis.set(
          [...tatamis].sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name)),
        );
      },
      error: () => this.displayTatamis.set([]),
    });
  }

  private updateShellVisibility(url: string): void {
    const hideShell =
      url.startsWith('/display') ||
      url.startsWith('/public') ||
      url.startsWith('/draw/print-match-lists');
    const params = this.router.parseUrl(url).queryParams;
    const tatamiId = typeof params['tatamiId'] === 'string' ? params['tatamiId'] : null;
    this.routeTatamiId.set(tatamiId);

    // Any SPA navigation dismisses the mobile drawer so the shell isn't left
    // covering the routed page on narrow screens.
    this.closeDrawer();

    this.showShell.set(!hideShell);
    if (hideShell) {
      this.displayMenuOpen.set(false);
      this.matchMenuOpen.set(false);
    }
  }

  protected async logout(): Promise<void> {
    await this.auth.logout();
    await this.router.navigateByUrl('/login', { replaceUrl: true });
  }
}
