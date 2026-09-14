import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { concat, forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { extractApiError } from '../../core/http-error';
import { I18nService } from '../../core/i18n.service';
import { Athlete, RegistrationDetail, Tatami, TeamEncounter, TeamLineupAssignment, TeamMatchday } from '../../core/models';
import { TournamentContextService } from '../../core/tournament-context.service';
import { TranslatePipe } from '../../core/translate.pipe';

@Component({
  selector: 'app-team-matchday',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, TranslatePipe],
  templateUrl: './team-matchday.component.html',
  styleUrl: './team-matchday.component.css',
})
export class TeamMatchdayComponent implements OnInit {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthStateService);
  private readonly i18n = inject(I18nService);
  protected readonly context = inject(TournamentContextService);

  protected readonly matchday = signal<TeamMatchday | null>(null);
  protected readonly athletes = signal<Athlete[]>([]);
  protected readonly registrations = signal<RegistrationDetail[]>([]);
  protected readonly tatamis = signal<Tatami[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly info = signal<string | null>(null);
  protected readonly canOperate = this.auth.canOperate;
  protected readonly isTeamMatchday = computed(() => this.context.tournament()?.competitionMode === 'TeamMatchday');
  protected readonly homeTeamId = signal('');
  protected readonly awayTeamId = signal('');
  protected readonly encounterTatamiId = signal('');
  protected readonly lineupEncounterId = signal('');
  protected readonly lineupLegNumber = signal(1);
  protected readonly filterLineupAthletes = signal(false);
  protected readonly homeLineupAthleteIds = signal<string[]>([]);
  protected readonly awayLineupAthleteIds = signal<string[]>([]);
  protected readonly noShowTeamId = signal('');
  protected readonly weightClassLabels = computed(() => {
    switch (this.matchday()?.profile) {
      case 'SeniorMen': return ['-66 kg', '-73 kg', '-81 kg', '-90 kg', '+90 kg'];
      case 'SeniorWomen': return ['-52 kg', '-57 kg', '-63 kg', '-70 kg', '+70 kg'];
      case 'U16Boys': return ['-46 kg', '-52 kg', '-58 kg', '-66 kg', '+66 kg'];
      case 'U16Girls': return ['-42 kg', '-47 kg', '-53 kg', '-60 kg', '+60 kg'];
      default: return [];
    }
  });
  protected readonly weightClassUpperLimitsKg = computed<ReadonlyArray<number | null>>(() => {
    switch (this.matchday()?.profile) {
      case 'SeniorMen': return [66, 73, 81, 90, null];
      case 'SeniorWomen': return [52, 57, 63, 70, null];
      case 'U16Boys': return [46, 52, 58, 66, null];
      case 'U16Girls': return [42, 47, 53, 60, null];
      default: return [];
    }
  });
  protected readonly weightClassOrderLabels = computed(() => {
    const labels = this.weightClassLabels();
    return (this.matchday()?.weightClassOrder ?? []).map((index) => labels[index] ?? '');
  });

  ngOnInit(): void {
    this.load();
  }

  protected load(): void {
    const tournamentId = this.context.tournamentId();
    if (!tournamentId || !this.isTeamMatchday()) {
      return;
    }

    this.loading.set(true);
    this.clearMessages();
    forkJoin({
      matchday: this.api.getTeamMatchday(tournamentId),
      athletes: this.api.getAthletes(tournamentId),
      registrations: this.api.getRegistrations(tournamentId),
      tatamis: this.api.getTatamis(tournamentId),
    }).subscribe({
      next: ({ matchday, athletes, registrations, tatamis }) => {
        this.matchday.set(matchday);
        this.athletes.set(athletes);
        this.registrations.set(registrations);
        this.tatamis.set(tatamis.filter((tatami) => tatami.isActive));
        if (this.lineupEncounterId() && !matchday.encounters.some((encounter) => encounter.id === this.lineupEncounterId())) {
          this.lineupEncounterId.set('');
          this.clearLineups();
        }
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.error.set(extractApiError(error, this.i18n.translate('errors.load')));
        this.loading.set(false);
      },
    });
  }

  protected addEncounter(): void {
    const tournamentId = this.context.tournamentId();
    const homeTeamId = this.homeTeamId();
    const awayTeamId = this.awayTeamId();
    if (!tournamentId || !homeTeamId || !awayTeamId || homeTeamId === awayTeamId || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.clearMessages();
    this.api.createTeamEncounter(tournamentId, {
      homeTeamId,
      awayTeamId,
      tatamiId: this.encounterTatamiId() || null,
    }).subscribe({
      next: () => {
        this.saving.set(false);
        this.load();
      },
      error: (error: unknown) => this.finishWithError(error),
    });
  }

  protected deleteEncounter(encounter: TeamEncounter): void {
    const tournamentId = this.context.tournamentId();
    if (!tournamentId || this.saving() || !confirm(this.i18n.translate('teamMatchday.confirmDeleteEncounter'))) {
      return;
    }

    this.saving.set(true);
    this.clearMessages();
    this.api.deleteTeamEncounter(tournamentId, encounter.id).subscribe({
      next: () => {
        this.info.set(this.i18n.translate('teamMatchday.encounterDeleted'));
        this.saving.set(false);
        this.load();
      },
      error: (error: unknown) => this.finishWithError(error),
    });
  }

  protected teamNameFor(teamId: string): string {
    return this.matchday()?.teams.find((team) => team.id === teamId)?.name ?? this.i18n.translate('teamMatchday.unknownTeam');
  }

  protected athletesForLineupTeam(teamId: string, weightClassIndex: number): Athlete[] {
    const team = this.matchday()?.teams.find((candidate) => candidate.id === teamId);
    const registeredAthleteIds = new Set(this.registrations().map((registration) => registration.athleteId));
    const candidates = team
      ? this.athletes().filter((athlete) => athlete.clubId === team.clubId && athlete.weightKg !== null && registeredAthleteIds.has(athlete.id))
      : [];
    if (!this.filterLineupAthletes()) {
      return candidates;
    }

    const lineup = teamId === this.selectedLineupEncounter()?.homeTeamId
      ? this.homeLineupAthleteIds()
      : this.awayLineupAthleteIds();
    const selectedAthleteId = lineup[weightClassIndex] || '';
    const selectedInOtherWeightClass = new Set(
      lineup.filter((athleteId, index) => index !== weightClassIndex && athleteId),
    );
    const upperLimitKg = this.weightClassUpperLimitsKg()[weightClassIndex];

    return candidates.filter((athlete) => athlete.id === selectedAthleteId
      || (!selectedInOtherWeightClass.has(athlete.id)
        && (upperLimitKg === null || upperLimitKg === undefined || athlete.weightKg! <= upperLimitKg)));
  }

  protected selectedLineupEncounter(): TeamEncounter | null {
    return this.matchday()?.encounters.find((encounter) => encounter.id === this.lineupEncounterId()) ?? null;
  }

  protected selectLineupEncounter(encounterId: string): void {
    this.lineupEncounterId.set(encounterId);
    this.loadLineup();
  }

  protected selectLineupLeg(legNumber: string): void {
    this.lineupLegNumber.set(Number(legNumber));
    this.loadLineup();
  }

  protected updateLineupAthlete(teamId: string, weightClassIndex: number, athleteId: string): void {
    const lineup = teamId === this.selectedLineupEncounter()?.homeTeamId
      ? this.homeLineupAthleteIds
      : this.awayLineupAthleteIds;
    lineup.update((athleteIds) => {
      const updated = [...athleteIds];
      updated[weightClassIndex] = athleteId;
      return updated;
    });
  }

  protected lineupAthleteId(teamId: string, weightClassIndex: number): string {
    const lineup = teamId === this.selectedLineupEncounter()?.homeTeamId
      ? this.homeLineupAthleteIds()
      : this.awayLineupAthleteIds();
    return lineup[weightClassIndex] || '';
  }

  protected saveLineups(): void {
    const tournamentId = this.context.tournamentId();
    const encounterId = this.lineupEncounterId();
    const encounter = this.selectedLineupEncounter();
    if (!tournamentId || !encounterId || !encounter || this.saving()) {
      return;
    }

    const assignmentsFor = (athleteIds: string[]): TeamLineupAssignment[] =>
      this.weightClassLabels().map((_, weightClassIndex) => ({
        weightClassIndex,
        athleteId: athleteIds[weightClassIndex] || null,
      }));

    this.saving.set(true);
    this.clearMessages();
    concat(
      this.api.replaceTeamEncounterLineup(
        tournamentId,
        encounterId,
        this.lineupLegNumber(),
        encounter.homeTeamId,
        assignmentsFor(this.homeLineupAthleteIds()),
      ),
      this.api.replaceTeamEncounterLineup(
        tournamentId,
        encounterId,
        this.lineupLegNumber(),
        encounter.awayTeamId,
        assignmentsFor(this.awayLineupAthleteIds()),
      ),
    ).subscribe({
      next: () => {
        this.info.set(this.i18n.translate('teamMatchday.lineupSaved'));
        this.saving.set(false);
      },
      error: (error: unknown) => this.finishWithError(error),
    });
  }

  private loadLineup(): void {
    const tournamentId = this.context.tournamentId();
    const encounterId = this.lineupEncounterId();
    if (!tournamentId || !encounterId) {
      this.clearLineups();
      return;
    }

    const encounter = this.selectedLineupEncounter();
    if (!encounter) {
      this.clearLineups();
      return;
    }

    const legNumber = this.lineupLegNumber();
    this.clearLineups();
    this.api.getTeamEncounterLineup(tournamentId, encounterId, legNumber).subscribe({
      next: (entries) => {
        if (this.lineupEncounterId() !== encounterId || this.lineupLegNumber() !== legNumber) {
          return;
        }
        const homeLineup = this.emptyLineup();
        const awayLineup = this.emptyLineup();
        for (const entry of entries) {
          if (entry.teamId === encounter.homeTeamId) {
            homeLineup[entry.weightClassIndex] = entry.athleteId ?? '';
          } else if (entry.teamId === encounter.awayTeamId) {
            awayLineup[entry.weightClassIndex] = entry.athleteId ?? '';
          }
        }
        this.homeLineupAthleteIds.set(homeLineup);
        this.awayLineupAthleteIds.set(awayLineup);
      },
      error: (error: unknown) => {
        if (this.lineupEncounterId() === encounterId && this.lineupLegNumber() === legNumber) {
          this.error.set(extractApiError(error, this.i18n.translate('errors.load')));
        }
      },
    });
  }

  private emptyLineup(): string[] {
    return this.weightClassLabels().map(() => '');
  }

  private clearLineups(): void {
    this.homeLineupAthleteIds.set(this.emptyLineup());
    this.awayLineupAthleteIds.set(this.emptyLineup());
  }

  protected prepareLineupLeg(): void {
    const tournamentId = this.context.tournamentId();
    const encounterId = this.lineupEncounterId();
    if (!tournamentId || !encounterId || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.clearMessages();
    this.api.prepareTeamEncounterLeg(tournamentId, encounterId, this.lineupLegNumber()).subscribe({
      next: () => {
        this.info.set(this.i18n.translate('teamMatchday.legPrepared'));
        this.saving.set(false);
        this.load();
      },
      error: (error: unknown) => this.finishWithError(error),
    });
  }

  protected recordNoShow(): void {
    const tournamentId = this.context.tournamentId();
    const encounterId = this.lineupEncounterId();
    const teamId = this.noShowTeamId();
    if (!tournamentId || !encounterId || !teamId || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.clearMessages();
    this.api.recordTeamEncounterNoShow(tournamentId, encounterId, teamId).subscribe({
      next: () => {
        this.info.set(this.i18n.translate('teamMatchday.noShowRecorded'));
        this.saving.set(false);
        this.load();
      },
      error: (error: unknown) => this.finishWithError(error),
    });
  }

  private clearMessages(): void {
    this.error.set(null);
    this.info.set(null);
  }

  private finishWithError(error: unknown): void {
    this.error.set(extractApiError(error, this.i18n.translate('errors.save')));
    this.saving.set(false);
  }
}