import { Component, inject } from '@angular/core';
import { RouterOutlet, RouterLink, RouterLinkActive } from '@angular/router';
import { TranslatePipe } from './core/translate.pipe';
import { I18nService, AppLanguage } from './core/i18n.service';
import { TournamentContextService } from './core/tournament-context.service';
import { AuthStateService } from './core/auth-state.service';

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
export class AppComponent {
  private readonly i18n = inject(I18nService);
  private readonly auth = inject(AuthStateService);
  protected readonly context = inject(TournamentContextService);

  protected readonly language = this.i18n.language;
  protected readonly isAuthenticated = this.auth.isAuthenticated;
  protected readonly isAdmin = this.auth.isAdmin;
  protected readonly canOperate = this.auth.canOperate;
  protected readonly currentUser = this.auth.user;

  protected switchLanguage(event: Event): void {
    const value = (event.target as HTMLSelectElement).value as AppLanguage;
    this.i18n.use(value);
  }

  protected async logout(): Promise<void> {
    await this.auth.logout();
  }
}
