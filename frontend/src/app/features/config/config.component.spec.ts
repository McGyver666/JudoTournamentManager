import { HttpErrorResponse } from '@angular/common/http';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { of, throwError } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { I18nService } from '../../core/i18n.service';
import { Athlete, Club, Tournament } from '../../core/models';
import { TournamentContextService } from '../../core/tournament-context.service';
import { ConfigComponent } from './config.component';

class TournamentContextStub {
  readonly tournamentId = signal<string | null>(null);
  readonly tournament = signal<Tournament | null>(null);
}

class AuthStateStub {
  readonly canOperate = signal(true);
}

class I18nServiceStub {
  private readonly values: Record<string, string> = {
    'common.confirmDelete': 'delete?',
    'errors.delete': 'Löschen fehlgeschlagen.',
    'errors.clubHasAthletes':
      'Der Verein kann nicht gelöscht werden, solange ihm Athleten zugeordnet sind. Bitte Athleten zuerst entfernen oder einem anderen Verein zuordnen.',
    'athletes.filterBirthYearFrom': 'Jahrgang von',
    'athletes.filterBirthYearTo': 'Jahrgang bis',
    'athletes.filterAllGenders': 'Alle',
    'athletes.filterAllClubs': 'Alle Vereine',
    'athletes.resetFilters': 'Filter zurücksetzen',
    'athletes.filteredCount': '{visible} von {total} Athleten',
    'athletes.noFilterResults': 'Keine Athleten entsprechen den Filtern.',
    'athletes.gradeNotSpecified': '– nicht angegeben –',
    'athletes.gradeNotSpecifiedShort': '–',
    'athletes.gradeOption2': '8. Kyu (weiß-gelber Gürtel)',
  };

  translate(key: string, params?: Record<string, string | number>): string {
    const template = this.values[key] ?? key;
    return template.replace(/\{(\w+)\}/g, (_match, name: string) =>
      params?.[name] === undefined ? `{${name}}` : String(params[name]));
  }
}

