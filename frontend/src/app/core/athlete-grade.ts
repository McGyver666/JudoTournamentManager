export interface AthleteGradeOption {
  value: number;
  labelKey: string;
}

export const ATHLETE_GRADE_OPTIONS: ReadonlyArray<AthleteGradeOption> = [
  { value: 1, labelKey: 'athletes.gradeOption1' },
  { value: 2, labelKey: 'athletes.gradeOption2' },
  { value: 3, labelKey: 'athletes.gradeOption3' },
  { value: 4, labelKey: 'athletes.gradeOption4' },
  { value: 5, labelKey: 'athletes.gradeOption5' },
  { value: 6, labelKey: 'athletes.gradeOption6' },
  { value: 7, labelKey: 'athletes.gradeOption7' },
  { value: 8, labelKey: 'athletes.gradeOption8' },
  { value: 9, labelKey: 'athletes.gradeOption9' },
  { value: 10, labelKey: 'athletes.gradeOption10' },
  { value: 11, labelKey: 'athletes.gradeOption11' },
  { value: 12, labelKey: 'athletes.gradeOption12' },
  { value: 13, labelKey: 'athletes.gradeOption13' },
  { value: 14, labelKey: 'athletes.gradeOption14' },
];

export function athleteGradeLabelKey(grade: number): string {
  if (grade < 1 || grade > 14) {
    return 'athletes.gradeUnknown';
  }

  return `athletes.gradeOption${grade}`;
}
