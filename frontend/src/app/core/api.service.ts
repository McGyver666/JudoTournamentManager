import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { HttpResponse } from '@angular/common/http';
import { Observable } from 'rxjs';
import {
  AdjustScoreRequest,
  Athlete,
  AssignCategoryRequest,
  AssignTatamiRequest,
  AutoAssignResult,
  Category,
  CategoryPreset,
  CategoryPresetItemRequest,
  Club,
  ConfirmResultRequest,
  CategoryGenerationApplyResponse,
  CategoryGenerationPreviewResponse,
  CreateUserRequest,
  CreateAthleteRequest,
  CreateCategoryRequest,
  CreateClubRequest,
  CreateRegistrationRequest,
  CreateTatamiRequest,
  CreateTournamentRequest,
  Fight,
  GenerateCategoriesRequest,
  GenerateDrawRequest,
  LocalUserAccount,
  LoginRequest,
  LoginResponse,
  MedalEntry,
  OsaeKomiRequest,
  RankingEntry,
  RecordScoreRequest,
  ResetUserPasswordRequest,
  Registration,
  RegistrationDetail,
  RoundRobinStanding,
  SetUserActiveRequest,
  SwapAthletesRequest,
  Tatami,
  TatamiQueue,
  Tournament,
  UpdateAthleteRequest,
  UpdateCategoryRequest,
  UpdateClubRequest,
  UpdateTatamiRequest,
  UpdateTournamentRequest,
} from './models';

/**
 * Single typed gateway to the backend REST API.
 *
 * All URLs are same-origin relative paths so the bundle works unchanged when
 * served from the API host's <c>wwwroot</c> (offline-first deployment).
 */
@Injectable({ providedIn: 'root' })
export class ApiService {
  constructor(private readonly http: HttpClient) {}

  // Tournaments ------------------------------------------------------------
  getTournaments(): Observable<Tournament[]> {
    return this.http.get<Tournament[]>('api/tournaments');
  }

  getTournament(id: string): Observable<Tournament> {
    return this.http.get<Tournament>(`api/tournaments/${id}`);
  }

  createTournament(body: CreateTournamentRequest): Observable<Tournament> {
    return this.http.post<Tournament>('api/tournaments', body);
  }

  updateTournament(id: string, body: UpdateTournamentRequest): Observable<void> {
    return this.http.put<void>(`api/tournaments/${id}`, body);
  }

  deleteTournament(id: string): Observable<void> {
    return this.http.delete<void>(`api/tournaments/${id}`);
  }

