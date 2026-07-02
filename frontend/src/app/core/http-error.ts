import { HttpErrorResponse } from '@angular/common/http';

/**
 * Extracts a human-readable message from an API error response.
 *
 * The backend returns RFC 7807 ProblemDetails / ValidationProblemDetails with
 * German titles and field-level messages. This helper prefers the most specific
 * message available and falls back to a translated generic key supplied by the
 * caller.
 */
export function extractApiError(error: unknown, fallback: string): string {
  if (error instanceof HttpErrorResponse) {
    const body = error.error;
    if (body && typeof body === 'object') {
      const problem = body as {
        detail?: string;
        title?: string;
        errors?: Record<string, string[]>;
      };
      if (problem.errors) {
        const first = Object.values(problem.errors).flat()[0];
        if (first) {
          return first;
        }
      }
      if (problem.detail) {
        return problem.detail;
      }
      if (problem.title) {
        return problem.title;
      }
    }
  }
  return fallback;
}
