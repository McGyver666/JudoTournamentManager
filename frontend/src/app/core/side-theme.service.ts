import { Injectable } from '@angular/core';
import { AccentSideColor, FightSide, Tournament } from './models';

/** Shared side-color mapping for tournament display preferences. */
@Injectable({ providedIn: 'root' })
export class SideThemeService {
  accentSideColor(tournament: Tournament | null): AccentSideColor {
    return tournament?.accentSideColor ?? 'Blue';
  }

  accentSideKey(tournament: Tournament | null): 'blue' | 'red' {
    return this.accentSideColor(tournament) === 'Red' ? 'red' : 'blue';
  }

  accentSideLabelKey(tournament: Tournament | null): string {
    return `match.${this.accentSideKey(tournament)}Side`;
  }

  startOsaeLabelKey(tournament: Tournament | null): string {
    return `match.startOsae${this.accentSideColor(tournament)}`;
  }

  confirmWinnerLabelKey(tournament: Tournament | null): string {
    return `match.confirm${this.accentSideColor(tournament)}Wins`;
  }

  sideLabelKey(side: FightSide, tournament: Tournament | null): string {
    return side === 'white' ? 'match.whiteSide' : this.accentSideLabelKey(tournament);
  }

  applyTheme(root: HTMLElement, tournament: Tournament | null): void {
    const accent = this.accentSideColor(tournament);
    const vars = accent === 'Red'
      ? {
          '--side-accent': '#b3261e',
          '--side-accent-soft': 'rgba(179, 38, 30, 0.18)',
          '--side-accent-contrast': '#ff8f86',
          '--side-accent-muted': '#ffb6b1',
        }
      : {
          '--side-accent': '#1565c0',
          '--side-accent-soft': 'rgba(21, 101, 192, 0.10)',
          '--side-accent-contrast': '#74c8ff',
          '--side-accent-muted': '#8fd4ff',
        };

    for (const [key, value] of Object.entries(vars)) {
      root.style.setProperty(key, value);
    }
  }
}