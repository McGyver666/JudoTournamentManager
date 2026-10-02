export interface AthleteGradeOption {
  value: number;
  labelKey: string;
}

const CSV_GRADE_LABELS: ReadonlyArray<readonly [number, string, string]> = [
  [1, '9. Kyu', '9th Kyu'],
  [2, '8. Kyu', '8th Kyu'],
  [3, '7. Kyu', '7th Kyu'],
  [4, '6. Kyu', '6th Kyu'],
  [5, '5. Kyu', '5th Kyu'],
  [6, '4. Kyu', '4th Kyu'],
  [7, '3. Kyu', '3rd Kyu'],
  [8, '2. Kyu', '2nd Kyu'],
  [9, '1. Kyu', '1st Kyu'],
  [10, '1. Dan', '1st Dan'],
  [11, '2. Dan', '2nd Dan'],
  [12, '3. Dan', '3rd Dan'],
  [13, '4. Dan', '4th Dan'],
  [14, '5. Dan', '5th Dan'],
];

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

export function athleteGradeLabelKey(grade: number | null): string {
  if (grade === null) {
    return 'athletes.gradeNotSpecifiedShort';
  }

  if (grade < 1 || grade > 14) {
    return 'athletes.gradeUnknown';
  }

  return `athletes.gradeOption${grade}`;
}

export function athleteGradeCsvLabelMap(): Map<string, number> {
  const map = new Map<string, number>();
  for (const [grade, germanLabel, englishLabel] of CSV_GRADE_LABELS) {
    map.set(normalizeGradeLabel(germanLabel), grade);
    map.set(normalizeGradeLabel(englishLabel), grade);
  }
  return map;
}

function normalizeGradeLabel(label: string): string {
  return label.trim().toLocaleLowerCase().replace(/\s+/g, '');
}
