import { signal } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, Subject } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { AuthStateService } from '../../core/auth-state.service';
import { I18nService } from '../../core/i18n.service';
import { Athlete, Tournament } from '../../core/models';
import { TournamentContextService } from '../../core/tournament-context.service';
import { RegistrationsComponent } from './registrations.component';

describe('RegistrationsComponent', () => {
  let apiSpy: jasmine.SpyObj<Pick<
    ApiService,
    'getRegistrations' | 'getAthletes' | 'getCategories' | 'getClubs' | 'createRegistration' | 'updateAthlete'
  >>;
  let fixture: ComponentFixture<RegistrationsComponent>;
  let component: RegistrationsComponent;

  beforeEach(() => {
    apiSpy = jasmine.createSpyObj('ApiService', [
      'getRegistrations',
      'getAthletes',
      'getCategories',
      'getClubs',
      'createRegistration',
      'updateAthlete',
    ]);
    apiSpy.getRegistrations.and.returnValue(of([]));
    apiSpy.getAthletes.and.returnValue(of([createAthlete()]));
    apiSpy.getCategories.and.returnValue(of([]));
    apiSpy.getClubs.and.returnValue(of([]));
    apiSpy.createRegistration.and.returnValue(of({
      id: 'registration-1',
      tournamentId: 'tournament-1',
      athleteId: 'athlete-1',
      categoryId: null,
      createdAtUtc: '',
    }));
    apiSpy.updateAthlete.and.returnValue(of(void 0));

    TestBed.configureTestingModule({
      imports: [RegistrationsComponent],
      providers: [
        { provide: ApiService, useValue: apiSpy },
        { provide: AuthStateService, useValue: { canOperate: signal(true) } },
        { provide: I18nService, useValue: { translate: (key: string) => key } },
        {
          provide: TournamentContextService,
          useValue: {
            tournamentId: signal('tournament-1'),
            tournament: signal<Tournament | null>(null),
          },
        },
      ],
    });

    fixture = TestBed.createComponent(RegistrationsComponent);
    component = fixture.componentInstance;
  });

  it('sends the weigh-in grade and license in one registration request', async () => {
    const athleteResponse = new Subject<Athlete[]>();
    apiSpy.getAthletes.and.returnValue(athleteResponse);
    fixture.detectChanges();
    athleteResponse.next([createAthlete()]);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();
    const testComponent = component as any;
    expect(testComponent.form.grade).toBe(1);
    expect(testComponent.form.licenseId).toBe('OLD-123');

    const gradeSelect = fixture.nativeElement.querySelector('select[name="grade"]') as HTMLSelectElement;
    expect(gradeSelect).toBeTruthy();
    expect(gradeSelect.selectedIndex).toBe(1);

    testComponent.form.weightKg = 65;
    testComponent.form.grade = 5;
    testComponent.form.licenseId = 'NEW-456';

    testComponent.save();

    expect(apiSpy.createRegistration).toHaveBeenCalledWith(
      'tournament-1',
      jasmine.objectContaining({
        athleteId: 'athlete-1',
        weightKg: 65,
        licenseId: 'NEW-456',
        grade: 5,
      }),
    );
    expect(apiSpy.updateAthlete).not.toHaveBeenCalled();
  });

  it('shows the localized belt grade for weighed athletes', () => {
    fixture.detectChanges();
    const testComponent = component as any;
    testComponent.registrations.set([{
      id: 'registration-1',
      categoryName: null,
      athleteLastName: 'Mustermann',
      athleteFirstName: 'Max',
      athleteBirthYear: 2010,
      athleteWeightKg: 65,
      athleteGrade: 5,
      licenseConfirmed: true,
      licenseNumber: null,
      licenseCheckPassed: null,
      passExpiryDate: null,
    }]);
    fixture.detectChanges();

    const table = fixture.nativeElement.querySelector('table') as HTMLTableElement;
    expect(table.textContent).toContain('athletes.gradeOption5');
  });
});

function createAthlete(): Athlete {
  return {
    id: 'athlete-1',
    tournamentId: 'tournament-1',
    clubId: 'club-1',
    firstName: 'Max',
    lastName: 'Mustermann',
    birthYear: 2010,
    gender: 'Male',
    licenseId: 'OLD-123',
    weightKg: 60,
    grade: 1,
    lastFightDurationSeconds: null,
    lastFightEndedAtUtc: null,
    createdAtUtc: '',
    updatedAtUtc: '',
  };
}