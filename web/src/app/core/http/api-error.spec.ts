import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { toApiError } from './api-error';

describe('toApiError', () => {
  it('reads title, code and camelCases field errors from ProblemDetails', () => {
    const error = toApiError(
      new HttpErrorResponse({
        status: 400,
        error: { title: 'Invalid', code: 'auth.invalid_input', errors: { Email: ['Bad email'], password: ['Too short'] } },
      }),
    );
    expect(error).toEqual({
      status: 400,
      title: 'Invalid',
      code: 'auth.invalid_input',
      fieldErrors: { email: ['Bad email'], password: ['Too short'] },
    });
  });

  it('gives a connection message for network failures', () => {
    expect(toApiError(new HttpErrorResponse({ status: 0 })).title).toContain('Cannot reach the server');
  });
});
