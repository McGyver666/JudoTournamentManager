import { HttpErrorResponse } from '@angular/common/http';
import { extractApiError } from './http-error';

describe('extractApiError', () => {
  const translations: Record<string, string> = {
    'errors.ageGroups.startAgeGroupInvalid': 'Start age group does not match.',
  };
  const translate = (key: string): string => translations[key] ?? key;

  function problem(body: object): HttpErrorResponse {
    return new HttpErrorResponse({ status: 400, error: body });
  }

  it('prefers the translated messageKey over the German backend text', () => {
    const error = problem({
      messageKey: 'errors.ageGroups.startAgeGroupInvalid',
      errors: { startAgeGroup: ['Die Startaltersklasse passt nicht.'] },
    });

    expect(extractApiError(error, 'fallback', translate)).toBe('Start age group does not match.');
  });

  it('falls back to the backend text when the key is unknown or no translator is given', () => {
    const error = problem({
      messageKey: 'errors.unknown',
      errors: { startAgeGroup: ['Die Startaltersklasse passt nicht.'] },
    });

    expect(extractApiError(error, 'fallback', translate)).toBe('Die Startaltersklasse passt nicht.');
    expect(extractApiError(error, 'fallback')).toBe('Die Startaltersklasse passt nicht.');
  });
});
