/** Domain models and DTOs mirroring the backend API contracts. */

/** Athlete / category gender. Serialized as string enum names by the API. */
export type Gender = 'Male' | 'Female';

/** Bracket generation format. */
export type BracketFormat =
  | 'SingleElimination'
  | 'SingleEliminationWithRepechage'
  | 'RoundRobin'
  | 'RoundRobinWithKnockout';

/** Lifecycle state of a fight. */
export type FightStatus = 'Pending' | 'InProgress' | 'Paused' | 'Completed';

/** Supported score buckets. */
export type ScoreType = 'Ippon' | 'WazaAri' | 'Yuko' | 'Shido';

/** Side color used for the non-white athlete in a tournament. */
export type AccentSideColor = 'Blue' | 'Red';

/** Side designation used by the match API. */
export type FightSide = 'white' | 'blue';

/** Whether a fight belongs to the main bracket, the repechage, or a round-robin group stage. */
export type FightBracketType = 'Main' | 'Repechage' | 'GroupStage';

export interface Tournament {
  id: string;
  name: string;
  /** ISO date string (yyyy-MM-dd). */
  date: string;
  venue: string;
  organizer: string;
  accentSideColor: AccentSideColor;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateTournamentRequest {
  name: string;
  date: string;
  venue: string;
  organizer: string;
  accentSideColor: AccentSideColor;
}

export type UpdateTournamentRequest = CreateTournamentRequest;

export interface Category {
  id: string;
  tournamentId: string;
  name: string;
  ageGroup: string;
  gender: Gender;
  weightClassKg: number | null;
  minBirthYear: number | null;
  maxBirthYear: number | null;
  rulesetNotes: string | null;
  matchDurationSeconds: number;
  goldenScoreEnabled: boolean;
  goldenScoreDurationSeconds: number;
  drawFormat: BracketFormat | null;
  isLocked: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateCategoryRequest {
  name: string;
  ageGroup: string;
  gender: Gender;
  weightClassKg: number | null;
  minBirthYear: number | null;
  maxBirthYear: number | null;
  rulesetNotes: string | null;
  matchDurationSeconds: number;
  goldenScoreEnabled: boolean;
  goldenScoreDurationSeconds: number;
}

export type UpdateCategoryRequest = CreateCategoryRequest;

export interface Club {
  id: string;
  tournamentId: string;
  name: string;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateClubRequest {
  name: string;
}

export type UpdateClubRequest = CreateClubRequest;

export interface Athlete {
  id: string;
  tournamentId: string;
  clubId: string;
  firstName: string;
  lastName: string;
  birthYear: number;
  gender: Gender;
  licenseId: string | null;
  weightKg: number | null;
  grade: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateAthleteRequest {
  clubId: string;
  firstName: string;
  lastName: string;
  birthYear: number;
  gender: Gender;
  licenseId: string | null;
  weightKg: number | null;
  grade: number;
}

export type UpdateAthleteRequest = CreateAthleteRequest;

export interface Tatami {
  id: string;
  tournamentId: string;
  name: string;
  displayOrder: number;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface CreateTatamiRequest {
  name: string;
  displayOrder: number | null;
}

export interface UpdateTatamiRequest {
  name: string;
  displayOrder: number;
  isActive: boolean;
}

export interface Registration {
  id: string;
  tournamentId: string;
  athleteId: string;
  categoryId: string | null;
  createdAtUtc: string;
}

export interface RegistrationDetail {
  id: string;
  tournamentId: string;
  athleteId: string;
  athleteLastName: string;
  athleteFirstName: string;
  athleteBirthYear: number;
  athleteGender: Gender;
  athleteClubName: string;
  athleteLicenseId: string | null;
  athleteWeightKg: number | null;
  categoryId: string | null;
  categoryName: string | null;
  categoryAgeGroup: string | null;
  categoryGender: Gender | null;
  categoryWeightClassKg: number | null;
  licenseConfirmed: boolean;
  createdAtUtc: string;
}

export interface CreateRegistrationRequest {
  athleteId: string;
  weightKg: number;
  licenseId: string | null;
  licenseConfirmed: boolean;
}

export interface AssignCategoryRequest {
  categoryId: string;
}

export interface UnassignedAthlete {
  athleteId: string;
  firstName: string;
  lastName: string;
  reason: string;
}

export interface AutoAssignResult {
  assignedCount: number;
  unassignedCount: number;
  unassigned: UnassignedAthlete[];
}

export interface Fight {
  id: string;
  tournamentId: string;
  categoryId: string;
  bracketType: FightBracketType;
  round: number;
  fightNumber: number;
  poolNumber: number | null;
  whiteAthleteId: string | null;
  blueAthleteId: string | null;
  winnerId: string | null;
  isBye: boolean;
  status: FightStatus;
  tatamiId: string | null;
  whiteScore: number;
  blueScore: number;
  whitePenalties: number;
  bluePenalties: number;
  whiteIpponCount: number;
  whiteWazaAriCount: number;
  whiteYukoCount: number;
  blueIpponCount: number;
  blueWazaAriCount: number;
  blueYukoCount: number;
  pausedAtUtc: string | null;
  osaeKomiSide: 'White' | 'Blue' | null;
  osaeKomiStartedAtUtc: string | null;
  startedAtUtc: string | null;
  completedAtUtc: string | null;
  isGoldenScore: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface GenerateDrawRequest {
  format: BracketFormat;
}

export interface SwapAthletesRequest {
  athleteId1: string;
  athleteId2: string;
}

/** Current queue state for a single tatami. */
export interface TatamiQueue {
  current: Fight | null;
  next: Fight | null;
  onDeck: Fight | null;
  upcoming: Fight[];
}

export interface RecordScoreRequest {
  whiteScore: number;
  blueScore: number;
  whitePenalties: number;
  bluePenalties: number;
}

export interface AdjustScoreRequest {
  side: FightSide;
  scoreType: ScoreType;
  delta: number;
}

export interface OsaeKomiRequest {
  side: FightSide;
}

export interface ConfirmResultRequest {
  winnerId: string;
}

export interface AssignTatamiRequest {
  tatamiId: string | null;
}

/** A provisional ranking placement in a category. */
export interface RankingEntry {
  place: number;
  athleteId: string;
  athleteName: string;
  clubName: string;
}

/** Club-level medal counts for the medal table. */
export interface MedalEntry {
  clubId: string;
  clubName: string;
  gold: number;
  silver: number;
  bronze: number;
}

export type UserRole = 'Admin' | 'Operator' | 'Display';

export interface LoginRequest {
  userName: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAtUtc: string;
  userName: string;
  role: UserRole;
}

export interface AuthenticatedUser {
  userId: string;
  userName: string;
  role: UserRole;
}

export interface LocalUserAccount {
  id: string;
  userName: string;
  role: UserRole;
  isActive: boolean;
  createdUtc: string;
  updatedUtc: string;
}

export interface CreateUserRequest {
  userName: string;
  role: UserRole;
  password: string;
}

export interface SetUserActiveRequest {
  isActive: boolean;
}

export interface ResetUserPasswordRequest {
  newPassword: string;
}

/** Athlete's standing within a round-robin pool or full round-robin bracket. */
export interface RoundRobinStanding {
  athleteId: string;
  athleteName: string;
  clubName: string;
  poolNumber: number;
  rank: number;
  wins: number;
  wazaAriScored: number;
  yukoScored: number;
  shidos: number;
}