  downloadTournamentBackup(id: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`api/tournaments/${id}/backup`, {
      observe: 'response',
      responseType: 'blob',
    });
  }

  restoreTournamentBackup(payload: unknown): Observable<unknown> {
    return this.http.post('api/tournaments/restore', payload);
  }

  // Clubs ------------------------------------------------------------------
  getClubs(tournamentId: string): Observable<Club[]> {
    return this.http.get<Club[]>(`api/tournaments/${tournamentId}/clubs`);
  }

  createClub(tournamentId: string, body: CreateClubRequest): Observable<Club> {
    return this.http.post<Club>(`api/tournaments/${tournamentId}/clubs`, body);
  }

  updateClub(tournamentId: string, clubId: string, body: UpdateClubRequest): Observable<void> {
    return this.http.put<void>(`api/tournaments/${tournamentId}/clubs/${clubId}`, body);
  }

  deleteClub(tournamentId: string, clubId: string): Observable<void> {
    return this.http.delete<void>(`api/tournaments/${tournamentId}/clubs/${clubId}`);
  }

  // Athletes ---------------------------------------------------------------
  getAthletes(tournamentId: string): Observable<Athlete[]> {
    return this.http.get<Athlete[]>(`api/tournaments/${tournamentId}/athletes`);
  }

  createAthlete(
    tournamentId: string,
    body: CreateAthleteRequest,
    allowDuplicate = false,
  ): Observable<Athlete> {
    const suffix = allowDuplicate ? '?allowDuplicate=true' : '';
    return this.http.post<Athlete>(`api/tournaments/${tournamentId}/athletes${suffix}`, body);
  }

  updateAthlete(
    tournamentId: string,
    athleteId: string,
    body: UpdateAthleteRequest,
  ): Observable<void> {
    return this.http.put<void>(`api/tournaments/${tournamentId}/athletes/${athleteId}`, body);
  }

  deleteAthlete(tournamentId: string, athleteId: string): Observable<void> {
    return this.http.delete<void>(`api/tournaments/${tournamentId}/athletes/${athleteId}`);
  }

  // Categories -------------------------------------------------------------
  getCategories(tournamentId: string): Observable<Category[]> {
    return this.http.get<Category[]>(`api/tournaments/${tournamentId}/categories`);
  }

  createCategory(tournamentId: string, body: CreateCategoryRequest): Observable<Category> {
    return this.http.post<Category>(`api/tournaments/${tournamentId}/categories`, body);
  }

  updateCategory(
    tournamentId: string,
    categoryId: string,
    body: UpdateCategoryRequest,
  ): Observable<void> {
    return this.http.put<void>(`api/tournaments/${tournamentId}/categories/${categoryId}`, body);
  }

  deleteCategory(tournamentId: string, categoryId: string): Observable<void> {
    return this.http.delete<void>(`api/tournaments/${tournamentId}/categories/${categoryId}`);
  }

  previewGeneratedCategories(
    tournamentId: string,
    body: GenerateCategoriesRequest,
  ): Observable<CategoryGenerationPreviewResponse> {
    return this.http.post<CategoryGenerationPreviewResponse>(
      `api/tournaments/${tournamentId}/categories/generate/preview`,
      body,
    );
  }

  applyGeneratedCategories(
    tournamentId: string,
    body: GenerateCategoriesRequest,
  ): Observable<CategoryGenerationApplyResponse> {
    return this.http.post<CategoryGenerationApplyResponse>(
      `api/tournaments/${tournamentId}/categories/generate/apply`,
      body,
    );
  }

  // Category Presets -------------------------------------------------------
  getCategoryPresets(tournamentId: string): Observable<CategoryPreset[]> {
    return this.http.get<CategoryPreset[]>(
      `api/tournaments/${tournamentId}/category-presets`);
  }

  updateCategoryPresets(
    tournamentId: string,
    presets: CategoryPresetItemRequest[],
  ): Observable<CategoryPreset[]> {
    return this.http.put<CategoryPreset[]>(
      `api/tournaments/${tournamentId}/category-presets`,
      { presets });
  }

  resetCategoryPresetsToDefaults(tournamentId: string): Observable<CategoryPreset[]> {
    return this.http.post<CategoryPreset[]>(
      `api/tournaments/${tournamentId}/category-presets/reset-defaults`, {});
  }

  // Tatamis ----------------------------------------------------------------
  getTatamis(tournamentId: string): Observable<Tatami[]> {
    return this.http.get<Tatami[]>(`api/tournaments/${tournamentId}/tatamis`);
  }

  createTatami(tournamentId: string, body: CreateTatamiRequest): Observable<Tatami> {
    return this.http.post<Tatami>(`api/tournaments/${tournamentId}/tatamis`, body);
  }

  updateTatami(tournamentId: string, tatamiId: string, body: UpdateTatamiRequest): Observable<void> {
    return this.http.put<void>(`api/tournaments/${tournamentId}/tatamis/${tatamiId}`, body);
  }

  deleteTatami(tournamentId: string, tatamiId: string): Observable<void> {
    return this.http.delete<void>(`api/tournaments/${tournamentId}/tatamis/${tatamiId}`);
  }

  // Registrations ----------------------------------------------------------
  getRegistrations(tournamentId: string): Observable<RegistrationDetail[]> {
    return this.http.get<RegistrationDetail[]>(`api/tournaments/${tournamentId}/registrations`);
  }

  createRegistration(
    tournamentId: string,
    body: CreateRegistrationRequest,
  ): Observable<Registration> {
    return this.http.post<Registration>(`api/tournaments/${tournamentId}/registrations`, body);
  }

  deleteRegistration(tournamentId: string, registrationId: string): Observable<void> {
    return this.http.delete<void>(
      `api/tournaments/${tournamentId}/registrations/${registrationId}`);
  }

  assignCategory(
    tournamentId: string,
    registrationId: string,
    body: AssignCategoryRequest,
  ): Observable<Registration> {
    return this.http.post<Registration>(
      `api/tournaments/${tournamentId}/registrations/${registrationId}/category`, body);
  }

  autoAssignCategories(tournamentId: string): Observable<AutoAssignResult> {
    return this.http.post<AutoAssignResult>(
      `api/tournaments/${tournamentId}/registrations/auto-assign`, {});
  }

  /** Returns the absolute URL of the CSV export endpoint for direct download. */
  registrationsExportUrl(tournamentId: string): string {
    return `api/tournaments/${tournamentId}/registrations/export`;
  }

  // Fights / draw ----------------------------------------------------------
  getFights(tournamentId: string, categoryId: string): Observable<Fight[]> {
    return this.http.get<Fight[]>(
      `api/tournaments/${tournamentId}/categories/${categoryId}/fights`);
  }

  generateDraw(
    tournamentId: string,
    categoryId: string,
    body: GenerateDrawRequest,
  ): Observable<Fight[]> {
    return this.http.post<Fight[]>(
      `api/tournaments/${tournamentId}/categories/${categoryId}/draw`, body);
  }

  swapAthletes(
    tournamentId: string,
    categoryId: string,
    body: SwapAthletesRequest,
  ): Observable<void> {
    return this.http.post<void>(
      `api/tournaments/${tournamentId}/categories/${categoryId}/swap`, body);
  }

  // Match control ----------------------------------------------------------

  getTatamiQueue(tournamentId: string, tatamiId: string): Observable<TatamiQueue> {
    return this.http.get<TatamiQueue>(
      `api/tournaments/${tournamentId}/tatamis/${tatamiId}/queue`);
  }

  startFight(tournamentId: string, fightId: string, userName: string): Observable<void> {
    void userName;
    return this.http.post<void>(
      `api/tournaments/${tournamentId}/fights/${fightId}/start`, {});
  }

  pauseFight(tournamentId: string, fightId: string, userName: string): Observable<void> {
    void userName;
    return this.http.post<void>(
      `api/tournaments/${tournamentId}/fights/${fightId}/stop`, {});
  }

  resumeFight(tournamentId: string, fightId: string, userName: string): Observable<void> {
    void userName;
    return this.http.post<void>(
      `api/tournaments/${tournamentId}/fights/${fightId}/resume`, {});
  }

  recordScore(
    tournamentId: string,
    fightId: string,
    body: RecordScoreRequest,
    userName: string,
  ): Observable<void> {
    void userName;
    return this.http.put<void>(
      `api/tournaments/${tournamentId}/fights/${fightId}/score`, body);
  }

  adjustScore(
    tournamentId: string,
    fightId: string,
    body: AdjustScoreRequest,
    userName: string,
  ): Observable<void> {
    void userName;
    return this.http.post<void>(
      `api/tournaments/${tournamentId}/fights/${fightId}/score/adjust`, body);
  }

  startOsaeKomi(
    tournamentId: string,
    fightId: string,
    body: OsaeKomiRequest,
    userName: string,
  ): Observable<void> {
    void userName;
    return this.http.post<void>(
      `api/tournaments/${tournamentId}/fights/${fightId}/osae-komi/start`, body);
  }

  stopOsaeKomi(tournamentId: string, fightId: string, userName: string): Observable<void> {
    void userName;
    return this.http.post<void>(
      `api/tournaments/${tournamentId}/fights/${fightId}/osae-komi/stop`, {});
  }

  confirmResult(
    tournamentId: string,
    fightId: string,
    body: ConfirmResultRequest,
    userName: string,
  ): Observable<void> {
    void userName;
    return this.http.post<void>(
      `api/tournaments/${tournamentId}/fights/${fightId}/result`, body);
  }

  assignTatami(
    tournamentId: string,
    fightId: string,
    body: AssignTatamiRequest,
    userName: string,
  ): Observable<void> {
    void userName;
    return this.http.post<void>(
      `api/tournaments/${tournamentId}/fights/${fightId}/assign-tatami`, body);
  }

  // Rankings & Results -----------------------------------------------------

  getCategoryRankings(tournamentId: string, categoryId: string): Observable<RankingEntry[]> {
    return this.http.get<RankingEntry[]>(
      `api/tournaments/${tournamentId}/categories/${categoryId}/rankings`);
  }

  getCategoryStandings(tournamentId: string, categoryId: string): Observable<RoundRobinStanding[]> {
    return this.http.get<RoundRobinStanding[]>(
      `api/tournaments/${tournamentId}/categories/${categoryId}/standings`);
  }

  getMedalTable(tournamentId: string): Observable<MedalEntry[]> {
    return this.http.get<MedalEntry[]>(`api/tournaments/${tournamentId}/medal-table`);
  }

  // Authentication --------------------------------------------------------

  login(body: LoginRequest): Observable<LoginResponse> {
    return this.http.post<LoginResponse>('api/auth/login', body);
  }

  logout(): Observable<void> {
    return this.http.post<void>('api/auth/logout', {});
  }

  me(): Observable<{ userId: string; userName: string; role: string }> {
    return this.http.get<{ userId: string; userName: string; role: string }>('api/auth/me');
  }

  getUsers(): Observable<LocalUserAccount[]> {
    return this.http.get<LocalUserAccount[]>('api/auth/users');
  }

  createUser(body: CreateUserRequest): Observable<void> {
    return this.http.post<void>('api/auth/users', body);
  }

  setUserActive(userId: string, body: SetUserActiveRequest): Observable<void> {
    return this.http.patch<void>(`api/auth/users/${userId}/active`, body);
  }

  resetUserPassword(userId: string, body: ResetUserPasswordRequest): Observable<void> {
    return this.http.post<void>(`api/auth/users/${userId}/reset-password`, body);
  }
}
