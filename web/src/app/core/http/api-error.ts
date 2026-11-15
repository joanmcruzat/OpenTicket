import { HttpErrorResponse } from '@angular/common/http';

/** A ProblemDetails response from the API, normalized for the UI. */
export interface ApiError {
  status: number;
  title: string;
  code: string | null;
  fieldErrors: Record<string, string[]>;
}

interface ProblemBody {
  title?: unknown;
  code?: unknown;
  errors?: unknown;
}

export function toApiError(error: unknown): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, title: 'Something went wrong. Please try again.', code: null, fieldErrors: {} };
  }

  const body = (typeof error.error === 'object' && error.error !== null ? error.error : {}) as ProblemBody;
  const fieldErrors: Record<string, string[]> = {};
  if (typeof body.errors === 'object' && body.errors !== null) {
    for (const [field, messages] of Object.entries(body.errors as Record<string, unknown>)) {
      if (Array.isArray(messages)) {
        // The API may return PascalCase keys for built-in validation; normalize to camelCase form control names.
        fieldErrors[field.charAt(0).toLowerCase() + field.slice(1)] = messages.map(String);
      }
    }
  }

  return {
    status: error.status,
    title:
      typeof body.title === 'string'
        ? body.title
        : error.status === 0
          ? 'Cannot reach the server. Check your connection.'
          : 'Something went wrong. Please try again.',
    code: typeof body.code === 'string' ? body.code : null,
    fieldErrors,
  };
}

export function isApiError(value: unknown): value is ApiError {
  return typeof value === 'object' && value !== null && 'status' in value && 'fieldErrors' in value;
}