describe('ConfigComponent', () => {
  const club: Club = {
    id: 'club-1',
    tournamentId: 't-1',
    name: 'DJK Test',
    contactName: null,
    contactEmail: null,
    contactPhone: null,
    createdAtUtc: '2026-01-01T00:00:00Z',
    updatedAtUtc: '2026-01-01T00:00:00Z',
  };

  let apiSpy: jasmine.SpyObj<Pick<ApiService, 'deleteClub' | 'deleteAthlete' | 'getCategoryPresetWarnings'>>;
  let context: TournamentContextStub;
  let component: ConfigComponent;
  let fixture: ComponentFixture<ConfigComponent>;

  beforeEach(async () => {
    apiSpy = jasmine.createSpyObj<Pick<ApiService, 'deleteClub' | 'deleteAthlete' | 'getCategoryPresetWarnings'>>(
      'ApiService',
      ['deleteClub', 'deleteAthlete', 'getCategoryPresetWarnings'],
    );
    context = new TournamentContextStub();

    await TestBed.configureTestingModule({
      imports: [ConfigComponent],
      providers: [
        { provide: ApiService, useValue: apiSpy },
        { provide: TournamentContextService, useValue: context },
        { provide: AuthStateService, useClass: AuthStateStub },
        { provide: I18nService, useClass: I18nServiceStub },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ConfigComponent);
    component = fixture.componentInstance;
  });

  it('shows a specific localized message when deleting a club fails with HTTP 409', () => {
    context.tournamentId.set('t-1');
    apiSpy.deleteClub.and.returnValue(
      throwError(
        () =>
          new HttpErrorResponse({
            status: 409,
            error: { title: 'Verein hat Athleten.' },
          }),
      ),
    );
    spyOn(window, 'confirm').and.returnValue(true);

    (component as any).deleteClub(club);

    expect(apiSpy.deleteClub).toHaveBeenCalledWith('t-1', 'club-1');
    expect((component as any).error()).toBe(
      'Der Verein kann nicht gelöscht werden, solange ihm Athleten zugeordnet sind. Bitte Athleten zuerst entfernen oder einem anderen Verein zuordnen.',
    );
  });

  it('keeps generic delete error handling for non-409 club delete failures', () => {
    context.tournamentId.set('t-1');
    apiSpy.deleteClub.and.returnValue(throwError(() => new Error('network')));
    spyOn(window, 'confirm').and.returnValue(true);

    (component as any).deleteClub(club);

    expect((component as any).error()).toBe('Löschen fehlgeschlagen.');
  });

  it('removes the club from local state after successful delete', () => {
    context.tournamentId.set('t-1');
    apiSpy.deleteClub.and.returnValue(of(void 0));
    spyOn(window, 'confirm').and.returnValue(true);
    (component as any).clubs.set([club]);

    (component as any).deleteClub(club);

    expect((component as any).clubs()).toEqual([]);
    expect((component as any).error()).toBeNull();
  });

  it('filters the athlete table by the selected club', () => {
    fixture.detectChanges();
    context.tournamentId.set('t-1');
    const testComponent = component as any;
    testComponent.tab.set('athletes');
    testComponent.clubs.set([
      club,
      { ...club, id: 'club-2', name: 'Judo Club' },
    ]);
    testComponent.athletes.set([
      createAthlete('athlete-1', 'club-1', 'Anna'),
      createAthlete('athlete-2', 'club-2', 'Berta'),
    ]);
    expect(testComponent.tab()).toBe('athletes');
    expect(testComponent.athletes().length).toBe(2);
    fixture.detectChanges();

    expect(fixture.nativeElement.innerHTML).toContain('athlete-filters');
    const clubFilter = fixture.nativeElement.querySelector(
      '#athlete-filter-club',
    ) as HTMLSelectElement;
    expect(clubFilter).toBeTruthy();

    clubFilter.value = 'club-1';
    clubFilter.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    const rows = fixture.nativeElement.querySelectorAll('tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0].textContent).toContain('Anna');
  });

  it('filters inclusive birth year boundaries and swaps reversed values', () => {
    const testComponent = component as any;
    testComponent.athletes.set([
      createAthlete('athlete-2012', 'club-1', 'Anna', 2012),
      createAthlete('athlete-2013', 'club-1', 'Berta', 2013),
      createAthlete('athlete-2014', 'club-1', 'Clara', 2014),
    ]);

    testComponent.selectedAthleteBirthYearFrom.set(2012);
    testComponent.selectedAthleteBirthYearTo.set(2014);
    expect(testComponent.filteredAthletes().map((athlete: Athlete) => athlete.id))
      .toEqual(['athlete-2012', 'athlete-2013', 'athlete-2014']);

    testComponent.selectedAthleteBirthYearFrom.set(2013);
    expect(testComponent.filteredAthletes().map((athlete: Athlete) => athlete.id))
      .toEqual(['athlete-2013', 'athlete-2014']);

    testComponent.selectedAthleteBirthYearFrom.set(null);
    testComponent.selectedAthleteBirthYearTo.set(2013);
    expect(testComponent.filteredAthletes().map((athlete: Athlete) => athlete.id))
      .toEqual(['athlete-2012', 'athlete-2013']);

    testComponent.selectedAthleteBirthYearFrom.set(2014);
    expect(testComponent.filteredAthletes().map((athlete: Athlete) => athlete.id))
      .toEqual(['athlete-2013', 'athlete-2014']);
  });

  it('combines gender and club filters with AND', () => {
    const testComponent = component as any;
    testComponent.athletes.set([
      createAthlete('athlete-female', 'club-1', 'Anna', 2012, 'Female'),
      createAthlete('athlete-other-club', 'club-2', 'Berta', 2012, 'Male'),
      createAthlete('athlete-match', 'club-1', 'Clara', 2012, 'Male'),
    ]);
    testComponent.selectedAthleteGender.set('Male');
    testComponent.selectedAthleteClubId.set('club-1');

    expect(testComponent.filteredAthletes().map((athlete: Athlete) => athlete.id))
      .toEqual(['athlete-match']);
  });

  it('resets only filter values whose options disappear', () => {
    context.tournamentId.set('t-1');
    const testComponent = component as any;
    const olderAthlete = createAthlete('athlete-2012', 'club-1', 'Anna', 2012);
    const youngerAthlete = createAthlete('athlete-2013', 'club-1', 'Berta', 2013);
    testComponent.clubs.set([club]);
    testComponent.athletes.set([olderAthlete, youngerAthlete]);
    testComponent.selectedAthleteBirthYearFrom.set(2012);
    testComponent.selectedAthleteBirthYearTo.set(2013);
    testComponent.selectedAthleteGender.set('Male');
    testComponent.selectedAthleteClubId.set('club-1');
    spyOn(window, 'confirm').and.returnValue(true);
    apiSpy.deleteClub.and.returnValue(of(void 0));
    apiSpy.deleteAthlete.and.returnValue(of(void 0));

    testComponent.deleteClub(club);
    expect(testComponent.selectedAthleteClubId()).toBe('');
    expect(testComponent.selectedAthleteBirthYearFrom()).toBe(2012);
    expect(testComponent.selectedAthleteBirthYearTo()).toBe(2013);

    testComponent.deleteAthlete(youngerAthlete);
    expect(testComponent.selectedAthleteBirthYearFrom()).toBe(2012);
    expect(testComponent.selectedAthleteBirthYearTo()).toBeNull();
    expect(testComponent.selectedAthleteGender()).toBe('Male');
  });

  it('resets all athlete filters together', () => {
    const testComponent = component as any;
    testComponent.selectedAthleteBirthYearFrom.set(2012);
    testComponent.selectedAthleteBirthYearTo.set(2014);
    testComponent.selectedAthleteGender.set('Female');
    testComponent.selectedAthleteClubId.set('club-1');

    testComponent.resetAthleteFilters();

    expect(testComponent.hasAthleteFilters()).toBeFalse();
    expect(testComponent.filteredAthletes().length).toBe(0);
  });

  it('shows a distinct empty state when filters have no matching athletes', () => {
    fixture.detectChanges();
    context.tournamentId.set('t-1');
    const testComponent = component as any;
    testComponent.tab.set('athletes');
    testComponent.athletes.set([createAthlete('athlete-1', 'club-1', 'Anna')]);
    testComponent.selectedAthleteClubId.set('missing-club');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('Keine Athleten entsprechen den Filtern.');
    expect(fixture.nativeElement.textContent).not.toContain('athletes.empty');
    expect(fixture.nativeElement.querySelector('tbody')).toBeNull();
    expect(fixture.nativeElement.querySelector('.toolbar-actions button').disabled).toBeTrue();
  });

  it('starts new athlete forms without a grade and shows a dash for missing grades', () => {
    fixture.detectChanges();
    context.tournamentId.set('t-1');
    const testComponent = component as any;
    testComponent.tab.set('athletes');
    testComponent.clubs.set([club]);
    testComponent.athletes.set([
      { ...createAthlete('athlete-1', 'club-1', 'Anna'), grade: null },
    ]);
    testComponent.newAthlete();
    fixture.detectChanges();

    expect(testComponent.athleteForm.grade).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('– nicht angegeben –');
    const athleteRow = fixture.nativeElement.querySelector('tbody tr');
    expect(athleteRow.cells[7].textContent.trim()).toBe('–');
  });

  it('shows the filtered count and clears it when filters are reset', () => {
    fixture.detectChanges();
    context.tournamentId.set('t-1');
    const testComponent = component as any;
    testComponent.tab.set('athletes');
    testComponent.clubs.set([
      { ...club, id: 'club-2', name: 'Judo Club' },
      club,
    ]);
    testComponent.athletes.set([
      createAthlete('athlete-1', 'club-1', 'Anna', 2012, 'Male'),
      createAthlete('athlete-2', 'club-2', 'Berta', 2013, 'Female'),
    ]);
    testComponent.selectedAthleteGender.set('Male');
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('1 von 2 Athleten');
    const yearOptions = Array.from(
      (fixture.nativeElement.querySelector('#athlete-filter-year-from') as HTMLSelectElement).options,
    ).map((option) => option.value);
    expect(yearOptions).toEqual(['', '2012', '2013']);
    const clubOptions = Array.from(
      (fixture.nativeElement.querySelector('#athlete-filter-club') as HTMLSelectElement).options,
    ).map((option) => option.textContent?.trim());
    expect(clubOptions).toEqual(['Alle Vereine', 'DJK Test', 'Judo Club']);

    (fixture.nativeElement.querySelector('.athlete-filters button') as HTMLButtonElement).click();
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).not.toContain('1 von 2 Athleten');
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(2);
  });

  it('exports only the filtered athletes', async () => {
    const createObjectUrl = spyOn(URL, 'createObjectURL').and.returnValue('blob:athletes');
    spyOn(URL, 'revokeObjectURL');
    spyOn(HTMLAnchorElement.prototype, 'click');
    const testComponent = component as any;
    testComponent.athletes.set([
      createAthlete('athlete-1', 'club-1', 'Anna'),
      createAthlete('athlete-2', 'club-2', 'Berta'),
    ]);
    testComponent.selectedAthleteClubId.set('club-1');

    testComponent.exportAthletesCsv();

    const csv = await (createObjectUrl.calls.first().args[0] as Blob).text();
    expect(csv).toContain('Anna');
    expect(csv).not.toContain('Berta');
  });

  it('parses empty, German, and English CSV belt grades independently of the UI language', () => {
    const rows = (component as any).parseCsvRows([
      'Nachname;Vorname;Jahrgang;Geschlecht;Verein;Graduierung;Gewicht',
      'Leer;Grad;2012;w;DJK Test;;',
      'Deutsch;Grad;2012;w;DJK Test;8.kyu;',
      'English;Grade;2012;w;DJK Test;8th Kyu (white-yellow belt);',
    ].join('\n'));

    expect(rows.map((row: { grade: number | null }) => row.grade)).toEqual([null, 2, 2]);
  });

  it('reports invalid CSV belt grades with the physical line and value', () => {
    let parseError: any;
    try {
      (component as any).parseCsvRows([
        'Nachname;Vorname;Jahrgang;Geschlecht;Verein;Graduierung;Gewicht',
        '',
        'Leer;Grad;2012;w;DJK Test;Yellow;',
      ].join('\n'));
    } catch (error) {
      parseError = error;
    }

    expect(parseError?.isCsvGradeError).toBeTrue();
    expect(parseError?.lineNumber).toBe(3);
    expect(parseError?.value).toBe('Yellow');

    let numericGradeError: any;
    try {
      (component as any).parseCsvRows([
        'Nachname;Vorname;Jahrgang;Geschlecht;Verein;Graduierung;Gewicht',
        'Zahl;Grad;2012;w;DJK Test;3;',
      ].join('\n'));
    } catch (error) {
      numericGradeError = error;
    }
    expect(numericGradeError?.value).toBe('3');
  });

  it('exports an athlete without a belt grade as an empty CSV field', async () => {
    const createObjectUrl = spyOn(URL, 'createObjectURL').and.returnValue('blob:athletes');
    spyOn(URL, 'revokeObjectURL');
    spyOn(HTMLAnchorElement.prototype, 'click');
    (component as any).athletes.set([
      { ...createAthlete('athlete-1', 'club-1', 'Anna'), grade: null },
    ]);

    (component as any).exportAthletesCsv();

    const csv = await (createObjectUrl.calls.first().args[0] as Blob).text();
    expect(csv).toContain('Test;Anna;2012;w;;;');
  });

  it('derives generator modes from tournament presets and groups U9 by weight', () => {
    const testComponent = component as any;
    testComponent.presets.set([
      createPreset('u9-male', 'U9', 'Male', 8, 6, 2018, 2020, []),
      createPreset('u9-female', 'U9', 'Female', 8, 6, 2018, 2020, []),
    ]);
    testComponent.categoryGeneratorForm.ageGroup = 'U9';
    testComponent.categoryGeneratorForm.genderMode = 'Male';

    expect(testComponent.generationAgeGroups()).toEqual(['U9']);
    expect(testComponent.generationGenderModes()).toEqual(['Male', 'Female', 'Mixed']);
    expect(testComponent.canUseStandardWeightClasses()).toBeFalse();

    testComponent.categoryGeneratorForm.weightMode = 'StandardClasses';
    testComponent.onCategoryGeneratorSelectionChanged();
    expect(testComponent.categoryGeneratorForm.weightMode).toBe('AthletesByTargetSize');
  });

  it('shows preset warnings computed by the backend with their translation parameters', () => {
    context.tournamentId.set('t-1');
    apiSpy.getCategoryPresetWarnings.and.returnValue(of([
      { key: 'presets.warningNoAgeGroup', ageGroup: null, count: 2 },
      { key: 'presets.warningHiddenAgeGroup', ageGroup: 'U11', count: null },
    ]));

    (component as any).loadPresetWarnings();

    expect(apiSpy.getCategoryPresetWarnings).toHaveBeenCalledWith('t-1');
    expect((component as any).presetWarnings()).toEqual([
      { key: 'presets.warningNoAgeGroup', params: { count: 2 } },
      { key: 'presets.warningHiddenAgeGroup', params: { ageGroup: 'U11' } },
    ]);
  });

  it('persists an empty preset weight list instead of an open class', () => {
    const testComponent = component as any;
    const preset = createPreset('u9-male', 'U9', 'Male', 8, 6, 2018, 2020, [30]);

    testComponent.parseWeightLimitsInput(preset, '');

    expect(preset.weightClassLimitsKg).toEqual([]);
  });

  function createPreset(
    id: string,
    ageGroup: string,
    gender: 'Male' | 'Female',
    maxAgeYears: number,
    minAgeYears: number,
    minBirthYear: number,
    maxBirthYear: number,
    weightClassLimitsKg: (number | null)[],
  ) {
    return {
      id,
      ageGroup,
      gender,
      maxAgeYears,
      minAgeYears,
      minBirthYear,
      maxBirthYear,
      defaultMatchDurationSeconds: 120,
      weightClassLimitsKg,
      sortOrder: 0,
    };
  }

  function createAthlete(
    id: string,
    clubId: string,
    firstName: string,
    birthYear = 2012,
    gender: Athlete['gender'] = 'Female',
  ): Athlete {
    return {
      id,
      tournamentId: 't-1',
      clubId,
      firstName,
      lastName: 'Test',
      birthYear,
      gender,
      licenseId: null,
      weightKg: null,
      grade: 1,
      lastFightDurationSeconds: null,
      lastFightEndedAtUtc: null,
      createdAtUtc: '2026-01-01T00:00:00Z',
      updatedAtUtc: '2026-01-01T00:00:00Z',
    };
  }
});
