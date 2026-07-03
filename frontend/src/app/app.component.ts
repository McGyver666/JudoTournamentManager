import { Component, ElementRef, HostListener, OnDestroy, OnInit, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { filter, Subscription } from 'rxjs';
import { TranslatePipe } from './core/translate.pipe';
import { I18nService, AppLanguage } from './core/i18n.service';
import { TournamentContextService } from './core/tournament-context.service';
import { AuthStateService } from './core/auth-state.service';
import { ApiService } from './core/api.service';
import { Tatami } from './core/models';

/**
 * Application shell: top navigation, active-tournament indicator and the
 * language switcher. All visible labels are resolved through the translation
 * pipe so the UI stays fully localizable.
 */
@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TranslatePipe],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css',
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly i18n = inject(I18nService);
  private readonly auth = inject(AuthStateService);
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly hostElement = inject(ElementRef<HTMLElement>);
  protected readonly context = inject(TournamentContextService);

  protected readonly language = this.i18n.language;
  protected readonly isAuthenticated = this.auth.isAuthenticated;
  protected readonly isAdmin = this.auth.isAdmin;
  protected readonly canOperate = this.auth.canOperate;
  protected readonly currentUser = this.auth.user;
  protected readonly displayTatamis = signal<Tatami[]>([]);
  protected readonly displayMenuOpen = signal(false);
  protected readonly tournamentleitungMenuOpen = signal(false);
  protected readonly showShell = signal(true);

  private shellRouteSub?: Subscription;

  private readonly loadTatamisEffect = effect((onCleanup) => {
    const tournamentId = this.context.tournamentId();
    const authenticated = this.isAuthenticated();

    if (!authenticated || !tournamentId) {
      this.displayTatamis.set([]);
      return;
    }

    const sub = this.api.getTatamis(tournamentId).subscribe({
      next: (tatamis) => {
        this.displayTatamis.set(
          [...tatamis].sort((a, b) => a.displayOrder - b.displayOrder || a.name.localeCompare(b.name)),
        );
      },
      error: () => this.displayTatamis.set([]),
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

  protected toggleDisplayMenu(): void {
    const willOpen = !this.displayMenuOpen();
    this.displayMenuOpen.update((open) => !open);
    if (willOpen) {
      this.tournamentleitungMenuOpen.set(false);
    }
  }

  protected closeDisplayMenu(): void {
    this.displayMenuOpen.set(false);
  }

  protected toggleTournamentleitungMenu(): void {
    const willOpen = !this.tournamentleitungMenuOpen();
    this.tournamentleitungMenuOpen.update((open) => !open);
    if (willOpen) {
      this.displayMenuOpen.set(false);
    }
  }

  protected closeTournamentleitungMenu(): void {
    this.tournamentleitungMenuOpen.set(false);
  }

  protected displayOverviewUrl(tournamentId: string): string {
    return `/display?tournamentId=${encodeURIComponent(tournamentId)}`;
  }

  protected tatamiDisplayUrl(tournamentId: string, tatamiId: string): string {
    return `/display/tatami/${encodeURIComponent(tatamiId)}?tournamentId=${encodeURIComponent(tournamentId)}`;
  }

  @HostListener('document:click', ['$event'])
  protected onDocumentClick(event: MouseEvent): void {
    if (!this.displayMenuOpen() && !this.tournamentleitungMenuOpen()) {
      return;
    }

    const target = event.target;
    if (!(target instanceof Node)) {
      this.closeDisplayMenu();
      this.closeTournamentleitungMenu();
      return;
    }

    if (!this.hostElement.nativeElement.contains(target)) {
      this.closeDisplayMenu();
      this.closeTournamentleitungMenu();
      return;
    }

    const targetElement = target as Element;
    if (!targetElement.closest('.nav-dropdown')) {
      this.closeDisplayMenu();
      this.closeTournamentleitungMenu();
    }
  }

  private updateShellVisibility(url: string): void {
    const hideShell = url.startsWith('/display');
    this.showShell.set(!hideShell);
    if (hideShell) {
      this.closeDisplayMenu();
      this.closeTournamentleitungMenu();
    }
  }

  protected async logout(): Promise<void> {
    await this.auth.logout();
  }
}
