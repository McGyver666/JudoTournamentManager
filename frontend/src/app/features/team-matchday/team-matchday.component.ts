import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { extractApiError } from '../../core/http-error';
import { I18nService } from '../../core/i18n.service';
import { Athlete, Club, Tatami, TeamLineupAssignment, TeamMatchday } from '../../core/models';
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
  protected readonly clubs = signal<Club[]>([]);
  protected readonly athletes = signal<Athlete[]>([]);
  protected readonly tatamis = signal<Tatami[]>([]);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly info = signal<string | null>(null);
  protected readonly canOperate = this.auth.canOperate;
  protected readonly isTeamMatchday = computed(() => this.context.tournament()?.competitionMode === 'TeamMatchday');
  protected readonly selectedClubId = signal('');
  protected readonly teamName = signal('');
  protected readonly selectedAthleteId = signal('');
  protected readonly weightKg = signal<number | null>(null);
  protected readonly homeTeamId = signal('');
  protected readonly awayTeamId = signal('');
  protected readonly encounterTatamiId = signal('');
  protected readonly lineupEncounterId = signal('');
  protected readonly lineupLegNumber = signal(1);
  protected readonly lineupTeamId = signal('');
  protected readonly lineupAthleteIds = signal<string[]>([]);
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
      clubs: this.api.getClubs(tournamentId),
      athletes: this.api.getAthletes(tournamentId),
      tatamis: this.api.getTatamis(tournamentId),
    }).subscribe({
      next: ({ matchday, clubs, athletes, tatamis }) => {
        this.matchday.set(matchday);
        this.clubs.set(clubs);
        this.athletes.set(athletes);
        this.tatamis.set(tatamis.filter((tatami) => tatami.isActive));
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.error.set(extractApiError(error, this.i18n.translate('errors.load')));
        this.loading.set(false);
      },
    });
  }

  protected addTeam(): void {
    const tournamentId = this.context.tournamentId();
    const clubId = this.selectedClubId();
    const name = this.teamName().trim();
    if (!tournamentId || !clubId || !name || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.clearMessages();
    this.api.createTeamMatchdayTeam(tournamentId, { clubId, name }).subscribe({
      next: () => {
        this.teamName.set('');
        this.saving.set(false);
        this.load();
      },
      error: (error: unknown) => this.finishWithError(error),
    });
  }

  protected confirmWeighIn(): void {
    const tournamentId = this.context.tournamentId();
    const athleteId = this.selectedAthleteId();
    const weightKg = this.weightKg();
    if (!tournamentId || !athleteId || weightKg === null || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.clearMessages();
    this.api.confirmMatchdayWeighIn(tournamentId, { athleteId, weightKg }).subscribe({
      next: () => {
        this.weightKg.set(null);
        this.saving.set(false);
        this.load();
      },
      error: (error: unknown) => this.finishWithError(error),
    });
  }

  protected drawWeightClassOrder(): void {
    const tournamentId = this.context.tournamentId();
    if (!tournamentId || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.clearMessages();
    this.api.drawTeamMatchdayWeightClassOrder(tournamentId).subscribe({
      next: () => {
        this.info.set(this.i18n.translate('teamMatchday.orderDrawn'));
        this.saving.set(false);
        this.load();
      },
      error: (error: unknown) => this.finishWithError(error),
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

  protected clubName(clubId: string): string {
    return this.clubs().find((club) => club.id === clubId)?.name ?? this.i18n.translate('teamMatchday.unknownClub');
  }

  protected teamNameFor(teamId: string): string {
    return this.matchday()?.teams.find((team) => team.id === teamId)?.name ?? this.i18n.translate('teamMatchday.unknownTeam');
  }

  protected athleteName(athleteId: string): string {
    const athlete = this.athletes().find((candidate) => candidate.id === athleteId);
    return athlete ? `${athlete.firstName} ${athlete.lastName}` : this.i18n.translate('teamMatchday.unknownAthlete');
  }

  protected athletesForLineupTeam(): Athlete[] {
    const team = this.matchday()?.teams.find((candidate) => candidate.id === this.lineupTeamId());
    return team ? this.athletes().filter((athlete) => athlete.clubId === team.clubId) : [];
  }

  protected updateLineupAthlete(weightClassIndex: number, athleteId: string): void {
    this.lineupAthleteIds.update((athleteIds) => {
      const updated = [...athleteIds];
      updated[weightClassIndex] = athleteId;
      return updated;
    });
  }

  protected lineupAthleteId(weightClassIndex: number): string {
    return this.lineupAthleteIds()[weightClassIndex] || '';
  }

  protected saveLineup(): void {
    const tournamentId = this.context.tournamentId();
    const encounterId = this.lineupEncounterId();
    const teamId = this.lineupTeamId();
    if (!tournamentId || !encounterId || !teamId || this.saving()) {
      return;
    }

    const assignments: TeamLineupAssignment[] = this.weightClassLabels().map((_, weightClassIndex) => ({
      weightClassIndex,
      athleteId: this.lineupAthleteIds()[weightClassIndex] ?? '',
    }));
    if (assignments.some((assignment) => !assignment.athleteId)) {
      this.error.set(this.i18n.translate('teamMatchday.completeLineup'));
      return;
    }

    this.saving.set(true);
    this.clearMessages();
    this.api.replaceTeamEncounterLineup(tournamentId, encounterId, this.lineupLegNumber(), teamId, assignments).subscribe({
      next: () => {
        this.info.set(this.i18n.translate('teamMatchday.lineupSaved'));
        this.saving.set(false);
      },
      error: (error: unknown) => this.finishWithError(error),
    });
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