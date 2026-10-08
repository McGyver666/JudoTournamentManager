import type { MockedObject } from 'vitest';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { ApiService } from '../../core/api.service';
import { I18nService } from '../../core/i18n.service';
import { Category, RegistrationDetail } from '../../core/models';
import { TournamentContextService } from '../../core/tournament-context.service';
import { CategoryAssignmentComponent } from './category-assignment.component';

describe('CategoryAssignmentComponent', () => {
  let apiSpy: MockedObject<Pick<ApiService, 'assignCategory'>>;
  let component: any;

  const category = {
    id: 'u11-40',
    name: 'U11 W -40 kg',
    ageGroup: 'U11',
    gender: 'Female',
    weightClassKg: 40,
    minBirthYear: 2017,
    maxBirthYear: 2018,
    isLocked: false,
  } as Category;
  const registration = {
    id: 'registration-1',
    categoryId: null,
    athleteBirthYear: 2016,
  } as RegistrationDetail;

  beforeEach(() => {
    apiSpy = {
      assignCategory: vi.fn().mockName('ApiService.assignCategory')
    };
    apiSpy.assignCategory.mockReturnValue(of({} as any));
    TestBed.configureTestingModule({
      imports: [CategoryAssignmentComponent],
      providers: [
        { provide: ApiService, useValue: apiSpy },
        { provide: I18nService, useValue: { translate: (key: string) => key } },
        { provide: TournamentContextService, useValue: { tournamentId: signal('tournament-1') } },
      ],
    });
    component = TestBed.createComponent(CategoryAssignmentComponent).componentInstance;
    component.categories.set([category]);
    component.registrations.set([registration]);
  });

  function selectWith(value: string): HTMLSelectElement {
    const select = document.createElement('select');
    select.add(new Option('', ''));
    select.add(new Option(category.name, category.id));
    select.value = value;
    return select;
  }

  it('asks for confirmation when the birth year is outside the category range and keeps the old value on cancel', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    const select = selectWith(category.id);

    component.reassign(registration, select);

    expect(window.confirm).toHaveBeenCalled();
    expect(apiSpy.assignCategory).not.toHaveBeenCalled();
    expect(select.value).toBe('');
  });

  it('assigns after confirmation because the birth-year range is only a plausibility check', () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    component.reassign(registration, selectWith(category.id));

    expect(apiSpy.assignCategory).toHaveBeenCalledWith('tournament-1', 'registration-1', { categoryId: 'u11-40' });
  });
});
